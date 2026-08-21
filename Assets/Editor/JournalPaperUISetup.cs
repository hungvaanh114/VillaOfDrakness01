using System;
using System.Collections.Generic;
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
    private const string JournalPagesFolder = "Assets/MainGame/UI/UI old/NhatKy";

    private static readonly Vector2 PageSize = new(590f, 830f);
    private const float PageGap = 38f;

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
            canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
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

        var legacyPaper = FindOrCreate(root.transform, "JournalPaper", typeof(CanvasRenderer), typeof(Image));
        legacyPaper.layer = root.layer;
        legacyPaper.SetActive(false);
        var legacyPaperImage = legacyPaper.GetComponent<Image>();

        var legacyContent = FindOrCreate(legacyPaper.transform, "Content", typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        legacyContent.layer = root.layer;
        legacyContent.SetActive(false);
        var legacyContentText = legacyContent.GetComponent<TextMeshProUGUI>();
        legacyContentText.text = string.Empty;

        var book = FindOrCreate(root.transform, "JournalBook", typeof(RectTransform));
        book.layer = root.layer;
        var bookRect = book.GetComponent<RectTransform>();
        bookRect.anchorMin = new Vector2(0.5f, 0.5f);
        bookRect.anchorMax = new Vector2(0.5f, 0.5f);
        bookRect.pivot = new Vector2(0.5f, 0.5f);
        bookRect.anchoredPosition = Vector2.zero;
        bookRect.sizeDelta = new Vector2(PageSize.x * 2f + PageGap, PageSize.y);

        var leftStack = CreatePageImage(book.transform, "LeftPageStack", new Color(0.47f, 0.39f, 0.28f, 0.72f));
        var rightStack = CreatePageImage(book.transform, "RightPageStack", new Color(0.47f, 0.39f, 0.28f, 0.72f));
        var leftPage = CreatePageImage(book.transform, "LeftPage", Color.white);
        var rightPage = CreatePageImage(book.transform, "RightPage", Color.white);
        var pageFlipOverlay = CreatePageImage(book.transform, "PageFlipOverlay", Color.white);
        PositionPage(leftStack.rectTransform, -0.5f, new Vector2(-10f, -8f));
        PositionPage(rightStack.rectTransform, 0.5f, new Vector2(10f, -8f));
        PositionPage(leftPage.rectTransform, -0.5f, Vector2.zero);
        PositionPage(rightPage.rectTransform, 0.5f, Vector2.zero);
        PositionPage(pageFlipOverlay.rectTransform, 0.5f, Vector2.zero);
        pageFlipOverlay.enabled = false;

        var previousButton = CreatePageButton(book.transform, "PreviousPageButton");
        var nextButton = CreatePageButton(book.transform, "NextPageButton");
        PositionButton(previousButton.GetComponent<RectTransform>(), -0.5f);
        PositionButton(nextButton.GetComponent<RectTransform>(), 0.5f);

        var hintText = CreateText(book.transform, "Hint");
        PositionText(hintText.rectTransform, new Vector2(0f, -PageSize.y * 0.5f - 44f), new Vector2(980f, 42f));
        hintText.text = "A/D hoặc ←/→ để lật trang • E để đóng";
        hintText.fontSize = 24f;
        hintText.color = new Color(0.95f, 0.88f, 0.76f, 0.85f);
        hintText.alignment = TextAlignmentOptions.Center;
        hintText.raycastTarget = false;

        var pageNumberText = CreateText(book.transform, "PageNumber");
        PositionText(pageNumberText.rectTransform, new Vector2(0f, PageSize.y * 0.5f + 34f), new Vector2(520f, 38f));
        pageNumberText.text = "Trang 1-2";
        pageNumberText.fontSize = 22f;
        pageNumberText.color = new Color(0.95f, 0.88f, 0.76f, 0.72f);
        pageNumberText.alignment = TextAlignmentOptions.Center;
        pageNumberText.raycastTarget = false;

        var canvasGroup = root.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        var pages = LoadJournalSprites();
        var journalUi = root.GetComponent<JournalPaperUI>();
        var serialized = new SerializedObject(journalUi);
        Set(serialized, "canvasGroup", canvasGroup);
        Set(serialized, "dimImage", dimImage);
        Set(serialized, "paperImage", legacyPaperImage);
        Set(serialized, "contentText", legacyContentText);
        Set(serialized, "bookRoot", bookRect);
        Set(serialized, "leftStackImage", leftStack);
        Set(serialized, "rightStackImage", rightStack);
        Set(serialized, "leftPageImage", leftPage);
        Set(serialized, "rightPageImage", rightPage);
        Set(serialized, "pageFlipOverlay", pageFlipOverlay);
        Set(serialized, "previousPageButton", previousButton);
        Set(serialized, "nextPageButton", nextButton);
        Set(serialized, "hintText", hintText);
        Set(serialized, "pageNumberText", pageNumberText);
        SetArray(serialized, "journalPages", pages);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        root.SetActive(false);
        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"Đã tạo/cập nhật JournalPaperUIRoot bằng {pages.Count} ảnh nhật ký.");
    }

    private static Image CreatePageImage(Transform parent, string name, Color color)
    {
        var go = FindOrCreate(parent, name, typeof(CanvasRenderer), typeof(Image));
        go.layer = parent.gameObject.layer;
        var image = go.GetComponent<Image>();
        image.color = color;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }

    private static Button CreatePageButton(Transform parent, string name)
    {
        var go = FindOrCreate(parent, name, typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.layer = parent.gameObject.layer;
        var image = go.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true;
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name)
    {
        var go = FindOrCreate(parent, name, typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.layer = parent.gameObject.layer;
        return go.GetComponent<TextMeshProUGUI>();
    }

    private static GameObject FindOrCreate(Transform parent, string name, params Type[] components)
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

    private static void PositionPage(RectTransform rect, float side, Vector2 offset)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = PageSize;
        rect.anchoredPosition = new Vector2(side * (PageSize.x + PageGap), 0f) + offset;
    }

    private static void PositionButton(RectTransform rect, float side)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = PageSize;
        rect.anchoredPosition = new Vector2(side * (PageSize.x + PageGap), 0f);
    }

    private static void PositionText(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Set(SerializedObject serialized, string propertyName, UnityEngine.Object value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void SetArray(SerializedObject serialized, string propertyName, IReadOnlyList<Sprite> sprites)
    {
        var property = serialized.FindProperty(propertyName);
        if (property == null)
            return;

        property.arraySize = sprites.Count;
        for (var i = 0; i < sprites.Count; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
    }

    private static List<Sprite> LoadJournalSprites()
    {
        var guids = AssetDatabase.FindAssets("t:Sprite", new[] { JournalPagesFolder });
        var sprites = new List<Sprite>();

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
            if (path.IndexOf("/bg/", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
                sprites.Add(sprite);
        }

        sprites.Sort(CompareJournalPages);
        return sprites;
    }

    private static int CompareJournalPages(Sprite left, Sprite right)
    {
        var leftKey = ParseJournalPageName(left != null ? left.name : string.Empty);
        var rightKey = ParseJournalPageName(right != null ? right.name : string.Empty);
        return leftKey.CompareTo(rightKey);
    }

    private readonly struct JournalPageSortKey : IComparable<JournalPageSortKey>
    {
        private readonly int year;
        private readonly int month;
        private readonly int day;
        private readonly int page;
        private readonly string fallbackName;

        public JournalPageSortKey(int year, int month, int day, int page, string fallbackName)
        {
            this.year = year;
            this.month = month;
            this.day = day;
            this.page = page;
            this.fallbackName = fallbackName;
        }

        public int CompareTo(JournalPageSortKey other)
        {
            var result = year.CompareTo(other.year);
            if (result != 0)
                return result;

            result = month.CompareTo(other.month);
            if (result != 0)
                return result;

            result = day.CompareTo(other.day);
            if (result != 0)
                return result;

            result = page.CompareTo(other.page);
            return result != 0 ? result : string.Compare(fallbackName, other.fallbackName, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static JournalPageSortKey ParseJournalPageName(string pageName)
    {
        var fallback = new JournalPageSortKey(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, pageName);
        var dateParts = pageName.Split('-');
        if (dateParts.Length < 3)
            return fallback;

        if (!int.TryParse(dateParts[0], out var day) || !int.TryParse(dateParts[1], out var month))
            return fallback;

        var yearAndPage = dateParts[2].Split('.');
        if (yearAndPage.Length < 2 || !int.TryParse(yearAndPage[0], out var year) || !int.TryParse(yearAndPage[1], out var page))
            return fallback;

        return new JournalPageSortKey(year, month, day, page, pageName);
    }
}
