using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class SettingsTabsSceneUpdater
{
    private static readonly Color SelectedTabColor = new(0.04f, 0.18f, 0.26f, 0.72f);
    private static readonly Color NormalTabColor = new(0.01f, 0.035f, 0.06f, 0.46f);
    private static readonly Color SelectedTextColor = new(0.78f, 0.94f, 1f, 1f);
    private static readonly Color NormalTextColor = new(0.72f, 0.78f, 0.82f, 0.92f);

    [MenuItem("Tools/MainGame/Apply Settings Tabs")]
    public static void Apply()
    {
        var settingUI = FindInActiveScene("SettingUI");
        var settingsBody = FindInActiveScene("SettingsBody");
        var contentPanel = FindInActiveScene("ContentPanel");
        var shortcutPanel = FindInActiveScene("ShortcutPanel");

        if (settingUI == null || settingsBody == null || contentPanel == null)
        {
            Debug.LogError("SettingUI, SettingsBody, or ContentPanel was not found.");
            return;
        }

        var settingsTab = FindAny("SettingsTabButton", "GeneralTab");

        if (settingsTab == null)
        {
            Debug.LogError("Settings tab source object was not found.");
            return;
        }

        SetInactiveIfFound("GraphicsTab");
        SetInactiveIfFound("VisualTab");
        SetInactiveIfFound("AudioTab");
        DeleteIfFound("ControlsTab");
        SetInactiveIfFound("GameplayTab");
        DeleteIfFound("ShortcutsTabButton");
        DeleteIfFound("ShortcutPanel");

        var settingsButton = ConfigureTab(settingsTab, "SettingsTabButton", "C\u00e0i \u0111\u1eb7t", new Vector2(0f, 0f), true);
        var controlsTab = EnsureTab(settingsTab.parent, "ControlsTabButton");
        var controlsButton = ConfigureTab(controlsTab, "ControlsTabButton", "Ph\u00edm", new Vector2(0f, -68f), false);
        var controlsLabel = controlsButton.GetComponentInChildren<TMP_Text>(true);
        if (controlsLabel != null)
            controlsLabel.text = "Ph\u00edm";
        var controlsPage = EnsureControlsPage(contentPanel.parent);

        contentPanel.gameObject.SetActive(true);
        controlsPage.gameObject.SetActive(false);

        var controller = settingUI.GetComponent<SettingsUIController>() ?? settingUI.gameObject.AddComponent<SettingsUIController>();
        WireController(controller, contentPanel, settingsButton, controlsPage, controlsButton);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("Settings tabs were applied.");
    }

    [MenuItem("Assets/MainGame/Apply Settings Tabs")]
    public static void ApplyMainGameScene()
    {
        EditorSceneManager.OpenScene("Assets/MainGame/Game.unity");
        Apply();
    }

    private static Button ConfigureTab(Transform tab, string name, string label, Vector2 anchoredPosition, bool selected)
    {
        tab.name = name;
        tab.gameObject.SetActive(true);

        var rect = tab.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(238f, 58f);

        var image = tab.GetComponent<Image>() ?? tab.gameObject.AddComponent<Image>();
        image.raycastTarget = true;
        image.color = selected ? SelectedTabColor : NormalTabColor;

        var button = tab.GetComponent<Button>() ?? tab.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = CreateButtonColors();

        var text = tab.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.text = label;
            text.color = selected ? SelectedTextColor : NormalTextColor;
            text.alignment = TextAlignmentOptions.Center;
        }

        EditorUtility.SetDirty(tab);
        return button;
    }

    private static Transform EnsureTab(Transform parent, string name)
    {
        var existing = FindInActiveScene(name);
        if (existing != null)
            return existing;

        var tab = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        tab.layer = LayerMask.NameToLayer("UI");
        tab.transform.SetParent(parent, false);

        var label = new GameObject("LabelText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        label.layer = tab.layer;
        label.transform.SetParent(tab.transform, false);
        var labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 0f);
        labelRect.offsetMax = new Vector2(-12f, 0f);
        return tab.transform;
    }

    private static Transform EnsureControlsPage(Transform parent)
    {
        var existing = FindInActiveScene("ControlsPage");
        if (existing != null)
            return existing;

        var page = new GameObject("ControlsPage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        page.layer = LayerMask.NameToLayer("UI");
        page.transform.SetParent(parent, false);
        var rect = page.GetComponent<RectTransform>();
        var contentRect = FindInActiveScene("ContentPanel")?.GetComponent<RectTransform>();
        if (contentRect != null)
        {
            rect.anchorMin = contentRect.anchorMin;
            rect.anchorMax = contentRect.anchorMax;
            rect.pivot = contentRect.pivot;
            rect.sizeDelta = contentRect.sizeDelta;
            rect.anchoredPosition = contentRect.anchoredPosition;
        }

        var image = page.GetComponent<Image>();
        image.color = new Color(0.01f, 0.035f, 0.055f, 0.40f);
        image.raycastTarget = false;
        AddGuide(page.transform);
        return page.transform;
    }

    private static void AddGuide(Transform parent)
    {
        AddGuideText(parent, "GuideTitle_Movement", "DI CHUYỂN", 23f, SelectedTextColor, new Vector2(4f, -14f), new Vector2(620f, 34f));
        AddGuideRow(parent, "W / A / S / D", "Di chuyển nhân vật", -58f);
        AddGuideRow(parent, "Chuột", "Nhìn / xoay camera", -98f);
        AddGuideRow(parent, "Shift", "Chạy", -138f);
        AddGuideRow(parent, "E", "Tương tác", -178f);
        AddGuideText(parent, "GuideTitle_Inventory", "HÀNH TRANG VÀ VẬT PHẨM", 23f, SelectedTextColor, new Vector2(4f, -246f), new Vector2(620f, 34f));
        AddGuideRow(parent, "TAB", "Mở / đóng hành trang", -290f);
        AddGuideRow(parent, "Chuột trái", "Chọn vật phẩm", -330f);
        AddGuideRow(parent, "Chuột phải / nhấp đúp", "Sử dụng hoặc trang bị vật phẩm", -370f);
        AddGuideRow(parent, "Q / E", "Đổi tab hành trang", -410f);
        AddGuideRow(parent, "F", "Bật / tắt đèn pin", -450f);
        AddGuideRow(parent, "ESC", "Đóng UI hoặc quay lại", -490f);
    }

    private static void AddGuideRow(Transform parent, string key, string desc, float y)
    {
        AddGuideText(parent, $"GuideKey_{key}", key, 20f, SelectedTextColor, new Vector2(28f, y), new Vector2(220f, 30f));
        AddGuideText(parent, $"GuideDesc_{key}", desc, 20f, NormalTextColor, new Vector2(260f, y), new Vector2(480f, 30f));
    }

    private static void AddGuideText(Transform parent, string name, string text, float size, Color color, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = sizeDelta;
        rect.anchoredPosition = anchoredPosition;
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.raycastTarget = false;
    }

    private static void WireController(
        SettingsUIController controller,
        Transform contentPanel,
        Button settingsButton,
        Transform controlsPage,
        Button controlsButton)
    {
        var settingsImage = settingsButton.targetGraphic as Image;
        var controlsImage = controlsButton.targetGraphic as Image;

        var serialized = new SerializedObject(controller);
        Set(serialized, "settingsPage", contentPanel.gameObject);
        Set(serialized, "settingsTabButton", settingsButton);
        Set(serialized, "settingsTabGraphic", settingsImage);
        Set(serialized, "settingsTabLabel", settingsButton.GetComponentInChildren<TMP_Text>(true));
        Set(serialized, "controlsPage", controlsPage.gameObject);
        Set(serialized, "controlsTabButton", controlsButton);
        Set(serialized, "controlsTabGraphic", controlsImage);
        Set(serialized, "controlsTabLabel", controlsButton.GetComponentInChildren<TMP_Text>(true));
        Set(serialized, "pageTitleText", FindInActiveScene("SettingsTitleText")?.GetComponent<TMP_Text>());
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetInactiveIfFound(string name)
    {
        var target = FindInActiveScene(name);
        if (target != null)
            target.gameObject.SetActive(false);
    }

    private static void DeleteIfFound(string name)
    {
        var target = FindInActiveScene(name);
        if (target != null)
            Object.DestroyImmediate(target.gameObject);
    }

    private static Transform FindAny(params string[] names)
    {
        foreach (var name in names)
        {
            var target = FindInActiveScene(name);
            if (target != null)
                return target;
        }

        return null;
    }

    private static Transform FindInActiveScene(string name)
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        foreach (var transform in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (transform.name == name && transform.gameObject.scene == activeScene)
                return transform;
        }

        return null;
    }

    private static ColorBlock CreateButtonColors()
    {
        return new ColorBlock
        {
            normalColor = Color.white,
            highlightedColor = new Color(0.72f, 0.95f, 1f, 1f),
            pressedColor = new Color(0.48f, 0.72f, 0.82f, 1f),
            selectedColor = Color.white,
            disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.45f),
            colorMultiplier = 1f,
            fadeDuration = 0.12f
        };
    }

    private static void Set(SerializedObject serialized, string propertyName, Object value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

}
