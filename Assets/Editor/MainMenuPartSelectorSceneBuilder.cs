using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MainMenuPartSelectorSceneBuilder
{
    private const string MenuScenePath = "Assets/MainGame/Menu.unity";

    [MenuItem("Tools/MainGame/Rebuild Menu Part Selector")]
    public static void RebuildMenuPartSelector()
    {
        Scene previousActiveScene = SceneManager.GetActiveScene();
        Scene menuScene = EditorSceneManager.GetSceneByPath(MenuScenePath);
        bool openedScene = false;

        if (!menuScene.IsValid() || !menuScene.isLoaded)
        {
            menuScene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Additive);
            openedScene = true;
        }

        GameObject mainPanel = FindInScene(menuScene, "MainPanel");
        GameObject menuButtons = FindInScene(menuScene, "MenuButtons");
        MainMenuController controller = FindComponentInScene<MainMenuController>(menuScene);
        if (mainPanel == null || menuButtons == null || controller == null)
        {
            Debug.LogError("Could not rebuild menu part selector: MainPanel, MenuButtons, or MainMenuController was not found.");
            return;
        }

        DestroyIfExists(menuScene, "Part1Button");
        DestroyIfExists(menuScene, "Part2Button");
        DestroyIfExists(menuScene, "PartSelectorRoot");

        RectTransform menuButtonsRect = menuButtons.GetComponent<RectTransform>();
        if (menuButtonsRect != null)
            menuButtonsRect.sizeDelta = new Vector2(menuButtonsRect.sizeDelta.x, 416.96f);

        Image templateImage = FindInScene(menuScene, "NewGameButton")?.GetComponent<Image>();
        TMP_Text templateText = FindInScene(menuScene, "NewGameButton")?.GetComponentInChildren<TMP_Text>(true);

        GameObject selectorRoot = new("PartSelectorRoot", typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(selectorRoot, menuScene);
        selectorRoot.transform.SetParent(mainPanel.transform, false);
        RectTransform rootRect = selectorRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = new Vector2(-390f, -335f);
        rootRect.sizeDelta = new Vector2(540f, 82f);

        Button previousButton = CreateArrowButton(selectorRoot.transform, "PartPreviousButton", "<", new Vector2(-245f, 0f), templateImage, templateText);
        TMP_Text label = CreateDisplayLabel(selectorRoot.transform, templateImage, templateText);
        Button nextButton = CreateArrowButton(selectorRoot.transform, "PartNextButton", ">", new Vector2(245f, 0f), templateImage, templateText);

        SerializedObject serializedController = new(controller);
        serializedController.FindProperty("partTwoSceneName").stringValue = "EndingP2Transition";
        serializedController.FindProperty("chapterCount").intValue = 2;
        serializedController.FindProperty("previousPartButton").objectReferenceValue = previousButton;
        serializedController.FindProperty("nextPartButton").objectReferenceValue = nextButton;
        serializedController.FindProperty("selectedPartText").objectReferenceValue = label;
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(menuScene);
        EditorSceneManager.SaveScene(menuScene);

        if (openedScene)
            EditorSceneManager.CloseScene(menuScene, true);

        if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            SceneManager.SetActiveScene(previousActiveScene);

        Debug.Log("Menu part selector rebuilt.");
    }

    private static Button CreateArrowButton(
        Transform parent,
        string objectName,
        string label,
        Vector2 anchoredPosition,
        Image templateImage,
        TMP_Text templateText)
    {
        GameObject buttonObject = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(70f, 62f);
        rect.anchoredPosition = anchoredPosition;

        Image image = buttonObject.GetComponent<Image>();
        CopyImageStyle(templateImage, image);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        if (templateImage != null && templateImage.TryGetComponent(out Button templateButton))
            button.colors = templateButton.colors;

        TMP_Text text = CreateLabelText(buttonObject.transform, "LabelText", label, templateText);
        text.fontSize = 34f;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return button;
    }

    private static TMP_Text CreateDisplayLabel(Transform parent, Image templateImage, TMP_Text templateText)
    {
        GameObject panelObject = new("PartSelectorInfo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelObject.transform.SetParent(parent, false);

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(390f, 62f);
        rect.anchoredPosition = Vector2.zero;

        Image image = panelObject.GetComponent<Image>();
        CopyImageStyle(templateImage, image);
        image.raycastTarget = false;

        TMP_Text text = CreateLabelText(panelObject.transform, "PartSelectorLabel", "PH\u1EA6N 1", templateText);
        text.fontSize = 30f;
        return text;
    }

    private static TMP_Text CreateLabelText(Transform parent, string objectName, string textValue, TMP_Text templateText)
    {
        GameObject textObject = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(16f, 0f);
        rect.offsetMax = new Vector2(-16f, 0f);

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        if (templateText != null)
        {
            text.font = templateText.font;
            text.fontSharedMaterial = templateText.fontSharedMaterial;
            text.fontStyle = templateText.fontStyle;
            text.color = templateText.color;
            text.characterSpacing = templateText.characterSpacing;
        }

        text.text = textValue;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static void CopyImageStyle(Image from, Image to)
    {
        if (from == null || to == null)
            return;

        to.sprite = from.sprite;
        to.type = from.type;
        to.color = from.color;
        to.pixelsPerUnitMultiplier = from.pixelsPerUnitMultiplier;
        to.raycastTarget = true;
    }

    private static void DestroyIfExists(Scene scene, string objectName)
    {
        GameObject gameObject = FindInScene(scene, objectName);
        if (gameObject != null)
            Object.DestroyImmediate(gameObject);
    }

    private static GameObject FindInScene(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindChildRecursive(root.transform, objectName);
            if (found != null)
                return found.gameObject;
        }

        return null;
    }

    private static T FindComponentInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }

        return null;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent.name == childName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChildRecursive(parent.GetChild(i), childName);
            if (found != null)
                return found;
        }

        return null;
    }
}
