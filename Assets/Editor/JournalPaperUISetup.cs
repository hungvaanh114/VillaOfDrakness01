using System.IO;
using FpsHorrorKit;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class JournalPaperUISetup
{
    private const string MenuPath = "Tools/MainGame/Create Journal Paper UI";
    private const string AutoRunFlagPath = "Library/JournalPaperUISetup.run";

    [InitializeOnLoadMethod]
    private static void RunQueuedSetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(AutoRunFlagPath))
                return;

            File.Delete(AutoRunFlagPath);
            CreateOrUpdate();
        };
    }

    [MenuItem(MenuPath)]
    public static void CreateOrUpdate()
    {
        var canvas = GameObject.Find("MainGameUICanvas")?.GetComponent<Canvas>();
        if (canvas == null)
            canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("Không tìm thấy MainGameUICanvas để tạo UI nhật ký.");
            return;
        }

        var root = FindOrCreate(canvas.transform, "JournalPaperUIRoot", typeof(CanvasGroup), typeof(JournalPaperUI));
        root.layer = LayerMask.NameToLayer("UI");
        var rootRect = root.GetComponent<RectTransform>();
        Stretch(rootRect);

        var dim = FindOrCreate(root.transform, "JournalDim", typeof(CanvasRenderer), typeof(Image));
        dim.layer = root.layer;
        Stretch(dim.GetComponent<RectTransform>());
        var dimImage = dim.GetComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.78f);
        dimImage.raycastTarget = false;

        var paper = FindOrCreate(root.transform, "JournalPaper", typeof(CanvasRenderer), typeof(Image));
        paper.layer = root.layer;
        var paperRect = paper.GetComponent<RectTransform>();
        paperRect.anchorMin = new Vector2(0.5f, 0.5f);
        paperRect.anchorMax = new Vector2(0.5f, 0.5f);
        paperRect.pivot = new Vector2(0.5f, 0.5f);
        paperRect.anchoredPosition = Vector2.zero;
        paperRect.sizeDelta = new Vector2(980f, 720f);
        var paperImage = paper.GetComponent<Image>();
        paperImage.color = new Color(0.86f, 0.78f, 0.62f, 0.98f);
        paperImage.raycastTarget = false;

        var content = FindOrCreate(paper.transform, "Content", typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        content.layer = root.layer;
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.1f, 0.12f);
        contentRect.anchorMax = new Vector2(0.9f, 0.88f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;
        var contentText = content.GetComponent<TextMeshProUGUI>();
        contentText.text = "";
        contentText.fontSize = 30f;
        contentText.color = new Color(0.13f, 0.09f, 0.055f, 1f);
        contentText.alignment = TextAlignmentOptions.TopLeft;
        contentText.textWrappingMode = TextWrappingModes.Normal;
        contentText.overflowMode = TextOverflowModes.Overflow;
        contentText.raycastTarget = false;

        var hint = FindOrCreate(paper.transform, "Hint", typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        hint.layer = root.layer;
        var hintRect = hint.GetComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0.1f, 0.04f);
        hintRect.anchorMax = new Vector2(0.9f, 0.1f);
        hintRect.offsetMin = Vector2.zero;
        hintRect.offsetMax = Vector2.zero;
        var hintText = hint.GetComponent<TextMeshProUGUI>();
        hintText.text = "Nhấn E để đóng";
        hintText.fontSize = 22f;
        hintText.color = new Color(0.13f, 0.09f, 0.055f, 0.72f);
        hintText.alignment = TextAlignmentOptions.Center;
        hintText.raycastTarget = false;

        var canvasGroup = root.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        var journalUi = root.GetComponent<JournalPaperUI>();
        var serialized = new SerializedObject(journalUi);
        Set(serialized, "canvasGroup", canvasGroup);
        Set(serialized, "dimImage", dimImage);
        Set(serialized, "paperImage", paperImage);
        Set(serialized, "contentText", contentText);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        root.SetActive(false);
        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("Đã tạo/cập nhật JournalPaperUIRoot trong MainGameUICanvas.");
    }

    private static GameObject FindOrCreate(Transform parent, string name, params System.Type[] components)
    {
        var child = parent.Find(name);
        GameObject go;
        if (child != null)
        {
            go = child.gameObject;
        }
        else
        {
            go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            go.transform.SetParent(parent, false);
        }

        foreach (var component in components)
        {
            if (go.GetComponent(component) == null)
                go.AddComponent(component);
        }

        return go;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private static void Set(SerializedObject serialized, string propertyName, Object value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }
}
