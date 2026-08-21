using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FpsHorrorKit
{
    public sealed class JournalInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private JournalEntryData journalEntry;
        [SerializeField] private string interactText = "[E] Đọc nhật ký";
        [SerializeField] private string missingContentMessage = "Nhật ký chưa có nội dung.";
        [SerializeField] private GramophoneTapePlayer gramophoneTapePlayer;
        [SerializeField] private JournalPaperUI paperUi;

        private Renderer[] renderers;
        private Collider[] colliders;
        private GameController.GameState previousGameState;
        private bool wasPlayerRaycastEnabled = true;
        private bool isReading;
        private bool hasBeenRead;
        private int openedFrame;

        public static bool IsAnyPaperOpen { get; private set; }

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
            ResolvePaperUi();
        }

        private void Update()
        {
            if (!isReading || Time.frameCount <= openedFrame + 1)
                return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
                ClosePaper();
        }

        public void Interact()
        {
            if (hasBeenRead || isReading)
                return;

            if (journalEntry == null)
            {
                InteractMessageScript.Instance?.ShowMessage(missingContentMessage);
                return;
            }

            OpenPaper();
        }

        public void Highlight()
        {
            if (!hasBeenRead)
                PlayerInteract.Instance?.ChangeInteractText(interactText);
        }

        public void HoldInteract() { }
        public void UnHighlight() { }

        private void OpenPaper()
        {
            ResolvePaperUi();
            if (paperUi == null)
            {
                InteractMessageScript.Instance?.ShowMessage("Chưa tìm thấy UI nhật ký trong MainGameUICanvas.");
                return;
            }

            isReading = true;
            IsAnyPaperOpen = true;
            openedFrame = Time.frameCount;
            AudioManager.Instance?.PlayPaperPickup();
            SetModelVisible(false);
            paperUi.Show(journalEntry);

            if (GameController.Instance != null)
            {
                previousGameState = GameController.Instance.currentGameState;
                GameController.Instance.SetGameState(GameController.GameState.Cutscene);
            }

            if (PlayerInteract.Instance != null)
            {
                wasPlayerRaycastEnabled = PlayerInteract.Instance.sendRaycast;
                PlayerInteract.Instance.sendRaycast = false;
            }
        }

        private void ClosePaper()
        {
            if (!isReading)
                return;

            isReading = false;
            hasBeenRead = true;
            IsAnyPaperOpen = false;
            paperUi?.Hide();

            SetModelVisible(true);
            if (GameController.Instance != null)
                GameController.Instance.SetGameState(previousGameState);
            if (PlayerInteract.Instance != null)
                PlayerInteract.Instance.sendRaycast = wasPlayerRaycastEnabled;

            AudioManager.Instance?.PlayGenericInteract();
            var tapePlayer = ResolveGramophoneTapePlayer();
            if (!global::ChapterOneStoryFlow.TryBeginAfterStudyLetter(tapePlayer))
                tapePlayer?.PlayTape();
            Destroy(this);
        }

        private void SetModelVisible(bool visible)
        {
            foreach (var itemRenderer in renderers)
            {
                if (itemRenderer != null)
                    itemRenderer.enabled = visible;
            }

            foreach (var itemCollider in colliders)
            {
                if (itemCollider != null)
                    itemCollider.enabled = visible && !hasBeenRead;
            }
        }

        private void ResolvePaperUi()
        {
            if (paperUi != null)
                return;

            var root = GameObject.Find("JournalPaperUIRoot");
            paperUi = root != null
                ? root.GetComponent<JournalPaperUI>()
                : FindFirstObjectByType<JournalPaperUI>(FindObjectsInactive.Include);
        }

        private GramophoneTapePlayer ResolveGramophoneTapePlayer()
        {
            if (gramophoneTapePlayer != null)
                return gramophoneTapePlayer;

            gramophoneTapePlayer = FindFirstObjectByType<GramophoneTapePlayer>();
            if (gramophoneTapePlayer != null)
                return gramophoneTapePlayer;

            var gramophone = GameObject.Find("gramophone");
            return gramophone != null ? gramophone.GetComponent<GramophoneTapePlayer>() : null;
        }
    }

    public sealed class JournalPaperUI : MonoBehaviour
    {
        private const string JournalPagesFolder = "Assets/MainGame/UI/UI old/NhatKy";

        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image dimImage;
        [SerializeField] private Image paperImage;
        [SerializeField] private TextMeshProUGUI contentText;
        [SerializeField] private RectTransform bookRoot;
        [SerializeField] private Image leftStackImage;
        [SerializeField] private Image rightStackImage;
        [SerializeField] private Image leftPageImage;
        [SerializeField] private Image rightPageImage;
        [SerializeField] private Image pageFlipOverlay;
        [SerializeField] private Button previousPageButton;
        [SerializeField] private Button nextPageButton;
        [SerializeField] private TextMeshProUGUI hintText;
        [SerializeField] private TextMeshProUGUI pageNumberText;
        [SerializeField] private Sprite[] journalPages = Array.Empty<Sprite>();
        [SerializeField] private Color dimColor = new(0f, 0f, 0f, 0.78f);
        [SerializeField] private Color paperColor = new(0.86f, 0.78f, 0.62f, 0.98f);
        [SerializeField] private Color inkColor = new(0.13f, 0.09f, 0.055f, 1f);
        [SerializeField] private Color pageStackColor = new(0.47f, 0.39f, 0.28f, 0.72f);
        [SerializeField] private Vector2 pageSize = new(590f, 830f);
        [SerializeField] private float pageGap = 38f;
        [SerializeField] private float pageFlipDuration = 0.28f;
        [SerializeField] private float pageFlipTilt = 8f;

        private int pageIndex;
        private bool buttonsWired;
        private bool isFlipping;
        private Coroutine pageFlipRoutine;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            ResolveReferences();
            EnsureLayout();
            ApplyStyle();
            WireButtons();
            HideImmediate();
        }

        private void OnDestroy()
        {
            UnwireButtons();
        }

        private void Update()
        {
            if (!IsOpen)
                return;

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
                PreviousPage();
            else if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
                NextPage();
        }

        public void Show(JournalEntryData entry)
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            ResolveReferences();
            EnsureLayout();
            ApplyStyle();
            WireButtons();

            if (contentText != null)
            {
                contentText.text = string.Empty;
                contentText.gameObject.SetActive(false);
            }

            if (paperImage != null)
                paperImage.gameObject.SetActive(false);

            pageIndex = 0;
            ResetPageTransforms();
            ShowCurrentPages();

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            IsOpen = true;
        }

        public void Hide()
        {
            HideImmediate();
        }

        private void HideImmediate()
        {
            IsOpen = false;
            if (pageFlipRoutine != null)
            {
                StopCoroutine(pageFlipRoutine);
                pageFlipRoutine = null;
            }

            isFlipping = false;
            ResetPageTransforms();

            if (contentText != null)
                contentText.text = string.Empty;

            ClearPage(leftPageImage);
            ClearPage(rightPageImage);
            ClearPage(leftStackImage);
            ClearPage(rightStackImage);
            ClearPage(pageFlipOverlay);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            gameObject.SetActive(false);
        }

        private void ResolveReferences()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

            if (dimImage == null)
                dimImage = transform.Find("JournalDim")?.GetComponent<Image>();

            if (paperImage == null)
                paperImage = transform.Find("JournalPaper")?.GetComponent<Image>();

            if (contentText == null)
                contentText = transform.Find("JournalPaper/Content")?.GetComponent<TextMeshProUGUI>();

            if (bookRoot == null)
                bookRoot = transform.Find("JournalBook") as RectTransform;

            if (leftStackImage == null)
                leftStackImage = transform.Find("JournalBook/LeftPageStack")?.GetComponent<Image>();

            if (rightStackImage == null)
                rightStackImage = transform.Find("JournalBook/RightPageStack")?.GetComponent<Image>();

            if (leftPageImage == null)
                leftPageImage = transform.Find("JournalBook/LeftPage")?.GetComponent<Image>();

            if (rightPageImage == null)
                rightPageImage = transform.Find("JournalBook/RightPage")?.GetComponent<Image>();

            if (pageFlipOverlay == null)
                pageFlipOverlay = transform.Find("JournalBook/PageFlipOverlay")?.GetComponent<Image>();

            if (previousPageButton == null)
                previousPageButton = transform.Find("JournalBook/PreviousPageButton")?.GetComponent<Button>();

            if (nextPageButton == null)
                nextPageButton = transform.Find("JournalBook/NextPageButton")?.GetComponent<Button>();

            if (hintText == null)
                hintText = transform.Find("JournalBook/Hint")?.GetComponent<TextMeshProUGUI>();

            if (pageNumberText == null)
                pageNumberText = transform.Find("JournalBook/PageNumber")?.GetComponent<TextMeshProUGUI>();
        }

        private void ApplyStyle()
        {
            if (dimImage != null)
                dimImage.color = dimColor;

            if (paperImage != null)
                paperImage.color = paperColor;

            if (contentText != null)
            {
                contentText.color = inkColor;
                contentText.textWrappingMode = TextWrappingModes.Normal;
                contentText.overflowMode = TextOverflowModes.Overflow;
            }

            ConfigurePageImage(leftPageImage, Color.white);
            ConfigurePageImage(rightPageImage, Color.white);
            ConfigurePageImage(leftStackImage, pageStackColor);
            ConfigurePageImage(rightStackImage, pageStackColor);
            ConfigurePageImage(pageFlipOverlay, Color.white);

            if (hintText != null)
            {
                hintText.text = "A/D hoặc ←/→ để lật trang • E để đóng";
                hintText.fontSize = 24f;
                hintText.color = new Color(0.95f, 0.88f, 0.76f, 0.85f);
                hintText.alignment = TextAlignmentOptions.Center;
                hintText.raycastTarget = false;
            }

            if (pageNumberText != null)
            {
                pageNumberText.fontSize = 22f;
                pageNumberText.color = new Color(0.95f, 0.88f, 0.76f, 0.72f);
                pageNumberText.alignment = TextAlignmentOptions.Center;
                pageNumberText.raycastTarget = false;
            }
        }

        public void PreviousPage()
        {
            TryFlipToPage(Mathf.Max(0, pageIndex - 2), -1);
        }

        public void NextPage()
        {
            TryFlipToPage(pageIndex + 2, 1);
        }

        private void TryFlipToPage(int targetIndex, int direction)
        {
            if (isFlipping || journalPages == null || journalPages.Length == 0)
                return;

            targetIndex = Mathf.Clamp(targetIndex, 0, Mathf.Max(0, journalPages.Length - 1));
            if (targetIndex == pageIndex || targetIndex >= journalPages.Length)
                return;

            AudioManager.Instance?.PlayDiaryPageFlip();

            if (!gameObject.activeInHierarchy)
            {
                pageIndex = targetIndex;
                ShowCurrentPages();
                return;
            }

            if (pageFlipRoutine != null)
                StopCoroutine(pageFlipRoutine);

            pageFlipRoutine = StartCoroutine(FlipPageRoutine(targetIndex, direction));
        }

        private void ShowCurrentPages()
        {
            var pages = journalPages ?? Array.Empty<Sprite>();
            SetPage(leftPageImage, GetPage(pages, pageIndex));
            SetPage(rightPageImage, GetPage(pages, pageIndex + 1));
            SetPage(leftStackImage, GetPage(pages, pageIndex + 2), true);
            SetPage(rightStackImage, GetPage(pages, pageIndex + 3), true);

            if (previousPageButton != null)
                previousPageButton.interactable = pageIndex > 0;

            if (nextPageButton != null)
                nextPageButton.interactable = pageIndex + 2 < pages.Length;

            if (pageNumberText != null)
            {
                if (pages.Length == 0)
                {
                    pageNumberText.text = "Chưa gán ảnh nhật ký";
                }
                else
                {
                    var lastVisiblePage = Mathf.Min(pageIndex + 2, pages.Length);
                    pageNumberText.text = $"Trang {pageIndex + 1}-{lastVisiblePage} / {pages.Length}";
                }
            }
        }

        private IEnumerator FlipPageRoutine(int targetIndex, int direction)
        {
            isFlipping = true;
            SetNavigationInteractable(false);

            var startPage = direction >= 0 ? rightPageImage : leftPageImage;
            var startSprite = startPage != null ? startPage.sprite : null;
            if (startPage == null || startSprite == null || pageFlipOverlay == null)
            {
                pageIndex = targetIndex;
                ShowCurrentPages();
                isFlipping = false;
                pageFlipRoutine = null;
                yield break;
            }

            var halfDuration = Mathf.Max(0.01f, pageFlipDuration * 0.5f);
            var startWasEnabled = startPage.enabled;
            startPage.enabled = false;

            SetupFlipOverlay(startPage, startSprite, startPage == rightPageImage);
            yield return AnimateFlipOverlay(1f, 0.02f, halfDuration);
            startPage.enabled = startWasEnabled;

            pageIndex = targetIndex;
            ShowCurrentPages();

            var endPage = direction >= 0 ? leftPageImage : rightPageImage;
            var endSprite = endPage != null ? endPage.sprite : null;
            if (endPage != null && endSprite != null)
            {
                var endWasEnabled = endPage.enabled;
                endPage.enabled = false;
                SetupFlipOverlay(endPage, endSprite, endPage == rightPageImage);
                yield return AnimateFlipOverlay(0.02f, 1f, halfDuration);
                endPage.enabled = endWasEnabled;
            }

            ClearPage(pageFlipOverlay);
            ResetPageTransform(pageFlipOverlay);
            isFlipping = false;
            pageFlipRoutine = null;
            ShowCurrentPages();
        }

        private void SetupFlipOverlay(Image sourcePage, Sprite sprite, bool pageIsRight)
        {
            if (pageFlipOverlay == null || sourcePage == null || sprite == null)
                return;

            var sourceRect = sourcePage.rectTransform;
            var overlayRect = pageFlipOverlay.rectTransform;
            var sourceSize = sourceRect.sizeDelta;
            overlayRect.anchorMin = sourceRect.anchorMin;
            overlayRect.anchorMax = sourceRect.anchorMax;
            overlayRect.sizeDelta = sourceSize;
            overlayRect.pivot = pageIsRight ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f);
            overlayRect.anchoredPosition = sourceRect.anchoredPosition + new Vector2(pageIsRight ? -sourceSize.x * 0.5f : sourceSize.x * 0.5f, 0f);
            overlayRect.localScale = Vector3.one;
            overlayRect.localRotation = Quaternion.identity;

            pageFlipOverlay.sprite = sprite;
            pageFlipOverlay.color = Color.white;
            pageFlipOverlay.preserveAspect = true;
            pageFlipOverlay.raycastTarget = false;
            pageFlipOverlay.enabled = true;
            pageFlipOverlay.transform.SetAsLastSibling();
        }

        private IEnumerator AnimateFlipOverlay(float fromScaleX, float toScaleX, float duration)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                var scaleX = Mathf.Lerp(fromScaleX, toScaleX, eased);
                if (pageFlipOverlay != null)
                    pageFlipOverlay.rectTransform.localScale = new Vector3(scaleX, 1f, 1f);

                yield return null;
            }
        }

        private void SetNavigationInteractable(bool interactable)
        {
            if (previousPageButton != null)
                previousPageButton.interactable = interactable && pageIndex > 0;

            if (nextPageButton != null)
                nextPageButton.interactable = interactable && journalPages != null && pageIndex + 2 < journalPages.Length;
        }

        private void ResetPageTransforms()
        {
            ResetPageTransform(leftPageImage);
            ResetPageTransform(rightPageImage);
            ResetPageTransform(leftStackImage);
            ResetPageTransform(rightStackImage);
            ResetPageTransform(pageFlipOverlay);
        }

        private static void ResetPageTransform(Image image)
        {
            if (image == null)
                return;

            var rect = image.rectTransform;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private void EnsureLayout()
        {
            if (bookRoot == null)
            {
                var bookObject = new GameObject("JournalBook", typeof(RectTransform));
                bookObject.transform.SetParent(transform, false);
                bookRoot = bookObject.GetComponent<RectTransform>();
            }

            bookRoot.anchorMin = new Vector2(0.5f, 0.5f);
            bookRoot.anchorMax = new Vector2(0.5f, 0.5f);
            bookRoot.pivot = new Vector2(0.5f, 0.5f);
            bookRoot.anchoredPosition = Vector2.zero;
            bookRoot.sizeDelta = new Vector2(pageSize.x * 2f + pageGap, pageSize.y);

            leftStackImage = FindOrCreateImage(bookRoot, "LeftPageStack");
            rightStackImage = FindOrCreateImage(bookRoot, "RightPageStack");
            leftPageImage = FindOrCreateImage(bookRoot, "LeftPage");
            rightPageImage = FindOrCreateImage(bookRoot, "RightPage");
            pageFlipOverlay = FindOrCreateImage(bookRoot, "PageFlipOverlay");
            previousPageButton = FindOrCreateButton(bookRoot, "PreviousPageButton");
            nextPageButton = FindOrCreateButton(bookRoot, "NextPageButton");
            hintText = FindOrCreateText(bookRoot, "Hint");
            pageNumberText = FindOrCreateText(bookRoot, "PageNumber");

            PositionPage(leftStackImage.rectTransform, -0.5f, new Vector2(-10f, -8f));
            PositionPage(rightStackImage.rectTransform, 0.5f, new Vector2(10f, -8f));
            PositionPage(leftPageImage.rectTransform, -0.5f, Vector2.zero);
            PositionPage(rightPageImage.rectTransform, 0.5f, Vector2.zero);
            PositionPage(pageFlipOverlay.rectTransform, 0.5f, Vector2.zero);
            pageFlipOverlay.enabled = false;

            StretchButton(previousPageButton.GetComponent<RectTransform>(), -0.5f);
            StretchButton(nextPageButton.GetComponent<RectTransform>(), 0.5f);

            PositionText(hintText.rectTransform, new Vector2(0f, -pageSize.y * 0.5f - 44f), new Vector2(980f, 42f));
            PositionText(pageNumberText.rectTransform, new Vector2(0f, pageSize.y * 0.5f + 34f), new Vector2(520f, 38f));

            if (paperImage != null)
                paperImage.gameObject.SetActive(false);

            if (contentText != null)
                contentText.gameObject.SetActive(false);
        }

        private void WireButtons()
        {
            if (buttonsWired)
                return;

            if (previousPageButton != null)
                previousPageButton.onClick.AddListener(PreviousPage);

            if (nextPageButton != null)
                nextPageButton.onClick.AddListener(NextPage);

            buttonsWired = true;
        }

        private void UnwireButtons()
        {
            if (!buttonsWired)
                return;

            if (previousPageButton != null)
                previousPageButton.onClick.RemoveListener(PreviousPage);

            if (nextPageButton != null)
                nextPageButton.onClick.RemoveListener(NextPage);

            buttonsWired = false;
        }

        private static Sprite GetPage(Sprite[] pages, int index)
        {
            return pages != null && index >= 0 && index < pages.Length ? pages[index] : null;
        }

        private static void SetPage(Image image, Sprite sprite, bool stackLayer = false)
        {
            if (image == null)
                return;

            image.sprite = sprite;
            image.enabled = sprite != null;
            image.preserveAspect = true;
            image.raycastTarget = false;
            if (stackLayer)
                image.transform.SetAsFirstSibling();
        }

        private static void ClearPage(Image image)
        {
            if (image == null)
                return;

            image.sprite = null;
            image.enabled = false;
        }

        private static void ConfigurePageImage(Image image, Color color)
        {
            if (image == null)
                return;

            image.color = color;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private static Image FindOrCreateImage(Transform parent, string childName)
        {
            var child = parent.Find(childName);
            var go = child != null ? child.gameObject : new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            if (child == null)
                go.transform.SetParent(parent, false);

            if (go.GetComponent<CanvasRenderer>() == null)
                go.AddComponent<CanvasRenderer>();

            return go.GetComponent<Image>() ?? go.AddComponent<Image>();
        }

        private static Button FindOrCreateButton(Transform parent, string childName)
        {
            var child = parent.Find(childName);
            var go = child != null ? child.gameObject : new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            if (child == null)
                go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;

            var button = go.GetComponent<Button>() ?? go.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        private static TextMeshProUGUI FindOrCreateText(Transform parent, string childName)
        {
            var child = parent.Find(childName);
            var go = child != null ? child.gameObject : new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            if (child == null)
                go.transform.SetParent(parent, false);

            if (go.GetComponent<CanvasRenderer>() == null)
                go.AddComponent<CanvasRenderer>();

            return go.GetComponent<TextMeshProUGUI>() ?? go.AddComponent<TextMeshProUGUI>();
        }

        private void PositionPage(RectTransform rect, float side, Vector2 offset)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = pageSize;
            rect.anchoredPosition = new Vector2(side * (pageSize.x + pageGap), 0f) + offset;
        }

        private void StretchButton(RectTransform rect, float side)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(pageSize.x, pageSize.y);
            rect.anchoredPosition = new Vector2(side * (pageSize.x + pageGap), 0f);
        }

        private static void PositionText(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            AutoAssignJournalPages();
            ResolveReferences();
            ApplyStyle();
        }

        private void AutoAssignJournalPages()
        {
            var guids = UnityEditor.AssetDatabase.FindAssets("t:Sprite", new[] { JournalPagesFolder });
            var pages = new List<Sprite>();

            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                if (path.IndexOf("/bg/", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null)
                    pages.Add(sprite);
            }

            pages.Sort(CompareJournalPages);
            journalPages = pages.ToArray();
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
#endif
    }
}
