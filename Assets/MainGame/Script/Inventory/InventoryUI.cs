using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FpsHorrorKit
{
    public sealed class InventoryUI : MonoBehaviour
    {
        private enum InventoryTab
        {
            Inventory,
            Music
        }

        public static InventoryUI Instance { get; private set; }

        [Header("Sprites")]
        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private Sprite panelSprite;
        [SerializeField] private Sprite infoPanelSprite;
        [SerializeField] private Sprite slotSprite;
        [SerializeField] private Sprite selectedSlotSprite;
        [SerializeField] private Sprite tabSprite;
        [SerializeField] private Sprite selectedTabSprite;
        [SerializeField] private Sprite closeSprite;
        [SerializeField] private Sprite dividerSprite;
        [SerializeField] private Sprite keycapSprite;
        [SerializeField] private Sprite unknownMusicSprite;
        [SerializeField] private TMP_FontAsset titleFont;
        [SerializeField] private TMP_FontAsset bodyFont;

        [Header("Defaults")]
        [SerializeField] private Sprite flashlightFallbackIcon;
        [SerializeField] private Sprite keyFallbackIcon;

        private readonly List<InventorySlotUI> inventorySlots = new();
        private readonly List<Image> musicIcons = new();
        private readonly List<Button> tabButtons = new();
        private readonly List<TextMeshProUGUI> tabTexts = new();

        private GameObject root;
        private GameObject inventoryTabRoot;
        private GameObject musicTabRoot;
        private Image detailIcon;
        private TextMeshProUGUI detailName;
        private TextMeshProUGUI detailDescription;
        private TextMeshProUGUI detailAmount;
        private TextMeshProUGUI detailEquipped;
        private Button useButton;
        private TextMeshProUGUI useButtonText;
        private TextMeshProUGUI musicProgressText;
        private InventoryTab currentTab;
        private ItemData selectedItem;

        public bool IsOpen => root != null && root.activeSelf;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            Build();
            Subscribe();
            Close();
        }

        private void OnDestroy()
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnInventoryChanged -= RefreshInventory;
                InventoryManager.Instance.OnItemEquipped -= OnItemEquipped;
            }
            if (MusicSheetManager.Instance != null)
            {
                MusicSheetManager.Instance.OnMusicSheetCollected -= OnMusicSheetCollected;
                MusicSheetManager.Instance.OnMusicSheetCompleted -= RefreshMusic;
            }
        }

        private void Update()
        {
            if (GameController.IsCutsceneOrEndInputLocked())
            {
                if (IsOpen)
                    Close();
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.tabKey.wasPressedThisFrame)
                Toggle();

            if (!IsOpen)
                return;

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            if (keyboard.qKey.wasPressedThisFrame) ChangeTab(-1);
            if (keyboard.eKey.wasPressedThisFrame) ChangeTab(1);
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void OpenJournal()
        {
            Open();
        }

        public void Open()
        {
            if (!CanOpenFromCurrentState())
                return;

            Build();
            AudioManager.Instance?.PlayButtonClick();
            root.SetActive(true);
            SetTab(currentTab);
            RefreshAll();
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

        public void SelectInventoryItem(ItemData item)
        {
            selectedItem = item;
            AudioManager.Instance?.PlayButtonClick();
            InventoryManager.Instance?.SelectItem(item);
            RefreshInventory();
        }

        public void UseInventoryItem(ItemData item)
        {
            if (item == null)
                return;

            selectedItem = item;
            InventoryManager.Instance?.UseItem(item);
            RefreshInventory();
        }

        private void Subscribe()
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnInventoryChanged += RefreshInventory;
                InventoryManager.Instance.OnItemEquipped += OnItemEquipped;
            }
            if (MusicSheetManager.Instance != null)
            {
                MusicSheetManager.Instance.OnMusicSheetCollected += OnMusicSheetCollected;
                MusicSheetManager.Instance.OnMusicSheetCompleted += RefreshMusic;
            }
        }

        private void OnMusicSheetCollected(MusicSheetData sheet)
        {
            RefreshMusic();
        }

        private void OnItemEquipped(ItemData item)
        {
            RefreshInventory();
        }

        private void ChangeTab(int direction)
        {
            int next = ((int)currentTab + direction + 2) % 2;
            SetTab((InventoryTab)next);
        }

        private void SetTab(InventoryTab tab)
        {
            currentTab = tab;
            if (IsOpen)
                AudioManager.Instance?.PlayButtonClick();
            if (inventoryTabRoot != null) inventoryTabRoot.SetActive(tab == InventoryTab.Inventory);
            if (musicTabRoot != null) musicTabRoot.SetActive(tab == InventoryTab.Music);

            for (int i = 0; i < tabButtons.Count; i++)
            {
                var image = tabButtons[i].GetComponent<Image>();
                bool selected = i == (int)tab;
                if (image != null) image.sprite = selected && selectedTabSprite != null ? selectedTabSprite : tabSprite;
                if (tabTexts[i] != null) tabTexts[i].color = selected ? new Color(0.86f, 0.96f, 1f) : new Color(0.62f, 0.72f, 0.78f);
            }

            RefreshAll();
        }

        private void RefreshAll()
        {
            RefreshInventory();
            RefreshMusic();
        }

        private void RefreshInventory()
        {
            var manager = InventoryManager.Instance;
            IReadOnlyList<InventoryItem> items = manager != null ? manager.Items : new List<InventoryItem>();
            if (selectedItem == null && items.Count > 0)
                selectedItem = items[0].Data;

            for (int i = 0; i < inventorySlots.Count; i++)
            {
                InventoryItem item = i < items.Count ? items[i] : null;
                inventorySlots[i].SetItem(item, item != null && item.Data == selectedItem);
            }

            var selectedStack = selectedItem != null && manager != null ? manager.Find(selectedItem) : null;
            bool hasSelected = selectedStack != null;
            if (detailName != null) detailName.text = hasSelected ? selectedItem.itemName : "Chưa chọn vật phẩm";
            if (detailDescription != null) detailDescription.text = hasSelected ? selectedItem.description : "Chọn một ô trong hành trang để xem thông tin.";
            if (detailAmount != null) detailAmount.text = hasSelected ? $"Số lượng: {selectedStack.Amount}" : "";
            if (detailIcon != null)
            {
                detailIcon.enabled = hasSelected;
                detailIcon.sprite = hasSelected ? ResolveIcon(selectedItem) : null;
            }
            if (detailEquipped != null)
            {
                bool equipped = hasSelected && manager.CurrentEquippedItem == selectedItem;
                detailEquipped.text = equipped ? "Đang cầm" : "";
            }
            if (useButton != null)
            {
                useButton.gameObject.SetActive(hasSelected && selectedItem.canUse);
                if (useButtonText != null)
                {
                    if (DebtBookUI.IsDebtBook(selectedItem))
                        useButtonText.text = "ĐỌC";
                    else
                        useButtonText.text = selectedItem != null && selectedItem.itemType == ItemType.Key ? "TRANG BỊ" : "SỬ DỤNG";
                }
            }
        }

        private void RefreshMusic()
        {
            var manager = MusicSheetManager.Instance;
            var sheets = manager != null ? manager.Sheets : new List<MusicSheetData>();
            int total = manager != null ? manager.RequiredMusicSheetCount : 5;
            EnsureMusicSlotCount(total);
            for (int i = 0; i < musicIcons.Count; i++)
            {
                var sheet = i < sheets.Count ? sheets[i] : null;
                bool collected = sheet != null && manager != null && manager.IsCollected(sheet);
                musicIcons[i].sprite = collected && sheet.icon != null ? sheet.icon : unknownMusicSprite;
                musicIcons[i].color = collected ? Color.white : new Color(0.2f, 0.24f, 0.27f, 0.82f);
            }
            if (musicProgressText != null)
            {
                int count = manager != null ? manager.CollectedMusicSheetCount : 0;
                musicProgressText.text = $"{count} / {total}";
            }
        }

        private void UseSelectedItem()
        {
            UseInventoryItem(selectedItem);
        }

        private Sprite ResolveIcon(ItemData item)
        {
            if (item == null)
                return null;
            if (item.icon != null)
                return item.icon;
            if (item.itemType == ItemType.Flashlight)
                return flashlightFallbackIcon;
            if (item.itemType == ItemType.Key)
                return keyFallbackIcon;
            return null;
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

            if (GameController.Instance != null && GameController.Instance.currentGameState == GameController.GameState.Puzzle && !PianoPuzzleUI.IsAnyOpen)
                GameController.Instance.SetGameState(GameController.GameState.Gameplay);
            if (PlayerInteract.Instance != null) PlayerInteract.Instance.sendRaycast = true;
            if (GameController.Instance == null)
                InteractCameraSettings.Instance?.HideCursor();
        }

        private bool CanOpenFromCurrentState()
        {
            var controller = GameController.Instance;
            return controller == null || controller.CanUseGameplayInput();
        }

        private void Build()
        {
            if (root != null)
                return;

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            if (GetComponent<CanvasScaler>() == null)
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }
            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            root = CreateUIObject(transform, "InventoryOverlay").gameObject;
            Stretch(root.GetComponent<RectTransform>());
            AddImage(root.transform, "DimBackground", backgroundSprite, new Color(0.01f, 0.035f, 0.055f, 0.84f), Image.Type.Sliced, true);

            BuildTabs(root.transform);
            inventoryTabRoot = CreateUIObject(root.transform, "InventoryTab").gameObject;
            musicTabRoot = CreateUIObject(root.transform, "MusicSheetTab").gameObject;
            Stretch(inventoryTabRoot.GetComponent<RectTransform>());
            Stretch(musicTabRoot.GetComponent<RectTransform>());

            BuildInventoryTab(inventoryTabRoot.transform);
            BuildMusicTab(musicTabRoot.transform);
            BuildFooter(root.transform);
        }

        private void BuildTabs(Transform parent)
        {
            string[] labels = { "HÀNH TRANG", "MẢNH NỐT NHẠC" };
            for (int i = 0; i < labels.Length; i++)
            {
                var button = CreateButton(parent, $"Tab_{i + 1}", tabSprite, labels[i], 30);
                var rect = button.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(i == 1 ? 360f : 300f, 76f);
                rect.anchoredPosition = new Vector2((i - 0.5f) * 380f, -70f);
                int capture = i;
                button.onClick.AddListener(() => SetTab((InventoryTab)capture));
                tabButtons.Add(button);
                tabTexts.Add(button.GetComponentInChildren<TextMeshProUGUI>());
            }

            var closeButton = CreateButton(parent, "CloseButton", closeSprite, "X", 42);
            var closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.sizeDelta = new Vector2(72f, 72f);
            closeRect.anchoredPosition = new Vector2(-70f, -62f);
            closeButton.onClick.AddListener(Close);
        }

        private void BuildInventoryTab(Transform parent)
        {
            var gridPanel = AddImage(parent, "GridPanel", panelSprite, new Color(0.02f, 0.07f, 0.1f, 0.58f), Image.Type.Sliced, false);
            SetRect(gridPanel.rectTransform, new Vector2(0.08f, 0.12f), new Vector2(0.62f, 0.78f), Vector2.zero, Vector2.zero);

            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    var slot = CreateButton(gridPanel.transform, $"Slot_{row}_{col}", slotSprite, "", 18);
                    var rect = slot.GetComponent<RectTransform>();
                    rect.anchorMin = new Vector2(0f, 1f);
                    rect.anchorMax = new Vector2(0f, 1f);
                    rect.pivot = new Vector2(0f, 1f);
                    rect.sizeDelta = new Vector2(202f, 172f);
                    rect.anchoredPosition = new Vector2(52f + col * 224f, -58f - row * 224f);
                    var icon = AddImage(slot.transform, "Icon", null, Color.white, Image.Type.Simple, false);
                    SetRect(icon.rectTransform, new Vector2(0.16f, 0.24f), new Vector2(0.84f, 0.84f), Vector2.zero, Vector2.zero);
                    AddText(slot.transform, "Name", "", 20, new Color(0.82f, 0.86f, 0.88f), TextAlignmentOptions.Center, new Vector2(0f, 0f), new Vector2(1f, 0.22f));
                    AddText(slot.transform, "Amount", "", 18, new Color(0.7f, 0.9f, 1f), TextAlignmentOptions.Right, new Vector2(0.58f, 0.02f), new Vector2(0.94f, 0.22f));
                    var slotUI = slot.gameObject.AddComponent<InventorySlotUI>();
                    slotUI.Setup(this, slotSprite, selectedSlotSprite);
                    inventorySlots.Add(slotUI);
                }
            }

            var detail = AddImage(parent, "DetailPanel", infoPanelSprite, new Color(0.018f, 0.06f, 0.09f, 0.68f), Image.Type.Sliced, false);
            SetRect(detail.rectTransform, new Vector2(0.67f, 0.12f), new Vector2(0.93f, 0.78f), Vector2.zero, Vector2.zero);
            detailName = AddText(detail.transform, "DetailName", "ĐÈN PIN", 32, new Color(0.85f, 0.95f, 1f), TextAlignmentOptions.Center, new Vector2(0.08f, 0.84f), new Vector2(0.92f, 0.94f));
            AddImage(detail.transform, "TopDivider", dividerSprite, new Color(0.68f, 0.85f, 0.95f, 0.7f), Image.Type.Sliced, false).rectTransform.sizeDelta = new Vector2(260f, 12f);
            detailIcon = AddImage(detail.transform, "DetailIcon", null, Color.white, Image.Type.Simple, false);
            SetRect(detailIcon.rectTransform, new Vector2(0.18f, 0.42f), new Vector2(0.82f, 0.78f), Vector2.zero, Vector2.zero);
            detailAmount = AddText(detail.transform, "DetailAmount", "", 20, new Color(0.7f, 0.84f, 0.9f), TextAlignmentOptions.Center, new Vector2(0.1f, 0.33f), new Vector2(0.9f, 0.39f));
            detailEquipped = AddText(detail.transform, "EquippedText", "", 22, new Color(0.6f, 0.9f, 1f), TextAlignmentOptions.Center, new Vector2(0.1f, 0.28f), new Vector2(0.9f, 0.34f));
            detailDescription = AddText(detail.transform, "DetailDescription", "", 22, new Color(0.78f, 0.82f, 0.84f), TextAlignmentOptions.TopLeft, new Vector2(0.13f, 0.14f), new Vector2(0.87f, 0.31f));
            useButton = CreateButton(detail.transform, "UseButton", selectedTabSprite != null ? selectedTabSprite : tabSprite, "SỬ DỤNG", 24);
            var useRect = useButton.GetComponent<RectTransform>();
            useRect.anchorMin = new Vector2(0.28f, 0.04f);
            useRect.anchorMax = new Vector2(0.72f, 0.12f);
            useRect.offsetMin = Vector2.zero;
            useRect.offsetMax = Vector2.zero;
            useButtonText = useButton.GetComponentInChildren<TextMeshProUGUI>();
            useButton.onClick.AddListener(UseSelectedItem);
        }

        private void BuildMusicTab(Transform parent)
        {
            var panel = AddImage(parent, "MusicPanel", panelSprite, new Color(0.02f, 0.07f, 0.1f, 0.58f), Image.Type.Sliced, false);
            SetRect(panel.rectTransform, new Vector2(0.14f, 0.16f), new Vector2(0.86f, 0.74f), Vector2.zero, Vector2.zero);
            AddText(panel.transform, "MusicTitle", "MẢNH NỐT NHẠC", 38, new Color(0.85f, 0.95f, 1f), TextAlignmentOptions.Center, new Vector2(0.1f, 0.83f), new Vector2(0.9f, 0.94f));
            musicProgressText = AddText(panel.transform, "MusicProgress", "0 / 4", 30, new Color(0.68f, 0.9f, 1f), TextAlignmentOptions.Center, new Vector2(0.42f, 0.73f), new Vector2(0.58f, 0.82f));

            EnsureMusicSlotCount(5);
        }

        private void EnsureMusicSlotCount(int total)
        {
            if (musicProgressText == null)
                return;

            var panel = musicProgressText.transform.parent;
            if (panel == null)
                return;

            total = Mathf.Max(1, total);
            while (musicIcons.Count < total)
            {
                int index = musicIcons.Count;
                var frame = AddImage(panel, $"MusicSlot_{index + 1}", slotSprite, new Color(0.03f, 0.07f, 0.1f, 0.82f), Image.Type.Sliced, false);
                var icon = AddImage(frame.transform, "MusicIcon", unknownMusicSprite, Color.white, Image.Type.Simple, false);
                SetRect(icon.rectTransform, new Vector2(0.1f, 0.13f), new Vector2(0.9f, 0.87f), Vector2.zero, Vector2.zero);
                musicIcons.Add(icon);
            }

            float gap = 0.025f;
            float slotWidth = Mathf.Min(0.16f, (0.84f - gap * (total - 1)) / total);
            float totalWidth = slotWidth * total + gap * (total - 1);
            float start = 0.5f - totalWidth * 0.5f;
            for (int i = 0; i < musicIcons.Count; i++)
            {
                var frame = musicIcons[i].transform.parent as RectTransform;
                if (frame == null)
                    continue;

                bool active = i < total;
                frame.gameObject.SetActive(active);
                if (!active)
                    continue;

                float min = start + i * (slotWidth + gap);
                frame.anchorMin = new Vector2(min, 0.22f);
                frame.anchorMax = new Vector2(min + slotWidth, 0.62f);
                frame.offsetMin = Vector2.zero;
                frame.offsetMax = Vector2.zero;
            }
        }

        private void BuildFooter(Transform parent)
        {
            var footer = AddText(parent, "FooterHelp", "TAB - Đóng     |     Chuột trái - Chọn     |     Chuột phải / nhấp đúp - Sử dụng     |     Q / E - Đổi tab     |     ESC - Đóng", 22, new Color(0.68f, 0.76f, 0.8f), TextAlignmentOptions.Center, new Vector2(0.08f, 0.03f), new Vector2(0.92f, 0.08f));
            footer.textWrappingMode = TextWrappingModes.NoWrap;
        }

        private Button CreateButton(Transform parent, string name, Sprite sprite, string label, int fontSize)
        {
            var image = AddImage(parent, name, sprite, new Color(0.02f, 0.06f, 0.09f, 0.85f), Image.Type.Sliced, false);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var text = AddText(image.transform, "Text", label, fontSize, new Color(0.82f, 0.88f, 0.92f), TextAlignmentOptions.Center, new Vector2(0f, 0f), new Vector2(1f, 1f));
            text.raycastTarget = false;
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
            if (size >= 30 && titleFont != null) label.font = titleFont;
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
    }
}
