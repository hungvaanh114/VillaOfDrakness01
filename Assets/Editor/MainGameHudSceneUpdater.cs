using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MainGameHudSceneUpdater
{
    private const string SpriteFolder = "Assets/MainGame/UI/Sprites";
    private const string FontPath = "Assets/FpsHorrorKit/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    private static readonly Color PanelColor = new(0.01f, 0.035f, 0.055f, 0.74f);
    private static readonly Color PanelSoftColor = new(0.01f, 0.035f, 0.055f, 0.58f);
    private static readonly Color LineColor = new(0.66f, 0.82f, 0.92f, 0.64f);
    private static readonly Color AccentColor = new(0.56f, 0.86f, 1f, 1f);
    private static readonly Color TextColor = new(0.86f, 0.89f, 0.91f, 1f);
    private static readonly Color MutedTextColor = new(0.72f, 0.76f, 0.78f, 0.96f);

    private static TMP_FontAsset font;
    private static readonly Dictionary<string, Sprite> sprites = new();

    [MenuItem("Tools/MainGame/Apply Main Game HUD")]
    public static void Apply()
    {
        LoadAssets();

        var canvas = MainGameEditorCanvasUtility.FindOrCreateScreenCanvas();
        if (canvas == null)
        {
            Debug.LogError("Canvas not found in current scene.");
            return;
        }

        SetupCanvas(canvas);

        var gameUI = canvas.transform.Find("GameUI");
        if (gameUI == null)
        {
            var gameObject = new GameObject("GameUI", typeof(RectTransform));
            gameObject.layer = LayerMask.NameToLayer("UI");
            gameObject.transform.SetParent(canvas.transform, false);
            gameUI = gameObject.transform;
        }

        RebuildHud(gameUI);
        WireGameController(gameUI.gameObject);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("Main game HUD applied.");
    }

    private static void RebuildHud(Transform root)
    {
        root.gameObject.SetActive(true);
        SetStretch(EnsureRect(root), Vector2.zero, Vector2.zero);

        for (var i = root.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(root.GetChild(i).gameObject);

        AddVignette(root);
        AddCrosshair(root);
        AddObjectivePanel(root);
        AddFlashlightPanel(root);
        AddInteractPrompt(root);
        AddNarrationPanel(root);
    }

    private static void AddObjectivePanel(Transform root)
    {
        var panel = AddImage(root, "ObjectivePanel", SpriteOrNull("panel"), PanelSoftColor, Image.Type.Sliced);
        SetRect(panel.rectTransform, new Vector2(0f, 1f), new Vector2(380f, 162f), new Vector2(16f, -18f));

        var title = AddText(panel.transform, "ObjectiveTitleText", "Mục tiêu:", 28f, AccentColor, TextAlignmentOptions.Left);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(260f, 36f), new Vector2(42f, -34f));

        var divider = AddImage(panel.transform, "ObjectiveDivider", SpriteOrNull("line"), LineColor, Image.Type.Sliced);
        SetRect(divider.rectTransform, new Vector2(0f, 1f), new Vector2(250f, 3f), new Vector2(42f, -78f));

        var text = AddText(panel.transform, "ObjectiveText", "Tìm lối vào biệt thự", 27f, MutedTextColor, TextAlignmentOptions.Left);
        SetRect(text.rectTransform, new Vector2(0f, 1f), new Vector2(300f, 38f), new Vector2(42f, -104f));
    }

    private static void AddFlashlightPanel(Transform root)
    {
        var panel = AddImage(root, "FlashlightPanel", SpriteOrNull("panel"), PanelSoftColor, Image.Type.Sliced);
        SetRect(panel.rectTransform, new Vector2(0f, 0f), new Vector2(332f, 158f), new Vector2(20f, 20f));

        var title = AddText(panel.transform, "FlashlightTitleText", "PIN ĐÈN PIN", 23f, TextColor, TextAlignmentOptions.Left);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(210f, 32f), new Vector2(30f, -28f));

        var divider = AddImage(panel.transform, "FlashlightDivider", SpriteOrNull("line"), LineColor, Image.Type.Sliced);
        SetRect(divider.rectTransform, new Vector2(0f, 1f), new Vector2(206f, 3f), new Vector2(30f, -68f));

        var batteryFrame = AddImage(panel.transform, "BatteryFrame", SpriteOrNull("bar"), new Color(0.02f, 0.045f, 0.06f, 0.86f), Image.Type.Sliced);
        SetRect(batteryFrame.rectTransform, new Vector2(0f, 0f), new Vector2(168f, 52f), new Vector2(32f, 26f));

        var cap = AddImage(panel.transform, "BatteryCap", null, LineColor, Image.Type.Simple);
        SetRect(cap.rectTransform, new Vector2(0f, 0f), new Vector2(9f, 24f), new Vector2(200f, 40f));

        for (var i = 0; i < 6; i++)
        {
            var fill = i < 5;
            var cellColor = fill
                ? new Color(0.48f, 0.85f, 1f, 0.90f)
                : new Color(0.04f, 0.08f, 0.10f, 0.66f);
            var cell = AddImage(batteryFrame.transform, $"BatteryCell{i + 1}", SpriteOrNull("bar_fill"), cellColor, Image.Type.Sliced);
            SetRect(cell.rectTransform, new Vector2(0f, 0.5f), new Vector2(22f, 38f), new Vector2(10f + i * 25f, 0f));
        }

        var percent = AddText(panel.transform, "BatteryPercentText", "78%", 28f, TextColor, TextAlignmentOptions.Left);
        SetRect(percent.rectTransform, new Vector2(0f, 0f), new Vector2(90f, 40f), new Vector2(238f, 34f));
    }

    private static void AddInteractPrompt(Transform root)
    {
        var panel = AddImage(root, "InteractPrompt", SpriteOrNull("button"), new Color(0.015f, 0.035f, 0.047f, 0.72f), Image.Type.Sliced);
        SetRect(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(322f, 72f), new Vector2(0f, -206f));

        var left = AddText(panel.transform, "InteractPrefixText", "Nhấn", 24f, MutedTextColor, TextAlignmentOptions.Right);
        SetRect(left.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(72f, 34f), new Vector2(-78f, 0f));

        var key = AddImage(panel.transform, "KeycapBackground", SpriteOrNull("keycap"), new Color(0.02f, 0.04f, 0.055f, 0.88f), Image.Type.Sliced);
        SetRect(key.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(36f, 36f), new Vector2(-34f, 0f));

        var keyText = AddText(key.transform, "KeycapText", "E", 21f, TextColor, TextAlignmentOptions.Center);
        SetStretch(keyText.rectTransform, Vector2.zero, Vector2.zero);

        var right = AddText(panel.transform, "InteractSuffixText", "để tương tác", 24f, MutedTextColor, TextAlignmentOptions.Left);
        SetRect(right.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(150f, 34f), new Vector2(56f, 0f));
    }

    private static void AddNarrationPanel(Transform root)
    {
        var panel = AddImage(root, "NarrationPanel", SpriteOrNull("panel"), PanelSoftColor, Image.Type.Sliced);
        SetRect(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(700f, 94f), new Vector2(0f, 62f));

        var text = AddText(panel.transform, "NarrationText", "", 26f, AccentColor, TextAlignmentOptions.Center);
        text.textWrappingMode = TextWrappingModes.Normal;
        SetStretch(text.rectTransform, new Vector2(34f, 14f), new Vector2(-34f, -14f));
        panel.gameObject.SetActive(false);
    }

    private static void AddCrosshair(Transform root)
    {
        var crosshair = AddImage(root, "Crosshair", SpriteOrNull("crosshair"), new Color(0.90f, 0.94f, 1f, 0.86f), Image.Type.Simple);
        SetRect(crosshair.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(38f, 38f), Vector2.zero);
    }

    private static void AddVignette(Transform root)
    {
        var vignette = AddImage(root, "HudVignette", SpriteOrNull("vignette"), new Color(0f, 0.02f, 0.04f, 0.18f), Image.Type.Sliced);
        SetStretch(vignette.rectTransform, Vector2.zero, Vector2.zero);
    }

    private static void SetupCanvas(Canvas canvas)
    {
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvas.GetComponent<CanvasScaler>() ?? canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private static void WireGameController(GameObject gameUI)
    {
        var controller = Object.FindFirstObjectByType<GameController>();
        if (controller == null)
            return;

        var serialized = new SerializedObject(controller);
        var property = serialized.FindProperty("gameUI");
        if (property != null)
            property.objectReferenceValue = gameUI;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void LoadAssets()
    {
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        sprites.Clear();

        foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { SpriteFolder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
                sprites[System.IO.Path.GetFileNameWithoutExtension(path)] = sprite;
        }
    }

    private static Image AddImage(Transform parent, string name, Sprite sprite, Color color, Image.Type type)
    {
        var gameObject = AddObject(parent, name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var image = gameObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = sprite != null ? type : Image.Type.Simple;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text AddText(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions alignment)
    {
        var gameObject = AddObject(parent, name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var tmp = gameObject.GetComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.fontStyle = FontStyles.SmallCaps;
        tmp.outlineColor = new Color(0.01f, 0.04f, 0.06f, 0.92f);
        tmp.outlineWidth = size >= 24f ? 0.11f : 0.08f;

        var shadow = gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.03f, 0.18f, 0.25f, 0.45f);
        shadow.effectDistance = new Vector2(1.2f, -1.4f);
        shadow.useGraphicAlpha = true;
        return tmp;
    }

    private static GameObject AddObject(Transform parent, string name, params System.Type[] components)
    {
        var gameObject = new GameObject(name, components);
        gameObject.layer = LayerMask.NameToLayer("UI");
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static RectTransform EnsureRect(Transform transform)
    {
        return transform.GetComponent<RectTransform>() ?? transform.gameObject.AddComponent<RectTransform>();
    }

    private static Sprite SpriteOrNull(string name)
    {
        return sprites.TryGetValue(name, out var sprite) ? sprite : null;
    }

    private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one;
    }

    private static void SetStretch(RectTransform rect, Vector2 minOffset, Vector2 maxOffset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = minOffset;
        rect.offsetMax = maxOffset;
        rect.localScale = Vector3.one;
    }
}
