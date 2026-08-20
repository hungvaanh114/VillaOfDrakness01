using System.Collections;
using UnityEngine;
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
    [SerializeField, Min(0.2f)] private float skipPromptRefreshInterval = 1.2f;

    [Header("Carry Gramophone")]
    [SerializeField] private Transform gramophoneHoldPoint;
    [SerializeField, Min(0f)] private float gramophonePickupDelay = 0.85f;
    [SerializeField] private Vector3 gramophoneHeldLocalPosition = new(0.04f, -0.08f, 0.32f);
    [SerializeField] private Vector3 gramophoneHeldLocalEuler = new(-12f, 24f, 0f);
    [SerializeField] private Vector3 gramophoneHeldLocalScale = Vector3.one;

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
        controller?.SetChapterPhase(GameController.ChapterPhase.HallEncounter);
        controller?.SetGameState(GameController.GameState.Cutscene);

        if (monster != null)
            monster.DisableHunt(true);

        gramophoneTapePlayer.TapeFinished -= HandleTapeFinished;
        gramophoneTapePlayer.PlayTape();

        yield return WatchGramophoneUntilTapeEnds();
        yield return CarryGramophoneInHand();

        if (controller != null && controller.currentGameState != GameController.GameState.Dead)
            controller.SetGameState(GameController.GameState.Gameplay);

        yield return PlayDiaryLine(1, "...Ổn.", 1.4f);
        yield return PlayDiaryLine(2, "Cái... cái gì vậy?", 2.2f);

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

        float nextPromptTime = 0f;
        while (gramophoneTapePlayer != null && gramophoneTapePlayer.IsPlayingTape)
        {
            RotatePlayerTowardGramophone(playerController);

            if (allowSkipGramophoneCutscene && Time.time >= nextPromptTime)
            {
                FpsHorrorKit.InteractMessageScript.Instance?.ShowMessage("Đang nghe gramophone... nhấn SPACE để bỏ qua.", skipPromptRefreshInterval + 0.15f);
                nextPromptTime = Time.time + skipPromptRefreshInterval;
            }

            if (allowSkipGramophoneCutscene && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                gramophoneTapePlayer.SkipTape();
                break;
            }

            yield return null;
        }

        if (changedRaycast && FpsHorrorKit.PlayerInteract.Instance != null)
            FpsHorrorKit.PlayerInteract.Instance.sendRaycast = previousRaycast;
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

    private IEnumerator CarryGramophoneInHand()
    {
        Transform holdPoint = ResolveGramophoneHoldPoint();
        if (gramophoneTapePlayer != null && holdPoint != null)
        {
            Transform gramophone = gramophoneTapePlayer.transform;
            gramophone.SetParent(holdPoint, false);
            gramophone.localPosition = gramophoneHeldLocalPosition;
            gramophone.localEulerAngles = gramophoneHeldLocalEuler;
            gramophone.localScale = gramophoneHeldLocalScale;

            var colliders = gramophone.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    colliders[i].enabled = false;
            }
        }

        if (gramophonePickupDelay > 0f)
            yield return new WaitForSeconds(gramophonePickupDelay);
    }

    private Transform ResolveGramophoneHoldPoint()
    {
        if (gramophoneHoldPoint != null)
            return gramophoneHoldPoint;

        Transform leftHand = FindSceneTransform("LeftHandProp") ?? FindSceneTransform("LeftHand");
        if (leftHand != null)
            return gramophoneHoldPoint = EnsureCarryPoint(leftHand);

        Transform existingHoldPoint = FindSceneTransform("ItemHoldPoint");
        if (existingHoldPoint != null)
            return gramophoneHoldPoint = existingHoldPoint;

        Camera camera = Camera.main;
        return camera != null ? gramophoneHoldPoint = EnsureCarryPoint(camera.transform) : null;
    }

    private Transform EnsureCarryPoint(Transform parent)
    {
        Transform existing = parent.Find("GramophoneCarryPoint");
        if (existing != null)
            return existing;

        var carryPoint = new GameObject("GramophoneCarryPoint").transform;
        carryPoint.SetParent(parent, false);
        carryPoint.localPosition = Vector3.zero;
        carryPoint.localRotation = Quaternion.identity;
        return carryPoint;
    }

    private void HandleTapeFinished()
    {
        QueueHuntAfterGramophone();
    }

    private void QueueHuntAfterGramophone()
    {
        if (huntStarted || huntStartQueued)
            return;

        huntStartQueued = true;
        StartCoroutine(StartHuntAfterGramophoneRoutine());
    }

    private IEnumerator StartHuntAfterGramophoneRoutine()
    {
        sequenceRoutine = null;
        if (gramophoneTapePlayer != null)
            gramophoneTapePlayer.TapeFinished -= HandleTapeFinished;

        if (monsterUpperFloorSpawn != null)
            monster.TeleportTo(monsterUpperFloorSpawn);

        monster.DisableHunt(true);

        if (postGramophoneSilence > 0f)
            yield return new WaitForSeconds(postGramophoneSilence);

        huntStarted = true;
        huntStartQueued = false;
        monsterSeenBeforeCloset = false;
        monster.EnableHunt(true);
        ShowSubtitle("Khoa ơi... con đâu rồi... ra đây với má đi con...", 7f);

        GameController.Instance?.SetChapterPhase(GameController.ChapterPhase.Escape);

        waitingForCloset = true;
        StartCoroutine(PlayerReactionRoutine());
    }

    private IEnumerator PlayerReactionRoutine()
    {
        if (playerReactionDelay > 0f)
            yield return new WaitForSeconds(playerReactionDelay);

        if (closetObjectiveCompleted || closetObjectiveActive || !waitingForCloset)
            yield break;

        float length = AudioManager.Instance != null ? AudioManager.Instance.PlayHideVoice(1) : 0f;
        ShowSubtitle("Cái gì vừa-", Mathf.Max(length, 2.2f));
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
        float callDuration = Mathf.Min(Mathf.Max(callLength, 2.2f), 3f);
        ShowSubtitle("Khoa ơi... con đâu rồi... ra đây với má đi con...", callDuration);
        yield return WaitForClosetObjective(callDuration);
        if (ShouldAbortClosetObjective())
            yield break;

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

        OpenEscapeRouteDoors();
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

    private void OpenEscapeRouteDoors()
    {
        if (doorsToOpenAfterCloset != null)
        {
            for (int i = 0; i < doorsToOpenAfterCloset.Length; i++)
                doorsToOpenAfterCloset[i]?.UnlockAndOpenFromStory();
        }

        var doors = FindObjectsByType<FpsHorrorKit.DoorSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var door in doors)
        {
            if (door == null)
                continue;

            if (door.openWhenPianoCompleted || door.closeAndLockWhenGramophoneTapePlays || ShouldOpenRouteDoor(door.name))
                door.UnlockAndOpenFromStory();
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
