using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static class DeathScreenSceneBuilder
{
    private const string GameScenePath = "Assets/MainGame/Game.unity";
    private const string BackdropPath = "Assets/MainGame/UI/DeathScreen/DeathScreenBackdrop.png";
    private const string TitleFontPath = "Assets/MainGame/UI/Fonts/ProtestGuerrilla-Regular SDF.asset";
    private const string BodyFontPath = "Assets/MainGame/UI/Fonts/PatrickHand-Regular SDF.asset";
    private const int DeathSortingOrder = 30000;
    private const string RunRequestPath = "Temp/RebuildDeathScreenUI.request";

    [InitializeOnLoadMethod]
    private static void RunRequestedRebuild()
    {
        if (!File.Exists(RunRequestPath))
            return;

        File.Delete(RunRequestPath);
        EditorApplication.delayCall += RebuildDeathScreenUI;
    }

    [MenuItem("Tools/MainGame/Rebuild Death Screen UI")]
    public static void RebuildDeathScreenUI()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != GameScenePath)
            scene = EditorSceneManager.OpenScene(GameScenePath);

        var canvasObject = GameObject.Find("MainGameUICanvas");
        if (canvasObject == null)
        {
            Debug.LogError("Cannot rebuild DeathUI. MainGameUICanvas was not found.");
            return;
        }

        ConfigureCanvas(canvasObject);

        var existing = FindSceneObject("DeathUI");
        if (existing != null)
            Undo.DestroyObjectImmediate(existing);

        var assets = new DeathUiAssets();
        var root = CreateUIObject(canvasObject.transform, "DeathUI");
        Stretch(root);
        root.SetAsLastSibling();

        var background = AddImage(root, "Backdrop", assets.Backdrop, Color.white);
        Stretch(background.rectTransform);
        background.type = Image.Type.Simple;
        background.preserveAspect = false;

        var veil = AddImage(root, "BlackVeil", null, new Color(0f, 0f, 0f, 0.24f));
        Stretch(veil.rectTransform);

        AddText(
            root,
            "DateLine",
            "\u0110\u00c0 L\u1ea0T TH\u1ee8 B\u1ea2O  |  14/3/2000",
            26,
            new Color(0.72f, 0.65f, 0.52f, 0.88f),
            TextAlignmentOptions.Center,
            new Vector2(0.18f, 0.755f),
            new Vector2(0.82f, 0.815f),
            assets.BodyFont,
            4f);

        var title = AddText(
            root,
            "Title",
            "PH\u00d3NG VI\u00caN M\u1ea4T T\u00cdCH T\u1ea0I BI\u1ec6T TH\u1ef0 \u0110\u1ed6 GIA",
            74,
            new Color(0.95f, 0.88f, 0.68f, 1f),
            TextAlignmentOptions.Center,
            new Vector2(0.055f, 0.585f),
            new Vector2(0.945f, 0.735f),
            assets.TitleFont,
            2f);
        title.fontStyle = FontStyles.Bold;
        title.enableAutoSizing = true;
        title.fontSizeMin = 42f;
        title.fontSizeMax = 74f;
        title.outlineWidth = 0.22f;
        title.outlineColor = Color.black;

        var subtitle = AddText(
            root,
            "ReportLine",
            "NGU\u1ed2N TIN T\u1eea C\u00d4NG AN L\u00c0M D\u1ea4Y L\u00caN NGHI V\u1ea4N\nTR\u01af\u1edcNG H\u1ee2P M\u1ea4T T\u00cdCH B\u00cd \u1ea8N V\u00c0O \u0110\u00caM 13/3.",
            25,
            new Color(0.74f, 0.67f, 0.52f, 0.9f),
            TextAlignmentOptions.Center,
            new Vector2(0.24f, 0.42f),
            new Vector2(0.76f, 0.525f),
            assets.BodyFont,
            3f);
        subtitle.textWrappingMode = TextWrappingModes.Normal;
        subtitle.lineSpacing = -12f;

        var nameLine = AddText(
            root,
            "MissingName",
            "NGUY\u1ec4N MINH KHOA, 1979 - 2000...",
            42,
            new Color(0.95f, 0.02f, 0.02f, 1f),
            TextAlignmentOptions.Center,
            new Vector2(0.14f, 0.295f),
            new Vector2(0.86f, 0.39f),
            assets.TitleFont,
            6f);
        nameLine.fontStyle = FontStyles.Bold;
        nameLine.enableAutoSizing = true;
        nameLine.fontSizeMin = 28f;
        nameLine.fontSizeMax = 42f;
        nameLine.outlineWidth = 0.2f;
        nameLine.outlineColor = Color.black;

        var retryButton = AddButton(root, "RetryButton", "RETRY", new Vector2(-235f, -285f), assets.BodyFont);
        var menuButton = AddButton(root, "MenuButton", "MENU", new Vector2(235f, -285f), assets.BodyFont);

        var controller = Object.FindFirstObjectByType<GameController>(FindObjectsInactive.Include);
        if (controller != null)
        {
            controller.deathUI = root.gameObject;
            WireButton(retryButton, controller, nameof(GameController.RestartGame));
            WireButton(menuButton, controller, nameof(GameController.LoadMainMenu));
            EditorUtility.SetDirty(controller);
        }
        else
        {
            Debug.LogWarning("DeathUI was created, but GameController was not found for button wiring.");
        }

        root.gameObject.SetActive(false);
        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeObject = root.gameObject;
        Debug.Log("DeathUI rebuilt in scene hierarchy under MainGameUICanvas.");
    }

    private static void ConfigureCanvas(GameObject canvasObject)
    {
        var canvas = canvasObject.GetComponent<Canvas>() ?? canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = DeathSortingOrder;

        var scaler = canvasObject.GetComponent<CanvasScaler>() ?? canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (canvasObject.GetComponent<GraphicRaycaster>() == null)
            canvasObject.AddComponent<GraphicRaycaster>();
    }

    private static void WireButton(Button button, GameController controller, string methodName)
    {
        button.onClick.RemoveAllListeners();
        var action = System.Delegate.CreateDelegate(typeof(UnityAction), controller, methodName) as UnityAction;
        UnityEventTools.AddPersistentListener(button.onClick, action);
        EditorUtility.SetDirty(button);
    }

    private static RectTransform CreateUIObject(Transform parent, string objectName)
    {
        var obj = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer));
        Undo.RegisterCreatedObjectUndo(obj, "Create Death UI Element");
        var rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static Image AddImage(RectTransform parent, string objectName, Sprite sprite, Color color)
    {
        var rect = CreateUIObject(parent, objectName);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = sprite != null ? Image.Type.Simple : Image.Type.Simple;
        return image;
    }

    private static TextMeshProUGUI AddText(
        RectTransform parent,
        string objectName,
        string text,
        int size,
        Color color,
        TextAlignmentOptions alignment,
        Vector2 anchorMin,
        Vector2 anchorMax,
        TMP_FontAsset font,
        float characterSpacing)
    {
        var rect = CreateUIObject(parent, objectName);
        SetRect(rect, anchorMin, anchorMax, Vector2.zero, Vector2.zero);

        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.characterSpacing = characterSpacing;
        if (font != null)
            label.font = font;
        return label;
    }

    private static Button AddButton(RectTransform parent, string objectName, string labelText, Vector2 anchoredPosition, TMP_FontAsset font)
    {
        var image = AddImage(parent, objectName, null, new Color(0.26f, 0.018f, 0.014f, 0.96f));
        var rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(300f, 64f);

        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = new Color(0.26f, 0.018f, 0.014f, 0.96f);
        colors.highlightedColor = new Color(0.48f, 0.04f, 0.03f, 1f);
        colors.pressedColor = new Color(0.62f, 0.07f, 0.04f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        var label = AddText(
            rect,
            "Label",
            labelText,
            24,
            new Color(0.94f, 0.83f, 0.62f, 1f),
            TextAlignmentOptions.Center,
            Vector2.zero,
            Vector2.one,
            font,
            3f);
        label.fontStyle = FontStyles.Bold;
        return button;
    }

    private static Sprite CreateOrLoadBackdrop()
    {
        EnsureFolder("Assets/MainGame/UI", "DeathScreen");
        if (!File.Exists(BackdropPath))
            File.WriteAllBytes(BackdropPath, CreateBackdropTexture().EncodeToPNG());

        AssetDatabase.ImportAsset(BackdropPath);
        var importer = AssetImporter.GetAtPath(BackdropPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(BackdropPath);
    }

    private static Texture2D CreateBackdropTexture()
    {
        const int width = 1024;
        const int height = 576;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        var pixels = new Color32[width * height];

        for (int y = 0; y < height; y++)
        {
            float v = y / (float)(height - 1);
            for (int x = 0; x < width; x++)
            {
                float u = x / (float)(width - 1);
                float vignette = Mathf.Clamp01(Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 1.6f);
                byte tone = (byte)Mathf.Lerp(12f, 0f, vignette);
                pixels[y * width + x] = new Color32((byte)(tone * 0.35f), (byte)(tone * 0.55f), tone, 255);
            }
        }

        Color32 nearBlack = new(0, 0, 0, 255);
        Color32 farBlue = new(7, 14, 23, 255);
        Color32 fence = new(5, 9, 14, 255);
        Color32 mansion = new(5, 10, 17, 255);
        Color32 roof = new(1, 3, 7, 255);

        FillRect(pixels, width, height, 0, 0, width, 120, nearBlack);
        FillRect(pixels, width, height, 300, 175, 724, 360, mansion);
        FillTriangle(pixels, width, height, 250, 360, 510, 482, 760, 360, roof);
        FillRect(pixels, width, height, 630, 300, 705, 455, roof);
        FillTriangle(pixels, width, height, 603, 455, 667, 506, 730, 455, roof);
        FillRect(pixels, width, height, 335, 190, 380, 310, new Color32(2, 5, 9, 255));
        FillRect(pixels, width, height, 470, 190, 555, 315, new Color32(2, 5, 9, 255));
        FillRect(pixels, width, height, 620, 190, 665, 310, new Color32(2, 5, 9, 255));

        for (int y = 132; y < 178; y += 18)
            FillRect(pixels, width, height, 0, y, width, y + 5, fence);
        for (int x = 0; x < width; x += 34)
            FillRect(pixels, width, height, x, 108, x + 7, 206, fence);

        DrawBareTree(pixels, width, height, 85, 90, 150, nearBlack);
        DrawBareTree(pixels, width, height, 905, 92, 150, nearBlack);
        DrawBareTree(pixels, width, height, 815, 128, 82, farBlue);

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private static void FillRect(Color32[] pixels, int width, int height, int xMin, int yMin, int xMax, int yMax, Color32 color)
    {
        xMin = Mathf.Clamp(xMin, 0, width);
        xMax = Mathf.Clamp(xMax, 0, width);
        yMin = Mathf.Clamp(yMin, 0, height);
        yMax = Mathf.Clamp(yMax, 0, height);
        for (int y = yMin; y < yMax; y++)
        {
            int row = y * width;
            for (int x = xMin; x < xMax; x++)
                pixels[row + x] = color;
        }
    }

    private static void FillTriangle(Color32[] pixels, int width, int height, int x0, int y0, int x1, int y1, int x2, int y2, Color32 color)
    {
        int minX = Mathf.Max(0, Mathf.Min(x0, Mathf.Min(x1, x2)));
        int maxX = Mathf.Min(width - 1, Mathf.Max(x0, Mathf.Max(x1, x2)));
        int minY = Mathf.Max(0, Mathf.Min(y0, Mathf.Min(y1, y2)));
        int maxY = Mathf.Min(height - 1, Mathf.Max(y0, Mathf.Max(y1, y2)));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float w0 = Edge(x1, y1, x2, y2, x, y);
                float w1 = Edge(x2, y2, x0, y0, x, y);
                float w2 = Edge(x0, y0, x1, y1, x, y);
                if ((w0 >= 0f && w1 >= 0f && w2 >= 0f) || (w0 <= 0f && w1 <= 0f && w2 <= 0f))
                    pixels[y * width + x] = color;
            }
        }
    }

    private static float Edge(int ax, int ay, int bx, int by, int cx, int cy)
    {
        return (cx - ax) * (by - ay) - (cy - ay) * (bx - ax);
    }

    private static void DrawBareTree(Color32[] pixels, int width, int height, int rootX, int rootY, int treeHeight, Color32 color)
    {
        int topY = rootY + treeHeight;
        DrawLine(pixels, width, height, rootX, rootY, rootX + 10, topY, color, 10);
        DrawLine(pixels, width, height, rootX + 6, rootY + 58, rootX - 74, rootY + 130, color, 5);
        DrawLine(pixels, width, height, rootX + 14, rootY + 84, rootX + 86, rootY + 152, color, 5);
        DrawLine(pixels, width, height, rootX + 8, rootY + 105, rootX - 42, rootY + 174, color, 4);
        DrawLine(pixels, width, height, rootX + 13, rootY + 122, rootX + 56, rootY + 188, color, 4);
    }

    private static void DrawLine(Color32[] pixels, int width, int height, int x0, int y0, int x1, int y1, Color32 color, int thickness)
    {
        int dx = Mathf.Abs(x1 - x0);
        int dy = -Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;

        while (true)
        {
            FillRect(pixels, width, height, x0 - thickness / 2, y0 - thickness / 2, x0 + thickness / 2 + 1, y0 + thickness / 2 + 1, color);
            if (x0 == x1 && y0 == y1)
                break;

            int doubledError = 2 * error;
            if (doubledError >= dy)
            {
                error += dy;
                x0 += sx;
            }
            if (doubledError <= dx)
            {
                error += dx;
                y0 += sy;
            }
        }
    }

    private static void Stretch(RectTransform rect)
    {
        SetRect(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    private static void SetRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + child))
            AssetDatabase.CreateFolder(parent, child);
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

    private sealed class DeathUiAssets
    {
        public readonly Sprite Backdrop = CreateOrLoadBackdrop();
        public readonly TMP_FontAsset TitleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath);
        public readonly TMP_FontAsset BodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);
    }
}
