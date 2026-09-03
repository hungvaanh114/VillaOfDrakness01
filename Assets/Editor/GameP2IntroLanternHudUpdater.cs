using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class GameP2IntroLanternHudUpdater
{
    private const string ScenePath = "Assets/MainGame/GameP2.unity";
    private const string IntroCutSceneId = "intro";
    private const string LanternFramePath = "Assets/FpsHorrorKit/Png/UI/LanternOut.png";
    private const string LanternFillPath = "Assets/FpsHorrorKit/Png/UI/LanternIn_V2.png";

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
        WireLampFillImage();

        var controller = Object.FindFirstObjectByType<GameController>(FindObjectsInactive.Include);
        if (controller != null)
        {
            var serialized = new SerializedObject(controller);
            Set(serialized, "cutSceneManager", manager);
            Set(serialized, "playIntroOnStart", true);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("GameP2 front-door intro and lantern HUD applied.");
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

        DisableChild(manager.transform, "IntroWindowEntryCutSceneSequence");
        DisableChild(manager.transform, "IntroWindowCutsceneTrigger");

        WireFollowCamera(followCamera, fps);

        var serialized = new SerializedObject(manager);
        Set(serialized, "introCutSceneId", IntroCutSceneId);
        Set(serialized, "introWindowEntryCutSceneId", IntroCutSceneId);
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

    private static void DisableChild(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child != null)
            child.gameObject.SetActive(false);
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

    private static void SetSequenceList(SerializedObject serialized, params CutSceneSequence[] sequences)
    {
        var property = serialized.FindProperty("sequences");
        if (property == null)
            return;

        property.arraySize = sequences.Length;
        for (var i = 0; i < sequences.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = sequences[i];
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

    private static void Set(SerializedObject serialized, string propertyName, string value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.stringValue = value;
    }
}
