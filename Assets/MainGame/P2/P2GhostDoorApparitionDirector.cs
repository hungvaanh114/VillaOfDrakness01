using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;

namespace MainGame.P2
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class P2GhostDoorApparitionDirector : MonoBehaviour
    {
        private const float UpperFloorMinY = 2.4f;

        private enum DoorApparitionMode
        {
            Ambient,
            AudioLogScan,
            PostBreakHunt
        }

        [Header("Scene References")]
        [SerializeField] private Transform player;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform patrolRoot;
        [SerializeField] private Transform doorApparitionRoot;
        [SerializeField] private Transform downstairsPatrolRoot;
        [SerializeField] private Transform upstairsPatrolRoot;
        [SerializeField] private Transform downstairsDoorApparitionRoot;
        [SerializeField] private Transform upstairsDoorApparitionRoot;
        [SerializeField] private P2OilLamp oilLamp;
        [SerializeField] private NavMeshAgent agent;

        [Header("Patrol")]
        [SerializeField] private bool autoCollectPointsFromChildren = true;
        [SerializeField] private bool pingPongPatrol = true;
        [SerializeField] private Transform[] patrolPoints = Array.Empty<Transform>();
        [SerializeField, Min(0.05f)] private float waypointReachDistance = 0.35f;
        [SerializeField, Min(0.1f)] private float patrolSpeed = 1.6f;
        [SerializeField, Min(0.1f)] private float awakenedSpeed = 2.2f;
        [SerializeField, Min(0.1f)] private float navMeshSampleRadius = 2.5f;
        [SerializeField] private bool beginAwakened;

        [Header("Floor Routing")]
        [SerializeField] private bool lockUpperFloorUntilAwakened = true;
        [SerializeField] private bool showUpperFloorAfterAwakened = true;

        [Header("Door Apparition")]
        [SerializeField] private bool enableDoorApparitions = true;
        [SerializeField] private bool useDoorApparitionPoints;
        [SerializeField] private bool useSceneDoorsForApparitions = true;
        [SerializeField, Min(0.1f)] private float apparitionVisibleSeconds = 1.4f;
        [SerializeField, Min(0.1f)] private float minDistanceFromPlayer = 3f;
        [SerializeField, Min(0.1f)] private float maxDistanceFromPlayer = 13f;
        [SerializeField, Min(0.1f)] private float doorPointMaxDistance = 2.4f;
        [SerializeField] private bool avoidPlayerCameraView = true;
        [SerializeField] private bool hideVisualBetweenApparitions;

        [Header("Door Apparition Walk Away")]
        [SerializeField] private bool walkAwayAfterDoorApparition = true;
        [SerializeField] private bool useDoorApparitionPointsForWalkAway;
        [SerializeField, Min(0f)] private float avoidPlayerAfterTeleportSeconds = 3f;
        [SerializeField, Min(0f)] private float lampOffDirectApproachSeconds = 10f;
        [SerializeField, Min(0.5f)] private float apparitionWalkAwayDistance = 7f;
        [SerializeField, Min(0.1f)] private float apparitionWalkAwayMaxSeconds = 6f;
        [SerializeField, Range(-1f, 1f)] private float walkAwayMinDirectionDot = 0.25f;

        [Header("Chase")]
        [SerializeField, Min(0.1f)] private float chaseSpeed = 3.4f;
        [SerializeField, Min(0.1f)] private float catchDistance = 1.25f;
        [SerializeField, Min(0f)] private float attackDelayAfterTeleport = 3f;
        [SerializeField, Min(0.5f)] private float postBreakDoorJumpSeconds = 10f;
        [SerializeField, Range(5f, 180f)] private float chaseViewAngle = 95f;
        [SerializeField, Min(0.1f)] private float chaseSightDistance = 16f;
        [SerializeField, Min(0f)] private float chaseLostSightGraceSeconds = 0.25f;
        [SerializeField] private LayerMask chaseLineOfSightMask = ~0;
        [SerializeField, Min(0f)] private float ghostEyeHeight = 1.35f;
        [SerializeField, Min(0f)] private float playerEyeHeight = 1.35f;

        [Header("Attack")]
        [SerializeField] private Animator animator;
        [SerializeField] private string attackTriggerName = "swiping";
        [SerializeField, Min(0f)] private float attackHitDelay = 0.32f;
        [SerializeField, Min(0f)] private float attackRespawnDelay = 0f;
        [SerializeField] private bool playDeathVoiceImmediately;
        [SerializeField] private int deathVoiceIndex = 3;
        [SerializeField] private AudioClip[] attackClips = Array.Empty<AudioClip>();
        [SerializeField, Range(0f, 1f)] private float attackVolume = 0.85f;
        [SerializeField] private CinemachineCamera attackVirtualCamera;
        [SerializeField] private string attackVirtualCameraName = "CinemachineCameraMada";
        [SerializeField] private int attackCameraPriority = 1000;
        [SerializeField] private Transform attackLookTarget;
        [SerializeField] private string attackLookTargetName = "mixamorig:Head";
        [SerializeField] private Vector3 attackLookTargetOffset = new(0f, 0.22f, 0f);
        [SerializeField, Min(0.05f)] private float attackCameraHoldSeconds = 1f;
        [SerializeField] private bool repeatAttackWhileCaught = true;
        [SerializeField, Min(0.05f)] private float repeatedAttackInterval = 0.55f;

        [Header("Compatibility")]
        [SerializeField] private bool disableOtherGhostAiOnStart = true;

        public bool IsAwakened { get; private set; }

        private readonly List<Transform> reusablePoints = new();
        private Renderer[] visualRenderers = Array.Empty<Renderer>();
        private int patrolIndex;
        private int patrolDirection = 1;
        private int lastPatrolChildCount = -1;
        private int lastApparitionChildCount = -1;
        private string lastPatrolSignature;
        private string lastApparitionSignature;
        private Vector3 lastDestination;
        private bool hasDestination;
        private bool apparitionRunning;
        private bool scriptedPassRunning;
        private bool chasingPlayer;
        private bool attackRunning;
        private bool audioLogSuspended;
        private bool forceBaseSpeedDuringCurrentChase;
        private bool preBreakScanChaseActive;
        private float lostSightTimer;
        private float nextAttackAllowedTime;
        private float avoidPlayerUntilTime;
        private float nextPostBreakDoorJumpTime;
        private float lampOffTimer;
        private bool lampOffDirectApproachTriggered;
        private Coroutine apparitionCoroutine;
        private Coroutine scriptedPassRoutine;
        private DoorApparitionMode activeApparitionMode;
        private float nextApparitionTime;
        private PrioritySettings previousAttackCameraPriority;
        private CameraTarget previousAttackCameraTarget;
        private bool previousAttackCameraEnabled;
        private bool previousAttackCameraActive;
        private CinemachineBrain attackCameraBrain;
        private Transform attackRuntimeLookTarget;
        private float nextRepeatedAttackTime;

        private void Awake()
        {
            ResolveReferences();
            CacheVisualRenderers();
            RefreshPatrolPoints(true);
            IsAwakened = beginAwakened;
            ScheduleNextApparition();
            ApplyVisualHiddenState();
        }

        private void Start()
        {
            ResolveReferences();
            CacheVisualRenderers();
            RefreshPatrolPoints(true);

            if (disableOtherGhostAiOnStart)
                DisableOtherGhostControllers();
        }

        private void Update()
        {
            ResolveReferences();
            RefreshPatrolPoints(false);

            if (audioLogSuspended)
            {
                StopMovementForAudioLog();
                return;
            }

            if (IsGameLocked())
                return;

            if (attackRunning)
                return;

            if (TickTimedDoorJump())
                return;

            if (!apparitionRunning && !scriptedPassRunning && TickChase())
                return;

            if (!apparitionRunning && !scriptedPassRunning)
            {
                TickPatrol();
                TickDoorApparition();
            }
        }

        public void Awaken()
        {
            IsAwakened = true;
            ScheduleNextApparition(1f, 3f);
        }

        public void ForceApparitionNearPlayer()
        {
            if (!isActiveAndEnabled || audioLogSuspended)
                return;

            if (apparitionCoroutine != null)
            {
                StopCoroutine(apparitionCoroutine);
                apparitionCoroutine = null;
            }

            apparitionRunning = false;

            apparitionCoroutine = StartCoroutine(ApparitionRoutine(true, false, GetDefaultApparitionMode()));
        }

        public void ForceApparitionAtNearestDoor()
        {
            if (!isActiveAndEnabled || audioLogSuspended)
                return;

            if (apparitionCoroutine != null)
            {
                StopCoroutine(apparitionCoroutine);
                apparitionCoroutine = null;
            }

            apparitionRunning = false;

            apparitionCoroutine = StartCoroutine(ApparitionRoutine(true, true, DoorApparitionMode.AudioLogScan));
        }

        public void ForceChaseFromNearestDoor(bool keepBaseSpeed)
        {
            if (!isActiveAndEnabled || audioLogSuspended)
                return;

            if (apparitionCoroutine != null)
            {
                StopCoroutine(apparitionCoroutine);
                apparitionCoroutine = null;
            }

            if (scriptedPassRoutine != null)
            {
                StopCoroutine(scriptedPassRoutine);
                scriptedPassRoutine = null;
            }

            apparitionRunning = false;
            scriptedPassRunning = false;
            avoidPlayerUntilTime = 0f;
            forceBaseSpeedDuringCurrentChase = keepBaseSpeed;
            activeApparitionMode = IsPostBreakHuntActive() ? DoorApparitionMode.PostBreakHunt : DoorApparitionMode.Ambient;

            if ((TryChooseNearestSceneDoorApparitionPoint(out var point) || TryChooseApparitionPoint(out point)) && point != null)
                WarpTo(point.position, point.rotation);

            SetVisualVisible(true);
            FacePlayer();
            StartChase(false);
        }

        public void PlayScriptedPass(Transform startPoint, Transform endPoint, Action onComplete = null)
        {
            if (audioLogSuspended)
            {
                onComplete?.Invoke();
                return;
            }

            if (!isActiveAndEnabled || startPoint == null || endPoint == null)
            {
                onComplete?.Invoke();
                return;
            }

            if (scriptedPassRoutine != null)
                StopCoroutine(scriptedPassRoutine);

            scriptedPassRoutine = StartCoroutine(ScriptedPassRoutine(startPoint, endPoint, onComplete));
        }

        public void SetAudioLogSuspended(bool suspended)
        {
            if (audioLogSuspended == suspended)
                return;

            audioLogSuspended = suspended;
            if (suspended)
            {
                if (apparitionCoroutine != null)
                {
                    StopCoroutine(apparitionCoroutine);
                    apparitionCoroutine = null;
                }

                if (scriptedPassRoutine != null)
                {
                    StopCoroutine(scriptedPassRoutine);
                    scriptedPassRoutine = null;
                }

                apparitionRunning = false;
                scriptedPassRunning = false;
                chasingPlayer = false;
                preBreakScanChaseActive = false;
                lostSightTimer = 0f;
                lampOffTimer = 0f;
                lampOffDirectApproachTriggered = false;
                StopMovementForAudioLog();
                SetVisualVisible(false);
                return;
            }

            if (agent != null && agent.enabled && agent.isOnNavMesh && !attackRunning)
                agent.isStopped = false;

            ClearDestination();
            ScheduleNextApparition();
        }

        private void TickPatrol()
        {
            if (patrolPoints == null || patrolPoints.Length == 0)
                return;

            patrolIndex = Mathf.Clamp(patrolIndex, 0, patrolPoints.Length - 1);
            Transform target = patrolPoints[patrolIndex];
            if (target == null)
            {
                AdvancePatrolPoint();
                return;
            }

            if (MoveTo(target.position, CurrentPatrolSpeed(), waypointReachDistance))
                AdvancePatrolPoint();
        }

        private void TickDoorApparition()
        {
            if (!enableDoorApparitions || Time.time < nextApparitionTime)
                return;

            if (apparitionCoroutine == null)
                apparitionCoroutine = StartCoroutine(ApparitionRoutine(false, false, GetDefaultApparitionMode()));
        }

        private bool TickChase()
        {
            if (ShouldAvoidPlayerAfterTeleport() && Time.time < avoidPlayerUntilTime)
                return false;

            bool shouldChase = ShouldChasePlayer();
            if (!chasingPlayer && !shouldChase)
                return false;

            if (shouldChase)
            {
                if (!chasingPlayer)
                    StartChase();
                lostSightTimer = 0f;
            }
            else
            {
                lostSightTimer += Time.deltaTime;
                if (lostSightTimer >= chaseLostSightGraceSeconds)
                {
                    bool wasPreBreakScanChase = preBreakScanChaseActive;
                    StopChase();
                    if (wasPreBreakScanChase)
                        ScheduleNextApparition(1f, 2f);
                    return false;
                }
            }

            if (player == null)
                return false;

            SetVisualVisible(true);
            bool hasCaughtPlayer = MoveTo(player.position, CurrentChaseSpeed(), catchDistance)
                || Vector3.Distance(transform.position, player.position) <= catchDistance;
            if (hasCaughtPlayer && Time.time >= nextAttackAllowedTime)
                StartAttack();
            else if (hasCaughtPlayer)
                FacePlayer();

            return true;
        }

        private void StartAttack()
        {
            if (attackRunning)
                return;

            StartCoroutine(AttackRoutine());
        }

        private IEnumerator AttackRoutine()
        {
            attackRunning = true;
            chasingPlayer = false;
            lostSightTimer = 0f;

            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }

            var gameController = GameController.Instance;
            var p2Controller = P2GameController.Instance;
            var playerController = ResolveFpsController();
            var previousGameState = gameController != null ? gameController.currentGameState : GameController.GameState.Gameplay;
            bool previousCutsceneState = playerController != null && playerController.isCutScene;
            bool previousInteractingState = playerController != null && playerController.isInteracting;
            bool previousRaycastState = true;
            bool changedRaycastState = false;

            StoreAttackCameraState();
            SwitchAttackCamera(true);
            DisablePlayerPresentationController(playerController);
            SetCaughtPlayerModelVisible(playerController, false);

            if (gameController != null)
                gameController.SetGameState(GameController.GameState.Cutscene);
            p2Controller?.LockInput(true);

            if (playerController != null)
            {
                playerController.isCutScene = true;
                playerController.isInteracting = true;
                playerController.StopCutSceneMovement();
            }

            if (FpsHorrorKit.PlayerInteract.Instance != null)
            {
                previousRaycastState = FpsHorrorKit.PlayerInteract.Instance.sendRaycast;
                FpsHorrorKit.PlayerInteract.Instance.sendRaycast = false;
                changedRaycastState = true;
            }

            FacePlayer();
            PlayAttackAnimation();
            nextRepeatedAttackTime = Time.time + repeatedAttackInterval;
            PlayAttackAudio();

            if (attackHitDelay > 0f)
                yield return HoldAttackCameraSeconds(playerController, attackHitDelay);

            if (attackCameraHoldSeconds > 0f)
                yield return HoldAttackCameraSeconds(playerController, attackCameraHoldSeconds);

            if (changedRaycastState && FpsHorrorKit.PlayerInteract.Instance != null)
                FpsHorrorKit.PlayerInteract.Instance.sendRaycast = previousRaycastState;
            p2Controller?.LockInput(false);
            if (playerController != null)
            {
                playerController.isCutScene = previousCutsceneState;
                playerController.isInteracting = previousInteractingState;
                playerController.StopCutSceneMovement();
            }
            if (gameController != null && gameController.currentGameState == GameController.GameState.Cutscene)
                gameController.SetGameState(previousGameState);

            TriggerPlayerDeathLikeChapterOne();
        }

        private void StartChase(bool stopApparitionRoutine = true)
        {
            chasingPlayer = true;
            lostSightTimer = 0f;
            if (!IsPostBreakHuntActive())
                ScheduleNextApparition(postBreakDoorJumpSeconds, postBreakDoorJumpSeconds);

            if (stopApparitionRoutine && apparitionCoroutine != null)
            {
                StopCoroutine(apparitionCoroutine);
                apparitionCoroutine = null;
                apparitionRunning = false;
            }

            SetVisualVisible(true);
            ClearDestination();
        }

        private void StopChase()
        {
            chasingPlayer = false;
            forceBaseSpeedDuringCurrentChase = false;
            preBreakScanChaseActive = false;
            lostSightTimer = 0f;
            AlignPatrolIndexToNearest();
            ClearDestination();
            ScheduleNextApparition();
        }

        private bool ShouldChasePlayer()
        {
            ResolveReferences();
            if (player == null)
                return false;

            if (IsPostBreakHuntActive())
                return true;

            if (!preBreakScanChaseActive)
                return false;

            if (!IsPlayerInFront())
                return false;

            return HasLineOfSightToPlayer();
        }

        private bool TickTimedDoorJump()
        {
            if (attackRunning || scriptedPassRunning || apparitionRunning || audioLogSuspended)
                return false;

            if (IsPostBreakHuntActive())
            {
                if (nextPostBreakDoorJumpTime <= 0f)
                    nextPostBreakDoorJumpTime = Time.time;
                if (Time.time < nextPostBreakDoorJumpTime)
                    return false;

                chasingPlayer = false;
                preBreakScanChaseActive = false;
                ClearDestination();
                ScheduleNextPostBreakDoorJump();
                apparitionCoroutine = StartCoroutine(ApparitionRoutine(true, false, DoorApparitionMode.PostBreakHunt));
                return true;
            }

            if (!chasingPlayer || Time.time < nextApparitionTime)
                return false;

            chasingPlayer = false;
            preBreakScanChaseActive = false;
            ClearDestination();
            apparitionCoroutine = StartCoroutine(ApparitionRoutine(true, false, DoorApparitionMode.Ambient));
            return true;
        }

        private void ScheduleNextPostBreakDoorJump()
        {
            nextPostBreakDoorJumpTime = Time.time + Mathf.Max(0.5f, postBreakDoorJumpSeconds);
        }

        private DoorApparitionMode GetDefaultApparitionMode()
        {
            return IsPostBreakHuntActive() ? DoorApparitionMode.PostBreakHunt : DoorApparitionMode.Ambient;
        }

        private static bool IsPostBreakHuntActive()
        {
            return P2StorySequenceController.HasHouseGlassBroken
                || (P2GameController.Instance != null && P2GameController.Instance.MirrorEventTriggered);
        }

        private void StopMovementForAudioLog()
        {
            ClearDestination();

            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
                return;

            agent.isStopped = true;
            agent.ResetPath();
        }

        private bool IsGameplayLampOff()
        {
            if (oilLamp == null)
                ResolveOilLamp();

            return oilLamp != null && !oilLamp.IsLit;
        }

        private void TrackLampOffTimer()
        {
            if (!IsPostBreakHuntActive())
            {
                lampOffTimer = 0f;
                lampOffDirectApproachTriggered = false;
                return;
            }

            if (IsGameLocked() || !IsGameplayLampOff())
            {
                lampOffTimer = 0f;
                lampOffDirectApproachTriggered = false;
                return;
            }

            lampOffTimer += Time.deltaTime;
            if (lampOffDirectApproachTriggered || lampOffTimer < lampOffDirectApproachSeconds)
                return;

            lampOffDirectApproachTriggered = true;
            if (!attackRunning && !chasingPlayer && !apparitionRunning && !scriptedPassRunning && isActiveAndEnabled)
                apparitionCoroutine = StartCoroutine(ApparitionRoutine(true, false, GetDefaultApparitionMode()));
        }

        private bool ShouldApproachPlayerAfterTeleport()
        {
            return IsPostBreakHuntActive();
        }

        private bool ShouldAvoidPlayerAfterTeleport()
        {
            return !IsPostBreakHuntActive() && !preBreakScanChaseActive;
        }

        private bool ShouldAvoidPlayerCameraView()
        {
            return avoidPlayerCameraView && !IsPostBreakHuntActive();
        }

        private float CurrentPatrolSpeed()
        {
            return IsAwakened ? awakenedSpeed : patrolSpeed;
        }

        private float CurrentChaseSpeed()
        {
            return chaseSpeed;
        }

        private bool IsPlayerInFront()
        {
            if (player == null)
                return false;

            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude <= 0.01f)
                return true;

            float distance = toPlayer.magnitude;
            if (distance > chaseSightDistance)
                return false;

            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.01f)
                forward = toPlayer;

            float angle = Vector3.Angle(forward.normalized, toPlayer.normalized);
            return angle <= chaseViewAngle * 0.5f;
        }

        private bool HasLineOfSightToPlayer()
        {
            if (player == null)
                return false;

            Vector3 origin = transform.position + Vector3.up * ghostEyeHeight;
            Vector3 target = player.position + Vector3.up * playerEyeHeight;
            Vector3 direction = target - origin;
            float distance = direction.magnitude;
            if (distance > chaseSightDistance || distance <= 0.01f)
                return distance <= chaseSightDistance;

            var hits = Physics.RaycastAll(origin, direction.normalized, distance, chaseLineOfSightMask, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0)
                return true;

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null)
                    continue;

                Transform hitTransform = hitCollider.transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform))
                    continue;

                if (hitTransform == player || hitTransform.IsChildOf(player) || player.IsChildOf(hitTransform))
                    return true;

                return false;
            }

            return true;
        }

        private IEnumerator ApparitionRoutine(
            bool forced,
            bool preferNearestDoor = false,
            DoorApparitionMode mode = DoorApparitionMode.Ambient)
        {
            if (apparitionRunning)
                yield break;

            apparitionRunning = true;
            activeApparitionMode = mode;
            Transform point = null;
            bool hasPoint = mode switch
            {
                DoorApparitionMode.AudioLogScan => TryChooseNearestSceneDoorApparitionPoint(out point) || TryChooseApparitionPoint(out point),
                DoorApparitionMode.PostBreakHunt => TryChooseRandomSceneDoorApparitionPoint(out point) || TryChooseApparitionPoint(out point),
                _ => (preferNearestDoor && TryChooseNearestSceneDoorApparitionPoint(out point))
                    || TryChooseRandomSceneDoorApparitionPoint(out point)
                    || TryChooseApparitionPoint(out point)
            };

            if (hasPoint && point != null)
            {
                WarpTo(point.position, point.rotation);
                SetVisualVisible(true);
                if (agent != null)
                    agent.isStopped = false;

                if (ShouldApproachPlayerAfterTeleport())
                {
                    FacePlayer();
                    StartChase(false);
                    ScheduleNextPostBreakDoorJump();
                    apparitionRunning = false;
                    apparitionCoroutine = null;
                    yield break;
                }

                preBreakScanChaseActive = false;
                Vector3 walkAwayDestination = transform.position;
                bool avoidPlayerAfterTeleport = ShouldAvoidPlayerAfterTeleport();
                bool hasWalkAwayDestination = walkAwayAfterDoorApparition
                    && avoidPlayerAfterTeleport
                    && TryGetWalkAwayDestination(transform.position, out walkAwayDestination);
                float avoidTimer = 0f;
                float avoidSeconds = avoidPlayerAfterTeleport ? Mathf.Max(0f, avoidPlayerAfterTeleportSeconds) : 0f;
                avoidPlayerUntilTime = Mathf.Max(avoidPlayerUntilTime, Time.time + avoidSeconds);
                if (hasWalkAwayDestination)
                    FaceAwayFromPlayer();
                else
                    FacePlayer();

                while (avoidTimer < avoidSeconds)
                {
                    avoidTimer += Time.deltaTime;
                    if (hasWalkAwayDestination)
                        MoveTo(walkAwayDestination, CurrentPatrolSpeed(), waypointReachDistance);

                    yield return null;
                }

                preBreakScanChaseActive = true;
                float visibleTimer = 0f;
                float remainingVisibleSeconds = Mathf.Max(0f, apparitionVisibleSeconds - avoidSeconds);
                while (visibleTimer < remainingVisibleSeconds)
                {
                    visibleTimer += Time.deltaTime;
                    if (ShouldChasePlayer())
                    {
                        StartChase(false);
                        apparitionRunning = false;
                        apparitionCoroutine = null;
                        yield break;
                    }

                    yield return null;
                }

                if (hasWalkAwayDestination)
                {
                    float walkTimer = 0f;
                    float remainingWalkSeconds = Mathf.Max(0f, apparitionWalkAwayMaxSeconds - avoidSeconds);
                    while (walkTimer < remainingWalkSeconds)
                    {
                        walkTimer += Time.deltaTime;
                        if (ShouldChasePlayer())
                        {
                            StartChase(false);
                            apparitionRunning = false;
                            apparitionCoroutine = null;
                            yield break;
                        }

                        if (MoveTo(walkAwayDestination, CurrentPatrolSpeed(), waypointReachDistance))
                            break;

                        yield return null;
                    }
                }

                if (hideVisualBetweenApparitions)
                    SetVisualVisible(false);
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                    agent.isStopped = false;

                preBreakScanChaseActive = false;
                AlignPatrolIndexToNearest();
            }

            ScheduleNextApparition();
            apparitionRunning = false;
            apparitionCoroutine = null;
        }

        private IEnumerator ScriptedPassRoutine(Transform startPoint, Transform endPoint, Action onComplete)
        {
            scriptedPassRunning = true;
            apparitionRunning = true;
            int resumePatrolIndex = patrolIndex;
            int resumePatrolDirection = patrolDirection;
            ResolveReferences();
            CacheVisualRenderers();

            WarpTo(startPoint.position, startPoint.rotation);
            SetVisualVisible(true);
            if (agent != null && agent.enabled && agent.isOnNavMesh)
                agent.isStopped = false;

            const float maxSeconds = 12f;
            float timer = 0f;
            while (timer < maxSeconds)
            {
                timer += Time.deltaTime;
                if (MoveTo(endPoint.position, CurrentPatrolSpeed(), waypointReachDistance))
                    break;

                yield return null;
            }

            if (endPoint != null)
                WarpTo(endPoint.position, endPoint.rotation);

            if (hideVisualBetweenApparitions)
                SetVisualVisible(false);

            if (agent != null && agent.enabled && agent.isOnNavMesh)
                agent.isStopped = false;

            patrolIndex = Mathf.Clamp(resumePatrolIndex, 0, Mathf.Max(0, patrolPoints != null ? patrolPoints.Length - 1 : 0));
            patrolDirection = resumePatrolDirection == 0 ? 1 : resumePatrolDirection;
            ClearDestination();
            ScheduleNextApparition();
            apparitionRunning = false;
            scriptedPassRunning = false;
            scriptedPassRoutine = null;
            onComplete?.Invoke();
        }

        private bool TryChooseApparitionPoint(out Transform point)
        {
            point = null;
            CollectApparitionCandidates(reusablePoints);
            if (reusablePoints.Count == 0)
                return false;

            Transform best = null;
            float bestScore = float.MaxValue;
            Vector3 playerPosition = player != null ? player.position : transform.position;

            for (int i = 0; i < reusablePoints.Count; i++)
            {
                Transform candidate = reusablePoints[i];
                if (candidate == null)
                    continue;

                float distance = Vector3.Distance(playerPosition, candidate.position);
                if (distance < minDistanceFromPlayer || distance > maxDistanceFromPlayer)
                    continue;
                if (ShouldAvoidPlayerCameraView() && IsVisibleFromPlayerCamera(candidate.position))
                    continue;
                if (!NavMesh.SamplePosition(candidate.position, out _, navMeshSampleRadius, NavMesh.AllAreas))
                    continue;

                float doorScore = DistanceToNearestDoor(candidate.position);
                float score = distance + doorScore * 0.35f;
                if (best == null || score < bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            if (best == null)
                TryChooseFallbackApparitionPoint(playerPosition, out best);

            point = best;
            return point != null;
        }

        private bool TryChooseNearestSceneDoorApparitionPoint(out Transform point)
        {
            point = null;
            ResolveReferences();

            if (player == null)
                return false;

            var doors = FindObjectsByType<FpsHorrorKit.DoorSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Transform best = null;
            float bestScore = float.MaxValue;

            for (int i = 0; i < doors.Length; i++)
            {
                var door = doors[i];
                if (door == null || !door.gameObject.activeInHierarchy)
                    continue;
                if (!IsDoorAllowedForCurrentFloor(door.transform.position))
                    continue;
                if (!NavMesh.SamplePosition(door.transform.position, out var hit, navMeshSampleRadius, NavMesh.AllAreas))
                    continue;

                float score = Vector3.Distance(player.position, hit.position);
                if (best == null || score < bestScore)
                {
                    best = door.transform;
                    bestScore = score;
                }
            }

            point = best;
            return point != null;
        }

        private bool TryChooseRandomSceneDoorApparitionPoint(out Transform point)
        {
            point = null;
            ResolveReferences();

            var doors = FindObjectsByType<FpsHorrorKit.DoorSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var candidates = new List<Transform>();
            for (int i = 0; i < doors.Length; i++)
            {
                var door = doors[i];
                if (door == null || !door.gameObject.activeInHierarchy)
                    continue;
                if (!IsDoorAllowedForCurrentFloor(door.transform.position))
                    continue;
                if (ShouldAvoidPlayerCameraView() && IsVisibleFromPlayerCamera(door.transform.position))
                    continue;
                if (!NavMesh.SamplePosition(door.transform.position, out _, navMeshSampleRadius, NavMesh.AllAreas))
                    continue;

                candidates.Add(door.transform);
            }

            if (candidates.Count == 0 && ShouldAvoidPlayerCameraView())
            {
                for (int i = 0; i < doors.Length; i++)
                {
                    var door = doors[i];
                    if (door == null || !door.gameObject.activeInHierarchy)
                        continue;
                    if (!IsDoorAllowedForCurrentFloor(door.transform.position))
                        continue;
                    if (!NavMesh.SamplePosition(door.transform.position, out _, navMeshSampleRadius, NavMesh.AllAreas))
                        continue;

                    candidates.Add(door.transform);
                }
            }

            if (candidates.Count == 0)
                return false;

            point = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            return point != null;
        }

        private void CollectApparitionCandidates(List<Transform> points)
        {
            points.Clear();

            var roots = GetActiveDoorApparitionRoots();
            if (useDoorApparitionPoints && roots.Length > 0)
            {
                RefreshApparitionRootCache();
                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    Transform root = roots[rootIndex];
                    if (root == null)
                        continue;

                    for (int i = 0; i < root.childCount; i++)
                    {
                        Transform child = root.GetChild(i);
                        if (child != null && child.gameObject.activeSelf)
                            points.Add(child);
                    }
                }
            }

            if (points.Count == 0 && useSceneDoorsForApparitions)
                CollectSceneDoorCandidates(points);

            if (points.Count == 0 && patrolPoints != null)
            {
                for (int i = 0; i < patrolPoints.Length; i++)
                {
                    Transform waypoint = patrolPoints[i];
                    if (waypoint != null && IsNearAnyDoor(waypoint.position))
                        points.Add(waypoint);
                }
            }
        }

        private void CollectSceneDoorCandidates(List<Transform> points)
        {
            var doors = FindObjectsByType<FpsHorrorKit.DoorSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < doors.Length; i++)
            {
                var door = doors[i];
                if (door == null || !door.gameObject.activeInHierarchy)
                    continue;
                if (!IsDoorAllowedForCurrentFloor(door.transform.position))
                    continue;
                if (!points.Contains(door.transform))
                    points.Add(door.transform);
            }
        }

        private bool TryChooseFallbackApparitionPoint(Vector3 playerPosition, out Transform point)
        {
            point = null;
            float bestScore = float.MaxValue;

            for (int i = 0; i < reusablePoints.Count; i++)
            {
                Transform candidate = reusablePoints[i];
                if (candidate == null)
                    continue;
                if (!NavMesh.SamplePosition(candidate.position, out _, navMeshSampleRadius, NavMesh.AllAreas))
                    continue;

                float score = Vector3.Distance(playerPosition, candidate.position);
                if (ShouldAvoidPlayerCameraView() && IsVisibleFromPlayerCamera(candidate.position))
                    score += 1000f;

                if (point == null || score < bestScore)
                {
                    point = candidate;
                    bestScore = score;
                }
            }

            return point != null;
        }

        private bool TryGetWalkAwayDestination(Vector3 fromPosition, out Vector3 destination)
        {
            destination = fromPosition;
            if (player == null)
                return false;

            Vector3 awayDirection = fromPosition - player.position;
            awayDirection.y = 0f;
            if (awayDirection.sqrMagnitude <= 0.01f)
                awayDirection = -transform.forward;
            awayDirection.Normalize();

            if (TryChooseDirectAwayDestination(fromPosition, awayDirection, out destination))
                return true;

            float currentPlayerDistance = Vector3.Distance(player.position, fromPosition);
            if (TryChooseWalkAwayPoint(fromPosition, awayDirection, currentPlayerDistance, out destination))
                return true;

            float[] distances =
            {
                apparitionWalkAwayDistance,
                apparitionWalkAwayDistance * 0.7f,
                apparitionWalkAwayDistance * 0.45f
            };

            for (int i = 0; i < distances.Length; i++)
            {
                Vector3 rawDestination = fromPosition + awayDirection * Mathf.Max(0.5f, distances[i]);
                if (TrySampleReachableDestination(fromPosition, rawDestination, out destination))
                    return true;
            }

            return false;
        }

        private bool TryChooseDirectAwayDestination(Vector3 fromPosition, Vector3 awayDirection, out Vector3 destination)
        {
            destination = fromPosition;
            float[] distances =
            {
                apparitionWalkAwayDistance,
                apparitionWalkAwayDistance * 0.7f,
                apparitionWalkAwayDistance * 0.45f
            };
            float[] angleOffsets = { 0f, -25f, 25f, -45f, 45f };

            for (int angleIndex = 0; angleIndex < angleOffsets.Length; angleIndex++)
            {
                Vector3 direction = Quaternion.AngleAxis(angleOffsets[angleIndex], Vector3.up) * awayDirection;
                for (int distanceIndex = 0; distanceIndex < distances.Length; distanceIndex++)
                {
                    Vector3 rawDestination = fromPosition + direction * Mathf.Max(0.5f, distances[distanceIndex]);
                    if (TrySampleReachableDestination(fromPosition, rawDestination, out destination))
                        return true;
                }
            }

            return false;
        }

        private bool TryChooseWalkAwayPoint(
            Vector3 fromPosition,
            Vector3 awayDirection,
            float currentPlayerDistance,
            out Vector3 destination)
        {
            destination = fromPosition;
            float bestScore = float.NegativeInfinity;
            bool found = false;

            var candidates = new List<Transform>();
            if (patrolPoints != null)
            {
                for (int i = 0; i < patrolPoints.Length; i++)
                {
                    if (patrolPoints[i] != null)
                        candidates.Add(patrolPoints[i]);
                }
            }

            if (useDoorApparitionPointsForWalkAway)
            {
                CollectApparitionCandidates(reusablePoints);
                for (int i = 0; i < reusablePoints.Count; i++)
                {
                    if (reusablePoints[i] != null && !candidates.Contains(reusablePoints[i]))
                        candidates.Add(reusablePoints[i]);
                }
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                Transform point = candidates[i];
                if (point == null)
                    continue;

                Vector3 toPoint = point.position - fromPosition;
                toPoint.y = 0f;
                if (toPoint.sqrMagnitude <= 0.01f)
                    continue;

                float directionDot = Vector3.Dot(awayDirection, toPoint.normalized);
                if (directionDot < walkAwayMinDirectionDot)
                    continue;

                float playerDistance = Vector3.Distance(player.position, point.position);
                if (playerDistance <= currentPlayerDistance + 0.5f)
                    continue;

                if (!TrySampleReachableDestination(fromPosition, point.position, out var sampledDestination))
                    continue;

                float fromDistance = Vector3.Distance(fromPosition, sampledDestination);
                float score = playerDistance + directionDot * 4f - Mathf.Abs(fromDistance - apparitionWalkAwayDistance) * 0.25f;
                if (!found || score > bestScore)
                {
                    found = true;
                    bestScore = score;
                    destination = sampledDestination;
                }
            }

            return found;
        }

        private bool TrySampleReachableDestination(Vector3 fromPosition, Vector3 rawDestination, out Vector3 destination)
        {
            destination = rawDestination;
            if (!NavMesh.SamplePosition(rawDestination, out var hit, navMeshSampleRadius, NavMesh.AllAreas))
                return false;

            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(fromPosition, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                return false;

            destination = hit.position;
            return true;
        }

        private void RefreshPatrolPoints(bool force)
        {
            if (!autoCollectPointsFromChildren)
                return;

            var roots = GetActivePatrolRoots();
            if (roots.Length == 0)
                return;

            string signature = BuildChildSignature(roots);
            if (!force && lastPatrolSignature == signature)
                return;

            patrolPoints = CollectDirectChildren(roots);
            lastPatrolSignature = signature;
            lastPatrolChildCount = patrolPoints.Length;
            patrolIndex = Mathf.Clamp(patrolIndex, 0, Mathf.Max(0, patrolPoints.Length - 1));
            ClearDestination();
        }

        private void RefreshApparitionRootCache()
        {
            var roots = GetActiveDoorApparitionRoots();
            string signature = BuildChildSignature(roots);
            if (lastApparitionSignature == signature)
                return;

            lastApparitionSignature = signature;
            lastApparitionChildCount = CountActiveChildren(roots);
        }

        private Transform[] CollectDirectChildren(params Transform[] roots)
        {
            if (roots == null || roots.Length == 0)
                return Array.Empty<Transform>();

            var points = new List<Transform>();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Transform root = roots[rootIndex];
                if (root == null)
                    continue;

                for (int i = 0; i < root.childCount; i++)
                {
                    Transform child = root.GetChild(i);
                    if (child != null && child.gameObject.activeSelf)
                        points.Add(child);
                }
            }

            return points.ToArray();
        }

        private void AdvancePatrolPoint()
        {
            if (patrolPoints == null || patrolPoints.Length <= 1)
                return;

            if (!pingPongPatrol)
            {
                patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                ClearDestination();
                return;
            }

            if (patrolIndex >= patrolPoints.Length - 1)
                patrolDirection = -1;
            else if (patrolIndex <= 0)
                patrolDirection = 1;

            patrolIndex = Mathf.Clamp(patrolIndex + patrolDirection, 0, patrolPoints.Length - 1);
            ClearDestination();
        }

        private bool MoveTo(Vector3 target, float speed, float reachDistance)
        {
            if (agent == null || !agent.enabled)
                return false;
            if (!agent.isOnNavMesh && !TryWarpToNavMesh(transform.position))
                return false;
            if (!NavMesh.SamplePosition(target, out var hit, navMeshSampleRadius, NavMesh.AllAreas))
                return false;

            agent.isStopped = false;
            agent.speed = speed;
            agent.stoppingDistance = Mathf.Min(0.1f, reachDistance * 0.5f);

            Vector3 destination = hit.position;
            if (!hasDestination || Vector3.Distance(lastDestination, destination) > 0.1f)
            {
                if (!agent.SetDestination(destination))
                    return false;

                lastDestination = destination;
                hasDestination = true;
            }

            RotateAlongVelocity();
            return !agent.pathPending && agent.remainingDistance <= reachDistance;
        }

        private void WarpTo(Vector3 position, Quaternion rotation)
        {
            if (NavMesh.SamplePosition(position, out var hit, navMeshSampleRadius, NavMesh.AllAreas))
                position = hit.position;

            if (agent != null && agent.enabled)
            {
                if (agent.isOnNavMesh || TryWarpToNavMesh(transform.position))
                    agent.Warp(position);
                else
                    transform.position = position;
            }
            else
            {
                transform.position = position;
            }

            transform.rotation = rotation;
            ClearDestination();
            nextAttackAllowedTime = Time.time + attackDelayAfterTeleport;
        }

        private bool TryWarpToNavMesh(Vector3 position)
        {
            return agent != null
                && NavMesh.SamplePosition(position, out var hit, navMeshSampleRadius, NavMesh.AllAreas)
                && agent.Warp(hit.position);
        }

        private void RotateAlongVelocity()
        {
            if (agent == null)
                return;

            Vector3 velocity = agent.desiredVelocity.sqrMagnitude > 0.01f ? agent.desiredVelocity : agent.velocity;
            velocity.y = 0f;
            if (velocity.sqrMagnitude <= 0.01f)
                return;

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(velocity.normalized, Vector3.up),
                Time.deltaTime * 5f);
        }

        private void FacePlayer()
        {
            if (player == null)
                return;

            Vector3 direction = player.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.01f)
                return;

            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private void FaceAwayFromPlayer()
        {
            if (player == null)
                return;

            Vector3 direction = transform.position - player.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.01f)
                direction = transform.forward;
            if (direction.sqrMagnitude <= 0.01f)
                return;

            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private void PlayAttackAnimation()
        {
            if (animator == null)
                ResolveAnimator();

            if (animator == null || string.IsNullOrWhiteSpace(attackTriggerName))
                return;

            animator.SetTrigger(attackTriggerName);
        }

        private void PlayAttackAudio()
        {
            if (attackClips != null && attackClips.Length > 0)
            {
                var clip = attackClips[UnityEngine.Random.Range(0, attackClips.Length)];
                if (clip != null)
                {
                    AudioSource.PlayClipAtPoint(clip, transform.position, attackVolume);
                    return;
                }
            }

            AudioManager.Instance?.PlayGhostJumpscare(attackVolume);
        }

        private static void SetCaughtPlayerModelVisible(FpsHorrorKit.FpsController playerController, bool visible)
        {
            if (playerController == null)
                return;

            if (playerController.playerAnimator != null)
                SetRenderersVisible(playerController.playerAnimator.transform, visible);

            if (playerController.detachedHairRoot != null)
                SetRenderersVisible(playerController.detachedHairRoot, visible);

            SetRenderersVisible(FindSceneTransform("PlayerNew"), visible);

            var renderers = playerController.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer itemRenderer = renderers[i];
                if (itemRenderer != null)
                    itemRenderer.enabled = visible;
            }
        }

        private static void DisablePlayerPresentationController(FpsHorrorKit.FpsController playerController)
        {
            if (playerController == null)
                return;

            var presentation = playerController.GetComponent<FpsHorrorKit.FirstPersonPresentationController>();
            if (presentation == null)
                presentation = playerController.GetComponentInChildren<FpsHorrorKit.FirstPersonPresentationController>(true);
            if (presentation == null)
                presentation = playerController.GetComponentInParent<FpsHorrorKit.FirstPersonPresentationController>(true);

            if (presentation != null)
                presentation.enabled = false;
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

        private IEnumerator HoldAttackCameraSeconds(FpsHorrorKit.FpsController playerController, float seconds)
        {
            float elapsed = 0f;
            float duration = Mathf.Max(0f, seconds);
            while (elapsed < duration)
            {
                UpdateAttackCameraLookTarget();
                TickRepeatedCaughtAttack();
                playerController?.StopCutSceneMovement();
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private void TickRepeatedCaughtAttack()
        {
            if (!repeatAttackWhileCaught || Time.time < nextRepeatedAttackTime)
                return;

            FacePlayer();
            PlayAttackAnimation();
            nextRepeatedAttackTime = Time.time + repeatedAttackInterval;
        }

        private void StoreAttackCameraState()
        {
            var camera = ResolveAttackVirtualCamera();
            if (camera == null)
                return;

            previousAttackCameraPriority = camera.Priority;
            previousAttackCameraTarget = camera.Target;
            previousAttackCameraEnabled = camera.enabled;
            previousAttackCameraActive = camera.gameObject.activeSelf;
        }

        private void SwitchAttackCamera(bool active)
        {
            var camera = ResolveAttackVirtualCamera();
            if (camera == null)
                return;

            if (active)
            {
                if (!camera.gameObject.activeSelf)
                camera.gameObject.SetActive(true);

                camera.enabled = true;
                PrepareAttackCameraLookTarget(camera);
                ApplyAttackVirtualCameraPose(camera);
                camera.Priority = attackCameraPriority;
                ForceCutToAttackCamera();
            }
            else
            {
                camera.Priority = previousAttackCameraPriority;
                camera.Target = previousAttackCameraTarget;
                camera.enabled = previousAttackCameraEnabled;
                if (camera.gameObject.activeSelf != previousAttackCameraActive)
                    camera.gameObject.SetActive(previousAttackCameraActive);
                DestroyAttackCameraLookTarget();
            }
        }

        private void ForceCutToAttackCamera()
        {
            var brain = ResolveAttackCameraBrain();
            if (brain == null)
                return;

            var previousBlend = brain.DefaultBlend;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            brain.ActiveBlend = null;
            brain.ManualUpdate();
            SnapMainCameraToAttackCamera(brain);
            brain.DefaultBlend = previousBlend;
        }

        private void SnapMainCameraToAttackCamera(CinemachineBrain brain)
        {
            var camera = ResolveAttackVirtualCamera();
            if (camera == null)
                return;

            Camera outputCamera = brain != null ? brain.OutputCamera : Camera.main;
            if (outputCamera == null)
                return;

            outputCamera.transform.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);
        }

        private void PrepareAttackCameraLookTarget(CinemachineCamera camera)
        {
            if (camera == null)
                return;

            if (attackRuntimeLookTarget == null)
            {
                attackRuntimeLookTarget = new GameObject("P2_AttackCameraMada_RuntimeLookTarget").transform;
                attackRuntimeLookTarget.hideFlags = HideFlags.HideAndDontSave;
            }

            UpdateAttackCameraLookTarget();
            camera.Target.LookAtTarget = attackRuntimeLookTarget;
            camera.Target.CustomLookAtTarget = true;
        }

        private void UpdateAttackCameraLookTarget()
        {
            if (attackRuntimeLookTarget == null)
                return;

            Transform lookTarget = ResolveAttackLookTarget();
            attackRuntimeLookTarget.position = lookTarget != null
                ? lookTarget.position + attackLookTargetOffset
                : transform.position + Vector3.up * (ghostEyeHeight + attackLookTargetOffset.y);

            ApplyAttackVirtualCameraPose(attackVirtualCamera);
        }

        private void ApplyAttackVirtualCameraPose(CinemachineCamera camera)
        {
            if (camera == null)
                return;

            Quaternion rotation = camera.transform.rotation;
            if (attackRuntimeLookTarget != null)
            {
                Vector3 direction = attackRuntimeLookTarget.position - camera.transform.position;
                if (direction.sqrMagnitude > 0.0001f)
                    rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }

            camera.transform.rotation = rotation;
            camera.ForceCameraPosition(camera.transform.position, rotation);
        }

        private void DestroyAttackCameraLookTarget()
        {
            if (attackRuntimeLookTarget == null)
                return;

            Destroy(attackRuntimeLookTarget.gameObject);
            attackRuntimeLookTarget = null;
        }

        private CinemachineCamera ResolveAttackVirtualCamera()
        {
            if (attackVirtualCamera != null)
                return attackVirtualCamera;

            Transform cameraTransform = FindSceneTransform(attackVirtualCameraName);
            if (cameraTransform == null)
                cameraTransform = FindSceneTransform("CameraMada");
            if (cameraTransform == null)
                cameraTransform = FindSceneTransform("CameraMaDa");

            if (cameraTransform != null)
                attackVirtualCamera = cameraTransform.GetComponent<CinemachineCamera>();

            return attackVirtualCamera;
        }

        private CinemachineBrain ResolveAttackCameraBrain()
        {
            if (attackCameraBrain != null)
                return attackCameraBrain;

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
                attackCameraBrain = mainCamera.GetComponent<CinemachineBrain>();
            if (attackCameraBrain == null)
                attackCameraBrain = FindFirstObjectByType<CinemachineBrain>(FindObjectsInactive.Include);

            return attackCameraBrain;
        }

        private Transform ResolveAttackLookTarget()
        {
            if (attackLookTarget != null)
                return attackLookTarget;

            attackLookTarget = FindNamedTargetOnGhost(attackLookTargetName);
            if (attackLookTarget == null)
                attackLookTarget = FindNamedTargetOnGhost("head.x");
            if (attackLookTarget == null)
                attackLookTarget = FindNamedTargetOnGhost("Head");

            return attackLookTarget;
        }

        private Transform FindNamedTargetOnGhost(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
                return null;

            Transform child = FindChildRecursive(visualRoot != null ? visualRoot : transform, objectName);
            if (child != null)
                return child;

            return FindChildRecursive(transform, objectName);
        }

        private FpsHorrorKit.FpsController ResolveFpsController()
        {
            if (player != null && player.TryGetComponent<FpsHorrorKit.FpsController>(out var fps))
                return fps;

            if (GameController.Instance != null && GameController.Instance.playerController != null)
                return GameController.Instance.playerController;

            return FindFirstObjectByType<FpsHorrorKit.FpsController>(FindObjectsInactive.Include);
        }

        private void TriggerPlayerDeathLikeChapterOne()
        {
            var controller = GameController.Instance;
            if (controller != null)
            {
                if (attackRespawnDelay > 0f)
                    controller.TriggerJumpscareCheckpointRespawn(attackRespawnDelay, playDeathVoiceImmediately, deathVoiceIndex);
                else
                    controller.TriggerJumpscareCheckpointRespawn(playDeathVoiceImmediately, deathVoiceIndex);
                return;
            }

            P2GameController.Instance?.StartEndingSequence();
        }

        private void AlignPatrolIndexToNearest()
        {
            if (patrolPoints == null || patrolPoints.Length == 0)
                return;

            int nearest = patrolIndex;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (patrolPoints[i] == null)
                    continue;

                float distance = (patrolPoints[i].position - transform.position).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearest = i;
                    nearestDistance = distance;
                }
            }

            patrolIndex = nearest;
            ClearDestination();
        }

        private float DistanceToNearestDoor(Vector3 position)
        {
            float nearest = float.MaxValue;
            var doors = FindObjectsByType<FpsHorrorKit.DoorSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i] == null)
                    continue;

                nearest = Mathf.Min(nearest, Vector3.Distance(position, doors[i].transform.position));
            }

            return nearest;
        }

        private bool IsDoorAllowedForCurrentFloor(Vector3 position)
        {
            return activeApparitionMode switch
            {
                DoorApparitionMode.AudioLogScan => IsSameFloorAsPlayer(position),
                DoorApparitionMode.PostBreakHunt => !IsUpperFloor(position),
                _ => !IsUpperFloor(position)
            };
        }

        private bool IsSameFloorAsPlayer(Vector3 position)
        {
            if (player == null)
                return !IsUpperFloor(position);

            return IsUpperFloor(position) == IsUpperFloor(player.position);
        }

        private static bool IsUpperFloor(Vector3 position)
        {
            return position.y >= UpperFloorMinY;
        }

        private bool IsNearAnyDoor(Vector3 position)
        {
            return DistanceToNearestDoor(position) <= doorPointMaxDistance;
        }

        private bool IsVisibleFromPlayerCamera(Vector3 position)
        {
            Camera camera = Camera.main;
            if (camera == null || !camera.isActiveAndEnabled)
                return false;

            Vector3 viewport = camera.WorldToViewportPoint(position + Vector3.up * 1.1f);
            return viewport.z > camera.nearClipPlane
                && viewport.x > 0.05f
                && viewport.x < 0.95f
                && viewport.y > 0.05f
                && viewport.y < 0.95f;
        }

        private void ResolveReferences()
        {
            if (agent == null)
                agent = GetComponent<NavMeshAgent>();
            if (agent == null)
                agent = gameObject.AddComponent<NavMeshAgent>();
            agent.updateRotation = false;

            if (player == null)
            {
                var fps = FindFirstObjectByType<FpsHorrorKit.FpsController>(FindObjectsInactive.Include);
                if (fps != null)
                    player = fps.transform;
            }

            if (oilLamp == null)
                ResolveOilLamp();

            if (visualRoot == null)
            {
                var mada = FindChildRecursive(transform, "mada2");
                visualRoot = mada != null ? mada : transform;
            }

            if (animator == null)
                ResolveAnimator();

            if (patrolRoot == null)
                patrolRoot = FindSceneTransform("P2_GhostPatrolPoints") ?? FindSceneTransform("P2_GhostWaypoints");
            if (downstairsPatrolRoot == null)
                downstairsPatrolRoot = FindSceneTransform("P2_GhostPatrol_Downstairs");
            if (upstairsPatrolRoot == null)
                upstairsPatrolRoot = FindSceneTransform("P2_GhostPatrol_Upstairs");
            if (useDoorApparitionPoints)
            {
                if (doorApparitionRoot == null)
                    doorApparitionRoot = FindSceneTransform("P2_GhostDoorApparitionPoints");
                if (downstairsDoorApparitionRoot == null)
                    downstairsDoorApparitionRoot = FindSceneTransform("P2_GhostDoorApparition_Downstairs");
                if (upstairsDoorApparitionRoot == null)
                    upstairsDoorApparitionRoot = FindSceneTransform("P2_GhostDoorApparition_Upstairs");
            }
            else
            {
                doorApparitionRoot = null;
                downstairsDoorApparitionRoot = null;
                upstairsDoorApparitionRoot = null;
            }
        }

        private void ResolveAnimator()
        {
            if (animator != null)
                return;

            animator = visualRoot != null
                ? visualRoot.GetComponentInChildren<Animator>(true)
                : GetComponentInChildren<Animator>(true);
        }

        private void ResolveOilLamp()
        {
            P2OilLamp fallback = null;
            foreach (var lamp in FindObjectsByType<P2OilLamp>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (lamp == null)
                    continue;

                fallback ??= lamp;
                if (lamp.ControlsGameplaySystems)
                {
                    oilLamp = lamp;
                    return;
                }
            }

            oilLamp = fallback;
        }

        private void CacheVisualRenderers()
        {
            visualRenderers = visualRoot != null
                ? visualRoot.GetComponentsInChildren<Renderer>(true)
                : Array.Empty<Renderer>();
        }

        private void SetVisualVisible(bool visible)
        {
            if (visualRoot != null && visualRoot != transform)
                visualRoot.gameObject.SetActive(true);

            if (visualRenderers == null || visualRenderers.Length == 0)
                CacheVisualRenderers();

            for (int i = 0; i < visualRenderers.Length; i++)
            {
                if (visualRenderers[i] != null)
                    visualRenderers[i].enabled = visible;
            }
        }

        private void ApplyVisualHiddenState()
        {
            if (hideVisualBetweenApparitions)
                SetVisualVisible(false);
        }

        private void DisableOtherGhostControllers()
        {
            var p2Ghost = GetComponent<P2GhostController>();
            if (p2Ghost != null)
                p2Ghost.enabled = false;

            var monsterAi = GetComponent<global::MonsterAI>();
            if (monsterAi != null)
                monsterAi.enabled = false;
        }

        private static bool IsGameLocked()
        {
            var controller = GameController.Instance;
            return controller != null
                && (controller.currentGameState == GameController.GameState.Cutscene
                    || controller.currentGameState == GameController.GameState.Ending
                    || controller.currentGameState == GameController.GameState.Dead);
        }

        private void ScheduleNextApparition()
        {
            ScheduleNextApparition(postBreakDoorJumpSeconds, postBreakDoorJumpSeconds);
        }

        private void ScheduleNextApparition(float minSeconds, float maxSeconds)
        {
            float min = Mathf.Max(0.1f, Mathf.Min(minSeconds, maxSeconds));
            float max = Mathf.Max(min, Mathf.Max(minSeconds, maxSeconds));
            nextApparitionTime = Time.time + UnityEngine.Random.Range(min, max);
        }

        private void ClearDestination()
        {
            hasDestination = false;
        }

        private bool UpperFloorUnlocked => !lockUpperFloorUntilAwakened || IsAwakened;

        private Transform[] GetActivePatrolRoots()
        {
            var roots = new List<Transform>(3);
            AddUniqueRoot(roots, downstairsPatrolRoot != null ? downstairsPatrolRoot : patrolRoot);

            if (showUpperFloorAfterAwakened && UpperFloorUnlocked)
                AddUniqueRoot(roots, upstairsPatrolRoot);

            if (roots.Count == 0)
                AddUniqueRoot(roots, patrolRoot);

            return roots.ToArray();
        }

        private Transform[] GetActiveDoorApparitionRoots()
        {
            if (!useDoorApparitionPoints)
                return Array.Empty<Transform>();

            var roots = new List<Transform>(3);
            AddUniqueRoot(roots, downstairsDoorApparitionRoot != null ? downstairsDoorApparitionRoot : doorApparitionRoot);

            if (showUpperFloorAfterAwakened && UpperFloorUnlocked)
                AddUniqueRoot(roots, upstairsDoorApparitionRoot);

            if (roots.Count == 0)
                AddUniqueRoot(roots, doorApparitionRoot);

            return roots.ToArray();
        }

        private static void AddUniqueRoot(List<Transform> roots, Transform root)
        {
            if (root != null && !roots.Contains(root))
                roots.Add(root);
        }

        private static int CountActiveChildren(params Transform[] roots)
        {
            int count = 0;
            if (roots == null)
                return count;

            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Transform root = roots[rootIndex];
                if (root == null)
                    continue;

                for (int i = 0; i < root.childCount; i++)
                {
                    Transform child = root.GetChild(i);
                    if (child != null && child.gameObject.activeSelf)
                        count++;
                }
            }

            return count;
        }

        private static string BuildChildSignature(params Transform[] roots)
        {
            if (roots == null || roots.Length == 0)
                return string.Empty;

            var builder = new System.Text.StringBuilder();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Transform root = roots[rootIndex];
                if (root == null)
                    continue;

                builder.Append(root.GetInstanceID()).Append(':').Append(root.childCount).Append('|');
                for (int i = 0; i < root.childCount; i++)
                {
                    Transform child = root.GetChild(i);
                    if (child == null)
                        continue;

                    builder.Append(child.GetInstanceID())
                        .Append(child.gameObject.activeSelf ? '1' : '0')
                        .Append(';');
                }
            }

            return builder.ToString();
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null)
                return null;

            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != null && child.name == childName)
                    return child;
            }

            return null;
        }

        private static Transform FindSceneTransform(string objectName)
        {
            foreach (var candidate in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (candidate != null && candidate.name == objectName && candidate.gameObject.scene.IsValid())
                    return candidate;
            }

            return null;
        }
    }
}
