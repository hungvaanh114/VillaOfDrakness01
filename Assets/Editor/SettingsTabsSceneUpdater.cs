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
        SetInactiveIfFound("GameplayTab");
        DeleteIfFound("ShortcutsTabButton");
        DeleteIfFound("ControlsTab");
        DeleteIfFound("ShortcutPanel");

        var settingsButton = ConfigureTab(settingsTab, "SettingsTabButton", "C\u00e0i \u0111\u1eb7t", new Vector2(0f, 0f), true);

        contentPanel.gameObject.SetActive(true);

        var controller = settingUI.GetComponent<SettingsUIController>() ?? settingUI.gameObject.AddComponent<SettingsUIController>();
        WireController(controller, contentPanel, settingsButton);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("Settings tabs were applied.");
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

    private static void WireController(
        SettingsUIController controller,
        Transform contentPanel,
        Button settingsButton)
    {
        var settingsImage = settingsButton.targetGraphic as Image;

        var serialized = new SerializedObject(controller);
        Set(serialized, "settingsPage", contentPanel.gameObject);
        Set(serialized, "settingsTabButton", settingsButton);
        Set(serialized, "settingsTabGraphic", settingsImage);
        Set(serialized, "settingsTabLabel", settingsButton.GetComponentInChildren<TMP_Text>(true));
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
