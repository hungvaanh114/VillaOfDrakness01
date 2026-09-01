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
    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");

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
    [SerializeField, Min(0.1f)] private float chaseSpeed = 3.6f;
    [SerializeField, Min(0.1f)] private float scriptedPassSpeed = 12f;
    [SerializeField, Min(0.1f)] private float searchSpeed = 2.3f;
    [SerializeField, Min(0.1f)] private float wanderSpeed = 1.8f;
    [SerializeField, Min(0.1f)] private float acceleration = 4.5f;
    [SerializeField, Min(0.1f)] private float stoppingDistance = 1.35f;
    [SerializeField, Min(0.1f)] private float wanderRadius = 18f;
    [SerializeField, Min(0.1f)] private float navMeshSnapRadius = 6f;
    [SerializeField, Min(0.1f)] private float navMeshRetryInterval = 0.5f;
    [SerializeField, Min(0.1f)] private float repathInterval = 0.2f;
    [SerializeField, Min(1f)] private float movementTurnSpeed = 720f;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float visualYawOffset;

    [Header("Cutscene Camera Scare")]
    [SerializeField] private float cutsceneCameraScareYawOffset;
    [SerializeField] private float cutsceneCameraScareVerticalOffset = -0.75f;
    [SerializeField, Min(0.35f)] private float testCutsceneCameraScareDistance = 1.1f;
    [SerializeField, Min(0f)] private float testCutsceneCameraScareHoldSeconds = 0.5f;

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
    [SerializeField, Min(0.1f)] private float caughtDeathDelay = 4.5f;
    [SerializeField, Range(0f, 10f)] private float caughtCameraShakeAmplitude = 4.2f;
    [SerializeField, Range(0f, 20f)] private float caughtCameraShakeFrequency = 8f;
    [SerializeField, Min(0f)] private float caughtLookHeight = 1.6f;
    [SerializeField] private Transform caughtCameraTarget;
    [SerializeField] private string caughtCameraTargetName = "camtagetzom";
    [SerializeField] private Transform caughtHeadLookTarget;
    [SerializeField] private string caughtHeadLookTargetName = "head.x";
    [SerializeField] private bool hidePlayerModelDuringCaughtCamera = true;
    [SerializeField, Min(0.4f)] private float caughtProneMinCameraDistance = 2.2f;
    [SerializeField, Min(0.05f)] private float caughtProneEyeHeight = 0.42f;
    [SerializeField] private float caughtProneCameraLocalBackOffset = -0.25f;
    [SerializeField, Min(0.01f)] private float caughtProneSettleTime = 0.35f;
    [SerializeField, Min(0f)] private float caughtProneBackAwaySpeed = 5.5f;
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
    [SerializeField] private bool voiceLinePlaysOnce = true;
    [SerializeField, Range(0f, 1f)] private float voiceLineVolume = 0.85f;
    [SerializeField, Range(0f, 1f)] private float voiceMinAudibleVolume = 0.22f;
    [SerializeField, Min(0.1f)] private float voiceFullVolumeDistance = 3f;
    [SerializeField, Min(0.1f)] private float voiceFadeDistance = 30f;
    [SerializeField, Range(0f, 1f)] private float attackScreamVolume = 0.72f;

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
    private bool hasPlayedVoiceLine;
    private bool suppressRunUntilPlayerEscapesInitialRange;
    private bool caughtDeathSequenceRunning;
    private Quaternion visualRootBaseLocalRotation = Quaternion.identity;
    private Vector3 visualRootBaseLocalPosition;

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
        if (visualRoot == null && animator != null)
            visualRoot = animator.transform;
        if (visualRoot != null)
        {
            visualRootBaseLocalRotation = visualRoot.localRotation;
            visualRootBaseLocalPosition = visualRoot.localPosition;
        }
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

        if (caughtDeathSequenceRunning)
        {
            FacePlayer();
            return;
        }

        bool canDetectPlayer = CanDetectPlayer();
        if (canDetectPlayer)
        {
            lastSeenTime = Time.time;
            lastKnownPlayerPosition = player.position;
            hasSeenPlayerInSearch = searchWindowActive;

            if (state != MonsterState.Chasing && state != MonsterState.Attacking)
                StartChaseAudio();

            if (state != MonsterState.Attacking)
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

    public void EnableHunt(bool beginSearchNow = false, bool suppressInitialRunIfPlayerInRange = false)
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
        nextFootstepTime = Time.time;
        nextSearchTime = Time.time + searchInterval;
        searchEndTime = 0f;
        suppressRunUntilPlayerEscapesInitialRange = suppressInitialRunIfPlayerInRange && IsPlayerInRunDistance();
        caughtDeathSequenceRunning = false;

        if (beginSearchNow)
            BeginSearchWindow();
    }

    public void DisableHunt(bool hideMesh)
    {
        bool wasMakingThreatAudio = IsHuntActive
            || state == MonsterState.Scripted
            || (monsterAudioSource != null && monsterAudioSource.isPlaying);

        if (scriptedRoutine != null)
        {
            StopCoroutine(scriptedRoutine);
            scriptedRoutine = null;
        }

        state = MonsterState.Disabled;
        suppressRunUntilPlayerEscapesInitialRange = false;
        caughtDeathSequenceRunning = false;
        TryStopAgent(resetPath: true);

        SetRunAnimation(false);
        if (hideMesh)
        {
            SetMeshVisible(false);
            StopMonsterAudio(wasMakingThreatAudio);
        }
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

        Vector3 finalPosition = position;
        bool canPlaceAgent = agent != null
            && agent.gameObject.activeInHierarchy
            && TrySampleNavMesh(position, out finalPosition);

        transform.SetPositionAndRotation(canPlaceAgent ? finalPosition : position, rotation);

        if (canPlaceAgent)
            agent.enabled = true;

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
            TryResumeAgent();
            if (TrySampleNavMesh(point.position, out var sampledPosition))
            {
                currentWanderTarget = sampledPosition;
                TrySetAgentDestination(sampledPosition);
            }
        }
    }

    public void PlayScriptedApproachThenWander(Transform approachPoint, float moveSpeed, float randomRadius)
    {
        if (approachPoint == null)
            return;

        if (scriptedRoutine != null)
            StopCoroutine(scriptedRoutine);

        scriptedRoutine = StartCoroutine(ScriptedApproachThenWanderRoutine(approachPoint, moveSpeed, randomRadius));
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

    [ContextMenu("Test Cutscene Camera Scare")]
    private void TestCutsceneCameraScare()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Test Cutscene Camera Scare only runs in Play Mode.");
            return;
        }

        Camera targetCamera = Camera.main;
        if (targetCamera == null)
            targetCamera = FindFirstObjectByType<Camera>();

        if (targetCamera == null)
        {
            Debug.LogWarning("Cannot test cutscene camera scare because no camera was found.");
            return;
        }

        if (scriptedRoutine != null)
        {
            StopCoroutine(scriptedRoutine);
            scriptedRoutine = null;
        }

        StartCoroutine(PlayCutsceneBlinkTeleportScare(
            targetCamera,
            transform,
            0,
            0.08f,
            0f,
            testCutsceneCameraScareDistance,
            testCutsceneCameraScareHoldSeconds,
            0f));
    }

    public IEnumerator PlayCutsceneBlinkTeleportScare(
        Camera targetCamera,
        Transform returnPoint,
        int flickerCount,
        float flickerInterval,
        float teleportDelay,
        float cameraDistance,
        float cameraHoldSeconds,
        float hiddenSeconds)
    {
        if (scriptedRoutine != null)
        {
            StopCoroutine(scriptedRoutine);
            scriptedRoutine = null;
        }

        state = MonsterState.Scripted;
        ConfigureAgent();
        TryStopAgent(resetPath: true);
        SetRunAnimation(false);

        Vector3 returnPosition = returnPoint != null ? returnPoint.position : transform.position;
        Quaternion returnRotation = returnPoint != null ? returnPoint.rotation : transform.rotation;
        var savedBlocks = CaptureRendererPropertyBlocks();

        yield return FlickerNoiseRoutine(
            Mathf.Max(0, flickerCount),
            Mathf.Max(0.02f, flickerInterval));

        if (teleportDelay > 0f)
            yield return new WaitForSeconds(teleportDelay);

        if (targetCamera != null)
        {
            Vector3 scarePosition = targetCamera.transform.position
                + targetCamera.transform.forward * Mathf.Max(0.35f, cameraDistance);
            scarePosition.y = targetCamera.transform.position.y + cutsceneCameraScareVerticalOffset;

            Quaternion scareRotation = GetCameraScareFacingRotation(scarePosition, targetCamera);
            if (agent != null)
                agent.enabled = false;

            transform.SetPositionAndRotation(scarePosition, scareRotation);
            ApplyVisualYawOffset(cutsceneCameraScareYawOffset);
            SetMeshVisible(true);
            ApplyNoiseFrame(0.1f, 0.65f, cutsceneCameraScareYawOffset);
            PlayCutsceneRoar();

            if (cameraHoldSeconds > 0f)
                yield return new WaitForSeconds(cameraHoldSeconds);
        }

        RestoreRendererPropertyBlocks(savedBlocks);
        RestoreVisualRootNoise();
        TeleportTo(returnPosition, returnRotation);
        SetMeshVisible(false);
        TryStopAgent(resetPath: true);

        if (hiddenSeconds > 0f)
            yield return new WaitForSeconds(hiddenSeconds);

        TeleportTo(returnPosition, returnRotation);
        RestoreRendererPropertyBlocks(savedBlocks);
        RestoreVisualRootNoise();
        scriptedRoutine = null;
        state = MonsterState.Disabled;
    }

    private IEnumerator FlickerNoiseRoutine(int count, float interval)
    {
        if (count <= 0)
        {
            SetMeshVisible(true);
            yield break;
        }

        for (int i = 0; i < count; i++)
        {
            SetMeshVisible(false);
            yield return new WaitForSeconds(interval * 0.75f);

            SetMeshVisible(true);
            ApplyNoiseFrame(0.045f, 0.45f);
            yield return new WaitForSeconds(interval * 1.25f);
        }

        SetMeshVisible(true);
        RestoreVisualRootNoise();
    }

    private void PlayCutsceneRoar()
    {
        AudioClip roarClip = PickRandomClip(attackClips);
        float roarVolume = attackScreamVolume;

        var data = ResolveAudioData();
        if (roarClip == null && data != null)
            roarClip = data.ghostJumpscare;

        if (roarClip == null)
        {
            roarClip = PickRandomClip(voiceClips);
            roarVolume = voiceLineVolume;
        }

        if (roarClip == null && data != null)
        {
            roarClip = data.maVuDaiPatrolFull;
            roarVolume = voiceLineVolume;
        }

        if (roarClip == null)
            return;

        PlayClipOneShot(roarClip, roarVolume);
    }

    private MaterialPropertyBlock[] CaptureRendererPropertyBlocks()
    {
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>(true);

        var blocks = new MaterialPropertyBlock[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            blocks[i] = new MaterialPropertyBlock();
            renderers[i].GetPropertyBlock(blocks[i]);
        }

        return blocks;
    }

    private void RestoreRendererPropertyBlocks(MaterialPropertyBlock[] blocks)
    {
        if (renderers == null || blocks == null)
            return;

        int count = Mathf.Min(renderers.Length, blocks.Length);
        for (int i = 0; i < count; i++)
        {
            if (renderers[i] != null)
                renderers[i].SetPropertyBlock(blocks[i]);
        }
    }

    private void ApplyNoiseFrame(float jitterAmount, float colorNoise)
    {
        ApplyNoiseFrame(jitterAmount, colorNoise, 0f);
    }

    private void ApplyNoiseFrame(float jitterAmount, float colorNoise, float extraYawOffset)
    {
        ApplyVisualNoise(jitterAmount, extraYawOffset);

        if (renderers == null || renderers.Length == 0)
            return;

        Color noiseColor = Color.Lerp(
            new Color(0.5f, 0.85f, 1f, 1f),
            Color.white,
            Random.Range(0f, 1f));
        noiseColor *= 1f + Random.Range(0f, Mathf.Max(0f, colorNoise));
        noiseColor.a = 1f;

        var block = new MaterialPropertyBlock();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            renderers[i].GetPropertyBlock(block);
            block.SetColor(BaseColorProperty, noiseColor);
            block.SetColor(ColorProperty, noiseColor);
            block.SetColor(EmissionColorProperty, noiseColor * 1.35f);
            renderers[i].SetPropertyBlock(block);
            block.Clear();
        }
    }

    private void ApplyVisualNoise(float amount)
    {
        ApplyVisualNoise(amount, 0f);
    }

    private void ApplyVisualNoise(float amount, float extraYawOffset)
    {
        if (visualRoot == null || visualRoot == transform)
            return;

        visualRoot.localPosition = visualRootBaseLocalPosition + new Vector3(
            Random.Range(-amount, amount),
            Random.Range(-amount, amount),
            Random.Range(-amount, amount));

        visualRoot.localRotation = visualRootBaseLocalRotation
            * Quaternion.Euler(
                Random.Range(-2f, 2f),
                visualYawOffset + extraYawOffset + Random.Range(-4f, 4f),
                Random.Range(-2f, 2f));
    }

    private void RestoreVisualRootNoise()
    {
        if (visualRoot == null || visualRoot == transform)
            return;

        visualRoot.localPosition = visualRootBaseLocalPosition;
        ApplyVisualYawOffset();
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
        TryResetAgentPath();

        state = MonsterState.Disabled;
        scriptedRoutine = null;
    }

    private IEnumerator MoveScriptedTo(Vector3 target, bool running)
    {
        float moveSpeed = running ? scriptedPassSpeed : wanderSpeed;
        yield return MoveScriptedTo(target, moveSpeed, running);
    }

    private IEnumerator MoveScriptedTo(Vector3 target, float moveSpeed, bool running)
    {
        if (!EnsureAgentOnNavMesh(force: true))
        {
            yield return MoveScriptedTransformTo(target, moveSpeed, running);
            yield break;
        }

        agent.speed = moveSpeed;
        TryResumeAgent();
        if (!TrySetAgentDestination(target))
        {
            yield return MoveScriptedTransformTo(target, moveSpeed, running);
            yield break;
        }

        while (agent.enabled && agent.isOnNavMesh && agent.pathPending)
            yield return null;

        while (agent.enabled && agent.isOnNavMesh && agent.remainingDistance > stoppingDistance + 0.15f)
        {
            FaceMovementDirection();
            TickFootsteps(running);
            yield return null;
        }
    }

    private IEnumerator ScriptedApproachThenWanderRoutine(Transform approachPoint, float moveSpeed, float randomRadius)
    {
        state = MonsterState.Scripted;
        SetMeshVisible(true);
        ConfigureAgent();
        EnsureAgentOnNavMesh(force: true);
        SetRunAnimation(false);
        nextFootstepTime = Time.time;

        float speed = Mathf.Max(0.1f, moveSpeed);
        yield return MoveScriptedTo(approachPoint.position, speed, false);

        wanderCenter = transform.position;
        float radius = Mathf.Max(0.5f, randomRadius);

        while (state == MonsterState.Scripted)
        {
            Vector3 target = transform.position;
            if (TryGetRandomReachablePoint(wanderCenter, radius, out var randomPoint))
                target = randomPoint;

            yield return MoveScriptedTo(target, speed, false);

            float waitUntil = Time.time + patrolPointWaitTime;
            while (state == MonsterState.Scripted && Time.time < waitUntil)
            {
                SetRunAnimation(false);
                yield return null;
            }
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
            {
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                ApplyVisualYawOffset();
            }

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
        if (suppressRunUntilPlayerEscapesInitialRange && distanceToPlayer > runDistance)
            suppressRunUntilPlayerEscapesInitialRange = false;

        bool shouldRun = distanceToPlayer <= runDistance && !suppressRunUntilPlayerEscapesInitialRange;
        SetRunAnimation(shouldRun);

        if (distanceToPlayer <= attackRange && Time.time >= nextAttackTime)
        {
            StartCoroutine(AttackRoutine());
            return;
        }

        if (!HasActiveAgentOnNavMesh())
            return;

        agent.speed = shouldRun ? chaseSpeed : searchSpeed;
        TryResumeAgent();
        TryOpenDoorAhead();
        FaceMovementDirection();
        TickFootsteps(shouldRun);

        if (Time.time < nextRepathTime)
            return;

        nextRepathTime = Time.time + repathInterval;
        if (TrySampleNavMesh(player.position, out var targetPosition))
            TrySetAgentDestination(targetPosition);
    }

    private IEnumerator AttackRoutine()
    {
        if (caughtDeathSequenceRunning)
            yield break;

        state = MonsterState.Attacking;
        nextAttackTime = Time.time + attackCooldown;
        TryStopAgent(resetPath: true);

        FacePlayer();
        SetRunAnimation(false);
        animator?.SetTrigger(SwipingHash);
        bool playedAttack = PlayRandomOneShot(attackClips, attackScreamVolume);
        if (!playedAttack)
            playedAttack = PlayClipOneShot(ResolveAudioData()?.ghostJumpscare, attackScreamVolume);
        if (!playedAttack)
            AudioManager.Instance?.PlayGhostJumpscare(attackScreamVolume);

        if (killPlayerOnHit && player != null && Vector3.Distance(transform.position, player.position) <= attackRange + 0.65f)
        {
            StartCoroutine(CaughtDeathRoutine());
            yield break;
        }

        if (attackHitDelay > 0f)
            yield return new WaitForSeconds(attackHitDelay);

        if (killPlayerOnHit && player != null && Vector3.Distance(transform.position, player.position) <= attackRange + 0.65f)
        {
            StartCoroutine(CaughtDeathRoutine());
            yield break;
        }

        if (GameController.Instance == null || GameController.Instance.currentGameState != GameController.GameState.Dead)
            state = MonsterState.Chasing;
    }

    private IEnumerator CaughtDeathRoutine()
    {
        if (caughtDeathSequenceRunning)
            yield break;

        caughtDeathSequenceRunning = true;
        state = MonsterState.Attacking;

        TryStopAgent(resetPath: true);

        var controller = GameController.Instance;
        var playerController = player != null
            ? player.GetComponent<FpsHorrorKit.FpsController>()
            : FindFirstObjectByType<FpsHorrorKit.FpsController>();

        controller?.SetGameState(GameController.GameState.Cutscene);
        controller?.TriggerJumpscareCheckpointRespawn(true);

        float originalAmplitude = 0f;
        float originalFrequency = 0f;
        bool hasHeadBob = playerController != null && playerController.headBob != null;
        if (hasHeadBob)
        {
            originalAmplitude = playerController.headBob.AmplitudeGain;
            originalFrequency = playerController.headBob.FrequencyGain;
        }

        Vector3 originalFollowTargetLocalPosition = playerController != null && playerController.followTarget != null
            ? playerController.followTarget.localPosition
            : Vector3.zero;
        Transform cameraTarget = ResolveCaughtCameraTarget();
        if (hidePlayerModelDuringCaughtCamera && cameraTarget != null)
            SetCaughtPlayerModelVisible(playerController, false);
        FacePlayer();

        float elapsed = 0f;
        float duration = Mathf.Max(0.1f, caughtDeathDelay);
        while (elapsed < duration)
        {
            Vector3 caughtLookTarget = GetCaughtHeadLookPosition();
            float poseT = Mathf.Clamp01(elapsed / caughtProneSettleTime);
            ApplyCaughtCameraPose(playerController, cameraTarget, caughtLookTarget, originalFollowTargetLocalPosition, poseT);
            ForcePlayerLookAtMonsterHead(playerController, caughtLookTarget);
            ApplyCaughtCameraPose(playerController, cameraTarget, caughtLookTarget, originalFollowTargetLocalPosition, 1f);
            playerController?.StopCutSceneMovement();

            if (hasHeadBob)
            {
                float intensity = 1f - Mathf.Clamp01(elapsed / duration);
                playerController.headBob.AmplitudeGain = caughtCameraShakeAmplitude * intensity;
                playerController.headBob.FrequencyGain = caughtCameraShakeFrequency;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (hasHeadBob)
        {
            playerController.headBob.AmplitudeGain = originalAmplitude;
            playerController.headBob.FrequencyGain = originalFrequency;
        }

        AudioManager.Instance?.StopMonsterVoice();
        StopMonsterAudio(false);
        GameController.Instance?.TriggerJumpscareCheckpointRespawn(0f);
    }

    private static void SetCaughtPlayerModelVisible(FpsHorrorKit.FpsController playerController, bool visible)
    {
        if (playerController == null)
            return;

        bool handledVisualRoot = false;
        if (playerController.playerAnimator != null)
        {
            SetRenderersVisible(playerController.playerAnimator.transform, visible);
            handledVisualRoot = true;
        }

        if (playerController.detachedHairRoot != null)
        {
            SetRenderersVisible(playerController.detachedHairRoot, visible);
            handledVisualRoot = true;
        }

        if (handledVisualRoot)
            return;

        var renderers = playerController.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer itemRenderer = renderers[i];
            if (itemRenderer != null)
                itemRenderer.enabled = visible;
        }
    }

    private static void SetRenderersVisible(Transform root, bool visible)
    {
        if (root == null)
            return;

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer itemRenderer = renderers[i];
            if (itemRenderer != null)
                itemRenderer.enabled = visible;
        }
    }

    private void ApplyCaughtCameraPose(
        FpsHorrorKit.FpsController playerController,
        Transform cameraTarget,
        Vector3 lookTarget,
        Vector3 originalFollowTargetLocalPosition,
        float t)
    {
        if (playerController == null)
            return;

        if (cameraTarget != null && playerController.followTarget != null)
        {
            playerController.followTarget.position = cameraTarget.position;
            return;
        }

        ApplyCaughtPronePose(playerController, lookTarget, originalFollowTargetLocalPosition, t);
    }

    private void ApplyCaughtPronePose(
        FpsHorrorKit.FpsController playerController,
        Vector3 lookTarget,
        Vector3 originalFollowTargetLocalPosition,
        float t)
    {
        if (playerController == null)
            return;

        MoveCaughtPlayerAwayFromMonster(playerController, lookTarget);

        if (playerController.followTarget == null)
            return;

        Vector3 proneLocalPosition = originalFollowTargetLocalPosition;
        proneLocalPosition.y = caughtProneEyeHeight;
        proneLocalPosition.z += caughtProneCameraLocalBackOffset;
        playerController.followTarget.localPosition = Vector3.Lerp(
            originalFollowTargetLocalPosition,
            proneLocalPosition,
            Mathf.SmoothStep(0f, 1f, t));
    }

    private void MoveCaughtPlayerAwayFromMonster(FpsHorrorKit.FpsController playerController, Vector3 lookTarget)
    {
        if (caughtProneBackAwaySpeed <= 0f)
            return;

        Vector3 eyePosition = GetPlayerEyePosition(playerController);
        Vector3 flatEye = eyePosition;
        Vector3 flatTarget = lookTarget;
        flatEye.y = 0f;
        flatTarget.y = 0f;

        Vector3 away = flatEye - flatTarget;
        if (away.sqrMagnitude <= 0.01f)
        {
            away = playerController.transform.position - transform.position;
            away.y = 0f;
        }

        if (away.sqrMagnitude <= 0.01f)
            away = -playerController.transform.forward;

        float distance = Vector3.Distance(flatEye, flatTarget);
        float deficit = caughtProneMinCameraDistance - distance;
        if (deficit <= 0f)
            return;

        Vector3 motion = away.normalized * Mathf.Min(deficit, caughtProneBackAwaySpeed * Time.deltaTime);
        var characterController = playerController.GetComponent<CharacterController>();
        if (characterController != null && characterController.enabled && characterController.gameObject.activeInHierarchy)
            characterController.Move(motion);
        else
            playerController.transform.position += motion;
    }

    private Vector3 GetStableCaughtLookTarget(FpsHorrorKit.FpsController playerController)
    {
        Vector3 headPosition = GetLookAtHeadPosition();
        if (playerController == null)
            return headPosition;

        Vector3 eyePosition = GetPlayerEyePosition(playerController);
        Vector3 flatDirection = headPosition - eyePosition;
        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude >= 0.09f)
            return headPosition;

        Vector3 fallbackDirection = transform.position - playerController.transform.position;
        fallbackDirection.y = 0f;
        if (fallbackDirection.sqrMagnitude < 0.09f)
            fallbackDirection = playerController.transform.forward;

        float height = Mathf.Max(0.2f, headPosition.y - eyePosition.y);
        return eyePosition + fallbackDirection.normalized * 1.6f + Vector3.up * height;
    }

    private Vector3 GetCaughtHeadLookPosition()
    {
        Transform target = ResolveCaughtHeadLookTarget();
        return target != null ? target.position : GetLookAtHeadPosition();
    }

    private Transform ResolveCaughtCameraTarget()
    {
        if (caughtCameraTarget != null)
            return caughtCameraTarget;

        caughtCameraTarget = FindChildTransformByName(transform, caughtCameraTargetName);
        if (caughtCameraTarget == null && !string.IsNullOrWhiteSpace(caughtCameraTargetName))
        {
            GameObject targetObject = GameObject.Find(caughtCameraTargetName);
            if (targetObject != null)
                caughtCameraTarget = targetObject.transform;
        }

        return caughtCameraTarget;
    }

    private Transform ResolveCaughtHeadLookTarget()
    {
        if (caughtHeadLookTarget != null)
            return caughtHeadLookTarget;

        caughtHeadLookTarget = FindChildTransformByName(transform, caughtHeadLookTargetName);
        return caughtHeadLookTarget;
    }

    private static Transform FindChildTransformByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
            return null;

        if (root.name == targetName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChildTransformByName(root.GetChild(i), targetName);
            if (result != null)
                return result;
        }

        return null;
    }

    private void ForcePlayerLookAtMonsterHead(FpsHorrorKit.FpsController playerController, Vector3 headPosition)
    {
        if (playerController == null)
            return;

        Vector3 eyePosition = GetPlayerEyePosition(playerController);
        Vector3 direction = headPosition - eyePosition;
        if (direction.sqrMagnitude <= 0.01f)
            return;

        Vector3 flatDirection = direction;
        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude > 0.04f)
            playerController.transform.rotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);

        Vector3 normalized = direction.normalized;
        float pitch = -Mathf.Asin(Mathf.Clamp(normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
        playerController.SetCutSceneCameraPitch(pitch);
    }

    private static Vector3 GetPlayerEyePosition(FpsHorrorKit.FpsController playerController)
    {
        return playerController.followTarget != null
            ? playerController.followTarget.position
            : playerController.transform.position + Vector3.up * 1.55f;
    }

    private Vector3 GetLookAtHeadPosition()
    {
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>(true);

        bool hasBounds = false;
        Bounds bounds = default;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer itemRenderer = renderers[i];
            if (itemRenderer == null || !itemRenderer.enabled)
                continue;

            if (!hasBounds)
            {
                bounds = itemRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(itemRenderer.bounds);
            }
        }

        if (hasBounds)
            return new Vector3(bounds.center.x, bounds.max.y - 0.15f, bounds.center.z);

        return transform.position + Vector3.up * caughtLookHeight;
    }

    private void TickSearching()
    {
        SetRunAnimation(false);
        if (!HasActiveAgentOnNavMesh())
            return;

        agent.speed = searchSpeed;
        TryResumeAgent();
        TryOpenDoorAhead();
        FaceMovementDirection();
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
            TrySetAgentDestination(targetPosition);

        if (!agent.pathPending && agent.remainingDistance <= stoppingDistance + 0.25f)
            ChooseWanderTarget();
    }

    private void TickWandering()
    {
        if (!HasActiveAgentOnNavMesh())
            return;

        bool runningToPriorityTarget = hasPriorityWanderTarget && priorityWanderSpeed > wanderSpeed + 0.1f;
        SetRunAnimation(runningToPriorityTarget);
        agent.speed = hasPriorityWanderTarget ? priorityWanderSpeed : wanderSpeed;
        TryResumeAgent();
        FaceMovementDirection();
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
                TrySetAgentDestination(currentWanderTarget);
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
            TrySetAgentDestination(currentWanderTarget);
        }
    }

    private void TickAmbientAnimationAndAudio()
    {
        if (Time.time >= nextFlairTime && state == MonsterState.Wandering)
        {
            animator?.SetTrigger(FlairHash);
            if (!PlayRandomOneShot(idleHorrorClips, 0.55f))
            {
                var audioManager = AudioManager.Instance;
                if (audioManager != null && audioManager.CanPlaySanityWarningAudio())
                    PlayClipOneShot(ResolveAudioData()?.sanityWarning, 0.55f);
            }
            ScheduleNextFlair();
        }

        if (Time.time >= nextVoiceTime && (state == MonsterState.Wandering || state == MonsterState.Searching))
        {
            if (PlayVoiceLine())
                ScheduleNextVoice();
            else
                nextVoiceTime = float.PositiveInfinity;
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

    private bool PlayVoiceLine()
    {
        if ((voiceLinePlaysOnce && hasPlayedVoiceLine) || AudioManager.Instance != null && AudioManager.Instance.HasMaVuDaiPatrolPlayed)
            return false;

        hasPlayedVoiceLine = true;
        bool playedVoice = PlayRandomVoiceOneShot(voiceClips, voiceLineVolume);
        if (playedVoice)
        {
            AudioManager.Instance?.MarkMaVuDaiPatrolPlayed();
            return true;
        }

        var data = ResolveAudioData();
        playedVoice = data != null && PlayVoiceClipOneShot(data.maVuDaiPatrolFull, voiceLineVolume);
        if (playedVoice)
        {
            AudioManager.Instance?.MarkMaVuDaiPatrolPlayed();
            return true;
        }

        return AudioManager.Instance != null && AudioManager.Instance.PlayMaVuDaiPatrol(GetMonsterVoiceVolume(voiceLineVolume)) > 0f;
    }

    private bool PlayRandomVoiceOneShot(AudioClip[] clips, float volume)
    {
        if (clips == null || clips.Length == 0)
            return false;

        var clip = clips[Random.Range(0, clips.Length)];
        return PlayVoiceClipOneShot(clip, volume);
    }

    private bool PlayRandomOneShot(AudioClip[] clips, float volume)
    {
        if (clips == null || clips.Length == 0 || monsterAudioSource == null)
            return false;

        var clip = PickRandomClip(clips);
        return PlayClipOneShot(clip, volume);
    }

    private static AudioClip PickRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return null;

        for (int attempts = 0; attempts < clips.Length; attempts++)
        {
            var clip = clips[Random.Range(0, clips.Length)];
            if (clip != null)
                return clip;
        }

        return null;
    }

    private bool PlayClipOneShot(AudioClip clip, float volume)
    {
        if (clip == null || monsterAudioSource == null)
            return false;

        AudioManager.Instance?.PauseMonsterVoiceForRoar(clip.length);
        AudioManager.Instance?.BlockGameplayAmbience(clip.length);
        monsterAudioSource.PlayOneShot(clip, volume);
        return true;
    }

    private bool PlayVoiceClipOneShot(AudioClip clip, float volume)
    {
        if (clip == null)
            return false;

        float scaledVolume = GetMonsterVoiceVolume(volume);
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayMonsterVoice(clip, scaledVolume);
            return true;
        }

        if (monsterAudioSource == null)
            return false;

        monsterAudioSource.PlayOneShot(clip, scaledVolume);
        return true;
    }

    private float GetMonsterVoiceVolume(float baseVolume)
    {
        float volume = Mathf.Clamp01(baseVolume);
        if (player == null)
            return Mathf.Max(volume, voiceMinAudibleVolume);

        float distance = Vector3.Distance(transform.position, player.position);
        float fadeStart = Mathf.Max(0f, voiceFullVolumeDistance);
        float fadeEnd = Mathf.Max(fadeStart + 0.1f, voiceFadeDistance);
        float t = Mathf.InverseLerp(fadeStart, fadeEnd, distance);
        float distanceMultiplier = Mathf.Lerp(1f, voiceMinAudibleVolume, t);
        return Mathf.Clamp01(volume * distanceMultiplier);
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

    private void StopMonsterAudio(bool stopSharedThreatAudio)
    {
        if (monsterAudioSource != null)
        {
            monsterAudioSource.Stop();
            monsterAudioSource.clip = null;
        }

        nextFootstepTime = float.PositiveInfinity;
        nextVoiceTime = float.PositiveInfinity;

        if (stopSharedThreatAudio)
            AudioManager.Instance?.StopMonsterThreatAudio();
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
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            ApplyVisualYawOffset();
        }
    }

    private void FaceMovementDirection()
    {
        if (!HasActiveAgentOnNavMesh())
            return;

        Vector3 direction = agent.desiredVelocity;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.01f && agent.hasPath)
        {
            direction = agent.steeringTarget - transform.position;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude <= 0.01f)
        {
            ApplyVisualYawOffset();
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            Mathf.Max(1f, movementTurnSpeed) * Time.deltaTime);

        ApplyVisualYawOffset();
    }

    private void ApplyVisualYawOffset()
    {
        ApplyVisualYawOffset(0f);
    }

    private void ApplyVisualYawOffset(float extraYawOffset)
    {
        if (visualRoot == null || visualRoot == transform)
            return;

        visualRoot.localRotation = visualRootBaseLocalRotation * Quaternion.Euler(0f, visualYawOffset + extraYawOffset, 0f);
    }

    private static Quaternion GetCameraScareFacingRotation(Vector3 scarePosition, Camera targetCamera)
    {
        Vector3 lookDirection = targetCamera.transform.position - scarePosition;
        lookDirection.y = 0f;

        if (lookDirection.sqrMagnitude <= 0.0001f)
        {
            lookDirection = -targetCamera.transform.forward;
            lookDirection.y = 0f;
        }

        return lookDirection.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(lookDirection.normalized, Vector3.up)
            : targetCamera.transform.rotation;
    }

    private void ChooseWanderTarget()
    {
        hasWanderTarget = false;

        if (usePatrolPoints && TryChoosePatrolTarget())
            return;

        if (TryGetRandomReachablePoint(wanderCenter, wanderRadius, out var sampledPosition))
        {
            currentWanderTarget = sampledPosition;
            hasWanderTarget = true;
        }
    }

    private bool TryGetRandomReachablePoint(Vector3 center, float radius, out Vector3 sampledPosition)
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector2 offset = Random.insideUnitCircle * radius;
            Vector3 candidate = center + new Vector3(offset.x, 0f, offset.y);
            if (!TrySampleNavMesh(candidate, out sampledPosition))
                continue;

            if (!CanReach(sampledPosition))
                continue;

            return true;
        }

        sampledPosition = center;
        return false;
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
        TryResetAgentPath();
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
        if (agent == null)
            return;

        agent.speed = chaseSpeed;
        agent.acceleration = acceleration;
        agent.stoppingDistance = stoppingDistance;
        agent.autoBraking = true;
        agent.autoRepath = true;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        agent.updateRotation = false;
        TryResumeAgent();
    }

    private bool HasActiveAgentOnNavMesh()
    {
        return agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;
    }

    private bool TryResumeAgent()
    {
        if (!HasActiveAgentOnNavMesh())
            return false;

        agent.isStopped = false;
        return true;
    }

    private bool TryStopAgent(bool resetPath)
    {
        if (!HasActiveAgentOnNavMesh())
            return false;

        agent.isStopped = true;
        if (resetPath)
            TryResetAgentPath();
        return true;
    }

    private bool TryResetAgentPath()
    {
        if (!HasActiveAgentOnNavMesh() || !agent.hasPath)
            return false;

        agent.ResetPath();
        return true;
    }

    private bool TrySetAgentDestination(Vector3 destination)
    {
        return HasActiveAgentOnNavMesh() && agent.SetDestination(destination);
    }

    private void StartChaseAudio()
    {
        var controller = GameController.Instance;
        if (controller != null && controller.currentChapterPhase < GameController.ChapterPhase.Escape)
            controller.SetChapterPhase(GameController.ChapterPhase.Escape);

    }

    private bool IsPlayerInRunDistance()
    {
        return player != null && Vector3.Distance(transform.position, player.position) <= runDistance;
    }

    private bool IsPlayerCaughtOrDead()
    {
        var controller = GameController.Instance;
        return caughtDeathSequenceRunning
            || controller != null && controller.currentGameState == GameController.GameState.Dead;
    }

    private void SnapToNavMesh()
    {
        EnsureAgentOnNavMesh(force: true);
    }

    private bool EnsureAgentOnNavMesh(bool force = false)
    {
        if (agent == null || !agent.gameObject.activeInHierarchy)
            return false;

        if (!agent.enabled)
        {
            if (!force || !TrySampleNavMesh(transform.position, out var sampledPosition))
                return false;

            transform.position = sampledPosition;
            agent.enabled = true;
        }

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
