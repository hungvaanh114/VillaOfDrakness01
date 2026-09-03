using System.Collections.Generic;
using System.IO;
using MainGame.P2;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class GameP2SceneBuilder
{
    private const string ScenePath = "Assets/MainGame/GameP2.unity";
    private const string P2Root = "Assets/MainGame/P2";
    private const string MaterialRoot = P2Root + "/Materials";

    private static Material wallMaterial;
    private static Material floorMaterial;
    private static Material woodMaterial;
    private static Material darkWoodMaterial;
    private static Material redClothMaterial;
    private static Material mirrorMaterial;
    private static Material brokenMirrorMaterial;
    private static Material brassMaterial;
    private static Material candleMaterial;
    private static Material chalkMaterial;
    private static Material ghostMaterial;
    private static Material waterMaterial;
    private static Material glassShardMaterial;

    [MenuItem("MainGame/P2/Build GameP2 Scene")]
    public static void Build()
    {
        EnsureFolders();
        CreateMaterials();
        CopyChapterOneSceneAsBase();

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        DisableChapterOneRuntimeInCopy();

        var roots = new SceneRoots();
        roots.Environment = new GameObject("P2_StoryLayer_FromChapter1").transform;
        roots.Gameplay = new GameObject("P2_Gameplay").transform;
        roots.Waypoints = new GameObject("P2_GhostWaypoints").transform;
        roots.Audio = new GameObject("P2_Audio").transform;

        BuildInheritedSceneStoryGuides(roots);

        var player = UseChapterOnePlayer(roots.Gameplay);
        var ui = UseChapterOneHud();
        var audio = CreateAudio(roots.Audio);
        var ghostSetup = UseChapterOneGhost(roots.Waypoints, player.transform);
        var mirrorEvent = CreateInteractablesAndTriggers(roots, player.transform);
        var controller = CreateController(roots.Gameplay, player, ui, audio, ghostSetup.Ghost, mirrorEvent);

        ConfigureController(controller, player, ui, audio, ghostSetup.Ghost, mirrorEvent);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ConfigureBuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("GameP2 inherited scene built from Assets/MainGame/Game.unity at " + ScenePath);
    }

    private static void CopyChapterOneSceneAsBase()
    {
        var source = Path.GetFullPath("Assets/MainGame/Game.unity");
        var destination = Path.GetFullPath(ScenePath);
        if (!File.Exists(source))
            throw new FileNotFoundException("Cannot find Chapter 1 scene to inherit from.", source);

        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        File.Copy(source, destination, true);
        AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport);
    }

    private static void DisableChapterOneRuntimeInCopy()
    {
        var disabledTypes = new HashSet<string>
        {
            "ChapterOneCheckpointManager",
            "ChapterOneStoryFlow",
            "CutSceneManager",
            "IntroWindowCutsceneTrigger",
            "MirrorJumpscare",
            "MonsterAI",
            "OuttroCutSceneSequenceBinder",
            "WellEndingTrigger"
        };

        foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (behaviour == null)
                continue;

            var type = behaviour.GetType();
            if (type.Namespace == "MainGame.P2" || !disabledTypes.Contains(type.Name))
                continue;

            behaviour.enabled = false;
            EditorUtility.SetDirty(behaviour);
        }
        foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (behaviour == null || behaviour.GetType().Name != "GameController")
                continue;

            var serialized = new SerializedObject(behaviour);
            var playIntro = serialized.FindProperty("playIntroOnStart");
            if (playIntro != null)
                playIntro.boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(behaviour);
        }
    }

    private static void BuildInheritedSceneStoryGuides(SceneRoots roots)
    {
        roots.Environment.name = "P2_StoryLayer_FromChapter1_NoLightingOverride";
    }

    private static void BuildEnvironment(SceneRoots roots)
    {
        CreateFloor("Path_Ground", new Vector3(0f, -0.05f, -26f), new Vector3(5f, 0.1f, 18f), roots.Environment);
        CreateFloor("Lobby_Floor", new Vector3(0f, -0.05f, -13f), new Vector3(12f, 0.1f, 10f), roots.Environment);
        CreateFloor("Study_Floor", new Vector3(-10f, -0.05f, -12.5f), new Vector3(7f, 0.1f, 7f), roots.Environment);
        CreateFloor("Upper_Hall_Floor", new Vector3(4f, 3f, 1f), new Vector3(18f, 0.1f, 5f), roots.Environment);
        CreateFloor("Painting_Room_Floor", new Vector3(-6f, 3f, 5.5f), new Vector3(6f, 0.1f, 6f), roots.Environment);
        CreateFloor("Do_Office_Floor", new Vector3(2f, 3f, 7f), new Vector3(6f, 0.1f, 6f), roots.Environment);
        CreateFloor("BaLan_Room_Floor", new Vector3(9f, 3f, 6.5f), new Vector3(6f, 0.1f, 6f), roots.Environment);
        CreateFloor("Bath_Floor", new Vector3(14f, 3f, 0f), new Vector3(5.5f, 0.1f, 5f), roots.Environment);
        CreateFloor("Linh_Room_Floor", new Vector3(5f, 3f, -5.5f), new Vector3(8f, 0.1f, 7f), roots.Environment);
        CreateFloor("Escape_Shards_Floor", new Vector3(2f, -0.05f, 1f), new Vector3(5f, 0.1f, 10f), roots.Environment);
        CreateFloor("Backyard_Floor", new Vector3(0f, -0.05f, 13f), new Vector3(15f, 0.1f, 10f), roots.Environment);

        CreateWallRing("Lobby", Vector3.zero, new Rect(-6f, -18f, 12f, 10f), 0f, roots.Environment, openings: new[] { new Opening("north", -1.3f, 2.6f), new Opening("west", -12.5f, 3f), new Opening("south", 0f, 3f) });
        CreateWallRing("Study", Vector3.zero, new Rect(-13.5f, -16f, 7f, 7f), 0f, roots.Environment, openings: new[] { new Opening("east", -12.5f, 2.6f) });
        CreateWallRing("UpperHall", new Vector3(0f, 3f, 0f), new Rect(-5f, -1.5f, 18f, 5f), 0f, roots.Environment, openings: new[] { new Opening("south", 5f, 2.4f), new Opening("north", -6f, 2.4f), new Opening("north", 2f, 2.4f), new Opening("north", 9f, 2.4f), new Opening("east", 0f, 2.2f) });
        CreateWallRing("PaintingRoom", new Vector3(0f, 3f, 0f), new Rect(-9f, 2.5f, 6f, 6f), 0f, roots.Environment, openings: new[] { new Opening("south", -6f, 2.4f) });
        CreateWallRing("DoOffice", new Vector3(0f, 3f, 0f), new Rect(-1f, 4f, 6f, 6f), 0f, roots.Environment, openings: new[] { new Opening("south", 2f, 2.4f) });
        CreateWallRing("BaLanRoom", new Vector3(0f, 3f, 0f), new Rect(6f, 3.5f, 6f, 6f), 0f, roots.Environment, openings: new[] { new Opening("south", 9f, 2.4f) });
        CreateWallRing("Bath", new Vector3(0f, 3f, 0f), new Rect(11.25f, -2.5f, 5.5f, 5f), 0f, roots.Environment, openings: new[] { new Opening("west", 0f, 2.2f) });
        CreateWallRing("LinhRoom", new Vector3(0f, 3f, 0f), new Rect(1f, -9f, 8f, 7f), 0f, roots.Environment, openings: new[] { new Opening("north", 5f, 2.4f) });

        CreateRamp("Stair_Ramp_To_Floor1", new Vector3(5f, 1.45f, -5f), new Vector3(3f, 0.35f, 9f), new Vector3(-20f, 0f, 0f), roots.Environment);
        CreateWall("Backyard_Gate_Left", new Vector3(-4.5f, 1.1f, 7.5f), new Vector3(0.25f, 2.2f, 4.5f), roots.Environment);
        CreateWall("Backyard_Gate_Right", new Vector3(4.5f, 1.1f, 7.5f), new Vector3(0.25f, 2.2f, 4.5f), roots.Environment);

        CreateRoomLabel("TIỀN SẢNH", new Vector3(0f, 2.3f, -15.5f), roots.Environment);
        CreateRoomLabel("THƯ PHÒNG", new Vector3(-10f, 2.3f, -15.4f), roots.Environment);
        CreateRoomLabel("HÀNH LANG TẦNG MỘT", new Vector3(4f, 5.3f, -1.1f), roots.Environment);
        CreateRoomLabel("PHÒNG BÉ LINH", new Vector3(5f, 5.3f, -8.5f), roots.Environment);

        CreateLight("Moon_Key", new Vector3(-7f, 8f, 10f), new Vector3(52f, -35f, 0f), LightType.Directional, 1.7f, 12f, new Color(0.55f, 0.68f, 0.9f), roots.Environment);
        CreateLight("Lobby_Warm_Spill", new Vector3(0f, 2.7f, -12f), Vector3.zero, LightType.Point, 2.1f, 10f, new Color(1f, 0.62f, 0.32f), roots.Environment);
        CreateLight("Linh_Static_CandleGlow", new Vector3(2f, 4f, -7.4f), Vector3.zero, LightType.Point, 1.6f, 5.5f, new Color(1f, 0.58f, 0.25f), roots.Environment);
        CreateLight("Backyard_MoonPool", new Vector3(0f, 3f, 13f), Vector3.zero, LightType.Point, 1.2f, 8f, new Color(0.45f, 0.58f, 0.9f), roots.Environment);
    }

    private static P2FirstPersonController UseChapterOnePlayer(Transform parent)
    {
        var fpsController = FindBehaviourByTypeName("FpsController");
        var playerObject = fpsController != null ? fpsController.gameObject : null;
        var camera = FindBestGameplayCamera(playerObject);

        if (playerObject == null && camera != null)
            playerObject = camera.transform.root.gameObject;

        if (playerObject == null)
            return CreateFallbackPlayer(parent);

        var proxy = playerObject.GetComponent<P2FirstPersonController>();
        if (proxy == null)
            proxy = playerObject.AddComponent<P2FirstPersonController>();

        SetObject(proxy, "playerCamera", camera);
        SetObject(proxy, "characterController", playerObject.GetComponent<CharacterController>());
        SetBool(proxy, "driveInput", false);
        SetObjectArray(proxy, "externalMovementBehaviours", fpsController != null ? new Object[] { fpsController } : new Object[0]);
        SetBool(fpsController, "isCutScene", false);

        var interactor = playerObject.GetComponent<P2Interactor>();
        if (interactor == null)
            interactor = playerObject.AddComponent<P2Interactor>();
        SetObject(interactor, "sourceCamera", camera);

        return proxy;
    }

    private static P2FirstPersonController CreateFallbackPlayer(Transform parent)
    {
        var playerObject = new GameObject("P2_Player_BichNgoc");
        playerObject.transform.SetParent(parent);
        playerObject.transform.position = new Vector3(0f, 1.05f, -31f);
        playerObject.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

        var character = playerObject.AddComponent<CharacterController>();
        character.height = 1.72f;
        character.radius = 0.33f;
        character.center = new Vector3(0f, 0.86f, 0f);

        var cameraObject = new GameObject("P2_Camera");
        cameraObject.transform.SetParent(playerObject.transform);
        cameraObject.transform.localPosition = new Vector3(0f, 1.56f, 0f);
        var camera = cameraObject.AddComponent<Camera>();
        camera.nearClipPlane = 0.03f;
        camera.farClipPlane = 85f;
        camera.fieldOfView = 66f;
        cameraObject.AddComponent<AudioListener>();

        var lamp = new GameObject("OilLamp_Rig");
        lamp.transform.SetParent(cameraObject.transform);
        lamp.transform.localPosition = new Vector3(0.38f, -0.32f, 0.58f);
        lamp.transform.localRotation = Quaternion.Euler(8f, -16f, 0f);
        CreatePrimitive("Lamp_Handle", PrimitiveType.Cylinder, lamp.transform, new Vector3(0f, 0f, 0f), new Vector3(0.06f, 0.25f, 0.06f), brassMaterial);
        var flame = CreatePrimitive("Lamp_Flame", PrimitiveType.Sphere, lamp.transform, new Vector3(0f, 0.22f, 0f), new Vector3(0.08f, 0.14f, 0.08f), candleMaterial);
        var flameLight = lamp.AddComponent<Light>();
        flameLight.type = LightType.Point;
        flameLight.color = new Color(1f, 0.55f, 0.23f);
        flameLight.range = 7f;
        flameLight.intensity = 1.2f;
        var oilLamp = lamp.AddComponent<P2OilLamp>();
        SetObject(oilLamp, "flameLight", flameLight);
        SetObject(oilLamp, "flameRenderer", flame.GetComponent<Renderer>());

        var controller = playerObject.AddComponent<P2FirstPersonController>();
        SetObject(controller, "playerCamera", camera);
        SetObject(controller, "characterController", character);

        var interactor = playerObject.AddComponent<P2Interactor>();
        SetObject(interactor, "sourceCamera", camera);

        return controller;
    }

    private static HudRefs CreateHud(Transform parent)
    {
        var canvasObject = new GameObject("P2_HUD");
        canvasObject.transform.SetParent(parent);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObject.AddComponent<GraphicRaycaster>();
        var group = canvasObject.AddComponent<CanvasGroup>();

        var objective = CreateUiText("ObjectiveText", canvasObject.transform, new Vector2(32f, -28f), new Vector2(780f, 82f), TextAnchor.UpperLeft, 26f);
        objective.text = "Đi theo đường mòn tới cổng biệt thự.";

        var stage = CreateUiText("StageDebugText", canvasObject.transform, new Vector2(-32f, -28f), new Vector2(350f, 40f), TextAnchor.UpperRight, 18f);
        stage.text = "P2";

        var prompt = CreateUiText("InteractPrompt", canvasObject.transform, new Vector2(0f, 78f), new Vector2(720f, 48f), TextAnchor.MiddleCenter, 24f);
        prompt.text = string.Empty;

        var subtitle = CreateUiText("SubtitleText", canvasObject.transform, new Vector2(0f, 152f), new Vector2(1020f, 160f), TextAnchor.LowerCenter, 25f);
        subtitle.text = string.Empty;

        var deathCard = new GameObject("DeathCard");
        deathCard.transform.SetParent(canvasObject.transform, false);
        var cardImage = deathCard.AddComponent<Image>();
        cardImage.color = new Color(0.01f, 0.012f, 0.015f, 0.97f);
        var cardRect = deathCard.GetComponent<RectTransform>();
        cardRect.anchorMin = Vector2.zero;
        cardRect.anchorMax = Vector2.one;
        cardRect.offsetMin = Vector2.zero;
        cardRect.offsetMax = Vector2.zero;
        var deathText = CreateUiText("DeathCardText", deathCard.transform, Vector2.zero, new Vector2(1100f, 100f), TextAnchor.MiddleCenter, 42f);
        deathText.text = "NGUYỄN THỊ BÍCH NGỌC · 1951 - 1970";
        deathCard.SetActive(false);

        return new HudRefs
        {
            HudGroup = group,
            ObjectiveText = objective,
            PromptText = prompt,
            SubtitleText = subtitle,
            StageText = stage,
            DeathCard = deathCard,
            DeathCardText = deathText
        };
    }

    private static HudRefs UseChapterOneHud()
    {
        var canvas = FindMainCanvas();
        if (canvas == null)
        {
            var fallbackCanvas = new GameObject("Canvas");
            canvas = fallbackCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            fallbackCanvas.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            fallbackCanvas.AddComponent<GraphicRaycaster>();
        }

        canvas.enabled = true;
        canvas.gameObject.SetActive(true);

        var gameUi = FindSceneObject("GameUI");
        var groupHost = gameUi != null ? gameUi : canvas.gameObject;
        groupHost.SetActive(true);

        var group = groupHost.GetComponent<CanvasGroup>();
        if (group == null)
            group = groupHost.AddComponent<CanvasGroup>();

        var objective = FindSceneText("ObjectiveText") ?? CreateUiText("ObjectiveText", canvas.transform, new Vector2(32f, -28f), new Vector2(780f, 82f), TextAnchor.UpperLeft, 26f);
        objective.text = "Di theo duong mon toi cong biet thu.";

        var prompt = FindSceneText("InteractText") ?? FindSceneText("InteractPrompt") ?? CreateUiText("InteractText", canvas.transform, new Vector2(0f, 78f), new Vector2(720f, 48f), TextAnchor.MiddleCenter, 24f);
        prompt.text = string.Empty;

        var subtitle = CreateUiText("P2_SubtitleText", canvas.transform, new Vector2(0f, 152f), new Vector2(1020f, 160f), TextAnchor.LowerCenter, 25f);
        CopyTextStyle(objective, subtitle);
        subtitle.text = string.Empty;

        var deathCard = new GameObject("P2_DeathCard");
        deathCard.transform.SetParent(canvas.transform, false);
        var cardImage = deathCard.AddComponent<Image>();
        cardImage.color = new Color(0.01f, 0.012f, 0.015f, 0.97f);
        var cardRect = deathCard.GetComponent<RectTransform>();
        cardRect.anchorMin = Vector2.zero;
        cardRect.anchorMax = Vector2.one;
        cardRect.offsetMin = Vector2.zero;
        cardRect.offsetMax = Vector2.zero;
        var deathText = CreateUiText("P2_DeathCardText", deathCard.transform, Vector2.zero, new Vector2(1100f, 100f), TextAnchor.MiddleCenter, 42f);
        CopyTextStyle(objective, deathText);
        deathText.fontSize = 42f;
        deathText.text = "NGUYEN THI BICH NGOC - 1951 - 1970";
        deathCard.SetActive(false);

        return new HudRefs
        {
            HudGroup = group,
            ObjectiveText = objective,
            PromptText = prompt,
            SubtitleText = subtitle,
            StageText = null,
            DeathCard = deathCard,
            DeathCardText = deathText
        };
    }

    private static AudioRefs CreateAudio(Transform parent)
    {
        var voice = CreateAudioSource("P2_VoiceSource", parent, false, 1f);
        var sfx = CreateAudioSource("P2_SfxSource", parent, false, 0.85f);

        return new AudioRefs
        {
            VoiceSource = voice,
            SfxSource = sfx,
            AmbienceSource = null
        };
    }

    private static GhostSetup UseChapterOneGhost(Transform waypointParent, Transform player)
    {
        var oldGhost = FindSceneObject("MonsterPlaceholder");
        if (oldGhost == null)
            return CreateGhost(waypointParent, waypointParent, player);

        oldGhost.SetActive(true);
        var ghost = oldGhost.GetComponent<P2GhostController>();
        if (ghost == null)
            ghost = oldGhost.AddComponent<P2GhostController>();

        var quiet = new[]
        {
            Waypoint("Quiet_A", oldGhost.transform.position, waypointParent),
            Waypoint("Quiet_B", oldGhost.transform.position + new Vector3(2.5f, 0f, 0f), waypointParent),
            Waypoint("Quiet_C", oldGhost.transform.position + new Vector3(-2.5f, 0f, 1.8f), waypointParent)
        };
        var awakened = new[]
        {
            Waypoint("Full_A_OriginalGhost", oldGhost.transform.position, waypointParent),
            Waypoint("Full_B_PlayerRoute", player.position + new Vector3(4f, 0f, 2f), waypointParent),
            Waypoint("Full_C_PlayerRoute", player.position + new Vector3(-4f, 0f, 2f), waypointParent),
            Waypoint("Full_D_PlayerRoute", player.position + new Vector3(0f, 0f, 6f), waypointParent)
        };
        ghost.Configure(player, quiet, awakened);

        return new GhostSetup { Ghost = ghost, QuietWaypoints = quiet, FullWaypoints = awakened };
    }

    private static GhostSetup CreateGhost(Transform parent, Transform waypointParent, Transform player)
    {
        var ghostObject = new GameObject("MaVuDai_P2_Demo");
        ghostObject.transform.SetParent(parent);
        ghostObject.transform.position = new Vector3(6f, 3.8f, 1f);
        CreatePrimitive("Body", PrimitiveType.Capsule, ghostObject.transform, Vector3.zero, new Vector3(0.72f, 1.5f, 0.72f), ghostMaterial);
        CreatePrimitive("Hair", PrimitiveType.Cube, ghostObject.transform, new Vector3(0f, 0.75f, -0.1f), new Vector3(0.85f, 0.9f, 0.12f), darkWoodMaterial);
        var ghost = ghostObject.AddComponent<P2GhostController>();

        var quiet = new[]
        {
            Waypoint("Quiet_A", new Vector3(1f, 3.8f, 1f), waypointParent),
            Waypoint("Quiet_B", new Vector3(8f, 3.8f, 1f), waypointParent),
            Waypoint("Quiet_C", new Vector3(11f, 3.8f, -0.8f), waypointParent)
        };
        var awakened = new[]
        {
            Waypoint("Full_A_Lobby", new Vector3(0f, 0.8f, -12f), waypointParent),
            Waypoint("Full_B_Study", new Vector3(-10f, 0.8f, -12f), waypointParent),
            Waypoint("Full_C_Hall", new Vector3(5f, 3.8f, 1.2f), waypointParent),
            Waypoint("Full_D_BaLan", new Vector3(9f, 3.8f, 6.5f), waypointParent),
            Waypoint("Full_E_LinhDoor", new Vector3(5f, 3.8f, -2.6f), waypointParent),
            Waypoint("Full_F_Stair", new Vector3(4f, 0.8f, -1f), waypointParent)
        };
        ghost.Configure(player, quiet, awakened);

        return new GhostSetup { Ghost = ghost, QuietWaypoints = quiet, FullWaypoints = awakened };
    }

    private static MirrorEventRefs CreateInteractablesAndTriggers(SceneRoots roots, Transform player)
    {
        var refs = new MirrorEventRefs();

        var lobbyMirror = CreateMirror("Covered_Red_Mirror_Lobby", new Vector3(-3.7f, 1.25f, -9f), new Vector3(1.4f, 2.5f, 0.12f), roots.Gameplay, true);
        AddInteractable(lobbyMirror, P2InteractableKind.CoveredLobbyMirror, "Để nguyên gương phủ vải", true, false);

        var family = CreatePainting("Family_Portrait_Lobby", new Vector3(3.7f, 1.6f, -9f), roots.Gameplay, false);
        AddInteractable(family, P2InteractableKind.CoveredLobbyMirror, "Xem tranh gia đình", true, false);

        var desk = CreateFurnitureBlock("Study_Desk", new Vector3(-10f, 0.55f, -13.6f), new Vector3(2.2f, 0.8f, 1.1f), roots.Gameplay);
        var journal = CreatePrimitive("BaLan_Journal", PrimitiveType.Cube, roots.Gameplay, new Vector3(-10f, 1.03f, -13.6f), new Vector3(0.8f, 0.08f, 0.55f), chalkMaterial);
        AddInteractable(journal, P2InteractableKind.BaLanJournal, "Đọc nhật ký Bà Lan", true, true);

        var upsideDownPainting = CreatePainting("UpsideDown_FamilyPainting_FifthShadow", new Vector3(-6f, 4.35f, 8.25f), roots.Gameplay, true);
        AddInteractable(upsideDownPainting, P2InteractableKind.UpsideDownPainting, "Lật bức tranh úp", true, true);

        var officeDesk = CreateFurnitureBlock("Do_Office_Desk", new Vector3(2f, 3.55f, 7.4f), new Vector3(2f, 0.8f, 1.1f), roots.Gameplay);
        var key = CreatePrimitive("KEY_05_SmallKey", PrimitiveType.Cube, roots.Gameplay, new Vector3(2.3f, 4.02f, 7.4f), new Vector3(0.36f, 0.05f, 0.12f), brassMaterial);
        AddInteractable(key, P2InteractableKind.Key05, "Nhặt KEY_05", true, true);

        var cabinetContent = new GameObject("BaLan_Cabinet_OpenedContent");
        cabinetContent.transform.SetParent(roots.Gameplay);
        cabinetContent.SetActive(false);
        var recorder02 = CreatePrimitive("BL_LOG_02_MechanicalRecorder", PrimitiveType.Cylinder, cabinetContent.transform, new Vector3(0f, 0f, 0f), new Vector3(0.35f, 0.14f, 0.35f), brassMaterial);
        recorder02.transform.position = new Vector3(9.1f, 4.05f, 7.3f);
        AddInteractable(recorder02, P2InteractableKind.AudioLogBL02, "Nghe BL-LOG-02", true, false);

        var cabinet = CreateFurnitureBlock("BaLan_JewelryCabinet_LOCKED", new Vector3(9f, 3.65f, 7.3f), new Vector3(1.3f, 1.2f, 0.7f), roots.Gameplay);
        AddInteractable(cabinet, P2InteractableKind.JewelryCabinet, "Mở tủ trang sức bằng KEY_05", true, false, cabinetContent);

        var tub = CreatePrimitive("Bath_Tub_DarkWater", PrimitiveType.Cube, roots.Gameplay, new Vector3(14f, 3.35f, 0.2f), new Vector3(2f, 0.55f, 1f), wallMaterial);
        var water = CreatePrimitive("Still_Water_Surface", PrimitiveType.Cube, roots.Gameplay, new Vector3(14f, 3.66f, 0.2f), new Vector3(1.72f, 0.04f, 0.72f), waterMaterial);
        AddInteractable(water, P2InteractableKind.BathWater, "Bước qua mặt nước", true, true);

        var candle = CreatePrimitive("Linh_Candle_NoFlicker", PrimitiveType.Cylinder, roots.Gameplay, new Vector3(2.1f, 3.6f, -7.2f), new Vector3(0.13f, 0.45f, 0.13f), candleMaterial);
        AddInteractable(candle, P2InteractableKind.Candle, "Quan sát ngọn nến", true, false);

        var doll = CreatePrimitive("Linh_Doll_AudioLog", PrimitiveType.Capsule, roots.Gameplay, new Vector3(6.6f, 3.52f, -6.2f), new Vector3(0.32f, 0.62f, 0.32f), redClothMaterial);
        AddInteractable(doll, P2InteractableKind.Doll, "Nhặt búp bê", true, true);

        var chalk = CreateWorldText("ChalkNotes_E_C_F_D_G", "E  -  C  -  F  -  D  -  G", new Vector3(1.18f, 4.35f, -5.5f), Quaternion.Euler(0f, 90f, 0f), 0.55f, roots.Gameplay);
        AddInteractable(chalk.gameObject, P2InteractableKind.ChalkNotes, "Đọc nét phấn", true, false);

        var recorder03 = CreatePrimitive("BL_LOG_03_WindowRecorder", PrimitiveType.Cylinder, roots.Gameplay, new Vector3(2.2f, 3.25f, -3.1f), new Vector3(0.28f, 0.12f, 0.28f), brassMaterial);
        AddInteractable(recorder03, P2InteractableKind.AudioLogBL03, "Nghe BL-LOG-03", true, true);

        refs.HiddenCavity = new GameObject("HiddenWallCavity_SilverMirror");
        refs.HiddenCavity.transform.SetParent(roots.Gameplay);
        refs.HiddenCavity.SetActive(false);
        CreatePrimitive("Cavity_Back", PrimitiveType.Cube, refs.HiddenCavity.transform, new Vector3(1.05f, 4.05f, -4.6f), new Vector3(0.08f, 1.1f, 1.2f), darkWoodMaterial);
        var silverMirror = CreateMirror("SilverMirror_Pickup", new Vector3(1.0f, 4.05f, -4.6f), new Vector3(0.08f, 0.7f, 0.45f), refs.HiddenCavity.transform, false);
        AddInteractable(silverMirror, P2InteractableKind.SilverMirror, "Lấy tấm gương bạc", true, true);

        var wallPuzzleRoot = new GameObject("LinhRoom_WestWallKnockPuzzle");
        wallPuzzleRoot.transform.SetParent(roots.Gameplay);
        var wallPanels = new List<P2Interactable>();
        for (var i = 0; i < 5; i++)
        {
            var panel = CreatePrimitive("WestWall_KnockPanel_" + (i + 1), PrimitiveType.Cube, wallPuzzleRoot.transform, new Vector3(1.08f, 3.68f, -7.4f + i * 0.9f), new Vector3(0.14f, 1.1f, 0.72f), woodMaterial);
            wallPanels.Add(AddInteractable(panel, P2InteractableKind.WallPanel, "Gõ ô tường " + (i + 1), true, false));
        }
        var puzzle = wallPuzzleRoot.AddComponent<P2WallKnockPuzzle>();
        puzzle.Configure(3, refs.HiddenCavity, wallPanels.ToArray());

        refs.SilverMirrorProp = CreateMirror("Backyard_SilverMirror_Prop", new Vector3(0f, 0.6f, 14f), new Vector3(0.08f, 0.9f, 0.55f), roots.Gameplay, false).transform;
        AddInteractable(refs.SilverMirrorProp.gameObject, P2InteractableKind.BackyardMoonMirror, "Nâng gương soi trăng", false, false);
        refs.DeathPullTarget = new GameObject("DeathPullTarget_MirrorPlane").transform;
        refs.DeathPullTarget.SetParent(roots.Gameplay);
        refs.DeathPullTarget.position = new Vector3(0f, 0.9f, 14.7f);

        CreateShardField(roots.Gameplay);
        CreateMirrorBreakables(roots.Gameplay);
        CreateTriggers(roots.Gameplay);

        return refs;
    }

    private static P2GameController CreateController(Transform parent, P2FirstPersonController player, HudRefs ui, AudioRefs audio, P2GhostController ghost, MirrorEventRefs refs)
    {
        var controllerObject = new GameObject("P2_GameController");
        controllerObject.transform.SetParent(parent);
        var controller = controllerObject.AddComponent<P2GameController>();

        var interactor = player.GetComponent<P2Interactor>();
        SetObject(interactor, "controller", controller);
        var lamp = player.GetComponentInChildren<P2OilLamp>();
        SetObject(lamp, "ghost", ghost);

        return controller;
    }

    private static void ConfigureController(P2GameController controller, P2FirstPersonController player, HudRefs ui, AudioRefs audio, P2GhostController ghost, MirrorEventRefs refs)
    {
        SetObject(controller, "player", player);
        SetObject(controller, "oilLamp", player.GetComponentInChildren<P2OilLamp>());
        SetObject(controller, "ghost", ghost);
        SetObject(controller, "deathPullTarget", refs.DeathPullTarget);
        SetObject(controller, "silverMirrorProp", refs.SilverMirrorProp);
        SetObject(controller, "hiddenWallCavity", refs.HiddenCavity);
        SetObject(controller, "hudGroup", ui.HudGroup);
        SetObject(controller, "objectiveText", ui.ObjectiveText);
        SetObject(controller, "promptText", ui.PromptText);
        SetObject(controller, "subtitleText", ui.SubtitleText);
        SetObject(controller, "stageText", ui.StageText);
        SetObject(controller, "deathCard", ui.DeathCard);
        SetObject(controller, "deathCardText", ui.DeathCardText);
        SetObject(controller, "voiceSource", audio.VoiceSource);
        SetObject(controller, "sfxSource", audio.SfxSource);
        SetObject(controller, "ambienceSource", audio.AmbienceSource);

        SetObject(controller, "ngocIntro01", Clip("Assets/MainGame/Audio/Phan 2/ngọc 1_1.mp3"));
        SetObject(controller, "ngocIntro02", Clip("Assets/MainGame/Audio/Phan 2/ngọc 2_1.mp3"));
        SetObject(controller, "linhHallAutoLog", Clip("Assets/MainGame/Audio/Phan 2/linh 1_1.mp3"));
        SetObject(controller, "ngocAfterJournal", Clip("Assets/MainGame/Audio/Phan 2/ngọc 3_1.mp3"));
        SetObject(controller, "ngocBathWarning", Clip("Assets/MainGame/Audio/Phan 2/ngọc 4_1.mp3"));
        SetObject(controller, "linhDollLog", Clip("Assets/MainGame/Audio/Phan 2/linh 2_1.mp3"));
        SetObject(controller, "ngocCandleReaction", Clip("Assets/MainGame/Audio/Phan 2/ngọc 5_1.mp3"));
        SetObject(controller, "ngocChalkReaction", Clip("Assets/MainGame/Audio/Phan 2/ngọc 6_1.mp3"));
        SetObject(controller, "ngocHollowWall", Clip("Assets/MainGame/Audio/Phan 2/ngọc 7_1.mp3"));
        SetObject(controller, "ngocMirrorFound", Clip("Assets/MainGame/Audio/Phan 2/ngọc 8_1.mp3"));
        SetObject(controller, "ngocMirrorBreak", Clip("Assets/MainGame/Audio/Phan 2/ngọc 9_1.mp3"));
        SetObject(controller, "maVuDaiLine01", Clip("Assets/MainGame/Audio/Phan 2/ma 1_1.mp3"));
        SetObject(controller, "maVuDaiLine02", Clip("Assets/MainGame/Audio/Phan 2/ma 2_1.mp3"));
        SetObject(controller, "maDaMirror01", Clip("Assets/MainGame/Audio/Phan 2/ma da 2_1.mp3"));
        SetObject(controller, "maDaMirror02", Clip("Assets/MainGame/Audio/Phan 2/ma da 3_1.mp3"));
        SetObject(controller, "ngocFinalLine", Clip("Assets/MainGame/Audio/Phan 2/ngọc 10_1.mp3"));
        SetObject(controller, "audioLogBL02", Clip("Assets/MainGame/Audio/Phan 2/Mấy đêm nay tôi-197da84be4_a84be4.mp3"));
        SetObject(controller, "audioLogBL03", Clip("Assets/MainGame/Audio/Phan 2/Đây_là_chỗ_duy_nhất_3201f8dbc67d.mp3"));

        SetObject(controller, "knockSolidClip", Clip("Assets/MainGame/Audio/SFX/SFX_Interact_Generic_01.wav"));
        SetObject(controller, "knockHollowClip", Clip("Assets/MainGame/Audio/SFX/SFX_Door_Unlock.mp3"));
        SetObject(controller, "glassBreakClip", Clip("Assets/MainGame/Audio/SFX/SFX_Ghost_Jumpscare_Scream_01.wav"));
        SetObject(controller, "glassStepClip", Clip("Assets/MainGame/Audio/FootSteps/Footstep_Wood_01.wav"));
        SetObject(controller, "cabinetUnlockClip", Clip("Assets/MainGame/Audio/SFX/SFX_ItemLock_Unlock.wav"));
        SetObject(controller, "pickupClip", Clip("Assets/MainGame/Audio/SFX/SFX_KeyPickup.mp3"));
    }

    private static void CreateMirrorBreakables(Transform parent)
    {
        var mirrors = new[]
        {
            CreateMirror("BreakableMirror_Hall_A", new Vector3(1f, 4.3f, 3.4f), new Vector3(1f, 1.8f, 0.08f), parent, false),
            CreateMirror("BreakableMirror_BaLan", new Vector3(11.8f, 4.25f, 6.3f), new Vector3(0.08f, 1.9f, 1.05f), parent, false),
            CreateMirror("BreakableMirror_DressingHint", new Vector3(7.2f, 4.3f, 3.6f), new Vector3(1f, 1.9f, 0.08f), parent, false)
        };

        foreach (var mirror in mirrors)
        {
            var shardCluster = new GameObject(mirror.name + "_Shards");
            shardCluster.transform.SetParent(parent);
            shardCluster.transform.position = mirror.transform.position + Vector3.down * 1.2f;
            for (var i = 0; i < 5; i++)
            {
                var shard = CreatePrimitive("Shard_" + i, PrimitiveType.Cube, shardCluster.transform, RandomOffset(i), new Vector3(0.28f, 0.025f, 0.08f), glassShardMaterial);
                shard.transform.localRotation = Quaternion.Euler(0f, i * 31f, 0f);
            }

            mirror.AddComponent<P2MirrorBreakable>().Configure(mirror.GetComponent<Renderer>(), brokenMirrorMaterial, shardCluster);
        }
    }

    private static void CreateShardField(Transform parent)
    {
        var field = new GameObject("EscapeRoute_GlassShardNoiseField");
        field.transform.SetParent(parent);
        field.transform.position = new Vector3(2f, 0.45f, 2f);
        var collider = field.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(5f, 1f, 8f);
        field.AddComponent<P2GlassShardField>();

        for (var i = 0; i < 18; i++)
        {
            var x = -0.2f + (i % 6) * 0.8f;
            var z = -1.5f + (i / 6) * 1.2f;
            var shard = CreatePrimitive("Visible_GlassShard_" + i, PrimitiveType.Cube, parent, new Vector3(x, 0.06f, z), new Vector3(0.4f, 0.025f, 0.09f), glassShardMaterial);
            shard.transform.rotation = Quaternion.Euler(0f, i * 23f, 0f);
        }
    }

    private static void CreateTriggers(Transform parent)
    {
        CreateTrigger("Trigger_EnterLobby", P2TriggerKind.EnterLobby, new Vector3(0f, 0.8f, -18.3f), new Vector3(7f, 2f, 0.6f), parent);
        CreateTrigger("Trigger_HallAutoLog", P2TriggerKind.HallAutoLog, new Vector3(4f, 3.8f, -1.2f), new Vector3(11f, 2f, 1f), parent);
        CreateTrigger("Trigger_EnterLinhRoom", P2TriggerKind.EnterLinhRoom, new Vector3(5f, 3.8f, -2.8f), new Vector3(4f, 2f, 0.8f), parent);
        CreateTrigger("Trigger_EscapeShardRoute", P2TriggerKind.EscapeShardRoute, new Vector3(2f, 0.8f, 1f), new Vector3(5f, 2f, 7f), parent);
        CreateTrigger("Trigger_BackyardDeath", P2TriggerKind.BackyardDeath, new Vector3(0f, 0.8f, 13f), new Vector3(9f, 2f, 5f), parent);
    }

    private static void ConfigureBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        for (var i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].path == ScenePath)
            {
                scenes[i] = CreateBuildSettingsScene(ScenePath);
                EditorBuildSettings.scenes = scenes.ToArray();
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(AssetDatabase.AssetPathToGUID(ScenePath)))
            return;

        scenes.Add(CreateBuildSettingsScene(ScenePath));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static EditorBuildSettingsScene CreateBuildSettingsScene(string path)
    {
        var scene = new EditorBuildSettingsScene(path, true);
        var guid = AssetDatabase.AssetPathToGUID(path);
        if (!string.IsNullOrWhiteSpace(guid))
            scene.guid = new GUID(guid);
        return scene;
    }

    private static P2Interactable AddInteractable(GameObject target, P2InteractableKind kind, string prompt, bool needsLight, bool oneShot, GameObject linked = null)
    {
        if (target.GetComponent<Collider>() == null)
        {
            var box = target.AddComponent<BoxCollider>();
            box.size = Vector3.one;
        }
        var interactable = target.AddComponent<P2Interactable>();
        interactable.Configure(kind, prompt, needsLight, oneShot, linked);
        return interactable;
    }

    private static GameObject CreateMirror(string name, Vector3 position, Vector3 scale, Transform parent, bool covered)
    {
        var mirror = CreatePrimitive(name, PrimitiveType.Cube, parent, position, scale, covered ? redClothMaterial : mirrorMaterial);
        if (!covered)
        {
            var frame = CreatePrimitive(name + "_Frame", PrimitiveType.Cube, parent, position + Vector3.down * 0.02f, scale + new Vector3(0.12f, 0.12f, 0.05f), darkWoodMaterial);
            frame.transform.SetSiblingIndex(mirror.transform.GetSiblingIndex());
        }
        return mirror;
    }

    private static GameObject CreatePainting(string name, Vector3 position, Transform parent, bool upsideDown)
    {
        var painting = CreatePrimitive(name, PrimitiveType.Cube, parent, position, new Vector3(1.35f, 0.08f, 1f), woodMaterial);
        painting.transform.rotation = Quaternion.Euler(upsideDown ? 180f : 0f, 0f, 0f);
        CreatePrimitive(name + "_ShadowFigure", PrimitiveType.Cube, painting.transform, new Vector3(0.23f, 0.08f, 0.08f), new Vector3(0.16f, 0.08f, 0.5f), ghostMaterial);
        return painting;
    }

    private static GameObject CreateFurnitureBlock(string name, Vector3 position, Vector3 scale, Transform parent)
    {
        var obj = CreatePrimitive(name, PrimitiveType.Cube, parent, position, scale, darkWoodMaterial);
        CreatePrimitive(name + "_Top", PrimitiveType.Cube, parent, position + Vector3.up * (scale.y * 0.52f), new Vector3(scale.x * 1.08f, 0.08f, scale.z * 1.08f), woodMaterial);
        return obj;
    }

    private static TMP_Text CreateWorldText(string name, string text, Vector3 position, Quaternion rotation, float size, Transform parent)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent);
        obj.transform.position = position;
        obj.transform.rotation = rotation;
        var tmp = obj.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.9f, 0.88f, 0.72f);
        var box = obj.AddComponent<BoxCollider>();
        box.size = new Vector3(4f, 1f, 0.15f);
        return tmp;
    }

    private static TMP_Text CreateUiText(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, TextAnchor anchor, float fontSize)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.color = new Color(0.92f, 0.9f, 0.82f);
        tmp.alignment = anchor switch
        {
            TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
            TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
            TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
            TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
            _ => TextAlignmentOptions.Left
        };
        tmp.textWrappingMode = TextWrappingModes.Normal;

        var rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        if (anchor == TextAnchor.UpperLeft)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
        }
        else if (anchor == TextAnchor.UpperRight)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
        }
        else if (anchor == TextAnchor.LowerCenter)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
        }
        else
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        return tmp;
    }

    private static Transform Waypoint(string name, Vector3 position, Transform parent)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent);
        obj.transform.position = position;
        return obj.transform;
    }

    private static void CreateTrigger(string name, P2TriggerKind kind, Vector3 position, Vector3 size, Transform parent)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent);
        obj.transform.position = position;
        var collider = obj.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = size;
        obj.AddComponent<P2StageTrigger>().Configure(kind);
    }

    private static GameObject CreateFloor(string name, Vector3 position, Vector3 scale, Transform parent)
    {
        return CreatePrimitive(name, PrimitiveType.Cube, parent, position, scale, floorMaterial);
    }

    private static GameObject CreateWall(string name, Vector3 position, Vector3 scale, Transform parent)
    {
        return CreatePrimitive(name, PrimitiveType.Cube, parent, position, scale, wallMaterial);
    }

    private static GameObject CreateRamp(string name, Vector3 position, Vector3 scale, Vector3 euler, Transform parent)
    {
        var ramp = CreatePrimitive(name, PrimitiveType.Cube, parent, position, scale, woodMaterial);
        ramp.transform.rotation = Quaternion.Euler(euler);
        return ramp;
    }

    private static void CreateWallRing(string prefix, Vector3 yOffset, Rect rect, float y, Transform parent, Opening[] openings)
    {
        var height = 2.8f;
        var thickness = 0.18f;
        var centerY = yOffset.y + y + height * 0.5f;
        var xMin = rect.xMin;
        var xMax = rect.xMax;
        var zMin = rect.yMin;
        var zMax = rect.yMax;
        var xCenter = (xMin + xMax) * 0.5f;
        var zCenter = (zMin + zMax) * 0.5f;

        CreateSegmentedWall(prefix + "_NorthWall", "north", xMin, xMax, zMax, centerY, thickness, height, parent, openings);
        CreateSegmentedWall(prefix + "_SouthWall", "south", xMin, xMax, zMin, centerY, thickness, height, parent, openings);
        CreateSegmentedWall(prefix + "_EastWall", "east", zMin, zMax, xMax, centerY, thickness, height, parent, openings);
        CreateSegmentedWall(prefix + "_WestWall", "west", zMin, zMax, xMin, centerY, thickness, height, parent, openings);

        CreatePrimitive(prefix + "_CeilingHint", PrimitiveType.Cube, parent, new Vector3(xCenter, yOffset.y + y + height + 0.08f, zCenter), new Vector3(rect.width, 0.08f, rect.height), wallMaterial);
    }

    private static void CreateSegmentedWall(string name, string side, float min, float max, float fixedAxis, float y, float thickness, float height, Transform parent, Opening[] openings)
    {
        var opening = FindOpening(openings, side);
        if (opening.Width <= 0f)
        {
            if (side == "north" || side == "south")
                CreateWall(name, new Vector3((min + max) * 0.5f, y, fixedAxis), new Vector3(max - min, height, thickness), parent);
            else
                CreateWall(name, new Vector3(fixedAxis, y, (min + max) * 0.5f), new Vector3(thickness, height, max - min), parent);
            return;
        }

        var aMin = min;
        var aMax = opening.Center - opening.Width * 0.5f;
        var bMin = opening.Center + opening.Width * 0.5f;
        var bMax = max;
        if (aMax > aMin)
            CreateSegment(name + "_A", side, aMin, aMax, fixedAxis, y, thickness, height, parent);
        if (bMax > bMin)
            CreateSegment(name + "_B", side, bMin, bMax, fixedAxis, y, thickness, height, parent);
    }

    private static void CreateSegment(string name, string side, float min, float max, float fixedAxis, float y, float thickness, float height, Transform parent)
    {
        if (side == "north" || side == "south")
            CreateWall(name, new Vector3((min + max) * 0.5f, y, fixedAxis), new Vector3(max - min, height, thickness), parent);
        else
            CreateWall(name, new Vector3(fixedAxis, y, (min + max) * 0.5f), new Vector3(thickness, height, max - min), parent);
    }

    private static Opening FindOpening(Opening[] openings, string side)
    {
        if (openings == null)
            return default;

        foreach (var opening in openings)
        {
            if (opening.Side == side)
                return opening;
        }

        return default;
    }

    private static GameObject CreatePrimitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.SetParent(parent);
        obj.transform.position = position;
        obj.transform.localScale = scale;
        if (material != null && obj.TryGetComponent(out Renderer renderer))
            renderer.sharedMaterial = material;
        return obj;
    }

    private static void CreateRoomLabel(string label, Vector3 position, Transform parent)
    {
        var text = CreateWorldText("Label_" + label, label, position, Quaternion.Euler(0f, 0f, 0f), 0.45f, parent);
        text.color = new Color(0.75f, 0.7f, 0.58f, 0.75f);
    }

    private static Light CreateLight(string name, Vector3 position, Vector3 euler, LightType type, float intensity, float range, Color color, Transform parent)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent);
        obj.transform.position = position;
        obj.transform.rotation = Quaternion.Euler(euler);
        var light = obj.AddComponent<Light>();
        light.type = type;
        light.intensity = intensity;
        light.range = range;
        light.color = color;
        return light;
    }

    private static AudioSource CreateAudioSource(string name, Transform parent, bool loop, float volume)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent);
        var source = obj.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.volume = volume;
        return source;
    }

    private static Vector3 RandomOffset(int i)
    {
        return new Vector3((i % 3 - 1) * 0.25f, 0f, (i / 3) * 0.18f);
    }

    private static AudioClip Clip(string path)
    {
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    private static MonoBehaviour FindBehaviourByTypeName(string typeName)
    {
        foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (behaviour != null && behaviour.GetType().Name == typeName)
                return behaviour;
        }

        return null;
    }

    private static Camera FindBestGameplayCamera(GameObject playerObject)
    {
        if (playerObject != null)
        {
            var playerCamera = playerObject.GetComponentInChildren<Camera>(true);
            if (playerCamera != null)
                return playerCamera;
        }

        foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (camera != null && camera.gameObject.activeInHierarchy && camera.enabled)
                return camera;
        }

        foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (camera != null && camera.name == "Main Camera_1")
                return camera;
        }

        return Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
    }

    private static Canvas FindMainCanvas()
    {
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas != null && canvas.name == "Canvas")
                return canvas;
        }

        return Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
    }

    private static GameObject FindSceneObject(string objectName)
    {
        foreach (var transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (transform != null && transform.name == objectName)
                return transform.gameObject;
        }

        return null;
    }

    private static TMP_Text FindSceneText(string objectName)
    {
        foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (text != null && text.name == objectName)
                return text;
        }

        return null;
    }

    private static void CopyTextStyle(TMP_Text source, TMP_Text target)
    {
        if (source == null || target == null)
            return;

        target.font = source.font;
        target.fontSharedMaterial = source.fontSharedMaterial;
        target.color = source.color;
        target.fontStyle = source.fontStyle;
    }

    private static void SetObject(Object target, string property, Object value)
    {
        if (target == null)
            return;

        var serialized = new SerializedObject(target);
        var prop = serialized.FindProperty(property);
        if (prop == null)
        {
            Debug.LogWarning($"Missing serialized property {property} on {target.name}");
            return;
        }

        prop.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBool(Object target, string property, bool value)
    {
        if (target == null)
            return;

        var serialized = new SerializedObject(target);
        var prop = serialized.FindProperty(property);
        if (prop == null)
        {
            Debug.LogWarning($"Missing serialized property {property} on {target.name}");
            return;
        }

        prop.boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetObjectArray(Object target, string property, Object[] values)
    {
        if (target == null)
            return;

        var serialized = new SerializedObject(target);
        var prop = serialized.FindProperty(property);
        if (prop == null || !prop.isArray)
        {
            Debug.LogWarning($"Missing serialized array property {property} on {target.name}");
            return;
        }

        prop.arraySize = values != null ? values.Length : 0;
        for (var i = 0; i < prop.arraySize; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/MainGame", "P2");
        EnsureFolder(P2Root, "Materials");
    }

    private static void EnsureFolder(string parent, string folder)
    {
        var path = parent + "/" + folder;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, folder);
    }

    private static void CreateMaterials()
    {
        wallMaterial = EnsureMaterial("P2_Wall_DustyGrey", new Color(0.38f, 0.37f, 0.34f));
        floorMaterial = EnsureMaterial("P2_Floor_DarkStone", new Color(0.18f, 0.18f, 0.17f));
        woodMaterial = EnsureMaterial("P2_Wood_1970", new Color(0.38f, 0.23f, 0.12f));
        darkWoodMaterial = EnsureMaterial("P2_DarkWood", new Color(0.13f, 0.08f, 0.05f));
        redClothMaterial = EnsureMaterial("P2_RedMirrorCloth", new Color(0.46f, 0.03f, 0.025f));
        mirrorMaterial = EnsureMaterial("P2_Mirror_DimSilver", new Color(0.55f, 0.68f, 0.72f));
        brokenMirrorMaterial = EnsureMaterial("P2_Mirror_Cracked", new Color(0.12f, 0.2f, 0.25f));
        brassMaterial = EnsureMaterial("P2_Brass_Old", new Color(0.78f, 0.54f, 0.22f));
        candleMaterial = EnsureMaterial("P2_CandleFlame", new Color(1f, 0.58f, 0.2f));
        chalkMaterial = EnsureMaterial("P2_ChalkPaper", new Color(0.82f, 0.78f, 0.62f));
        ghostMaterial = EnsureMaterial("P2_Ghost_MaVuDai", new Color(0.18f, 0.2f, 0.23f, 0.84f));
        waterMaterial = EnsureMaterial("P2_StillWater_Black", new Color(0.02f, 0.04f, 0.05f));
        glassShardMaterial = EnsureMaterial("P2_GlassShard", new Color(0.48f, 0.72f, 0.82f, 0.72f));
    }

    private static Material EnsureMaterial(string name, Color color)
    {
        var path = MaterialRoot + "/" + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
            return existing;

        var shader = Shader.Find("HDRP/Lit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = new Material(shader) { name = name, color = color };
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private struct SceneRoots
    {
        public Transform Environment;
        public Transform Gameplay;
        public Transform Waypoints;
        public Transform Audio;
    }

    private struct Opening
    {
        public Opening(string side, float center, float width)
        {
            Side = side;
            Center = center;
            Width = width;
        }

        public string Side;
        public float Center;
        public float Width;
    }

    private sealed class HudRefs
    {
        public CanvasGroup HudGroup;
        public TMP_Text ObjectiveText;
        public TMP_Text PromptText;
        public TMP_Text SubtitleText;
        public TMP_Text StageText;
        public GameObject DeathCard;
        public TMP_Text DeathCardText;
    }

    private sealed class AudioRefs
    {
        public AudioSource VoiceSource;
        public AudioSource SfxSource;
        public AudioSource AmbienceSource;
    }

    private sealed class GhostSetup
    {
        public P2GhostController Ghost;
        public Transform[] QuietWaypoints;
        public Transform[] FullWaypoints;
    }

    private sealed class MirrorEventRefs
    {
        public Transform DeathPullTarget;
        public Transform SilverMirrorProp;
        public GameObject HiddenCavity;
    }
}
