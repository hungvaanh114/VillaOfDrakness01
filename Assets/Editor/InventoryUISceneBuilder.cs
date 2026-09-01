using FpsHorrorKit;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class InventoryUISceneBuilder
{
    private const int InventorySortingOrder = 20000;

    [MenuItem("Tools/MainGame/Rebuild Inventory UI Hierarchy")]
    public static void RebuildInventoryUIHierarchy()
    {
        var ui = Object.FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
        if (ui == null)
            ui = CreateInventoryUIRoot();

        if (ui == null)
        {
            Debug.LogError("Cannot rebuild inventory UI hierarchy. MainGameUICanvas was not found.");
            return;
        }

        var canvas = ui.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = InventorySortingOrder;

            var scaler = canvas.GetComponent<CanvasScaler>() ?? canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            if (canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        ClearChildren(ui.transform);

        var uiRoot = ui.transform as RectTransform;
        if (uiRoot == null)
        {
            Debug.LogError("Cannot rebuild inventory UI hierarchy. InventoryUIRoot must use a RectTransform.");
            return;
        }

        var assets = new InventoryUiAssets(ui);
        var overlay = CreateUIObject(uiRoot, "InventoryOverlay");
        Stretch(overlay);
        overlay.gameObject.SetActive(false);

        AddImage(overlay, "SceneShield", null, new Color(38f / 255f, 38f / 255f, 38f / 255f, 0.92f), Image.Type.Simple, true);
        AddImage(overlay, "DimBackground", assets.BackgroundSprite, new Color(0.01f, 0.035f, 0.055f, 0.96f), Image.Type.Sliced, true);
        BuildTabs(overlay, assets);

        var inventoryTab = CreateUIObject(overlay, "InventoryTab");
        var musicTab = CreateUIObject(overlay, "MusicSheetTab");
        Stretch(inventoryTab);
        Stretch(musicTab);

        BuildInventoryTab(inventoryTab, assets);
        BuildMusicTab(musicTab, assets);
        BuildFooter(overlay, assets);

        EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        EditorSceneManager.SaveScene(ui.gameObject.scene);
        Selection.activeObject = ui.gameObject;
        Debug.Log("Inventory UI hierarchy rebuilt under InventoryUIRoot.");
    }

    public static void RebuildGameSceneInventoryUI()
    {
        EditorSceneManager.OpenScene("Assets/MainGame/Game.unity");
        RebuildInventoryUIHierarchy();
    }

    private static InventoryUI CreateInventoryUIRoot()
    {
        var canvasObject = GameObject.Find("MainGameUICanvas");
        if (canvasObject == null)
            return null;

        var root = new GameObject("InventoryUIRoot", typeof(RectTransform), typeof(InventoryUI));
        Undo.RegisterCreatedObjectUndo(root, "Create InventoryUIRoot");
        var rect = root.GetComponent<RectTransform>();
        rect.SetParent(canvasObject.transform, false);
        Stretch(rect);
        return root.GetComponent<InventoryUI>();
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(parent.GetChild(i).gameObject);
    }

    private static void BuildTabs(RectTransform parent, InventoryUiAssets assets)
    {
        string[] labels = { "HANH TRANG", "MANH NOT NHAC" };
        for (int i = 0; i < labels.Length; i++)
        {
            var button = CreateButton(parent, $"Tab_{i + 1}", assets.TabSprite, labels[i], 30, assets);
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(i == 1 ? 360f : 300f, 76f);
            rect.anchoredPosition = new Vector2((i - 0.5f) * 380f, -70f);
        }

        var closeButton = CreateButton(parent, "CloseButton", assets.CloseSprite, "X", 42, assets);
        var closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.sizeDelta = new Vector2(72f, 72f);
        closeRect.anchoredPosition = new Vector2(-70f, -62f);
    }

    private static void BuildInventoryTab(RectTransform parent, InventoryUiAssets assets)
    {
        var gridPanel = AddImage(parent, "GridPanel", assets.PanelSprite, new Color(0.02f, 0.07f, 0.1f, 0.88f), Image.Type.Sliced, false);
        SetRect(gridPanel.rectTransform, new Vector2(0.08f, 0.12f), new Vector2(0.62f, 0.78f), Vector2.zero, Vector2.zero);

        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 4; col++)
            {
                var slot = CreateButton(gridPanel.rectTransform, $"Slot_{row}_{col}", assets.SlotSprite, "", 18, assets);
                var rect = slot.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = new Vector2(202f, 172f);
                rect.anchoredPosition = new Vector2(52f + col * 224f, -58f - row * 224f);

                var icon = AddImage(rect, "Icon", null, Color.white, Image.Type.Simple, false);
                SetRect(icon.rectTransform, new Vector2(0.16f, 0.24f), new Vector2(0.84f, 0.84f), Vector2.zero, Vector2.zero);
                AddText(rect, "Name", "", 20, new Color(0.82f, 0.86f, 0.88f), TextAlignmentOptions.Center, new Vector2(0f, 0f), new Vector2(1f, 0.22f), assets);
                AddText(rect, "Amount", "", 18, new Color(0.7f, 0.9f, 1f), TextAlignmentOptions.Right, new Vector2(0.58f, 0.02f), new Vector2(0.94f, 0.22f), assets);
                slot.gameObject.AddComponent<InventorySlotUI>();
            }
        }

        var detail = AddImage(parent, "DetailPanel", assets.InfoPanelSprite, new Color(0.018f, 0.06f, 0.09f, 0.9f), Image.Type.Sliced, false);
        SetRect(detail.rectTransform, new Vector2(0.67f, 0.12f), new Vector2(0.93f, 0.78f), Vector2.zero, Vector2.zero);
        AddText(detail.rectTransform, "DetailName", "DEN PIN", 32, new Color(0.85f, 0.95f, 1f), TextAlignmentOptions.Center, new Vector2(0.08f, 0.84f), new Vector2(0.92f, 0.94f), assets);
        var divider = AddImage(detail.rectTransform, "TopDivider", assets.DividerSprite, new Color(0.68f, 0.85f, 0.95f, 0.7f), Image.Type.Sliced, false);
        divider.rectTransform.sizeDelta = new Vector2(260f, 12f);
        var detailIcon = AddImage(detail.rectTransform, "DetailIcon", null, Color.white, Image.Type.Simple, false);
        SetRect(detailIcon.rectTransform, new Vector2(0.18f, 0.42f), new Vector2(0.82f, 0.78f), Vector2.zero, Vector2.zero);
        AddText(detail.rectTransform, "DetailAmount", "", 20, new Color(0.7f, 0.84f, 0.9f), TextAlignmentOptions.Center, new Vector2(0.1f, 0.33f), new Vector2(0.9f, 0.39f), assets);
        AddText(detail.rectTransform, "EquippedText", "", 22, new Color(0.6f, 0.9f, 1f), TextAlignmentOptions.Center, new Vector2(0.1f, 0.28f), new Vector2(0.9f, 0.34f), assets);
        AddText(detail.rectTransform, "DetailDescription", "", 22, new Color(0.78f, 0.82f, 0.84f), TextAlignmentOptions.TopLeft, new Vector2(0.13f, 0.14f), new Vector2(0.87f, 0.31f), assets);
        var useButton = CreateButton(detail.rectTransform, "UseButton", assets.SelectedTabSprite != null ? assets.SelectedTabSprite : assets.TabSprite, "SU DUNG", 24, assets);
        var useRect = useButton.GetComponent<RectTransform>();
        useRect.anchorMin = new Vector2(0.28f, 0.04f);
        useRect.anchorMax = new Vector2(0.72f, 0.12f);
        useRect.offsetMin = Vector2.zero;
        useRect.offsetMax = Vector2.zero;
    }

    private static void BuildMusicTab(RectTransform parent, InventoryUiAssets assets)
    {
        var panel = AddImage(parent, "MusicPanel", assets.PanelSprite, new Color(0.02f, 0.07f, 0.1f, 0.9f), Image.Type.Sliced, false);
        SetRect(panel.rectTransform, new Vector2(0.14f, 0.16f), new Vector2(0.86f, 0.74f), Vector2.zero, Vector2.zero);
        AddText(panel.rectTransform, "MusicTitle", "MANH NOT NHAC", 38, new Color(0.85f, 0.95f, 1f), TextAlignmentOptions.Center, new Vector2(0.1f, 0.83f), new Vector2(0.9f, 0.94f), assets);
        AddText(panel.rectTransform, "MusicProgress", "0 / 5", 30, new Color(0.68f, 0.9f, 1f), TextAlignmentOptions.Center, new Vector2(0.42f, 0.73f), new Vector2(0.58f, 0.82f), assets);

        const int slotCount = 5;
        float gap = 0.025f;
        float slotWidth = Mathf.Min(0.16f, (0.84f - gap * (slotCount - 1)) / slotCount);
        float totalWidth = slotWidth * slotCount + gap * (slotCount - 1);
        float start = 0.5f - totalWidth * 0.5f;
        for (int i = 0; i < slotCount; i++)
        {
            float min = start + i * (slotWidth + gap);
            var frame = AddImage(panel.rectTransform, $"MusicSlot_{i + 1}", assets.SlotSprite, new Color(0.03f, 0.07f, 0.1f, 0.82f), Image.Type.Sliced, false);
            SetRect(frame.rectTransform, new Vector2(min, 0.22f), new Vector2(min + slotWidth, 0.62f), Vector2.zero, Vector2.zero);
            var icon = AddImage(frame.rectTransform, "MusicIcon", assets.UnknownMusicSprite, Color.white, Image.Type.Simple, false);
            SetRect(icon.rectTransform, new Vector2(0.1f, 0.13f), new Vector2(0.9f, 0.87f), Vector2.zero, Vector2.zero);
        }
    }

    private static void BuildFooter(RectTransform parent, InventoryUiAssets assets)
    {
        var footer = AddText(parent, "FooterHelp", "TAB - Dong     |     Chuot trai - Chon     |     Chuot phai / nhap dup - Su dung     |     Q / E - Doi tab     |     ESC - Dong", 22, new Color(0.68f, 0.76f, 0.8f), TextAlignmentOptions.Center, new Vector2(0.08f, 0.03f), new Vector2(0.92f, 0.08f), assets);
        footer.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private static Button CreateButton(RectTransform parent, string name, Sprite sprite, string label, int fontSize, InventoryUiAssets assets)
    {
        var image = AddImage(parent, name, sprite, new Color(0.02f, 0.06f, 0.09f, 0.85f), Image.Type.Sliced, false);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var text = AddText(image.rectTransform, "Text", label, fontSize, new Color(0.82f, 0.88f, 0.92f), TextAlignmentOptions.Center, Vector2.zero, Vector2.one, assets);
        text.raycastTarget = false;
        return button;
    }

    private static TextMeshProUGUI AddText(RectTransform parent, string name, string text, int size, Color color, TextAlignmentOptions align, Vector2 min, Vector2 max, InventoryUiAssets assets)
    {
        var rect = CreateUIObject(parent, name);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = align;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        if (assets.BodyFont != null) label.font = assets.BodyFont;
        if (size >= 30 && assets.TitleFont != null) label.font = assets.TitleFont;
        SetRect(rect, min, max, Vector2.zero, Vector2.zero);
        return label;
    }

    private static Image AddImage(RectTransform parent, string name, Sprite sprite, Color color, Image.Type type, bool stretch)
    {
        var rect = CreateUIObject(parent, name);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = sprite != null ? type : Image.Type.Simple;
        if (stretch) Stretch(rect);
        return image;
    }

    private static RectTransform CreateUIObject(RectTransform parent, string name)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        Undo.RegisterCreatedObjectUndo(obj, "Create Inventory UI Element");
        var rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
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

    private sealed class InventoryUiAssets
    {
        public readonly Sprite BackgroundSprite;
        public readonly Sprite PanelSprite;
        public readonly Sprite InfoPanelSprite;
        public readonly Sprite SlotSprite;
        public readonly Sprite SelectedTabSprite;
        public readonly Sprite TabSprite;
        public readonly Sprite CloseSprite;
        public readonly Sprite DividerSprite;
        public readonly Sprite UnknownMusicSprite;
        public readonly TMP_FontAsset TitleFont;
        public readonly TMP_FontAsset BodyFont;

        public InventoryUiAssets(InventoryUI ui)
        {
            var serialized = new SerializedObject(ui);
            BackgroundSprite = GetAsset<Sprite>(serialized, "backgroundSprite");
            PanelSprite = GetAsset<Sprite>(serialized, "panelSprite");
            InfoPanelSprite = GetAsset<Sprite>(serialized, "infoPanelSprite");
            SlotSprite = GetAsset<Sprite>(serialized, "slotSprite");
            SelectedTabSprite = GetAsset<Sprite>(serialized, "selectedTabSprite");
            TabSprite = GetAsset<Sprite>(serialized, "tabSprite");
            CloseSprite = GetAsset<Sprite>(serialized, "closeSprite");
            DividerSprite = GetAsset<Sprite>(serialized, "dividerSprite");
            UnknownMusicSprite = GetAsset<Sprite>(serialized, "unknownMusicSprite");
            TitleFont = GetAsset<TMP_FontAsset>(serialized, "titleFont");
            BodyFont = GetAsset<TMP_FontAsset>(serialized, "bodyFont");
        }

        private static T GetAsset<T>(SerializedObject serialized, string propertyName) where T : Object
        {
            return serialized.FindProperty(propertyName)?.objectReferenceValue as T;
        }
    }
}

[InitializeOnLoad]
public static class InventoryUISceneBuilderAutoRun
{
    static InventoryUISceneBuilderAutoRun()
    {
        EditorApplication.delayCall += RebuildIfGameSceneNeedsInventoryHierarchy;
    }

    private static void RebuildIfGameSceneNeedsInventoryHierarchy()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/MainGame/Game.unity")
            return;

        var ui = Object.FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
        if (ui == null || ui.transform.Find("InventoryOverlay") != null)
            return;

        InventoryUISceneBuilder.RebuildInventoryUIHierarchy();
    }
}
