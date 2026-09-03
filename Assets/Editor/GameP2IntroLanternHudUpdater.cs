using System.Collections.Generic;
using MainGame.P2;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public static class GameP2IntroLanternHudUpdater
{
    private const string ScenePath = "Assets/MainGame/GameP2.unity";
    private const string IntroCutSceneId = "intro";
    private const string LanternFramePath = "Assets/FpsHorrorKit/Png/UI/LanternOut.png";
    private const string LanternFillPath = "Assets/FpsHorrorKit/Png/UI/LanternIn_V2.png";
    private const string FlameMaterialPath = "Assets/FpsHorrorKit/Materials/PM_CandleFlame.mat";
    private const string LanternPrefabPath = "Assets/FpsHorrorKit/Prefabs/Systems/Lantern.prefab";

    [MenuItem("MainGame/P2/Apply Front Door Intro And Lantern HUD")]
    public static void Apply()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        var manager = FindOrCreateCutSceneManager();
        manager.enabled = true;
        EditorUtility.SetDirty(manager);
        var player = FindSceneTransform("Player");
        var fps = player != null
            ? player.GetComponent<FpsHorrorKit.FpsController>()
            : Object.FindFirstObjectByType<FpsHorrorKit.FpsController>(FindObjectsInactive.Include);
        var mainCamera = Camera.main ?? Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
        var followCamera = FindSceneTransform("FollowCamera");
        var narrationPanel = FindSceneTransform("NarrationPanel");
        var narrationText = FindSceneTransform("NarrationText");
        var voiceSource = manager.GetComponent<AudioSource>() ?? manager.gameObject.AddComponent<AudioSource>();

        ApplyFrontDoorIntro(manager, player, fps, mainCamera, followCamera, narrationPanel, narrationText, voiceSource);
        ApplyLanternHud();
        ApplyLanternFireFx();
        ApplyP2LanternPresentation();
        ApplyGhostPatrolRoutes();
        RemoveP2WindowEntryMechanic();
        WireLampFillImage();

        var controller = Object.FindFirstObjectByType<GameController>(FindObjectsInactive.Include);
        if (controller != null)
        {
            var serialized = new SerializedObject(controller);
            Set(serialized, "cutSceneManager", manager);
            Set(serialized, "playIntroOnStart", true);
            Set(serialized, "useChapterOneCheckpoints", false);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("GameP2 front-door intro and lantern HUD applied.");
    }

    [MenuItem("MainGame/P2/Apply Lantern In-Lamp Fire FX")]
    public static void ApplyLanternFireFxMenu()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        ApplyLanternFireFx();
        ApplyP2LanternPresentation();
        ApplyGhostPatrolRoutes();
        RemoveP2WindowEntryMechanic();
        WireLampFillImage();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("GameP2 lantern in-lamp fire FX applied.");
    }

    [MenuItem("MainGame/P2/Apply Ghost Patrol Routes")]
    public static void ApplyGhostPatrolRoutesMenu()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        ApplyGhostPatrolRoutes();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("GameP2 ghost patrol routes applied.");
    }

    private static void ApplyFrontDoorIntro(
        CutSceneManager manager,
        Transform player,
        FpsHorrorKit.FpsController fps,
        Camera mainCamera,
        Transform followCamera,
        Transform narrationPanel,
        Transform narrationText,
        AudioSource voiceSource)
    {
        var sequenceRoot = EnsureChild(manager.transform, "IntroCutSceneSequence");
        var pointsRoot = EnsureChild(sequenceRoot, "IntroPathPoints");

        var forestStart = EnsurePoint(pointsRoot, "ForestStartPoint", new Vector3(-6f, 1.07f, -81.8f), Vector3.forward);
        var gateApproach = EnsurePoint(pointsRoot, "GateApproachPoint", new Vector3(-8.68f, 1.07f, -47.41f), Vector3.forward);
        var frontDoor = EnsurePoint(pointsRoot, "FrontDoorPoint", new Vector3(-4.73f, 0.66f, -35.57f), Vector3.forward);
        var lobbyEntry = EnsurePoint(
            pointsRoot,
            "FrontDoorEntryPoint",
            FindSceneTransform("Trigger_EnterLobby")?.position ?? new Vector3(0f, 0.8f, -18.3f),
            Vector3.forward);

        var sequence = sequenceRoot.GetComponent<CutSceneSequence>() ?? sequenceRoot.gameObject.AddComponent<CutSceneSequence>();
        sequence.Configure(IntroCutSceneId, true, forestStart, true, CreateFrontDoorRoute(gateApproach, frontDoor, lobbyEntry));
        EditorUtility.SetDirty(sequence);

        RemoveP2WindowEntryMechanic();

        WireFollowCamera(followCamera, fps);

        var serialized = new SerializedObject(manager);
        Set(serialized, "introCutSceneId", IntroCutSceneId);
        Set(serialized, "introWindowEntryCutSceneId", "p2_window_entry_disabled");
        Set(serialized, "autoResolveSequences", false);
        SetSequenceList(serialized, sequence);
        Set(serialized, "playerController", fps);
        Set(serialized, "playerRoot", player != null ? player : fps != null ? fps.transform : null);
        Set(serialized, "cinematicCamera", mainCamera);
        Set(serialized, "cinemachineBrain", mainCamera != null ? mainCamera.GetComponent<CinemachineBrain>() : null);
        Set(serialized, "gameplayVirtualCamera", followCamera != null ? followCamera.GetComponent<CinemachineCamera>() : null);
        Set(serialized, "subtitlePanel", narrationPanel != null ? narrationPanel.gameObject : null);
        Set(serialized, "subtitleText", narrationText != null ? narrationText.GetComponent<TMP_Text>() : null);
        Set(serialized, "voiceSource", voiceSource);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(manager);
    }

    private static IEnumerable<CutScenePoint> CreateFrontDoorRoute(Transform gateApproach, Transform frontDoor, Transform lobbyEntry)
    {
        return new[]
        {
            Point("Walk from forest to gate", gateApproach, "intro_gate_arrival", "Duong mon vao biet thu hien ro duoi bau troi xam duc. Ngoc di cham, mot tay cam den dau, tay kia giu cuon so cu cua ba.", 7.5f, true, CutSceneCameraShot.OverheadFollow),
            Point("Stop at gate", gateApproach, "intro_arrived_line", "\"Ba oi, con da den roi.\"", 3.2f, false, CutSceneCameraShot.DescendBehind),
            Point("Read gate sign", gateApproach, "intro_dogia_sign", "Tam bang ten cu hien len duoi anh den dau: Do Gia.", 3.2f, false, CutSceneCameraShot.SignClose),
            Point("Remember grandmother note", gateApproach, "intro_villa_history", "\"Ba noi can nha nay giu thu co the cuu ho. Con khong hieu het, nhung con tin ba.\"", 6f, false, CutSceneCameraShot.SignClose),
            Point("Walk to front door", frontDoor, "intro_walk_front_door", "Ngoc day cong sat, buoc qua san truoc va di thang ve phia cua chinh.", 5.2f, true, CutSceneCameraShot.DescendBehind),
            Point("Open front door", frontDoor, "intro_open_front_door", "\"Tam guong bac. Tim duoc thi dem ve. Ba dan... dung nhin vao mat nuoc trong nha. Tuyet doi khong.\"", 6.2f, false, CutSceneCameraShot.BehindShoulder),
            Point("Enter lobby through front door", lobbyEntry, "intro_enter_front_door", string.Empty, 1.2f, true, CutSceneCameraShot.InteriorSettle)
        };
    }

    private static CutScenePoint Point(string name, Transform transform, string dialogueId, string text, float duration, bool moveToPoint, CutSceneCameraShot shot)
    {
        return new CutScenePoint(name, transform, dialogueId, shot)
        {
            moveToPoint = moveToPoint,
            overrideText = text,
            overrideFallbackDuration = duration,
            waitAfter = 0.12f
        };
    }

    private static void ApplyLanternHud()
    {
        var gameUi = FindSceneTransform("GameUI");
        if (gameUi == null)
            return;

        var panel = FindSceneTransform("FlashlightPanel");
        if (panel == null)
        {
            var panelObject = new GameObject("FlashlightPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelObject.layer = LayerMask.NameToLayer("UI");
            panelObject.transform.SetParent(gameUi, false);
            panel = panelObject.transform;
        }

        for (var i = panel.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(panel.GetChild(i).gameObject);

        var panelRect = EnsureRect(panel);
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.zero;
        panelRect.pivot = Vector2.zero;
        panelRect.sizeDelta = new Vector2(132f, 164f);
        panelRect.anchoredPosition = new Vector2(24f, 22f);

        var panelImage = panel.GetComponent<Image>() ?? panel.gameObject.AddComponent<Image>();
        panelImage.color = Color.clear;
        panelImage.raycastTarget = false;

        var fill = AddImage(panel, "LanternFuelFill", AssetDatabase.LoadAssetAtPath<Sprite>(LanternFillPath), Image.Type.Filled);
        fill.fillMethod = Image.FillMethod.Vertical;
        fill.fillOrigin = (int)Image.OriginVertical.Bottom;
        fill.fillAmount = 0.78f;
        fill.preserveAspect = true;
        SetCentered(fill.rectTransform, new Vector2(120f, 148f), Vector2.zero);

        var frame = AddImage(panel, "LanternFrame", AssetDatabase.LoadAssetAtPath<Sprite>(LanternFramePath), Image.Type.Simple);
        frame.preserveAspect = true;
        SetCentered(frame.rectTransform, new Vector2(120f, 148f), Vector2.zero);
    }

    private static void WireLampFillImage()
    {
        var fillTransform = FindSceneTransform("LanternFuelFill");
        var fill = fillTransform != null ? fillTransform.GetComponent<Image>() : null;
        if (fill == null)
            return;

        foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (behaviour == null || behaviour.GetType().Name != "P2OilLamp")
                continue;

            var serialized = new SerializedObject(behaviour);
            Set(serialized, "oilFillImage", fill);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(behaviour);
        }
    }

    private static void RemoveP2WindowEntryMechanic()
    {
        DestroySceneObject("IntroWindowEntryCutSceneSequence");
        DestroySceneObject("IntroWindowCutsceneTrigger");

        foreach (var checkpointManager in Object.FindObjectsByType<FpsHorrorKit.ChapterOneCheckpointManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (checkpointManager != null)
                Object.DestroyImmediate(checkpointManager, true);
        }
    }

    private static void ApplyGhostPatrolRoutes()
    {
        var ghost = Object.FindFirstObjectByType<P2GhostController>(FindObjectsInactive.Include);
        if (ghost == null)
        {
            Debug.LogWarning("P2GhostController was not found in GameP2.");
            return;
        }

        var waypointRoot = FindSceneTransform("P2_GhostWaypoints");
        if (waypointRoot == null)
            waypointRoot = new GameObject("P2_GhostWaypoints").transform;

        var quietRoot = EnsureChild(waypointRoot, "QuietPatrolRoute");
        var awakenedRoot = EnsureChild(waypointRoot, "AwakenedPatrolRoute");

        MoveExistingWaypointChildren(waypointRoot, quietRoot, "Quiet_");
        MoveExistingWaypointChildren(waypointRoot, awakenedRoot, "Full_");

        var agent = ghost.GetComponent<NavMeshAgent>();
        if (agent == null)
            agent = ghost.gameObject.AddComponent<NavMeshAgent>();

        agent.radius = 0.35f;
        agent.height = 1.8f;
        agent.speed = 1.5f;
        agent.acceleration = 8f;
        agent.angularSpeed = 360f;
        agent.stoppingDistance = 0.1f;
        agent.autoBraking = true;
        agent.updateRotation = false;
        EditorUtility.SetDirty(agent);

        var fps = Object.FindFirstObjectByType<FpsHorrorKit.FpsController>(FindObjectsInactive.Include);
        var p2Player = Object.FindFirstObjectByType<P2FirstPersonController>(FindObjectsInactive.Include);
        var player = fps != null ? fps.transform : p2Player != null ? p2Player.transform : FindSceneTransform("Player");
        var serializedGhost = new SerializedObject(ghost);
        Set(serializedGhost, "player", player);
        Set(serializedGhost, "quietPatrolRoot", quietRoot);
        Set(serializedGhost, "awakenedPatrolRoot", awakenedRoot);
        Set(serializedGhost, "autoCollectWaypointsFromChildren", true);
        Set(serializedGhost, "pingPongPatrol", true);
        Set(serializedGhost, "agent", agent);
        Set(serializedGhost, "waypointReachDistance", 0.35f);
        Set(serializedGhost, "navMeshSampleRadius", 3f);
        SetTransformList(serializedGhost, "quietPatrolWaypoints", CollectDirectChildren(quietRoot));
        SetTransformList(serializedGhost, "awakenedWaypoints", CollectDirectChildren(awakenedRoot));
        serializedGhost.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ghost);
        EditorUtility.SetDirty(waypointRoot);
        EditorUtility.SetDirty(quietRoot);
        EditorUtility.SetDirty(awakenedRoot);
    }

    private static void MoveExistingWaypointChildren(Transform sourceRoot, Transform targetRoot, string namePrefix)
    {
        if (sourceRoot == null || targetRoot == null)
            return;

        var matches = new List<Transform>();
        for (var i = 0; i < sourceRoot.childCount; i++)
        {
            var child = sourceRoot.GetChild(i);
            if (child != null && child != targetRoot && child.name.StartsWith(namePrefix, System.StringComparison.OrdinalIgnoreCase))
                matches.Add(child);
        }

        foreach (var match in matches)
            match.SetParent(targetRoot, true);
    }

    private static void ApplyLanternFireFx()
    {
        var lantern = FindGameplayLantern();
        if (lantern == null)
        {
            Debug.LogWarning("Lantern_01_2k under FollowCamera was not found in GameP2.");
            return;
        }

        RemoveP2OilLampFromOtherLanterns(lantern);
        var oilLamp = ConfigureP2OilLamp(lantern, true);
        var flameLight = GetP2FlameLight(lantern);

        var fps = Object.FindFirstObjectByType<FpsHorrorKit.FpsController>(FindObjectsInactive.Include);
        if (fps != null)
        {
            var serializedFps = new SerializedObject(fps);
            Set(serializedFps, "flashlightLight", flameLight);
            Set(serializedFps, "driveFlashlightLightTransform", false);
            Set(serializedFps, "showFlashlightRay", false);
            serializedFps.ApplyModifiedPropertiesWithoutUndo();
            DisableLegacyFlashlightSpot(fps, flameLight);
            EditorUtility.SetDirty(fps);
        }

        var controller = Object.FindFirstObjectByType<P2GameController>(FindObjectsInactive.Include);
        if (controller != null)
        {
            var serializedController = new SerializedObject(controller);
            Set(serializedController, "oilLamp", oilLamp);
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }
    }

    private static void ApplyP2LanternPresentation()
    {
        var presentation = Object.FindFirstObjectByType<FpsHorrorKit.FirstPersonPresentationController>(FindObjectsInactive.Include);
        if (presentation == null)
            return;

        var serializedPresentation = new SerializedObject(presentation);
        var playerModelRoot = serializedPresentation.FindProperty("playerModelRoot")?.objectReferenceValue as GameObject;
        if (playerModelRoot == null)
            playerModelRoot = FindSceneTransform("PlayerNew")?.gameObject ?? presentation.gameObject;

        var firstPersonLantern = FindGameplayLantern();
        if (firstPersonLantern != null)
            Set(serializedPresentation, "firstPersonFlashlightViewModel", firstPersonLantern.gameObject);

        var legacyViewModel = FindSceneTransform("FirstPersonFlashlightViewModel");
        if (legacyViewModel != null && legacyViewModel != firstPersonLantern && !IsAncestorOf(legacyViewModel, firstPersonLantern))
        {
            legacyViewModel.gameObject.SetActive(false);
            EditorUtility.SetDirty(legacyViewModel.gameObject);
        }

        var rightHand = FindChildRecursive(playerModelRoot.transform, "mixamorig:RightHand");
        if (rightHand == null)
        {
            Debug.LogWarning("mixamorig:RightHand was not found under the player model, so cutscene lantern was parented to the player root.");
            rightHand = playerModelRoot.transform;
        }

        var cutsceneLantern = FindSceneTransform("Lantern_01_2kCutscene");
        if (cutsceneLantern == null)
            cutsceneLantern = FindSceneTransform("P2_CutsceneHeldLantern");
        if (cutsceneLantern == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LanternPrefabPath);
            var instance = prefab != null
                ? PrefabUtility.InstantiatePrefab(prefab, rightHand) as GameObject
                : new GameObject("Lantern_01_2kCutscene");

            cutsceneLantern = instance != null ? instance.transform : null;
        }

        if (cutsceneLantern == null)
            return;

        cutsceneLantern.name = "Lantern_01_2kCutscene";
        cutsceneLantern.SetParent(rightHand, false);
        cutsceneLantern.localPosition = new Vector3(0.266f, 0.097f, -0.075f);
        cutsceneLantern.localRotation = Quaternion.Euler(-35.535f, 4.645f, 107.675f);
        cutsceneLantern.localScale = Vector3.one;
        cutsceneLantern.gameObject.SetActive(false);

        DisableColliders(cutsceneLantern);
        ConfigureP2OilLamp(cutsceneLantern, false);

        Set(serializedPresentation, "cutsceneHeldLantern", cutsceneLantern.gameObject);
        serializedPresentation.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(presentation);
        EditorUtility.SetDirty(cutsceneLantern.gameObject);
    }

    private static P2OilLamp ConfigureP2OilLamp(Transform lantern, bool controlsGameplaySystems)
    {
        var fireFx = EnsureChild(lantern, "P2_Lantern_FireFX");
        fireFx.localPosition = new Vector3(0f, 0.096f, 0f);
        fireFx.localRotation = Quaternion.identity;
        fireFx.localScale = Vector3.one;

        var particleTransform = EnsureParticleChild(fireFx, "P2_Lantern_FlameParticle");
        particleTransform.localPosition = Vector3.zero;
        particleTransform.localRotation = Quaternion.identity;
        particleTransform.localScale = Vector3.one;

        var particles = particleTransform.GetComponent<ParticleSystem>();
        if (particles == null)
            particles = particleTransform.gameObject.AddComponent<ParticleSystem>();
        ConfigureLanternFlameParticles(particles);

        var particleRenderer = particleTransform.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer != null)
        {
            var flameMaterial = AssetDatabase.LoadAssetAtPath<Material>(FlameMaterialPath);
            if (flameMaterial != null)
                particleRenderer.sharedMaterial = flameMaterial;
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.alignment = ParticleSystemRenderSpace.View;
            particleRenderer.sortingFudge = 2f;
        }

        var lightTransform = EnsureChild(fireFx, "P2_Lantern_FlameLight");
        lightTransform.localPosition = new Vector3(0f, 0.012f, 0f);
        lightTransform.localRotation = Quaternion.identity;
        lightTransform.localScale = Vector3.one;

        var flameLight = lightTransform.GetComponent<Light>();
        if (flameLight == null)
            flameLight = lightTransform.gameObject.AddComponent<Light>();
        flameLight.type = LightType.Point;
        flameLight.color = new Color(1f, 0.48f, 0.16f);
        flameLight.intensity = controlsGameplaySystems ? 1.25f : 0.95f;
        flameLight.range = controlsGameplaySystems ? 2.4f : 2.1f;
        flameLight.shadows = LightShadows.None;
        flameLight.enabled = true;

        RecreateOilLampComponent(lantern);
        var oilLamp = lantern.gameObject.AddComponent<P2OilLamp>();
        var fillTransform = FindSceneTransform("LanternFuelFill");
        var fillImage = controlsGameplaySystems && fillTransform != null ? fillTransform.GetComponent<Image>() : null;
        var ghost = Object.FindFirstObjectByType<P2GhostController>(FindObjectsInactive.Include);

        var serializedLamp = new SerializedObject(oilLamp);
        Set(serializedLamp, "flameLight", flameLight);
        SetParticleList(serializedLamp, particles);
        Set(serializedLamp, "shakeRoot", lantern);
        Set(serializedLamp, "controlsGameplaySystems", controlsGameplaySystems);
        Set(serializedLamp, "oilFillImage", controlsGameplaySystems ? fillImage : (Object)null);
        Set(serializedLamp, "ghost", ghost);
        Set(serializedLamp, "flameBaseIntensity", controlsGameplaySystems ? 1.15f : 0.95f);
        Set(serializedLamp, "flamePulseIntensity", 0.45f);
        Set(serializedLamp, "flameBaseRange", controlsGameplaySystems ? 2.4f : 2.1f);
        Set(serializedLamp, "flameDangerRange", controlsGameplaySystems ? 1.8f : 1.6f);
        Set(serializedLamp, "nearGhostEffectStartDistance", 12f);
        Set(serializedLamp, "nearGhostFullEffectDistance", 2f);
        Set(serializedLamp, "dangerShakePosition", 0.045f);
        Set(serializedLamp, "dangerShakeRotation", 6f);
        Set(serializedLamp, "normalFlameColor", new Color(1f, 0.48f, 0.16f));
        Set(serializedLamp, "dangerFlameColor", new Color(0.35f, 0.85f, 1f));
        Set(serializedLamp, "debugNearGhostEffectZone", controlsGameplaySystems);
        serializedLamp.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(fireFx);
        EditorUtility.SetDirty(particles);
        EditorUtility.SetDirty(flameLight);
        EditorUtility.SetDirty(oilLamp);
        return oilLamp;
    }

    private static Light GetP2FlameLight(Transform lantern)
    {
        var fireFx = lantern != null ? lantern.Find("P2_Lantern_FireFX") : null;
        var lightTransform = fireFx != null ? fireFx.Find("P2_Lantern_FlameLight") : null;
        return lightTransform != null ? lightTransform.GetComponent<Light>() : null;
    }

    private static void DisableColliders(Transform root)
    {
        foreach (var collider in root.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
            EditorUtility.SetDirty(collider);
        }
    }

    private static void DisableLegacyFlashlightSpot(FpsHorrorKit.FpsController fps, Light replacementLight)
    {
        if (fps == null || fps.followTarget == null)
            return;

        var legacySpot = fps.followTarget.Find("Spot Light");
        if (legacySpot == null || legacySpot == replacementLight.transform)
            return;

        var legacyLight = legacySpot.GetComponent<Light>();
        if (legacyLight != null)
            legacyLight.enabled = false;

        legacySpot.gameObject.SetActive(false);
        EditorUtility.SetDirty(legacySpot.gameObject);
        if (legacyLight != null)
            EditorUtility.SetDirty(legacyLight);
    }

    private static Transform EnsureParticleChild(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child != null)
        {
            if (child.GetComponent<ParticleSystem>() == null)
                child.gameObject.AddComponent<ParticleSystem>();
            return child;
        }

        var obj = new GameObject(name, typeof(ParticleSystem));
        obj.transform.SetParent(parent, false);
        return obj.transform;
    }

    private static void ConfigureLanternFlameParticles(ParticleSystem particles)
    {
        if (particles == null)
        {
            Debug.LogWarning("Lantern flame ParticleSystem could not be created.");
            return;
        }

        var shouldPlay = particles.isPlaying || particles.main.playOnAwake;
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = particles.main;
        main.duration = 0.8f;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 64;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.48f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.24f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.085f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.86f, 0.25f, 0.95f),
            new Color(1f, 0.28f, 0.04f, 0.75f));

        var emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(18f, 28f);

        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 8f;
        shape.radius = 0.014f;
        shape.length = 0.025f;

        var colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.92f, 0.36f), 0f),
                new GradientColorKey(new Color(1f, 0.48f, 0.1f), 0.45f),
                new GradientColorKey(new Color(0.55f, 0.06f, 0.02f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.95f, 0.14f),
                new GradientAlphaKey(0.7f, 0.62f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.3f),
            new Keyframe(0.22f, 1f),
            new Keyframe(1f, 0.12f)));

        var noise = particles.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
        noise.frequency = 7f;
        noise.quality = ParticleSystemNoiseQuality.High;

        if (shouldPlay && !particles.isPlaying)
            particles.Play(true);
    }

    private static Image AddImage(Transform parent, string name, Sprite sprite, Image.Type type)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.layer = LayerMask.NameToLayer("UI");
        obj.transform.SetParent(parent, false);
        var image = obj.GetComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null ? type : Image.Type.Simple;
        image.color = Color.white;
        image.raycastTarget = false;
        return image;
    }

    private static void DestroySceneObject(string name)
    {
        var transform = FindSceneTransform(name);
        if (transform != null)
            Object.DestroyImmediate(transform.gameObject, true);
    }

    private static void WireFollowCamera(Transform followCamera, FpsHorrorKit.FpsController fps)
    {
        if (followCamera == null || fps == null)
            return;

        var virtualCamera = followCamera.GetComponent<CinemachineCamera>();
        if (virtualCamera == null)
            return;

        fps.virtualCamera = virtualCamera;
        virtualCamera.Target.TrackingTarget = fps.followTarget != null ? fps.followTarget : fps.transform;
        EditorUtility.SetDirty(fps);
        EditorUtility.SetDirty(virtualCamera);
    }

    private static CutSceneManager FindOrCreateCutSceneManager()
    {
        var existing = FindSceneTransform("CutSceneManager");
        if (existing != null)
            return existing.GetComponent<CutSceneManager>() ?? existing.gameObject.AddComponent<CutSceneManager>();

        var obj = new GameObject("CutSceneManager", typeof(CutSceneManager), typeof(AudioSource));
        return obj.GetComponent<CutSceneManager>();
    }

    private static Transform EnsurePoint(Transform parent, string name, Vector3 fallbackPosition, Vector3 forward)
    {
        var point = EnsureChild(parent, name);
        point.position = fallbackPosition;
        if (forward.sqrMagnitude > 0.001f)
            point.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        return point;
    }

    private static Transform EnsureChild(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child != null)
            return child;

        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        return obj.transform;
    }

    private static RectTransform EnsureRect(Transform transform)
    {
        return transform.GetComponent<RectTransform>() ?? transform.gameObject.AddComponent<RectTransform>();
    }

    private static void SetCentered(RectTransform rect, Vector2 size, Vector2 anchoredPosition)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        rect.localScale = Vector3.one;
    }

    private static Transform FindSceneTransform(string objectName)
    {
        foreach (var transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (transform != null && transform.name == objectName)
                return transform;
        }

        return null;
    }

    private static Transform FindGameplayLantern()
    {
        var followCamera = FindSceneTransform("FollowCamera");
        var lantern = FindChildRecursive(followCamera, "Lantern_01_2k");
        if (lantern != null)
            return lantern;

        foreach (var transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (transform == null || transform.name != "Lantern_01_2k")
                continue;

            var current = transform.parent;
            while (current != null)
            {
                if (current.name == "FollowCamera")
                    return transform;

                current = current.parent;
            }
        }

        return FindSceneTransform("Lantern_01_2k");
    }

    private static void RemoveP2OilLampFromOtherLanterns(Transform gameplayLantern)
    {
        foreach (var transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (transform == null || transform == gameplayLantern || transform.name != "Lantern_01_2k")
                continue;

            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
            foreach (var oilLamp in transform.GetComponents<P2OilLamp>())
                Object.DestroyImmediate(oilLamp, true);

            EditorUtility.SetDirty(transform.gameObject);
        }
    }

    private static void RecreateOilLampComponent(Transform lantern)
    {
        if (lantern == null)
            return;

        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(lantern.gameObject);
        foreach (var oilLamp in lantern.GetComponents<P2OilLamp>())
            Object.DestroyImmediate(oilLamp, true);

        EditorUtility.SetDirty(lantern.gameObject);
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

    private static bool IsAncestorOf(Transform possibleAncestor, Transform child)
    {
        if (possibleAncestor == null || child == null)
            return false;

        var current = child.parent;
        while (current != null)
        {
            if (current == possibleAncestor)
                return true;

            current = current.parent;
        }

        return false;
    }

    private static Transform[] CollectDirectChildren(Transform root)
    {
        if (root == null || root.childCount == 0)
            return System.Array.Empty<Transform>();

        var children = new Transform[root.childCount];
        for (var i = 0; i < root.childCount; i++)
            children[i] = root.GetChild(i);

        return children;
    }

    private static void SetSequenceList(SerializedObject serialized, params CutSceneSequence[] sequences)
    {
        var property = serialized.FindProperty("sequences");
        if (property == null)
            return;

        property.arraySize = sequences.Length;
        for (var i = 0; i < sequences.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = sequences[i];
    }

    private static void SetParticleList(SerializedObject serialized, params ParticleSystem[] particles)
    {
        var property = serialized.FindProperty("flameParticles");
        if (property == null)
            return;

        property.arraySize = particles.Length;
        for (var i = 0; i < particles.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = particles[i];
    }

    private static void SetTransformList(SerializedObject serialized, string propertyName, params Transform[] transforms)
    {
        var property = serialized.FindProperty(propertyName);
        if (property == null)
            return;

        property.arraySize = transforms.Length;
        for (var i = 0; i < transforms.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = transforms[i];
    }

    private static void Set(SerializedObject serialized, string propertyName, Object value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void Set(SerializedObject serialized, string propertyName, bool value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
    }

    private static void Set(SerializedObject serialized, string propertyName, float value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.floatValue = value;
    }

    private static void Set(SerializedObject serialized, string propertyName, Color value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.colorValue = value;
    }

    private static void Set(SerializedObject serialized, string propertyName, string value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.stringValue = value;
    }
}
