using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public sealed class MonsterAI : MonoBehaviour
{
    private const string ResourcesAudioDataPath = "Audio/AudioData";

    private enum MonsterState
    {
        Disabled,
        Scripted,
        Wandering,
        Searching,
        Chasing,
        Attacking
    }

    private static readonly int IsRunHash = Animator.StringToHash("isRun");
    private static readonly int SwipingHash = Animator.StringToHash("swiping");
    private static readonly int FlairHash = Animator.StringToHash("flair");

    [Header("Target")]
    [SerializeField] private Transform player;
    [SerializeField] private Collider houseBounds;
    [SerializeField] private bool requirePlayerInsideHouse = true;
    [SerializeField] private bool huntEnabledOnStart = false;

    [Header("Distance Chase")]
    [SerializeField, Min(0.5f)] private float followDistance = 42f;
    [SerializeField, Min(0.5f)] private float runDistance = 8.5f;
    [SerializeField] private Transform hiddenReturnPoint;

    [Header("Vision")]
    [SerializeField] private Transform visionOrigin;
    [SerializeField] private LayerMask obstacleMask = ~0;
    [SerializeField, Min(0f)] private float loseSightGraceTime = 8f;

    [Header("Search Cycle")]
    [SerializeField, Min(1f)] private float searchInterval = 60f;
    [SerializeField, Min(0.5f)] private float searchDuration = 45f;
    [SerializeField] private bool startSearchImmediately;

    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float chaseSpeed = 4.8f;
    [SerializeField, Min(0.1f)] private float scriptedPassSpeed = 12f;
    [SerializeField, Min(0.1f)] private float searchSpeed = 2.3f;
    [SerializeField, Min(0.1f)] private float wanderSpeed = 1.8f;
    [SerializeField, Min(0.1f)] private float acceleration = 4.5f;
    [SerializeField, Min(0.1f)] private float stoppingDistance = 1.35f;
    [SerializeField, Min(0.1f)] private float wanderRadius = 18f;
    [SerializeField, Min(0.1f)] private float navMeshSnapRadius = 6f;
    [SerializeField, Min(0.1f)] private float navMeshRetryInterval = 0.5f;
    [SerializeField, Min(0.1f)] private float repathInterval = 0.2f;

    [Header("Patrol Points")]
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private bool usePatrolPoints = true;
    [SerializeField] private bool autoFindPatrolPoints = true;
    [SerializeField] private string patrolPointNamePrefix = "MonsterPatrolPoint";
    [SerializeField, Min(0f)] private float patrolPointWaitTime = 0.15f;

    [Header("Attack")]
    [SerializeField, Min(0.2f)] private float attackRange = 1.65f;
    [SerializeField, Min(0.1f)] private float attackCooldown = 1.15f;
    [SerializeField, Min(0f)] private float attackHitDelay = 0.32f;
    [SerializeField] private bool killPlayerOnHit = true;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField, Min(0.1f)] private float randomFlairMinDelay = 5f;
    [SerializeField, Min(0.1f)] private float randomFlairMaxDelay = 14f;

    [Header("Audio")]
    [SerializeField] private AudioSource monsterAudioSource;
    [SerializeField] private AudioClip[] idleHorrorClips;
    [SerializeField] private AudioClip[] attackClips;
    [SerializeField] private AudioClip[] footstepClips;
    [SerializeField] private AudioClip[] voiceClips;
    [SerializeField, Min(0.1f)] private float walkFootstepInterval = 0.56f;
    [SerializeField, Min(0.1f)] private float runFootstepInterval = 0.32f;
    [SerializeField, Range(0f, 1f)] private float walkFootstepVolume = 0.72f;
    [SerializeField, Range(0f, 1f)] private float runFootstepVolume = 0.95f;
    [SerializeField, Range(0.5f, 1.5f)] private float footstepPitchMin = 0.92f;
    [SerializeField, Range(0.5f, 1.5f)] private float footstepPitchMax = 1.08f;
    [SerializeField, Min(0f)] private float footstepMinDistance = 3f;
    [SerializeField, Min(0.1f)] private float footstepMaxDistance = 28f;
    [SerializeField, Min(0.1f)] private float voiceMinInterval = 8f;
    [SerializeField, Min(0.1f)] private float voiceMaxInterval = 18f;

    [Header("Door Interaction")]
    [SerializeField, Min(0.5f)] private float doorCheckDistance = 2.2f;
    [SerializeField, Min(0.1f)] private float doorCheckRadius = 0.45f;

    private NavMeshAgent agent;
    private Renderer[] renderers;
    private MonsterState state = MonsterState.Disabled;
    private Vector3 wanderCenter;
    private Vector3 lastKnownPlayerPosition;
    private bool hasSeenPlayerInSearch;
    private float lastSeenTime = float.NegativeInfinity;
    private float nextSearchTime;
    private float searchEndTime;
    private float nextRepathTime;
    private float nextNavMeshRetryTime;
    private float nextAttackTime;
    private float nextFootstepTime;
    private int lastFootstepClipIndex = -1;
    private float nextFlairTime;
    private float nextVoiceTime;
    private Vector3 currentWanderTarget;
    private bool hasWanderTarget;
    private bool hasPriorityWanderTarget;
    private bool returningBecausePlayerHidden;
    private float priorityWanderSpeed;
    private int patrolIndex;
    private float nextPatrolMoveTime;
    private NavMeshPath reusablePath;
    private Coroutine scriptedRoutine;
    private AudioData fallbackAudioData;

    public bool IsHuntActive => state != MonsterState.Disabled && state != MonsterState.Scripted;
    public bool IsScriptedSequenceRunning => state == MonsterState.Scripted;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        renderers = GetComponentsInChildren<Renderer>(true);
        if (visionOrigin == null)
            visionOrigin = transform;
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
        if (monsterAudioSource == null)
            monsterAudioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

        monsterAudioSource.playOnAwake = false;
        ConfigureMonsterAudioSource();
        reusablePath = new NavMeshPath();
    }

    private void Start()
    {
        ResolveReferences();
        ResolvePatrolPoints();
        ConfigureAgent();
        wanderCenter = transform.position;
        AlignPatrolIndexToNearest(transform.position);
        nextSearchTime = startSearchImmediately ? Time.time : Time.time + searchInterval;
        ScheduleNextFlair();
        ScheduleNextVoice();
        SnapToNavMesh();

        if (huntEnabledOnStart)
            EnableHunt(startSearchImmediately);
        else
            DisableHunt(true);
    }

    private void Update()
    {
        if (state == MonsterState.Disabled || state == MonsterState.Scripted)
            return;

        if (player == null)
        {
            ResolveReferences();
            return;
        }

        TickAmbientAnimationAndAudio();

        if (FpsHorrorKit.ClosetHiding.IsAnyPlayerHidden)
        {
            StartHiddenReturnToSpawn();
            TickWandering();
            return;
        }

        returningBecausePlayerHidden = false;
        bool searchWindowActive = Time.time < searchEndTime;
        if (!searchWindowActive && Time.time >= nextSearchTime)
            BeginSearchWindow();

        bool canDetectPlayer = CanDetectPlayer();
        if (canDetectPlayer)
        {
            lastSeenTime = Time.time;
            lastKnownPlayerPosition = player.position;
            hasSeenPlayerInSearch = searchWindowActive;

            if (state != MonsterState.Chasing && state != MonsterState.Attacking)
                StartChaseAudio();

            state = MonsterState.Chasing;
        }
        else if (state == MonsterState.Chasing)
        {
            bool lostSight = Time.time - lastSeenTime > loseSightGraceTime;
            if (lostSight)
            {
                if (searchWindowActive && hasSeenPlayerInSearch)
                    state = MonsterState.Searching;
                else
                    state = MonsterState.Wandering;
            }
        }

        if (Time.time >= searchEndTime && state == MonsterState.Searching)
            state = MonsterState.Wandering;

        TickState();
    }

    public void EnableHunt(bool beginSearchNow = false)
    {
        if (scriptedRoutine != null)
        {
            StopCoroutine(scriptedRoutine);
            scriptedRoutine = null;
        }

        ResolveReferences();
        ResolvePatrolPoints();
        SetMeshVisible(true);
        ConfigureAgent();
        EnsureAgentOnNavMesh(force: true);
        wanderCenter = transform.position;
        AlignPatrolIndexToNearest(transform.position);
        hasWanderTarget = false;
        state = MonsterState.Wandering;
        nextSearchTime = Time.time + searchInterval;
        searchEndTime = 0f;

        if (beginSearchNow)
            BeginSearchWindow();
    }

    public void DisableHunt(bool hideMesh)
    {
        state = MonsterState.Disabled;
        if (agent != null)
        {
            agent.isStopped = true;
            if (agent.isOnNavMesh && agent.hasPath)
                agent.ResetPath();
        }

        SetRunAnimation(false);
        if (hideMesh)
            SetMeshVisible(false);
    }

    public void TeleportTo(Transform point)
    {
        if (point == null)
            return;

        TeleportTo(point.position, point.rotation);
    }

    public void TeleportTo(Vector3 position, Quaternion rotation)
    {
        if (agent != null)
            agent.enabled = false;

        transform.SetPositionAndRotation(position, rotation);

        if (agent != null)
        {
            agent.enabled = true;
            EnsureAgentOnNavMesh(force: true);
        }

        wanderCenter = transform.position;
        hasWanderTarget = false;
    }

    public void PlayScriptedHallCrossing(Transform[] route, Transform hideAtDoorPoint, bool playVoice = false)
    {
        if (scriptedRoutine != null)
            StopCoroutine(scriptedRoutine);

        scriptedRoutine = StartCoroutine(ScriptedHallCrossingRoutine(route, hideAtDoorPoint, playVoice));
    }

    public void MoveToWanderPoint(Transform point, bool running = false)
    {
        if (point == null)
            return;

        if (scriptedRoutine != null)
        {
            StopCoroutine(scriptedRoutine);
            scriptedRoutine = null;
        }

        ResolveReferences();
        ResolvePatrolPoints();
        SetMeshVisible(true);
        ConfigureAgent();
        EnsureAgentOnNavMesh(force: true);

        state = MonsterState.Wandering;
        wanderCenter = point.position;
        AlignPatrolIndexToNearest(point.position);
        hasWanderTarget = true;
        hasPriorityWanderTarget = true;
        priorityWanderSpeed = running ? chaseSpeed : wanderSpeed;
        currentWanderTarget = point.position;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.speed = priorityWanderSpeed;
            agent.isStopped = false;
            if (TrySampleNavMesh(point.position, out var sampledPosition))
            {
                currentWanderTarget = sampledPosition;
                agent.SetDestination(sampledPosition);
            }
        }
    }

    public void SetMeshVisible(bool visible)
    {
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>(true);

        foreach (var itemRenderer in renderers)
        {
            if (itemRenderer != null)
                itemRenderer.enabled = visible;
        }
    }

    private IEnumerator ScriptedHallCrossingRoutine(Transform[] route, Transform hideAtDoorPoint, bool playVoice)
    {
        state = MonsterState.Scripted;
        SetMeshVisible(true);
        ConfigureAgent();
        EnsureAgentOnNavMesh(force: true);
        SetRunAnimation(true);
        if (playVoice)
            PlayVoiceLine();

        if (route != null)
        {
            for (int i = 0; i < route.Length; i++)
            {
                if (route[i] == null)
                    continue;

                yield return MoveScriptedTo(route[i].position, true);
            }
        }

        if (hideAtDoorPoint != null)
            yield return MoveScriptedTo(hideAtDoorPoint.position, true);

        SetMeshVisible(false);
        SetRunAnimation(false);
        if (agent != null && agent.isOnNavMesh)
            agent.ResetPath();

        state = MonsterState.Disabled;
        scriptedRoutine = null;
    }

    private IEnumerator MoveScriptedTo(Vector3 target, bool running)
    {
        float moveSpeed = running ? scriptedPassSpeed : wanderSpeed;
        if (!EnsureAgentOnNavMesh(force: true))
        {
            yield return MoveScriptedTransformTo(target, moveSpeed, running);
            yield break;
        }

        agent.speed = moveSpeed;
        agent.isStopped = false;
        if (!agent.SetDestination(target))
        {
            yield return MoveScriptedTransformTo(target, moveSpeed, running);
            yield break;
        }

        while (agent.enabled && agent.isOnNavMesh && agent.pathPending)
            yield return null;

        while (agent.enabled && agent.isOnNavMesh && agent.remainingDistance > stoppingDistance + 0.15f)
        {
            TickFootsteps(running);
            yield return null;
        }
    }

    private IEnumerator MoveScriptedTransformTo(Vector3 target, float speed, bool running)
    {
        if (agent != null)
            agent.enabled = false;

        while (Vector3.Distance(transform.position, target) > stoppingDistance + 0.15f)
        {
            Vector3 nextPosition = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            Vector3 direction = nextPosition - transform.position;
            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

            transform.position = nextPosition;
            TickManualFootsteps(running);
            yield return null;
        }

        if (agent != null)
        {
            agent.enabled = true;
            EnsureAgentOnNavMesh(force: true);
        }
    }

    private void BeginSearchWindow()
    {
        searchEndTime = Time.time + searchDuration;
        nextSearchTime = searchEndTime + searchInterval;
        hasSeenPlayerInSearch = false;
        hasWanderTarget = false;
        PlayVoiceLine();

        if (player != null && !FpsHorrorKit.ClosetHiding.IsAnyPlayerHidden)
            lastKnownPlayerPosition = player.position;

        if (state != MonsterState.Chasing && state != MonsterState.Attacking)
            state = MonsterState.Searching;
    }

    private void TickState()
    {
        if (!EnsureAgentOnNavMesh())
            return;

        switch (state)
        {
            case MonsterState.Chasing:
                TickChasing();
                break;
            case MonsterState.Searching:
                TickSearching();
                break;
            case MonsterState.Attacking:
                break;
            default:
                TickWandering();
                break;
        }
    }

    private void TickChasing()
    {
        float distanceToPlayer = player != null ? Vector3.Distance(transform.position, player.position) : float.MaxValue;
        bool shouldRun = distanceToPlayer <= runDistance;
        SetRunAnimation(shouldRun);

        if (distanceToPlayer <= attackRange && Time.time >= nextAttackTime)
        {
            StartCoroutine(AttackRoutine());
            return;
        }

        agent.speed = shouldRun ? chaseSpeed : searchSpeed;
        agent.isStopped = false;
        TryOpenDoorAhead();
        TickFootsteps(shouldRun);

        if (Time.time < nextRepathTime)
            return;

        nextRepathTime = Time.time + repathInterval;
        if (TrySampleNavMesh(player.position, out var targetPosition))
            agent.SetDestination(targetPosition);
    }

    private IEnumerator AttackRoutine()
    {
        state = MonsterState.Attacking;
        nextAttackTime = Time.time + attackCooldown;
        if (agent != null)
        {
            agent.isStopped = true;
            if (agent.isOnNavMesh && agent.hasPath)
                agent.ResetPath();
        }

        FacePlayer();
        SetRunAnimation(false);
        animator?.SetTrigger(SwipingHash);
        bool playedAttack = PlayRandomOneShot(attackClips, 0.95f);
        if (!playedAttack)
            playedAttack = PlayClipOneShot(ResolveAudioData()?.ghostJumpscare, 0.95f);
        if (!playedAttack)
            AudioManager.Instance?.PlayGhostJumpscare();

        if (attackHitDelay > 0f)
            yield return new WaitForSeconds(attackHitDelay);

        if (killPlayerOnHit && player != null && Vector3.Distance(transform.position, player.position) <= attackRange + 0.65f)
            GameController.Instance?.TriggerDeath(false);

        if (GameController.Instance == null || GameController.Instance.currentGameState != GameController.GameState.Dead)
            state = MonsterState.Chasing;
    }

    private void TickSearching()
    {
        SetRunAnimation(false);
        agent.speed = searchSpeed;
        agent.isStopped = false;
        TryOpenDoorAhead();
        TickFootsteps(false);

        if (Time.time >= searchEndTime)
        {
            state = MonsterState.Wandering;
            return;
        }

        if (IsPlayerBlockedByClosedDoor())
        {
            TickWandering();
            return;
        }

        lastKnownPlayerPosition = player.position;

        if (Time.time < nextRepathTime)
            return;

        nextRepathTime = Time.time + repathInterval;
        if (TrySampleNavMesh(lastKnownPlayerPosition, out var targetPosition))
            agent.SetDestination(targetPosition);

        if (!agent.pathPending && agent.remainingDistance <= stoppingDistance + 0.25f)
            ChooseWanderTarget();
    }

    private void TickWandering()
    {
        bool runningToPriorityTarget = hasPriorityWanderTarget && priorityWanderSpeed > wanderSpeed + 0.1f;
        SetRunAnimation(runningToPriorityTarget);
        agent.speed = hasPriorityWanderTarget ? priorityWanderSpeed : wanderSpeed;
        agent.isStopped = false;
        TickFootsteps(runningToPriorityTarget);

        if (hasPriorityWanderTarget)
        {
            if (!agent.pathPending && agent.remainingDistance <= stoppingDistance + 0.25f)
            {
                hasPriorityWanderTarget = false;
                wanderCenter = transform.position;
                hasWanderTarget = false;
                nextPatrolMoveTime = Time.time + patrolPointWaitTime;
                return;
            }

            if (Time.time >= nextRepathTime)
            {
                nextRepathTime = Time.time + repathInterval;
                agent.SetDestination(currentWanderTarget);
            }

            return;
        }

        if (hasWanderTarget && !agent.pathPending && agent.remainingDistance <= stoppingDistance + 0.25f)
        {
            hasWanderTarget = false;
            nextPatrolMoveTime = Time.time + patrolPointWaitTime;
        }

        if (!hasWanderTarget && Time.time >= nextPatrolMoveTime)
            ChooseWanderTarget();

        if (hasWanderTarget && Time.time >= nextRepathTime)
        {
            nextRepathTime = Time.time + repathInterval;
            agent.SetDestination(currentWanderTarget);
        }
    }

    private void TickAmbientAnimationAndAudio()
    {
        if (Time.time >= nextFlairTime && state == MonsterState.Wandering)
        {
            animator?.SetTrigger(FlairHash);
            if (!PlayRandomOneShot(idleHorrorClips, 0.55f))
                PlayClipOneShot(ResolveAudioData()?.sanityWarning, 0.55f);
            ScheduleNextFlair();
        }

        if (Time.time >= nextVoiceTime && (state == MonsterState.Wandering || state == MonsterState.Searching))
        {
            PlayVoiceLine();
            ScheduleNextVoice();
        }
    }

    private void TickFootsteps(bool running)
    {
        if (agent == null || !agent.hasPath || agent.velocity.sqrMagnitude < 0.1f || Time.time < nextFootstepTime)
            return;

        PlayFootstep(running);
    }

    private void TickManualFootsteps(bool running)
    {
        if (Time.time < nextFootstepTime)
            return;

        PlayFootstep(running);
    }

    private void PlayFootstep(bool running)
    {
        nextFootstepTime = Time.time + (running ? runFootstepInterval : walkFootstepInterval);
        PlayMonsterFootstep(running);
    }

    private void PlayMonsterFootstep(bool running)
    {
        AudioClip[] clips = ResolveFootstepClips();
        if (clips == null || clips.Length == 0 || monsterAudioSource == null)
            return;

        int index = Random.Range(0, clips.Length);
        if (clips.Length > 1 && index == lastFootstepClipIndex)
            index = (index + 1) % clips.Length;

        AudioClip clip = clips[index];
        if (clip == null)
            return;

        lastFootstepClipIndex = index;
        float originalPitch = monsterAudioSource.pitch;
        monsterAudioSource.pitch = Random.Range(footstepPitchMin, Mathf.Max(footstepPitchMin, footstepPitchMax));
        monsterAudioSource.PlayOneShot(clip, running ? runFootstepVolume : walkFootstepVolume);
        monsterAudioSource.pitch = originalPitch;
    }

    private AudioClip[] ResolveFootstepClips()
    {
        if (footstepClips != null && footstepClips.Length > 0)
            return footstepClips;

        var data = ResolveAudioData();
        if (data == null)
            return null;

        if (data.footstepWood != null && data.footstepWood.Length > 0)
            return data.footstepWood;

        return data.footstepGround;
    }

    private void PlayVoiceLine()
    {
        if (!PlayRandomOneShot(voiceClips, 0.95f))
        {
            var data = ResolveAudioData();
            if (data == null || !PlayClipOneShot(data.maVuDaiPatrolFull, 0.95f))
                AudioManager.Instance?.PlayMaVuDaiPatrol();
        }
    }

    private bool PlayRandomOneShot(AudioClip[] clips, float volume)
    {
        if (clips == null || clips.Length == 0 || monsterAudioSource == null)
            return false;

        var clip = clips[Random.Range(0, clips.Length)];
        return PlayClipOneShot(clip, volume);
    }

    private bool PlayClipOneShot(AudioClip clip, float volume)
    {
        if (clip == null || monsterAudioSource == null)
            return false;

        monsterAudioSource.PlayOneShot(clip, volume);
        return true;
    }

    private void ConfigureMonsterAudioSource()
    {
        if (monsterAudioSource == null)
            return;

        monsterAudioSource.playOnAwake = false;
        monsterAudioSource.spatialBlend = 1f;
        monsterAudioSource.rolloffMode = AudioRolloffMode.Linear;
        monsterAudioSource.minDistance = footstepMinDistance;
        monsterAudioSource.maxDistance = Mathf.Max(footstepMinDistance + 0.1f, footstepMaxDistance);
    }

    private AudioData ResolveAudioData()
    {
        if (fallbackAudioData == null)
            fallbackAudioData = Resources.Load<AudioData>(ResourcesAudioDataPath);

        return fallbackAudioData;
    }

    private void ScheduleNextFlair()
    {
        nextFlairTime = Time.time + Random.Range(randomFlairMinDelay, Mathf.Max(randomFlairMinDelay, randomFlairMaxDelay));
    }

    private void ScheduleNextVoice()
    {
        nextVoiceTime = Time.time + Random.Range(voiceMinInterval, Mathf.Max(voiceMinInterval, voiceMaxInterval));
    }

    private void SetRunAnimation(bool running)
    {
        if (animator != null)
            animator.SetBool(IsRunHash, running);
    }

    private void FacePlayer()
    {
        if (player == null)
            return;

        Vector3 direction = player.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private void ChooseWanderTarget()
    {
        hasWanderTarget = false;

        if (usePatrolPoints && TryChoosePatrolTarget())
            return;

        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector2 offset = Random.insideUnitCircle * wanderRadius;
            Vector3 candidate = wanderCenter + new Vector3(offset.x, 0f, offset.y);
            if (!TrySampleNavMesh(candidate, out var sampledPosition))
                continue;

            if (!CanReach(sampledPosition))
                continue;

            currentWanderTarget = sampledPosition;
            hasWanderTarget = true;
            return;
        }
    }

    private bool TryChoosePatrolTarget()
    {
        ResolvePatrolPoints();

        if (patrolPoints == null || patrolPoints.Length == 0)
            return false;

        for (int attempt = 0; attempt < patrolPoints.Length; attempt++)
        {
            int index = (patrolIndex + attempt) % patrolPoints.Length;
            Transform point = patrolPoints[index];
            if (point == null)
                continue;

            if (!TrySampleNavMesh(point.position, out var sampledPosition))
                continue;

            if (!CanReach(sampledPosition))
                continue;

            currentWanderTarget = sampledPosition;
            hasWanderTarget = true;
            patrolIndex = (index + 1) % patrolPoints.Length;
            wanderCenter = sampledPosition;
            return true;
        }

        return false;
    }

    private bool CanDetectPlayer()
    {
        var gameController = GameController.Instance;
        if (gameController != null && (gameController.currentGameState == GameController.GameState.Cutscene || gameController.currentGameState == GameController.GameState.Ending))
            return false;

        if (requirePlayerInsideHouse && !IsPlayerInsideHouse())
            return false;

        return Vector3.Distance(transform.position, player.position) <= followDistance;
    }

    private bool HasLineOfSight(Vector3 origin, Vector3 target, float distance)
    {
        Vector3 direction = (target - origin).normalized;
        if (!Physics.Raycast(origin, direction, out var hit, distance, obstacleMask, QueryTriggerInteraction.Ignore))
            return true;

        return hit.transform == player || hit.transform.IsChildOf(player);
    }

    private bool IsPlayerBlockedByClosedDoor()
    {
        if (player == null || visionOrigin == null)
            return false;

        Vector3 origin = visionOrigin.position;
        Vector3 target = player.position + Vector3.up * 1.35f;
        Vector3 direction = target - origin;
        float distance = direction.magnitude;
        if (distance <= 0.01f)
            return false;

        if (!Physics.Raycast(origin, direction.normalized, out var hit, distance, obstacleMask, QueryTriggerInteraction.Ignore))
            return false;

        var door = hit.collider.GetComponentInParent<FpsHorrorKit.DoorSystem>();
        return door != null && !door.IsOpen;
    }

    private void TryOpenDoorAhead()
    {
        Vector3 origin = visionOrigin != null ? visionOrigin.position : transform.position + Vector3.up;
        Vector3 direction = transform.forward;
        int mask = obstacleMask.value == 0 ? Physics.DefaultRaycastLayers : obstacleMask.value;

        if (!TryFindDoor(origin, direction, mask, out var door))
        {
            Vector3 left = Quaternion.Euler(0f, -22f, 0f) * direction;
            Vector3 right = Quaternion.Euler(0f, 22f, 0f) * direction;
            TryFindDoor(origin, left, mask, out door);
            if (door == null)
                TryFindDoor(origin, right, mask, out door);
        }

        door?.TryOpenForMonster();
    }

    private bool TryFindDoor(Vector3 origin, Vector3 direction, int mask, out FpsHorrorKit.DoorSystem door)
    {
        if (Physics.SphereCast(origin, doorCheckRadius, direction, out var hit, doorCheckDistance, mask, QueryTriggerInteraction.Ignore))
        {
            door = hit.collider.GetComponentInParent<FpsHorrorKit.DoorSystem>();
            return door != null;
        }

        door = null;
        return false;
    }

    private void CancelPlayerPursuit()
    {
        state = MonsterState.Wandering;
        hasSeenPlayerInSearch = false;
        hasPriorityWanderTarget = false;
        hasWanderTarget = false;
        nextPatrolMoveTime = Time.time + patrolPointWaitTime;
        AlignPatrolIndexToNearest(transform.position);
        if (agent != null && agent.isOnNavMesh && agent.hasPath)
            agent.ResetPath();
    }

    private void StartHiddenReturnToSpawn()
    {
        if (returningBecausePlayerHidden)
            return;

        returningBecausePlayerHidden = true;
        hasSeenPlayerInSearch = false;
        if (hiddenReturnPoint != null)
        {
            MoveToWanderPoint(hiddenReturnPoint, false);
            return;
        }

        CancelPlayerPursuit();
    }

    private bool IsPlayerInsideHouse()
    {
        if (houseBounds != null)
            return houseBounds.bounds.Contains(player.position);

        var gameController = GameController.Instance;
        return gameController != null && gameController.currentChapterPhase >= GameController.ChapterPhase.EnterHouse;
    }

    private void ConfigureAgent()
    {
        agent.speed = chaseSpeed;
        agent.acceleration = acceleration;
        agent.stoppingDistance = stoppingDistance;
        agent.autoBraking = true;
        agent.autoRepath = true;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        agent.updateRotation = true;
        agent.isStopped = false;
    }

    private void StartChaseAudio()
    {
        var controller = GameController.Instance;
        if (controller != null && controller.currentChapterPhase < GameController.ChapterPhase.Escape)
            controller.SetChapterPhase(GameController.ChapterPhase.Escape);

        AudioManager.Instance?.PlayGhostJumpscare();
        AudioManager.Instance?.PlayChaseMusic();
    }

    private void SnapToNavMesh()
    {
        EnsureAgentOnNavMesh(force: true);
    }

    private bool EnsureAgentOnNavMesh(bool force = false)
    {
        if (agent == null || !agent.isActiveAndEnabled)
            return false;

        if (agent.isOnNavMesh)
            return true;

        if (!force && Time.time < nextNavMeshRetryTime)
            return false;

        nextNavMeshRetryTime = Time.time + navMeshRetryInterval;
        return TrySampleNavMesh(transform.position, out var hitPosition) && agent.Warp(hitPosition);
    }

    private bool TrySampleNavMesh(Vector3 position, out Vector3 sampledPosition)
    {
        if (NavMesh.SamplePosition(position, out var hit, navMeshSnapRadius, NavMesh.AllAreas))
        {
            sampledPosition = hit.position;
            return true;
        }

        sampledPosition = position;
        return false;
    }

    private bool CanReach(Vector3 destination)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return true;

        reusablePath ??= new NavMeshPath();
        if (!NavMesh.CalculatePath(agent.transform.position, destination, NavMesh.AllAreas, reusablePath))
            return false;

        return reusablePath.status == NavMeshPathStatus.PathComplete;
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            var controller = FindFirstObjectByType<FpsHorrorKit.FpsController>();
            if (controller != null)
                player = controller.transform;
        }

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (hiddenReturnPoint == null)
        {
            var marker = GameObject.Find("MonsterUpperFloorSpawn")
                ?? GameObject.Find("BossSpawnPoint")
                ?? GameObject.Find("SpawnPointBoss")
                ?? GameObject.Find("Spawn Point Boss")
                ?? GameObject.Find("spawm point boss");
            if (marker != null)
                hiddenReturnPoint = marker.transform;
        }
    }

    private void ResolvePatrolPoints()
    {
        if (!autoFindPatrolPoints || HasAssignedPatrolPoints())
            return;

        var found = new List<Transform>();
        var transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var candidate in transforms)
        {
            if (candidate == null || candidate == transform)
                continue;

            if (IsPatrolPointName(candidate.name))
                found.Add(candidate);
        }

        found.Sort((left, right) => string.Compare(left.name, right.name, System.StringComparison.OrdinalIgnoreCase));
        patrolPoints = found.ToArray();
    }

    private bool HasAssignedPatrolPoints()
    {
        if (patrolPoints == null)
            return false;

        for (int i = 0; i < patrolPoints.Length; i++)
        {
            if (patrolPoints[i] != null)
                return true;
        }

        return false;
    }

    private bool IsPatrolPointName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName) || string.IsNullOrWhiteSpace(patrolPointNamePrefix))
            return false;

        return objectName.Equals(patrolPointNamePrefix, System.StringComparison.OrdinalIgnoreCase)
            || objectName.StartsWith(patrolPointNamePrefix + "_", System.StringComparison.OrdinalIgnoreCase)
            || objectName.StartsWith(patrolPointNamePrefix + " ", System.StringComparison.OrdinalIgnoreCase);
    }

    private void AlignPatrolIndexToNearest(Vector3 position)
    {
        ResolvePatrolPoints();

        if (patrolPoints == null || patrolPoints.Length == 0)
            return;

        int nearestIndex = -1;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < patrolPoints.Length; i++)
        {
            Transform point = patrolPoints[i];
            if (point == null)
                continue;

            float distance = (point.position - position).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        if (nearestIndex >= 0)
            patrolIndex = (nearestIndex + 1) % patrolPoints.Length;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, followDistance);

        Gizmos.color = new Color(1f, 0.35f, 0.15f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, runDistance);

        Gizmos.color = new Color(0.95f, 0.55f, 0.1f, 0.25f);
        Gizmos.DrawWireSphere(wanderCenter, wanderRadius);
    }
}
