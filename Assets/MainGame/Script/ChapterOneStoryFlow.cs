using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

public sealed class ChapterOneStoryFlow : MonoBehaviour
{
    public static ChapterOneStoryFlow Instance { get; private set; }

    [Header("Actors")]
    [SerializeField] private MonsterAI monster;
    [SerializeField] private FpsHorrorKit.GramophoneTapePlayer gramophoneTapePlayer;
    [SerializeField] private FpsHorrorKit.ClosetHiding preferredCloset;
    [SerializeField] private WellEndingTrigger wellEndingTrigger;

    [Header("Hall Ghost Pass")]
    [SerializeField] private Transform hallGhostStart;
    [SerializeField] private Transform[] hallGhostRoutePoints;
    [SerializeField] private Transform hallGhostEnd;
    [SerializeField] private Transform hallGhostHidePoint;
    [SerializeField, Min(0.1f)] private float fallbackHallPassWait = 2f;

    [Header("Hunt")]
    [SerializeField] private Transform monsterUpperFloorSpawn;
    [SerializeField, Min(0f)] private float postGramophoneSilence = 5f;
    [SerializeField, Min(0f)] private float playerReactionDelay = 2.2f;
    [SerializeField] private bool requireMonsterSeenBeforeCloset = true;
    [SerializeField, Min(1f)] private float monsterSeenDistance = 22f;
    [SerializeField, Range(0.1f, 1f)] private float monsterSeenDot = 0.72f;
    [SerializeField, Min(0f)] private float monsterSeenRayRadius = 0.18f;

    [Header("Gramophone Cutscene")]
    [SerializeField] private bool allowSkipGramophoneCutscene = true;
    [SerializeField, Min(0f)] private float gramophoneLookAtHeight = 0.75f;
    [SerializeField, Min(0.1f)] private float gramophoneTurnSpeed = 540f;
    [SerializeField, Min(0f)] private float gramophoneSkipAvailableDelay = 3f;
    [SerializeField, Min(0.2f)] private float skipPromptRefreshInterval = 1.2f;
    [SerializeField] private bool debugSpaceSpamCutscene = true;
    [SerializeField, Min(0.05f)] private float debugMoveLogInterval = 0.35f;

    [Header("Stair Encounter Cutscene")]
    [SerializeField] private Transform stairMonsterCutscenePoint;
    [SerializeField] private Transform stairPlayerCutscenePoint;
    [SerializeField] private string stairMonsterCutscenePointName = "V\u1ecb tr\u00ed qu\u00e1i \u1edf c\u1ea7u thang cutscene";
    [SerializeField] private string stairPlayerCutscenePointName = "V\u1ecb tr\u00ed nh\u00e2n v\u1eadt \u1edf c\u1ea7u thang cutscene";
    [SerializeField] private string[] studyDoorsToLockNames = { "Villa2_Door_90_phongsach" };
    [SerializeField] private string studyDoorToOpenName = "Villa2_Door_90_cauthang_phongSach";
    [SerializeField, Min(0.1f)] private float stairCutsceneMoveSpeed = 3.2f;
    [SerializeField, Min(0.1f)] private float stairCutsceneTurnSpeed = 540f;
    [SerializeField, Min(0.05f)] private float stairCutsceneArriveDistance = 0.22f;
    [SerializeField, Min(0f)] private float stairCutsceneLookHoldTime = 1f;
    [SerializeField] private Vector3 stairCutsceneCameraOffset = new(0.55f, 2.05f, -3.25f);
    [SerializeField, Min(0.05f)] private float stairCutsceneCameraSmoothTime = 0.18f;
    [SerializeField, Min(0f)] private int stairMonsterFlickerCount = 2;
    [SerializeField, Min(0.02f)] private float stairMonsterFlickerInterval = 0.08f;
    [SerializeField, Min(0f)] private float stairMonsterTeleportDelay = 0f;
    [SerializeField, Min(0.35f)] private float stairMonsterCameraTeleportDistance = 1.1f;
    [SerializeField, Min(0f)] private float stairMonsterCameraHoldTime = 0.5f;
    [SerializeField, Min(0f)] private float stairMonsterHiddenBeforeChase = 10f;
    [SerializeField, Min(0.1f)] private float stairCutsceneNavMeshSampleRadius = 2.5f;
    [SerializeField, Min(1f)] private float stairCutsceneMaxMoveTime = 14f;

    [Header("Closet Objective")]
    [SerializeField, Min(0f)] private float closetObjectiveHoldTime = 1.25f;
    [SerializeField] private bool moveMonsterToSpawnAfterCloset = true;

    [Header("Route After Closet")]
    [SerializeField] private FpsHorrorKit.DoorSystem[] doorsToOpenAfterCloset;
    [SerializeField] private string[] routeDoorNameContains =
    {
        "phongkhach sau",
        "bep",
        "khosau",
        "backyard",
        "san sau",
        "sau"
    };

