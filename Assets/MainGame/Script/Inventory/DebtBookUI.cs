using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FpsHorrorKit
{
    public sealed class DebtBookUI : MonoBehaviour
    {
        public const string DebtBookItemId = "Item_DebtBook";

        public static DebtBookUI Instance { get; private set; }
        public static bool IsAnyOpen => Instance != null && Instance.IsOpen;

        [Header("Sprites")]
        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private Sprite pageSprite;
        [SerializeField] private Sprite closeButtonSprite;

        [Header("Fonts")]
        [SerializeField] private TMP_FontAsset titleFont;
        [SerializeField] private TMP_FontAsset bodyFont;

        private GameObject root;
        private Image pageImage;
        private bool hasOpened;

        public bool IsOpen => root != null && root.activeSelf;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            ConfigureHostRect();
#if UNITY_EDITOR
            AutoAssignSpritesInEditor();
#endif
        }

        private void Start()
        {
            Build();
            if (!hasOpened)
            {
                if (root != null)
                    root.SetActive(false);
                SetGameplayBlocked(false);
            }
        }

        private void Update()
        {
            if (!IsOpen)
                return;

            if (GameController.IsCutsceneOrEndInputLocked())
            {
                Close();
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.escapeKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame || keyboard.tabKey.wasPressedThisFrame)
                Close();
        }

        public static bool IsDebtBook(ItemData item)
        {
            return item != null && string.Equals(item.id, DebtBookItemId, System.StringComparison.OrdinalIgnoreCase);
        }

        public static bool Open(ItemData item)
        {
            if (item == null)
                return false;

            var ui = EnsureInstance();
            if (ui == null)
                return false;

            ui.Show(item);
            return true;
        }

        public void Show(ItemData item)
        {
            if (GameController.IsCutsceneOrEndInputLocked())
                return;

            Build();
            if (InventoryUI.Instance != null && InventoryUI.Instance.IsOpen)
                InventoryUI.Instance.Close();

            if (pageImage != null)
            {
                pageImage.sprite = item != null && item.icon != null ? item.icon : pageSprite;
                pageImage.enabled = pageImage.sprite != null;
                pageImage.preserveAspect = false;
            }

            AudioManager.Instance?.PlayButtonClick();
            hasOpened = true;
            root.SetActive(true);
            SetGameplayBlocked(true);
        }

        public void Close()
        {
            if (root != null && root.activeSelf)
                AudioManager.Instance?.PlayBack();

            if (root != null)
                root.SetActive(false);

            SetGameplayBlocked(false);
        }

        private static DebtBookUI EnsureInstance()
        {
            if (Instance != null)
            {
                Instance.ConfigureHostRect();
                return Instance;
            }

            var existing = FindFirstObjectByType<DebtBookUI>(FindObjectsInactive.Include);
            if (existing != null)
            {
                existing.ConfigureHostRect();
                return existing;
            }

            var canvas = FindMainCanvas();
            var obj = new GameObject("DebtBookUI", typeof(RectTransform));
            obj.transform.SetParent(canvas.transform, false);
            var ui = obj.AddComponent<DebtBookUI>();
            ui.ConfigureHostRect();
            return ui;
        }

        private static Canvas FindMainCanvas()
        {
            var named = GameObject.Find("MainGameUICanvas");
            if (named != null && named.TryGetComponent(out Canvas namedCanvas))
                return namedCanvas;

            var existing = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (existing != null)
                return existing;

            var obj = new GameObject("MainGameUICanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = obj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = obj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            return canvas;
        }

        private void SetGameplayBlocked(bool blocked)
        {
            if (blocked)
            {
                if (PlayerInteract.Instance != null) PlayerInteract.Instance.sendRaycast = false;
                if (GameController.Instance != null) GameController.Instance.SetGameState(GameController.GameState.Puzzle);
                else InteractCameraSettings.Instance?.ShowCursor();
                return;
            }

            if (GameController.Instance != null && GameController.Instance.currentGameState == GameController.GameState.Puzzle && !PianoPuzzleUI.IsAnyOpen && !(InventoryUI.Instance != null && InventoryUI.Instance.IsOpen))
                GameController.Instance.SetGameState(GameController.GameState.Gameplay);

            if (PlayerInteract.Instance != null) PlayerInteract.Instance.sendRaycast = true;
            if (GameController.Instance == null)
                InteractCameraSettings.Instance?.HideCursor();
        }

        private void Build()
        {
            ConfigureHostRect();
            if (root != null)
            {
                Stretch(root.GetComponent<RectTransform>());
                return;
            }

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                canvas = gameObject.AddComponent<Canvas>();

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 96;
            if (GetComponent<CanvasScaler>() == null)
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }
            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            root = CreateUIObject(transform, "DebtBookOverlay").gameObject;
            Stretch(root.GetComponent<RectTransform>());
            AddImage(root.transform, "DimBackground", null, new Color(0f, 0f, 0f, 0.78f), Image.Type.Simple, true);

            var bg = AddImage(root.transform, "DebtBookBackground", backgroundSprite, new Color(1f, 1f, 1f, 0.72f), Image.Type.Simple, false);
            Stretch(bg.rectTransform);
            bg.preserveAspect = false;
            bg.raycastTarget = false;

            pageImage = AddImage(root.transform, "DebtBookPage", pageSprite, Color.white, Image.Type.Simple, false);
            Stretch(pageImage.rectTransform);
            pageImage.preserveAspect = false;
            pageImage.raycastTarget = false;

            AddText(root.transform, "Hint", "E / TAB / ESC - Đóng", 22, new Color(0.92f, 0.88f, 0.78f), TextAlignmentOptions.Center, new Vector2(0.24f, 0.02f), new Vector2(0.76f, 0.07f));

            var close = CreateButton(root.transform, "CloseButton", closeButtonSprite, "X", 34);
            var closeRect = close.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.sizeDelta = new Vector2(70f, 70f);
            closeRect.anchoredPosition = new Vector2(-64f, -56f);
            close.onClick.AddListener(Close);
        }

        private void ConfigureHostRect()
        {
            if (transform is not RectTransform rect)
                return;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private Button CreateButton(Transform parent, string name, Sprite sprite, string label, int fontSize)
        {
            var image = AddImage(parent, name, sprite, new Color(0.06f, 0.025f, 0.02f, 0.9f), Image.Type.Sliced, false);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            AddText(image.transform, "Text", label, fontSize, Color.white, TextAlignmentOptions.Center, Vector2.zero, Vector2.one);
            return button;
        }

        private TextMeshProUGUI AddText(Transform parent, string name, string text, int size, Color color, TextAlignmentOptions align, Vector2 min, Vector2 max)
        {
            var rect = CreateUIObject(parent, name);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = align;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            if (bodyFont != null) label.font = bodyFont;
            if (size >= 32 && titleFont != null) label.font = titleFont;
            SetRect(rect, min, max, Vector2.zero, Vector2.zero);
            return label;
        }

        private Image AddImage(Transform parent, string name, Sprite sprite, Color color, Image.Type type, bool stretch)
        {
            var rect = CreateUIObject(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = sprite != null ? type : Image.Type.Simple;
            if (stretch) Stretch(rect);
            return image;
        }

        private static RectTransform CreateUIObject(Transform parent, string name)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
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

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                if (EventSystem.current.GetComponent<InputSystemUIInputModule>() == null)
                    EventSystem.current.gameObject.AddComponent<InputSystemUIInputModule>();
                return;
            }

            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

#if UNITY_EDITOR
        private void AutoAssignSpritesInEditor()
        {
            pageSprite ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/MainGame/UI/UI old/SoNo/sổ nợ.png");
            backgroundSprite ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/MainGame/UI/UI old/SoNo/sổ nợbg.png");
        }
#endif
    }
}
