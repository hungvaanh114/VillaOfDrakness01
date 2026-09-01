using System.Collections.Generic;
using System.IO;
using System.Linq;
using FpsHorrorKit;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class EndingP2SceneBuilder
{
    private const string AutoInstallSessionKey = "VillaOfDarkness.EndingP2SceneBuilder.AutoInstallDone";
    private const string MainScenePath = "Assets/MainGame/Game.unity";
    private const string TransitionScenePath = "Assets/MainGame/EndingP2Transition.unity";
    private const string CreditsScenePath = "Assets/MainGame/Credits.unity";
    private const string UpperFloorBlockerName = "UpperFloorBlocker_AfterGramophone";
    private const string StairMonsterPointName = "Vị trí quái ở cầu thang cutscene";

    [InitializeOnLoadMethod]
    private static void AutoInstallOnceAfterCompile()
    {
        if (SessionState.GetBool(AutoInstallSessionKey, false))
            return;
        if (!NeedsInstall())
            return;

        SessionState.SetBool(AutoInstallSessionKey, true);
        EditorApplication.delayCall += Install;
    }

    [MenuItem("MainGame/Install Ending P2 Scenes And Upper Floor Blocker")]
    public static void Install()
    {
        CreateTransitionScene();
        CreateCreditsScene();
        AddScenesToBuildSettings();
        InstallUpperFloorBlocker();
        AssetDatabase.SaveAssets();
        Debug.Log("Installed EndingP2Transition, Credits, and UpperFloorBlocker_AfterGramophone.");
    }

    private static void CreateTransitionScene()
    {
        var previousActiveScene = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        CreateCamera();
        var canvas = CreateCanvas("EndingP2Canvas", 0);

        AddText(canvas.transform, "PartTitle", "PHẦN 2", 72f, new Vector2(0.1f, 0.62f), new Vector2(0.9f, 0.76f), FontStyles.Bold);
        AddText(canvas.transform, "P2CharacterName", "NHÂN VẬT CHÍNH P2", 46f, new Vector2(0.1f, 0.49f), new Vector2(0.9f, 0.6f), FontStyles.Bold);
        AddText(canvas.transform, "ToBeContinued", "To be continued", 38f, new Vector2(0.1f, 0.38f), new Vector2(0.9f, 0.48f), FontStyles.Italic);

        var controllerObject = new GameObject("EndingP2TransitionController");
        var controller = controllerObject.AddComponent<EndingP2TransitionController>();
        var serialized = new SerializedObject(controller);
        serialized.FindProperty("creditsSceneName").stringValue = "Credits";
        serialized.FindProperty("holdSeconds").floatValue = 4f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, TransitionScenePath);
        EditorSceneManager.CloseScene(scene, true);
        if (previousActiveScene.IsValid())
            SceneManager.SetActiveScene(previousActiveScene);
    }

    private static void CreateCreditsScene()
    {
        var previousActiveScene = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        CreateCamera();
        var canvas = CreateCanvas("CreditsCanvas", 0);

        AddText(canvas.transform, "CreditsTitle", "CREDITS", 64f, new Vector2(0.1f, 0.76f), new Vector2(0.9f, 0.9f), FontStyles.Bold);
        AddText(
            canvas.transform,
            "CreditsBody",
            "Villa of Darkness\n\nNguyễn Minh Khoa\n\nCảm ơn bạn đã chơi.",
            34f,
            new Vector2(0.12f, 0.22f),
            new Vector2(0.88f, 0.72f),
            FontStyles.Normal);

        var controllerObject = new GameObject("CreditsSceneController");
        var controller = controllerObject.AddComponent<CreditsSceneController>();
        var serialized = new SerializedObject(controller);
        serialized.FindProperty("menuSceneName").stringValue = "Menu";
        serialized.FindProperty("returnToMenuDelay").floatValue = 18f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, CreditsScenePath);
        EditorSceneManager.CloseScene(scene, true);
        if (previousActiveScene.IsValid())
            SceneManager.SetActiveScene(previousActiveScene);
    }

    private static void CreateCamera()
    {
        var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
    }

    private static Canvas CreateCanvas(string name, int sortingOrder)
    {
        var canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private static TextMeshProUGUI AddText(
        Transform parent,
        string name,
        string text,
        float fontSize,
        Vector2 anchorMin,
        Vector2 anchorMax,
        FontStyles fontStyle)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        var rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.86f, 0.92f, 1f, 1f);
        label.fontStyle = fontStyle;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;
        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;
        return label;
    }

    private static void AddScenesToBuildSettings()
    {
        var paths = new[]
        {
            "Assets/MainGame/Menu.unity",
            MainScenePath,
            TransitionScenePath,
            CreditsScenePath
        };

        var existing = EditorBuildSettings.scenes
            .Where(scene => scene != null && !paths.Contains(scene.path))
            .ToList();

        var ordered = new List<EditorBuildSettingsScene>();
        foreach (string path in paths)
            ordered.Add(new EditorBuildSettingsScene(path, true));
        ordered.AddRange(existing);

        EditorBuildSettings.scenes = ordered.ToArray();
    }

    private static bool NeedsInstall()
    {
        if (!File.Exists(TransitionScenePath) || !File.Exists(CreditsScenePath))
            return true;

        if (!File.Exists(MainScenePath))
            return false;

        string sceneText = File.ReadAllText(MainScenePath);
        return !sceneText.Contains(UpperFloorBlockerName);
    }

    private static void InstallUpperFloorBlocker()
    {
        var scene = FindLoadedScene(MainScenePath);
        bool openedAdditive = false;
        if (!scene.IsValid())
        {
            scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Additive);
            openedAdditive = true;
        }

        var blockerObject = FindSceneGameObject(scene, UpperFloorBlockerName);
        if (blockerObject == null)
        {
            blockerObject = new GameObject(UpperFloorBlockerName);
            SceneManager.MoveGameObjectToScene(blockerObject, scene);
        }

        var stairPoint = FindSceneTransform(scene, StairMonsterPointName);
        blockerObject.transform.position = stairPoint != null
            ? stairPoint.position + new Vector3(0f, -0.65f, 0f)
            : new Vector3(29.22f, 3.02f, -2.25f);
        blockerObject.transform.rotation = Quaternion.identity;
        blockerObject.transform.localScale = Vector3.one;

        var box = blockerObject.GetComponent<BoxCollider>() ?? blockerObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = Vector3.zero;
        box.size = new Vector3(4.6f, 3.2f, 1.35f);

        var blocker = blockerObject.GetComponent<UpperFloorBlocker>() ?? blockerObject.AddComponent<UpperFloorBlocker>();
        var serialized = new SerializedObject(blocker);
        serialized.FindProperty("requiredProgressToPass").enumValueIndex = (int)GameProgress.PianoCompleted;
        serialized.FindProperty("blockedMessage").stringValue = "Chưa khám phá xong tầng trệt.";
        serialized.FindProperty("messageDuration").floatValue = 2.4f;
        serialized.FindProperty("showMessageOnlyOnce").boolValue = true;
        serialized.FindProperty("pushBackDistance").floatValue = 0.45f;
        serialized.FindProperty("fallbackPushDirection").vector3Value = Vector3.back;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (openedAdditive)
            EditorSceneManager.CloseScene(scene, true);
    }

    private static Scene FindLoadedScene(string path)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (scene.path == path)
                return scene;
        }

        return default;
    }

    private static GameObject FindSceneGameObject(Scene scene, string objectName)
    {
        if (!scene.IsValid())
            return null;

        foreach (var root in scene.GetRootGameObjects())
        {
            var match = FindChildByName(root.transform, objectName);
            if (match != null)
                return match.gameObject;
        }

        return null;
    }

    private static Transform FindSceneTransform(Scene scene, string objectName)
    {
        if (!scene.IsValid())
            return null;

        foreach (var root in scene.GetRootGameObjects())
        {
            var match = FindChildByName(root.transform, objectName);
            if (match != null)
                return match;
        }

        return null;
    }

    private static Transform FindChildByName(Transform root, string objectName)
    {
        if (root.name == objectName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            var match = FindChildByName(root.GetChild(i), objectName);
            if (match != null)
                return match;
        }

        return null;
    }
}