    private bool studyLetterSequenceStarted;
    private bool huntStarted;
    private bool huntStartQueued;
    private bool waitingForCloset;
    private bool closetObjectiveStarted;
    private bool closetObjectiveActive;
    private bool closetObjectiveCompleted;
    private bool playerExitedClosetDuringObjective;
    private bool monsterSeenBeforeCloset;
    private Coroutine sequenceRoutine;
    private float blockGramophoneSkipUntil;
    private Camera stairCutsceneCamera;
    private CinemachineBrain stairCutsceneBrain;
    private CinemachineCamera stairGameplayVirtualCamera;
    private bool previousStairBrainEnabled;
    private bool previousStairVirtualCameraEnabled;
    private Vector3 stairCutsceneCameraVelocity;
    private NavMeshPath stairCutscenePath;
    private bool gramophoneSkipRequested;
    private bool blockSpaceUntilPostGramophoneCutsceneEnds;
    private int debugGramophoneSpacePressCount;
    private float nextStairMoveDebugLogTime;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveReferences();
        if (monster != null && !monster.IsHuntActive)
            monster.DisableHunt(true);
    }

    private void OnEnable()
    {
        FpsHorrorKit.ClosetHiding.PlayerEnteredCloset += HandlePlayerEnteredCloset;
        FpsHorrorKit.ClosetHiding.PlayerExitedCloset += HandlePlayerExitedCloset;
    }

    private void OnDisable()
    {
        FpsHorrorKit.ClosetHiding.PlayerEnteredCloset -= HandlePlayerEnteredCloset;
        FpsHorrorKit.ClosetHiding.PlayerExitedCloset -= HandlePlayerExitedCloset;
        if (gramophoneTapePlayer != null)
            gramophoneTapePlayer.TapeFinished -= HandleTapeFinished;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        blockGramophoneSkipUntil = Time.unscaledTime + 0.5f;
    }

    private void Update()
    {
        if (!waitingForCloset || monsterSeenBeforeCloset || monster == null)
            return;

        if (!IsPlayerLookingAtMonster())
            return;

        monsterSeenBeforeCloset = true;
        ShowSubtitle("Cái gì vừa-", 2.2f);
    }

    public static bool TryBeginAfterStudyLetter(FpsHorrorKit.GramophoneTapePlayer tapePlayer)
    {
        var flow = Instance != null ? Instance : FindFirstObjectByType<ChapterOneStoryFlow>(FindObjectsInactive.Include);
        if (flow == null)
        {
            var flowObject = new GameObject("ChapterOneStoryFlow");
            flow = flowObject.AddComponent<ChapterOneStoryFlow>();
        }

        return flow.BeginAfterStudyLetter(tapePlayer);
    }

    public bool BeginAfterStudyLetter(FpsHorrorKit.GramophoneTapePlayer tapePlayer)
    {
        if (studyLetterSequenceStarted)
            return true;

        if (tapePlayer != null)
            gramophoneTapePlayer = tapePlayer;

        ResolveReferences();
        if (monster == null || gramophoneTapePlayer == null)
            return false;

        studyLetterSequenceStarted = true;
        sequenceRoutine = StartCoroutine(StudyLetterSequence());
        return true;
    }

    private IEnumerator StudyLetterSequence()
    {
        var controller = GameController.Instance;
        controller?.SetGameState(GameController.GameState.Cutscene);

        if (monster != null)
            monster.DisableHunt(true);

        gramophoneTapePlayer.TapeFinished -= HandleTapeFinished;
        gramophoneTapePlayer.PlayTape();

        yield return WatchGramophoneUntilTapeEnds();

        QueueHuntAfterGramophone();
    }

    private IEnumerator WatchGramophoneUntilTapeEnds()
    {
        var playerController = GameController.Instance != null && GameController.Instance.playerController != null
            ? GameController.Instance.playerController
            : FindFirstObjectByType<FpsHorrorKit.FpsController>();

        bool previousRaycast = true;
        bool changedRaycast = false;
        if (FpsHorrorKit.PlayerInteract.Instance != null)
        {
            previousRaycast = FpsHorrorKit.PlayerInteract.Instance.sendRaycast;
            FpsHorrorKit.PlayerInteract.Instance.sendRaycast = false;
            changedRaycast = true;
        }

        bool canSkipGramophone = allowSkipGramophoneCutscene;
        gramophoneSkipRequested = false;
        debugGramophoneSpacePressCount = 0;
        float skipAllowedTime = Time.unscaledTime + gramophoneSkipAvailableDelay;
        float nextPromptTime = skipAllowedTime;
        blockGramophoneSkipUntil = skipAllowedTime;
        bool skippedWithSpace = false;
        LogSpaceSpamDebug(
            $"GramophoneWatchStart skipEnabled={canSkipGramophone} skipAllowedAt={skipAllowedTime:F2} delay={gramophoneSkipAvailableDelay:F2} tapePlaying={IsGramophoneTapePlaying()} player={FormatPlayerDebug(playerController)}");
        while (gramophoneTapePlayer != null && gramophoneTapePlayer.IsPlayingTape)
        {
            RotatePlayerTowardGramophone(playerController);

            if (!Application.isFocused)
            {
                blockGramophoneSkipUntil = Mathf.Max(blockGramophoneSkipUntil, Time.unscaledTime + 0.5f);
                nextPromptTime = Mathf.Max(nextPromptTime, blockGramophoneSkipUntil);
                yield return null;
                continue;
            }

            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                debugGramophoneSpacePressCount++;
                LogSpaceSpamDebug(
                    $"SpacePressedDuringGramophone count={debugGramophoneSpacePressCount} time={Time.unscaledTime:F2} skipEnabled={canSkipGramophone} canSkipNow={canSkipGramophone && Time.unscaledTime >= blockGramophoneSkipUntil} blockUntil={blockGramophoneSkipUntil:F2} tapePlaying={IsGramophoneTapePlaying()} player={FormatPlayerDebug(playerController)}");
            }

            if (canSkipGramophone
                && Time.unscaledTime >= blockGramophoneSkipUntil
                && Time.unscaledTime >= nextPromptTime)
            {
                FpsHorrorKit.InteractMessageScript.Instance?.ShowMessage("Đang nghe gramophone... nhấn SPACE để bỏ qua.", skipPromptRefreshInterval + 0.15f);
                nextPromptTime = Time.unscaledTime + skipPromptRefreshInterval;
            }

            if (canSkipGramophone
                && !gramophoneSkipRequested
                && Application.isFocused
                && Time.unscaledTime >= blockGramophoneSkipUntil
                && Keyboard.current != null
                && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                gramophoneSkipRequested = true;
                skippedWithSpace = true;
                SetPostGramophoneSpaceBlocked(true);
                FpsHorrorKit.FpsAssetsInputs.Instance?.ClearGameplayInput();
                LogSpaceSpamDebug($"GramophoneSkipAccepted count={debugGramophoneSpacePressCount} time={Time.unscaledTime:F2}");
                gramophoneTapePlayer.SkipTape();
                break;
            }

            if (Keyboard.current != null && Keyboard.current.spaceKey.isPressed)
                FpsHorrorKit.FpsAssetsInputs.Instance?.ClearGameplayInput();

            yield return null;
        }

        if (changedRaycast && FpsHorrorKit.PlayerInteract.Instance != null)
            FpsHorrorKit.PlayerInteract.Instance.sendRaycast = previousRaycast;

        if (skippedWithSpace)
        {
            LogSpaceSpamDebug("WaitingForSpaceReleaseAfterGramophoneSkip");
            yield return WaitForSpaceReleased();
        }

        playerController?.StopCutSceneMovement();
        FpsHorrorKit.FpsAssetsInputs.Instance?.ClearGameplayInput();
        LogSpaceSpamDebug(
            $"GramophoneWatchEnd skipped={skippedWithSpace} tapePlaying={IsGramophoneTapePlaying()} player={FormatPlayerDebug(playerController)}");
    }

    private static IEnumerator WaitForSpaceReleased()
    {
        var keyboard = Keyboard.current;
        while (keyboard != null && keyboard.spaceKey.isPressed)
        {
            FpsHorrorKit.FpsAssetsInputs.Instance?.ClearGameplayInput();
            yield return null;
            keyboard = Keyboard.current;
        }

        FpsHorrorKit.FpsAssetsInputs.Instance?.ClearGameplayInput();
    }

    private void RotatePlayerTowardGramophone(FpsHorrorKit.FpsController playerController)
    {
        if (playerController == null || gramophoneTapePlayer == null)
            return;

        Vector3 target = gramophoneTapePlayer.transform.position + Vector3.up * gramophoneLookAtHeight;
        Vector3 direction = target - playerController.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.001f)
            return;

        playerController.SetCutSceneCameraPitch(0f);
        playerController.RotateCutSceneTowards(direction, gramophoneTurnSpeed);
        playerController.StopCutSceneMovement();
    }

    private void HandleTapeFinished()
    {
        QueueHuntAfterGramophone();
    }

    private void QueueHuntAfterGramophone()
    {
        LogSpaceSpamDebug(
            $"QueueHuntAfterGramophone requested huntStarted={huntStarted} huntStartQueued={huntStartQueued} spacePressed={IsSpacePressed()}");
        if (huntStarted || huntStartQueued)
            return;

        huntStartQueued = true;
        LogSpaceSpamDebug("QueueHuntAfterGramophone accepted");
        StartCoroutine(StartPostGramophoneStairEncounterRoutine());
    }

    private IEnumerator StartPostGramophoneStairEncounterRoutine()
    {
        sequenceRoutine = null;
        if (gramophoneTapePlayer != null)
            gramophoneTapePlayer.TapeFinished -= HandleTapeFinished;

        ResolveReferences();
        ResolveStairEncounterPoints();
        LogSpaceSpamDebug(
            $"PostGramophoneStairStart monsterMarker={FormatTransformDebug(stairMonsterCutscenePoint)} playerMarker={FormatTransformDebug(stairPlayerCutscenePoint)} spacePressed={IsSpacePressed()}");
        SetPostGramophoneSpaceBlocked(true);
        SuppressBlockedGramophoneSpaceInput();
        AudioManager.Instance?.BlockGameplayAmbience(postGramophoneSilence + 30f);

        var controller = GameController.Instance;
        var playerController = controller != null && controller.playerController != null
            ? controller.playerController
            : FindFirstObjectByType<FpsHorrorKit.FpsController>();

        controller?.SetGameState(GameController.GameState.Cutscene);

        if (monster == null || playerController == null || stairMonsterCutscenePoint == null || stairPlayerCutscenePoint == null)
        {
            Debug.LogWarning("Cannot play stair encounter cutscene because player, monster, or the required stair cutscene markers are missing.");
            huntStartQueued = false;
            RestoreStairEncounterControl(controller, playerController, true, true);
            yield break;
        }

        bool previousRaycast = true;
        bool changedRaycast = false;
        if (FpsHorrorKit.PlayerInteract.Instance != null)
        {
            previousRaycast = FpsHorrorKit.PlayerInteract.Instance.sendRaycast;
            FpsHorrorKit.PlayerInteract.Instance.sendRaycast = false;
            changedRaycast = true;
        }

        Transform openedDoor = PreparePostGramophoneDoors();

        monster.DisableHunt(false);
        monster.TeleportTo(stairMonsterCutscenePoint);
        monster.SetMeshVisible(false);

        if (ShouldAbortStairEncounter())
        {
            huntStartQueued = false;
            RestoreStairEncounterControl(controller, playerController, changedRaycast, previousRaycast);
            yield break;
        }

        if (playerController != null && openedDoor != null)
            yield return RotatePlayerTowardTarget(playerController, openedDoor.position, 0.2f);

        SuppressBlockedGramophoneSpaceInput();
        BeginStairCinematicCamera(playerController, playerController.transform.position + Vector3.up * 1.35f);

        if (playerController != null && stairPlayerCutscenePoint != null)
            yield return MovePlayerToCutscenePoint(playerController, stairPlayerCutscenePoint);

        SuppressBlockedGramophoneSpaceInput();
        playerController.StopCutSceneMovement();
        FpsHorrorKit.DoorSystem.CloseAllDoorsFromStory();

        if (monster != null)
            monster.SetMeshVisible(true);

        if (playerController != null && monster != null)
            yield return RotatePlayerTowardTarget(playerController, GetMonsterLookTarget(), stairCutsceneLookHoldTime);

        yield return WaitUntilMonsterVisibleInCutscene(playerController);

        if (ShouldAbortStairEncounter())
        {
            huntStartQueued = false;
            RestoreStairEncounterControl(controller, playerController, changedRaycast, previousRaycast);
            yield break;
        }

        monsterSeenBeforeCloset = true;
        Camera scareCamera = stairCutsceneCamera != null ? stairCutsceneCamera : Camera.main;
        RestoreStairEncounterControl(controller, playerController, changedRaycast, previousRaycast);

        yield return monster.PlayCutsceneBlinkTeleportScare(
            scareCamera != null ? scareCamera : Camera.main,
            stairMonsterCutscenePoint,
            stairMonsterFlickerCount,
            stairMonsterFlickerInterval,
            stairMonsterTeleportDelay,
            stairMonsterCameraTeleportDistance,
            stairMonsterCameraHoldTime,
            0f);

        AudioManager.Instance?.PlayStairEncounterThreatOnce(10f);

        float reactionLength = AudioManager.Instance != null ? AudioManager.Instance.PlayHideVoice(1) : 0f;
        float reactionDuration = Mathf.Max(reactionLength, playerReactionDelay);
        ShowSubtitle("C\u00e1i g\u00ec v\u1eeba-", reactionDuration);
        if (reactionDuration > 0f)
            yield return new WaitForSeconds(reactionDuration);

        if (ShouldAbortStairEncounter())
        {
            huntStartQueued = false;
            SetPostGramophoneSpaceBlocked(false);
            yield break;
        }

        float remainingHiddenTime = Mathf.Max(0f, stairMonsterHiddenBeforeChase - reactionDuration);
        if (remainingHiddenTime > 0f)
            yield return new WaitForSeconds(remainingHiddenTime);

        if (ShouldAbortStairEncounter())
        {
            huntStartQueued = false;
            SetPostGramophoneSpaceBlocked(false);
            yield break;
        }

        AudioManager.Instance?.PlayMaVuDaiPatrol();

        if (ShouldAbortStairEncounter())
        {
            huntStartQueued = false;
            SetPostGramophoneSpaceBlocked(false);
            yield break;
        }

        huntStarted = true;
        huntStartQueued = false;
        waitingForCloset = true;

        if (controller != null && controller.currentGameState != GameController.GameState.Dead)
            controller.SetChapterPhase(GameController.ChapterPhase.Escape);

        if (monster != null)
            monster.EnableHunt(true, true);
    }

    private bool ShouldAbortStairEncounter()
    {
        var controller = GameController.Instance;
        if (controller != null && controller.currentGameState == GameController.GameState.Dead)
        {
            huntStartQueued = false;
            return true;
        }

        return false;
    }

    private void BeginStairCinematicCamera(FpsHorrorKit.FpsController playerController, Vector3 lookAt)
    {
        if (playerController == null)
            return;

        stairCutsceneCamera = Camera.main;
        if (stairCutsceneCamera == null)
            return;

        stairCutsceneBrain = stairCutsceneCamera.GetComponent<CinemachineBrain>();
        stairGameplayVirtualCamera = playerController.virtualCamera;
        previousStairBrainEnabled = stairCutsceneBrain == null || stairCutsceneBrain.enabled;
        previousStairVirtualCameraEnabled = stairGameplayVirtualCamera != null && stairGameplayVirtualCamera.enabled;

        if (stairCutsceneBrain != null)
            stairCutsceneBrain.enabled = false;

        if (stairGameplayVirtualCamera != null)
            stairGameplayVirtualCamera.enabled = false;

        stairCutsceneCamera.gameObject.SetActive(true);
        UpdateStairCinematicCamera(playerController, lookAt, true);
    }

    private void RestoreStairEncounterControl(
        GameController controller,
        FpsHorrorKit.FpsController playerController,
        bool restoreRaycast,
        bool raycastValue)
    {
        EndStairCinematicCamera();

        if (controller != null && controller.currentGameState != GameController.GameState.Dead)
        {
            controller.SetGameState(GameController.GameState.Gameplay);
        }
        else if (playerController != null)
        {
            playerController.isCutScene = false;
            playerController.isInteracting = false;
            playerController.StopCutSceneMovement();
        }

        if (restoreRaycast && FpsHorrorKit.PlayerInteract.Instance != null)
            FpsHorrorKit.PlayerInteract.Instance.sendRaycast = raycastValue;

        SetPostGramophoneSpaceBlocked(false);
        FpsHorrorKit.FpsAssetsInputs.Instance?.ClearGameplayInput();
    }

    private void EndStairCinematicCamera()
    {
        if (stairGameplayVirtualCamera != null)
            stairGameplayVirtualCamera.enabled = previousStairVirtualCameraEnabled;

        if (stairCutsceneBrain != null)
            stairCutsceneBrain.enabled = previousStairBrainEnabled;

        stairCutsceneCamera = null;
        stairCutsceneBrain = null;
        stairGameplayVirtualCamera = null;
        stairCutsceneCameraVelocity = Vector3.zero;
    }

    private void UpdateStairCinematicCamera(FpsHorrorKit.FpsController playerController, Vector3 lookAt, bool instant = false)
    {
        if (stairCutsceneCamera == null || playerController == null)
            return;

        Transform playerTransform = playerController.transform;
        Vector3 forward = playerTransform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.001f)
            forward = Vector3.forward;
        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 targetPosition = playerTransform.position
            + right * stairCutsceneCameraOffset.x
            + Vector3.up * stairCutsceneCameraOffset.y
            + forward * stairCutsceneCameraOffset.z;

        if (instant)
        {
            stairCutsceneCamera.transform.position = targetPosition;
            stairCutsceneCameraVelocity = Vector3.zero;
        }
        else
        {
            stairCutsceneCamera.transform.position = Vector3.SmoothDamp(
                stairCutsceneCamera.transform.position,
                targetPosition,
                ref stairCutsceneCameraVelocity,
                stairCutsceneCameraSmoothTime);
        }

        Vector3 direction = lookAt - stairCutsceneCamera.transform.position;
        if (direction.sqrMagnitude <= 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        stairCutsceneCamera.transform.rotation = instant
            ? targetRotation
            : Quaternion.Slerp(stairCutsceneCamera.transform.rotation, targetRotation, Time.deltaTime * 5.2f);
    }

    private IEnumerator WaitUntilMonsterVisibleInCutscene(FpsHorrorKit.FpsController playerController)
    {
        while (!IsMonsterVisibleToCamera(stairCutsceneCamera))
        {
            SuppressBlockedGramophoneSpaceInput();
            Vector3 lookTarget = GetMonsterLookTarget();
            playerController.RotateCutSceneTowards(lookTarget - playerController.transform.position, stairCutsceneTurnSpeed);
            playerController.StopCutSceneMovement();
            UpdateStairCinematicCamera(playerController, lookTarget);
            yield return null;
        }
    }

    private bool IsMonsterVisibleToCamera(Camera camera)
    {
        if (camera == null || !TryGetMonsterBounds(out var bounds))
            return false;

        Vector3 viewport = camera.WorldToViewportPoint(bounds.center);
        return viewport.z > 0f
            && viewport.x > 0.08f
            && viewport.x < 0.92f
            && viewport.y > 0.08f
            && viewport.y < 0.92f;
    }

    private Transform PreparePostGramophoneDoors()
    {
        if (studyDoorsToLockNames != null)
        {
            for (int i = 0; i < studyDoorsToLockNames.Length; i++)
            {
                var doorsToLock = FindDoorsByExactName(studyDoorsToLockNames[i]);
                for (int doorIndex = 0; doorIndex < doorsToLock.Count; doorIndex++)
                    doorsToLock[doorIndex]?.CloseFromStory();
            }
        }

        var doorToOpen = FindDoorByExactName(studyDoorToOpenName);
        if (doorToOpen != null)
        {
            doorToOpen.UnlockAndOpenFromStory();
            return doorToOpen.transform;
        }

        return FindSceneTransform(studyDoorToOpenName);
    }

    private void ResolveStairEncounterPoints()
    {
        if (stairMonsterCutscenePoint == null)
            stairMonsterCutscenePoint = FindSceneTransform(stairMonsterCutscenePointName);

        if (stairPlayerCutscenePoint == null)
            stairPlayerCutscenePoint = FindSceneTransform(stairPlayerCutscenePointName);
    }

    private IEnumerator RotatePlayerTowardTarget(FpsHorrorKit.FpsController playerController, Vector3 targetPosition, float holdTime)
    {
        float elapsed = 0f;
        float alignedTime = 0f;
        float timeout = Mathf.Max(1f, holdTime + 2f);

        while (elapsed < timeout)
        {
            SuppressBlockedGramophoneSpaceInput();
            Vector3 direction = targetPosition - playerController.transform.position;
            direction.y = 0f;
            bool aligned = playerController.RotateCutSceneTowards(direction, stairCutsceneTurnSpeed);
            playerController.SetCutSceneCameraPitch(0f);
            playerController.StopCutSceneMovement();
            UpdateStairCinematicCamera(playerController, targetPosition);

            if (aligned)
            {
                alignedTime += Time.deltaTime;
                if (alignedTime >= holdTime)
                    break;
            }
            else
            {
                alignedTime = 0f;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator MovePlayerToCutscenePoint(FpsHorrorKit.FpsController playerController, Transform point)
    {
        int cornerIndex = 1;
        float nextPathRefreshTime = 0f;
        float elapsed = 0f;
        int pathFailureCount = 0;
        Vector3[] corners = null;
        Vector3 moveGoal = ResolveStairCutsceneMoveGoal(point.position);
        LogSpaceSpamDebug(
            $"MoveToStairPointStart point={FormatTransformDebug(point)} goal={moveGoal} player={FormatPlayerDebug(playerController)}");

        while (HorizontalDistance(playerController.transform.position, moveGoal) > stairCutsceneArriveDistance)
        {
            SuppressBlockedGramophoneSpaceInput();
            elapsed += Time.deltaTime;
            if (elapsed >= stairCutsceneMaxMoveTime)
            {
                Debug.LogWarning($"Stair encounter cutscene movement timed out before reaching {point.name}.");
                break;
            }

            if (corners == null || (corners.Length > 0 && cornerIndex >= corners.Length) || Time.time >= nextPathRefreshTime)
            {
                moveGoal = ResolveStairCutsceneMoveGoal(point.position);
                corners = BuildStairCutscenePath(playerController.transform.position, moveGoal);
                cornerIndex = GetInitialStairPathCornerIndex(playerController.transform.position, corners);
                pathFailureCount = corners == null || corners.Length == 0 ? pathFailureCount + 1 : 0;
                nextPathRefreshTime = Time.time + 0.5f;
                LogSpaceSpamDebug(
                    $"PathRefresh failures={pathFailureCount} corners={(corners != null ? corners.Length : -1)} cornerIndex={cornerIndex} playerPos={playerController.transform.position} moveGoal={moveGoal} marker={point.position}");
            }

            if (corners == null || corners.Length == 0)
            {
                playerController.StopCutSceneMovement();
                UpdateStairCinematicCamera(playerController, point.position + Vector3.up * 1.35f);

                if (pathFailureCount == 2)
                {
                    Debug.LogWarning($"Stair encounter cutscene could not build a complete NavMesh path to {point.name}; waiting instead of snapping player to the cutscene marker.");
                }

                yield return null;
                continue;
            }

            Vector3 target = corners.Length > 0 && cornerIndex < corners.Length
                ? corners[cornerIndex]
                : moveGoal;

            Vector3 offset = target - playerController.transform.position;
            offset.y = 0f;
            if (offset.magnitude <= stairCutsceneArriveDistance * 0.75f)
            {
                cornerIndex++;
                playerController.StopCutSceneMovement();
                yield return null;
                continue;
            }

            playerController.MoveCutScene(offset.normalized, stairCutsceneMoveSpeed, true, stairCutsceneTurnSpeed);
            UpdateStairCinematicCamera(playerController, playerController.transform.position + Vector3.up * 1.35f);
            if (Time.unscaledTime >= nextStairMoveDebugLogTime)
            {
                nextStairMoveDebugLogTime = Time.unscaledTime + debugMoveLogInterval;
                LogSpaceSpamDebug(
                    $"MoveStep target={target} offset={offset} dist={offset.magnitude:F2} player={FormatPlayerDebug(playerController)}");
            }
            yield return null;
        }

        playerController.StopCutSceneMovement();
        LogSpaceSpamDebug(
            $"MoveToStairPointEnd player={FormatPlayerDebug(playerController)} goal={moveGoal} marker={point.position}");
        UpdateStairCinematicCamera(playerController, GetMonsterLookTarget(), true);
    }

    private Vector3 ResolveStairCutsceneMoveGoal(Vector3 desiredGoal)
    {
        float goalSampleRadius = Mathf.Max(stairCutsceneNavMeshSampleRadius * 8f, 6f);
        if (SampleStairCutsceneNavMesh(desiredGoal, out var goalHit, goalSampleRadius))
            return goalHit.position;

        if (SampleNearestStairNavMeshPoint(desiredGoal, out var nearestGoal))
            return nearestGoal;

        Debug.LogWarning("Cannot find a NavMesh point near the stair cutscene player marker; falling back to marker position.");
        LogSpaceSpamDebug($"ResolveMoveGoalFailed desired={desiredGoal} sampleRadius={goalSampleRadius:F2}");
        return desiredGoal;
    }

    private int GetInitialStairPathCornerIndex(Vector3 from, Vector3[] corners)
    {
        if (corners == null || corners.Length <= 1)
            return 0;

        return HorizontalDistance(from, corners[0]) > stairCutsceneArriveDistance * 1.5f ? 0 : 1;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private Vector3[] BuildStairCutscenePath(Vector3 from, Vector3 to)
    {
        stairCutscenePath ??= new NavMeshPath();

        float startSampleRadius = Mathf.Max(stairCutsceneNavMeshSampleRadius, stairCutsceneArriveDistance * 8f, 1.5f);
        bool sampledStart = SampleStairCutsceneNavMesh(from, out var startHit, startSampleRadius);
        bool sampledEnd = SampleStairCutsceneNavMesh(to, out var endHit, stairCutsceneNavMeshSampleRadius * 6f);
        if (sampledStart && HorizontalDistance(from, startHit.position) > startSampleRadius)
        {
            LogSpaceSpamDebug($"BuildPathRejected start sample is too far from player startDistance={HorizontalDistance(from, startHit.position):F2}");
            return BuildDirectStairCutsceneFallback(from, to, "start sample too far");
        }

        LogSpaceSpamDebug(
            $"BuildPath from={from} to={to} sampledStart={sampledStart} sampledEnd={sampledEnd} startHit={(sampledStart ? startHit.position.ToString() : "none")} endHit={(sampledEnd ? endHit.position.ToString() : "none")}");
        if (!sampledEnd)
        {
            if (!SampleNearestStairNavMeshPoint(to, out var nearestEnd))
                return BuildDirectStairCutsceneFallback(from, to, "end not on NavMesh");

            endHit.position = nearestEnd;
        }

        if (!sampledStart)
        {
            if (!SampleNearestStairNavMeshPoint(from, out var nearestVisibleNavMeshPoint))
                return BuildDirectStairCutsceneFallback(from, endHit.position, "start not on NavMesh");

            return new[] { from, nearestVisibleNavMeshPoint, endHit.position };
        }

        if (NavMesh.CalculatePath(startHit.position, endHit.position, NavMesh.AllAreas, stairCutscenePath)
            && stairCutscenePath.status == NavMeshPathStatus.PathComplete
            && stairCutscenePath.corners.Length >= 2)
        {
            LogSpaceSpamDebug($"BuildPathSuccess status={stairCutscenePath.status} corners={stairCutscenePath.corners.Length}");
            if (HorizontalDistance(from, startHit.position) > stairCutsceneArriveDistance)
            {
                var cornersWithStart = new Vector3[stairCutscenePath.corners.Length + 1];
                cornersWithStart[0] = from;
                System.Array.Copy(stairCutscenePath.corners, 0, cornersWithStart, 1, stairCutscenePath.corners.Length);
                return cornersWithStart;
            }

            return stairCutscenePath.corners;
        }

        LogSpaceSpamDebug($"BuildPathFailed status={(stairCutscenePath != null ? stairCutscenePath.status.ToString() : "null")}");
        return BuildDirectStairCutsceneFallback(from, endHit.position, "NavMesh path failed");
    }

    private Vector3[] BuildDirectStairCutsceneFallback(Vector3 from, Vector3 to, string reason)
    {
        LogSpaceSpamDebug($"BuildPathFallback direct movement accepted reason={reason} from={from} to={to}");
        return new[] { from, to };
    }

    private bool IsGramophoneTapePlaying()
    {
        return gramophoneTapePlayer != null && gramophoneTapePlayer.IsPlayingTape;
    }

    private void SuppressBlockedGramophoneSpaceInput()
    {
        if (!blockSpaceUntilPostGramophoneCutsceneEnds)
            return;

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            LogSpaceSpamDebug("SpaceIgnoredAfterGramophoneSkip");

        FpsHorrorKit.FpsAssetsInputs.Instance?.ClearGameplayInput();
    }

    private void SetPostGramophoneSpaceBlocked(bool blocked)
    {
        blockSpaceUntilPostGramophoneCutsceneEnds = blocked;
        CutSceneManager.SuppressSpaceSkipInput = blocked;
        FpsHorrorKit.FpsAssetsInputs.Instance?.ClearGameplayInput();
    }

    private static bool IsSpacePressed()
    {
        return Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
    }

    private void LogSpaceSpamDebug(string message)
    {
        if (!debugSpaceSpamCutscene)
            return;

        Debug.Log($"[SpaceSpamDebug] {message}");
    }

    private static string FormatTransformDebug(Transform target)
    {
        return target != null
            ? $"{target.name} pos={target.position} rot={target.rotation.eulerAngles}"
            : "null";
    }

    private static string FormatPlayerDebug(FpsHorrorKit.FpsController playerController)
    {
        if (playerController == null)
            return "null";

        return $"pos={playerController.transform.position} rot={playerController.transform.rotation.eulerAngles} cutscene={playerController.isCutScene} interacting={playerController.isInteracting}";
    }

    private static bool IsDirectStairCutsceneSegmentClear(Vector3 from, Vector3 to)
    {
        Vector3 origin = from + Vector3.up * 0.85f;
        Vector3 target = to + Vector3.up * 0.85f;
        return !Physics.Linecast(origin, target, ~0, QueryTriggerInteraction.Ignore);
    }

    private bool SampleStairCutsceneNavMesh(Vector3 position, out NavMeshHit hit, float radius)
    {
        return NavMesh.SamplePosition(position, out hit, Mathf.Max(0.1f, radius), NavMesh.AllAreas);
    }

    private bool SampleNearestStairNavMeshPoint(Vector3 from, out Vector3 point)
    {
        float baseRadius = Mathf.Max(stairCutsceneNavMeshSampleRadius, 0.5f);
        float maxRadius = baseRadius * 8f;

        for (float radius = baseRadius; radius <= maxRadius; radius += baseRadius)
        {
            if (NavMesh.SamplePosition(from, out var hit, radius, NavMesh.AllAreas))
            {
                point = hit.position;
                return true;
            }

            const int directionCount = 16;
            for (int i = 0; i < directionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / directionCount;
                Vector3 candidate = from + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                if (!NavMesh.SamplePosition(candidate, out hit, baseRadius * 0.45f, NavMesh.AllAreas))
                    continue;

                point = hit.position;
                return true;
            }
        }

        point = from;
        return false;
    }

    private Vector3 GetMonsterLookTarget()
    {
        if (monster == null)
            return Vector3.zero;

        if (TryGetMonsterBounds(out var bounds))
            return bounds.center;

        return monster.transform.position + Vector3.up * 1.35f;
    }

    private static FpsHorrorKit.DoorSystem FindDoorByExactName(string doorName)
    {
        var doors = FindDoorsByExactName(doorName);
        return doors.Count > 0 ? doors[0] : null;
    }

    private static List<FpsHorrorKit.DoorSystem> FindDoorsByExactName(string doorName)
    {
        var matches = new List<FpsHorrorKit.DoorSystem>();
        var seen = new HashSet<FpsHorrorKit.DoorSystem>();
        if (string.IsNullOrWhiteSpace(doorName))
            return matches;

        foreach (var door in Resources.FindObjectsOfTypeAll<FpsHorrorKit.DoorSystem>())
        {
            if (door == null || !door.gameObject.scene.IsValid())
                continue;

            if (door.name == doorName)
                AddDoorIfUnique(door, matches, seen);
        }

        foreach (var sceneTransform in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (sceneTransform == null || sceneTransform.name != doorName || !sceneTransform.gameObject.scene.IsValid())
                continue;

            AddDoorIfUnique(sceneTransform.GetComponent<FpsHorrorKit.DoorSystem>(), matches, seen);
            AddDoorIfUnique(sceneTransform.GetComponentInParent<FpsHorrorKit.DoorSystem>(true), matches, seen);
            AddDoorIfUnique(sceneTransform.GetComponentInChildren<FpsHorrorKit.DoorSystem>(true), matches, seen);
        }

        return matches;
    }

    private static void AddDoorIfUnique(FpsHorrorKit.DoorSystem door, List<FpsHorrorKit.DoorSystem> matches, HashSet<FpsHorrorKit.DoorSystem> seen)
    {
        if (door == null || !door.gameObject.scene.IsValid() || !seen.Add(door))
            return;

        matches.Add(door);
    }

    private void HandlePlayerEnteredCloset(FpsHorrorKit.ClosetHiding closet)
    {
        if (!waitingForCloset || closetObjectiveStarted || closetObjectiveCompleted)
            return;

        if (requireMonsterSeenBeforeCloset && !monsterSeenBeforeCloset)
        {
            ShowSubtitle("Chưa... mình phải biết nó ở đâu đã.", 2.2f);
            closet.SetExitAllowed(true);
            return;
        }

        preferredCloset = closet;
        StartCoroutine(ClosetObjectiveRoutine(closet));
    }

    private void HandlePlayerExitedCloset(FpsHorrorKit.ClosetHiding closet)
    {
        if (!closetObjectiveActive || closetObjectiveCompleted)
            return;

        playerExitedClosetDuringObjective = true;
        closetObjectiveActive = false;
        closetObjectiveStarted = false;
        waitingForCloset = true;
        StopClosetObjectiveDialogue();
    }

    private IEnumerator ClosetObjectiveRoutine(FpsHorrorKit.ClosetHiding closet)
    {
        closetObjectiveStarted = true;
        waitingForCloset = false;
        closetObjectiveActive = true;
        playerExitedClosetDuringObjective = false;
        closet.SetExitAllowed(true);

        if (moveMonsterToSpawnAfterCloset && monster != null && monsterUpperFloorSpawn != null)
            monster.MoveToWanderPoint(monsterUpperFloorSpawn, false);

        if (closetObjectiveHoldTime > 0f)
            yield return new WaitForSeconds(closetObjectiveHoldTime);

        if (ShouldAbortClosetObjective())
            yield break;

        yield return PlayClosetObjectiveKhoaLine(2, "Đừng thấy mình... đừng thấy mình...", 3.2f);
        if (ShouldAbortClosetObjective())
            yield break;

        float callLength = AudioManager.Instance != null ? AudioManager.Instance.PlayMaVuDaiPatrol() : 0f;
        if (callLength > 0f)
        {
            float callDuration = Mathf.Min(Mathf.Max(callLength, 2.2f), 3f);
            FpsHorrorKit.InteractMessageScript.Instance?.ClearMessage();
            yield return WaitForClosetObjective(callDuration);
            if (ShouldAbortClosetObjective())
                yield break;
        }

        yield return PlayClosetObjectiveKhoaLine(3, "Ổn rồi... hình như ổn rồi.", 1.4f);
        if (ShouldAbortClosetObjective())
            yield break;

        yield return PlayClosetObjectiveKhoaLine(4, "Mình vừa nghe thấy gì vậy? Không... cứ ở đây thêm chút đã.", 4f);
        if (ShouldAbortClosetObjective())
            yield break;

        yield return PlayClosetObjectiveKhoaLine(5, "Nó gọi đúng tên mình. Rồi nó hát... đúng cái điệu bản này.", 4.5f);
        if (ShouldAbortClosetObjective())
            yield break;

        yield return PlayClosetObjectiveKhoaLine(6, "Có cái gì đó thật sự ở đây.", 3.5f);
        if (ShouldAbortClosetObjective())
            yield break;

        UnlockEscapeRouteDoors();
        closet.SetExitAllowed(true);
        monster?.DisableHunt(true);
        ResolveReferences();
        wellEndingTrigger?.PlayWellFxBurst();
        ArmEndingTriggers();
        ShowSubtitle("Nhiệm vụ hoàn thành.", 2.5f);

        GameController.Instance?.SetChapterPhase(GameController.ChapterPhase.Escape);

        closetObjectiveCompleted = true;
        closetObjectiveActive = false;
        closetObjectiveStarted = false;
    }

    private bool ShouldAbortClosetObjective()
    {
        if (playerExitedClosetDuringObjective || !FpsHorrorKit.ClosetHiding.IsAnyPlayerHidden)
        {
            closetObjectiveActive = false;
            closetObjectiveStarted = false;
            waitingForCloset = true;
            StopClosetObjectiveDialogue();
            return true;
        }

        var controller = GameController.Instance;
        if (controller != null && controller.currentGameState == GameController.GameState.Dead)
        {
            closetObjectiveActive = false;
            return true;
        }

        return false;
    }

    private void StopClosetObjectiveDialogue()
    {
        AudioManager.Instance?.StopVoice();
        FpsHorrorKit.InteractMessageScript.Instance?.ClearMessage();
    }

    private IEnumerator PlayClosetObjectiveKhoaLine(int index, string subtitle, float fallbackDuration)
    {
        if (ShouldAbortClosetObjective())
            yield break;

        float length = AudioManager.Instance != null ? AudioManager.Instance.PlayHideVoice(index) : 0f;
        float duration = Mathf.Max(length, fallbackDuration);
        ShowSubtitle(subtitle, duration);
        yield return WaitForClosetObjective(duration);
    }

    private IEnumerator WaitForClosetObjective(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (ShouldAbortClosetObjective())
                yield break;

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator PlayDiaryLine(int index, string subtitle, float fallbackDuration)
    {
        float length = AudioManager.Instance != null ? AudioManager.Instance.PlayDiaryReaction(index) : 0f;
        float duration = Mathf.Max(length, fallbackDuration);
        ShowSubtitle(subtitle, duration);
        yield return new WaitForSeconds(duration);
    }

    private void UnlockEscapeRouteDoors()
    {
        if (doorsToOpenAfterCloset != null)
        {
            for (int i = 0; i < doorsToOpenAfterCloset.Length; i++)
                doorsToOpenAfterCloset[i]?.UnlockFromStory();
        }

        var doors = FindObjectsByType<FpsHorrorKit.DoorSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var door in doors)
        {
            if (door == null)
                continue;

            if (door.openWhenPianoCompleted || door.closeAndLockWhenGramophoneTapePlays || ShouldOpenRouteDoor(door.name))
                door.UnlockFromStory();
        }
    }

    private void ArmEndingTriggers()
    {
        var triggers = FindObjectsByType<EndingCutsceneTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < triggers.Length; i++)
            triggers[i]?.Arm();
    }

    private bool ShouldOpenRouteDoor(string doorName)
    {
        if (string.IsNullOrWhiteSpace(doorName) || routeDoorNameContains == null)
            return false;

        string lowerName = doorName.ToLowerInvariant();
        for (int i = 0; i < routeDoorNameContains.Length; i++)
        {
            string token = routeDoorNameContains[i];
            if (!string.IsNullOrWhiteSpace(token) && lowerName.Contains(token.ToLowerInvariant()))
                return true;
        }

        return false;
    }

    private void EnableWellEnding()
    {
        ResolveReferences();
        wellEndingTrigger?.ActivateEndingSetup();
    }

    private bool IsPlayerLookingAtMonster()
    {
        if (monster == null || !monster.IsHuntActive)
            return false;

        Camera camera = Camera.main;
        if (camera == null || !TryGetMonsterBounds(out var bounds))
            return false;

        Vector3 origin = camera.transform.position;
        Vector3 target = bounds.center;
        Vector3 toMonster = target - origin;
        float distance = toMonster.magnitude;
        if (distance <= 0.05f || distance > monsterSeenDistance)
            return false;

        Vector3 direction = toMonster / distance;
        if (Vector3.Dot(camera.transform.forward, direction) < monsterSeenDot)
            return false;

        Vector3 viewport = camera.WorldToViewportPoint(target);
        if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
            return false;

        if (monsterSeenRayRadius > 0f)
        {
            if (Physics.SphereCast(origin, monsterSeenRayRadius, direction, out var sphereHit, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return sphereHit.transform == monster.transform || sphereHit.transform.IsChildOf(monster.transform);
        }
        else if (Physics.Raycast(origin, direction, out var rayHit, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            return rayHit.transform == monster.transform || rayHit.transform.IsChildOf(monster.transform);
        }

        return false;
    }

    private bool TryGetMonsterBounds(out Bounds bounds)
    {
        bounds = default;
        if (monster == null)
            return false;

        var renderers = monster.GetComponentsInChildren<Renderer>(false);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer monsterRenderer = renderers[i];
            if (monsterRenderer == null || !monsterRenderer.enabled)
                continue;

            if (!hasBounds)
            {
                bounds = monsterRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(monsterRenderer.bounds);
            }
        }

        if (hasBounds)
            return true;

        bounds = new Bounds(monster.transform.position + Vector3.up * 1.2f, Vector3.one * 1.5f);
        return true;
    }

    private IEnumerator PlayHallGhostPass()
    {
        Transform player = ResolvePlayer();
        Transform start = hallGhostStart != null ? hallGhostStart : CreateRuntimePoint("HallGhostStart_Runtime", GetFallbackHallPoint(player, -5.5f));
        Transform end = hallGhostEnd != null ? hallGhostEnd : CreateRuntimePoint("HallGhostEnd_Runtime", GetFallbackHallPoint(player, 5.5f));
        Transform hide = hallGhostHidePoint != null ? hallGhostHidePoint : CreateRuntimePoint("HallGhostHide_Runtime", end.position + end.right * 1.5f);

        monster.TeleportTo(start);
        monster.PlayScriptedHallCrossing(BuildHallRoute(end), hide);

        float elapsed = 0f;
        while (elapsed < fallbackHallPassWait && monster.IsScriptedSequenceRunning)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        monster.SetMeshVisible(false);
    }

    private Transform[] BuildHallRoute(Transform fallbackEnd)
    {
        if (hallGhostRoutePoints == null || hallGhostRoutePoints.Length == 0)
            return new[] { fallbackEnd };

        int count = 0;
        for (int i = 0; i < hallGhostRoutePoints.Length; i++)
        {
            if (hallGhostRoutePoints[i] != null)
                count++;
        }

        if (count == 0)
            return new[] { fallbackEnd };

        var route = new Transform[count];
        int writeIndex = 0;
        for (int i = 0; i < hallGhostRoutePoints.Length; i++)
        {
            if (hallGhostRoutePoints[i] != null)
                route[writeIndex++] = hallGhostRoutePoints[i];
        }

        return route;
    }

    private void ShowSubtitle(string subtitle, float duration)
    {
        FpsHorrorKit.InteractMessageScript.Instance?.ShowMessage($"\"{subtitle}\"", duration);
    }

    private void ResolveReferences()
    {
        if (monster == null)
        {
            var monsterObject = GameObject.Find("MonsterPlaceholder");
            monster = monsterObject != null
                ? monsterObject.GetComponent<MonsterAI>()
                : FindFirstObjectByType<MonsterAI>(FindObjectsInactive.Include);
        }

        if (gramophoneTapePlayer == null)
            gramophoneTapePlayer = FindFirstObjectByType<FpsHorrorKit.GramophoneTapePlayer>(FindObjectsInactive.Include);

        if (preferredCloset == null)
            preferredCloset = FindFirstObjectByType<FpsHorrorKit.ClosetHiding>(FindObjectsInactive.Include);

        if (wellEndingTrigger == null)
        {
            wellEndingTrigger = FindFirstObjectByType<WellEndingTrigger>(FindObjectsInactive.Include);
            if (wellEndingTrigger == null)
            {
                var wellObject = GameObject.Find("Well");
                if (wellObject != null)
                    wellEndingTrigger = wellObject.AddComponent<WellEndingTrigger>();
            }
        }

        if (monsterUpperFloorSpawn == null)
        {
            var spawn = GameObject.Find("BossSpawnPoint")
                ?? GameObject.Find("SpawnPointBoss")
                ?? GameObject.Find("Spawn Point Boss")
                ?? GameObject.Find("spawm point boss")
                ?? GameObject.Find("MonsterUpperFloorSpawn");
            if (spawn != null)
                monsterUpperFloorSpawn = spawn.transform;
        }

        if (hallGhostStart == null)
            hallGhostStart = ResolveMarker("HallGhostStart");
        if (hallGhostEnd == null)
            hallGhostEnd = ResolveMarker("HallGhostEnd");
        if (hallGhostHidePoint == null)
            hallGhostHidePoint = ResolveMarker("HallGhostHidePoint");
        if (hallGhostRoutePoints == null || hallGhostRoutePoints.Length == 0)
        {
            var mid = ResolveMarker("HallGhostMid");
            if (mid != null)
                hallGhostRoutePoints = hallGhostEnd != null ? new[] { mid, hallGhostEnd } : new[] { mid };
        }
    }

    private static Transform ResolveMarker(string markerName)
    {
        var marker = GameObject.Find(markerName);
        return marker != null ? marker.transform : null;
    }

    private static Transform FindSceneTransform(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        foreach (var sceneTransform in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (sceneTransform.name == objectName && sceneTransform.gameObject.scene.IsValid())
                return sceneTransform;
        }

        return null;
    }

    private static Transform ResolvePlayer()
    {
        var playerController = FindFirstObjectByType<FpsHorrorKit.FpsController>();
        return playerController != null ? playerController.transform : null;
    }

    private static Vector3 GetFallbackHallPoint(Transform player, float sideOffset)
    {
        if (player == null)
            return new Vector3(sideOffset, 0f, 0f);

        Vector3 forward = player.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;
        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        return player.position + forward * 4f + right * sideOffset;
    }

    private static Transform CreateRuntimePoint(string objectName, Vector3 position)
    {
        var point = new GameObject(objectName).transform;
        point.position = position;
        point.rotation = Quaternion.identity;
        return point;
    }
}
