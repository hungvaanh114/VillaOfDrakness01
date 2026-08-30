using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class SettingsUIController : MonoBehaviour
{
    private const int SettingsUiSortingOrder = 30000;
    private const int SettingsDropdownSortingOrder = 30001;
    private const string SettingsPageTitle = "C\u00c0I \u0110\u1eb6T";
    private const string ControlsPageTitle = "PH\u00cdM";

    private static readonly Color SelectedTabColor = new(0.04f, 0.18f, 0.26f, 0.72f);
    private static readonly Color NormalTabColor = new(0.01f, 0.035f, 0.06f, 0.46f);
    private static readonly Color SelectedTextColor = new(0.78f, 0.94f, 1f, 1f);
    private static readonly Color NormalTextColor = new(0.72f, 0.78f, 0.82f, 0.92f);

    [Header("Pages")]
    public GameObject settingsPage;
    public Button settingsTabButton;
    public Image settingsTabGraphic;
    public TMP_Text settingsTabLabel;
    public TMP_Text pageTitleText;
    public GameObject controlsPage;
    public Button controlsTabButton;
    public Image controlsTabGraphic;
    public TMP_Text controlsTabLabel;

    [Header("Display")]
    public TMP_Dropdown resolutionDropdown;
    public TMP_Dropdown displayModeDropdown;
    public Slider brightnessSlider;
    public TMP_Text brightnessValueText;
    public TMP_Dropdown qualityDropdown;
    public TMP_Dropdown shadowsDropdown;
    public TMP_Dropdown fogDropdown;
    public TMP_Dropdown fpsDropdown;

    [Header("Audio")]
    public Slider masterVolumeSlider;
    public TMP_Text masterVolumeValueText;
    public Slider musicVolumeSlider;
    public TMP_Text musicVolumeValueText;
    public Slider sfxVolumeSlider;
    public TMP_Text sfxVolumeValueText;

    [Header("Actions")]
    public Button applyButton;
    public Button resetButton;
    public Button backButton;
    public GameObject panelToHide;
    public GameObject backTargetPanel;

    [Header("Layout Sync")]
    [SerializeField] private bool syncSettingsLayoutWithMenuScene = true;

    [Header("Static Drop Views")]
    public GameObject sharedDropView;
    public GameObject resolutionDropView;
    public GameObject displayModeDropView;
    public Button[] sharedDropViewButtons = System.Array.Empty<Button>();
    public Button[] resolutionDropViewButtons = System.Array.Empty<Button>();
    public Button[] displayModeDropViewButtons = System.Array.Empty<Button>();

    private GameSettings editingSettings;
    private bool suppressEvents;
    private bool wired;
    private int enabledFrame = -1;
    private StaticDropViewKind activeDropViewKind;
    private ScrollRect[] dropdownParentScrollRects = System.Array.Empty<ScrollRect>();
    private bool[] dropdownParentScrollRectStates = System.Array.Empty<bool>();

    private void Awake()
    {
        ResolveOptionalReferences();
        PopulateDropdowns();
        ApplyMenuSceneSettingsLayout();
        NormalizeSettingsCanvasLayers();
        CacheDropdownParentScrollRects();
        WireEvents();
        HideStaticDropViews();
        LoadFromGameData();
        ShowSettingsPage();
    }

    private void OnEnable()
    {
        enabledFrame = Time.frameCount;
        ResolveOptionalReferences();
        PopulateDropdowns();
        ApplyMenuSceneSettingsLayout();
        NormalizeSettingsCanvasLayers();
        CacheDropdownParentScrollRects();
        HideStaticDropViews();
        LoadFromGameData();
        ShowSettingsPage();
    }

    private void Update()
    {
        if (Time.frameCount == enabledFrame)
            return;

        HandleStaticDropViewPointerClose();

        var keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.escapeKey.wasPressedThisFrame && activeDropViewKind != StaticDropViewKind.None)
        {
            HideStaticDropViews();
            return;
        }

        if (keyboard.escapeKey.wasPressedThisFrame)
            Back();
        else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            Apply();
        else if (keyboard.rKey.wasPressedThisFrame)
            ResetDefaults();
    }

    private void LateUpdate()
    {
        NormalizeSettingsCanvasLayers();
        UpdateDropdownParentScrollLock();
    }

    public void Apply()
    {
        PullSettingsFromUi();
        GameData.EnsureInstance().SaveSettings(editingSettings);
        AudioManager.Instance?.PlayApplySettings();
        RefreshValueLabels();
    }

    public void ResetDefaults()
    {
        editingSettings = GameSettings.Default;
        PushSettingsToUi(editingSettings);
        GameData.EnsureInstance().SaveSettings(editingSettings);
        AudioManager.Instance?.PlayApplySettings();
    }

    public void Back()
    {
        HideStaticDropViews();
        AudioManager.Instance?.PlayBack();

        if (panelToHide != null)
            panelToHide.SetActive(false);
        else
            gameObject.SetActive(false);

        if (backTargetPanel != null)
        {
            backTargetPanel.SetActive(true);
            return;
        }

        if (GameController.Instance != null)
            GameController.Instance.ResumeGame();
    }

    public void LoadFromGameData()
    {
        editingSettings = GameData.EnsureInstance().Settings;
        PushSettingsToUi(editingSettings);
    }

    public void ShowSettingsPage()
    {
        HideStaticDropViews();
        if (settingsPage != null)
            settingsPage.SetActive(true);
        if (controlsPage != null)
            controlsPage.SetActive(false);
        if (pageTitleText != null)
            pageTitleText.text = SettingsPageTitle;
        SetTabVisual(settingsTabButton, settingsTabGraphic, settingsTabLabel, true);
        SetTabVisual(controlsTabButton, controlsTabGraphic, controlsTabLabel, false);
    }

    public void ShowControlsPage()
    {
        HideStaticDropViews();
        if (settingsPage != null)
            settingsPage.SetActive(false);
        if (controlsPage != null)
            controlsPage.SetActive(true);
        if (pageTitleText != null)
            pageTitleText.text = "PH\u00cdM";
        SetTabVisual(settingsTabButton, settingsTabGraphic, settingsTabLabel, false);
        SetTabVisual(controlsTabButton, controlsTabGraphic, controlsTabLabel, true);
    }

    private void WireEvents()
    {
        if (wired)
            return;

        wired = true;

        if (applyButton != null) applyButton.onClick.AddListener(Apply);
        if (resetButton != null) resetButton.onClick.AddListener(ResetDefaults);
        if (backButton != null) backButton.onClick.AddListener(Back);
        if (settingsTabButton != null) settingsTabButton.onClick.AddListener(ShowSettingsPage);
        if (controlsTabButton != null) controlsTabButton.onClick.AddListener(ShowControlsPage);

        if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(_ => RefreshPreview());
        if (displayModeDropdown != null) displayModeDropdown.onValueChanged.AddListener(_ => RefreshPreview());
        if (brightnessSlider != null) brightnessSlider.onValueChanged.AddListener(_ => RefreshPreview());
        if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(_ => RefreshPreview());
        if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.AddListener(_ => RefreshPreview());
        if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.AddListener(_ => RefreshPreview());
        WireStaticDropViewControls();
    }

    private void ResolveOptionalReferences()
    {
        if (settingsPage == null)
            settingsPage = FindChild("ContentPanel");
        if (settingsTabButton == null)
            settingsTabButton = FindChildComponent<Button>("SettingsTabButton") ?? FindChildComponent<Button>("GeneralTab");
        if (settingsTabGraphic == null && settingsTabButton != null)
            settingsTabGraphic = settingsTabButton.targetGraphic as Image ?? settingsTabButton.GetComponent<Image>();
        if (settingsTabLabel == null && settingsTabButton != null)
            settingsTabLabel = settingsTabButton.GetComponentInChildren<TMP_Text>(true);
        if (pageTitleText == null)
            pageTitleText = FindChildComponent<TMP_Text>("SettingsTitleText");
        if (controlsPage == null)
            controlsPage = FindChild("ControlsPage");
        ResolveStaticDropViewReferences();
        if (controlsTabButton == null)
            controlsTabButton = FindChildComponent<Button>("ControlsTabButton");
        if (controlsTabGraphic == null && controlsTabButton != null)
            controlsTabGraphic = controlsTabButton.targetGraphic as Image ?? controlsTabButton.GetComponent<Image>();
        if (controlsTabLabel == null && controlsTabButton != null)
            controlsTabLabel = controlsTabButton.GetComponentInChildren<TMP_Text>(true);
        EnsureControlsGuideUi();
        if (controlsTabLabel != null)
            controlsTabLabel.text = "Ph\u00edm";
    }

    private void PopulateDropdowns()
    {
        SetOptions(resolutionDropdown, GameSettings.ResolutionLabels);
        SetOptions(displayModeDropdown, GameSettings.DisplayModeLabels);
        ConfigureDropdownForSettingsUi(resolutionDropdown, HasStaticDropView(StaticDropViewKind.Resolution));
        ConfigureDropdownForSettingsUi(displayModeDropdown, HasStaticDropView(StaticDropViewKind.DisplayMode));
        ConfigureStaticDropViewOptions(StaticDropViewKind.Resolution);
        ConfigureStaticDropViewOptions(StaticDropViewKind.DisplayMode);
    }

    private void ApplyMenuSceneSettingsLayout()
    {
        if (!syncSettingsLayoutWithMenuScene || settingsPage == null || controlsPage == null)
            return;

        SetText("ResolutionDropdownLabelText", "\u0110\u1ed9 ph\u00e2n gi\u1ea3i");
        SetText("DisplayModeDropdownLabelText", "Ch\u1ebf \u0111\u1ed9 hi\u1ec3n th\u1ecb");
        SetText("BrightnessSliderLabelText", "\u0110\u1ed9 s\u00e1ng");
        SetText("MasterVolumeSliderLabelText", "\u00c2m l\u01b0\u1ee3ng t\u1ed5ng");
        SetText("MusicVolumeSliderLabelText", "\u00c2m nh\u1ea1c");
        SetText("SfxVolumeSliderLabelText", "Hi\u1ec7u \u1ee9ng");
        SetButtonText(applyButton, "\u00c1P D\u1ee4NG");
        SetButtonText(resetButton, "KH\u00d4I PH\u1ee4C");
        SetButtonText(backButton, "QUAY L\u1ea0I");

        HideIfFound(settingsPage.transform, "VisualSectionTitleText");
        HideIfFound(settingsPage.transform, "AudioSectionTitleText");
        HideIfFound(settingsPage.transform, "VisualSeparator");
        HideIfFound(settingsPage.transform, "AudioSeparator");

        PositionSettingLabel("ResolutionDropdownLabelText", 0);
        PositionSettingLabel("DisplayModeDropdownLabelText", 1);
        PositionSettingLabel("BrightnessSliderLabelText", 2);
        PositionSettingLabel("MasterVolumeSliderLabelText", 4);
        PositionSettingLabel("MusicVolumeSliderLabelText", 5);
        PositionSettingLabel("SfxVolumeSliderLabelText", 6);

        PositionDropdownLikeMenu(resolutionDropdown, 0);
        PositionDropdownLikeMenu(displayModeDropdown, 1);
        PositionSliderLikeMenu(brightnessSlider, brightnessValueText, 2);
        PositionSliderLikeMenu(masterVolumeSlider, masterVolumeValueText, 4);
        PositionSliderLikeMenu(musicVolumeSlider, musicVolumeValueText, 5);
        PositionSliderLikeMenu(sfxVolumeSlider, sfxVolumeValueText, 6);

        PositionButtonLikeMenu(applyButton, new Vector2(-300f, 44f));
        PositionButtonLikeMenu(resetButton, new Vector2(0f, 44f));
        PositionButtonLikeMenu(backButton, new Vector2(300f, 44f));
    }

    private void RefreshPreview()
    {
        if (suppressEvents)
            return;

        PullSettingsFromUi();
        GameData.EnsureInstance().PreviewSettings(editingSettings);
        RefreshValueLabels();
    }

    private void PullSettingsFromUi()
    {
        editingSettings.ResolutionIndex = resolutionDropdown != null ? resolutionDropdown.value : editingSettings.ResolutionIndex;
        editingSettings.DisplayModeIndex = displayModeDropdown != null ? displayModeDropdown.value : editingSettings.DisplayModeIndex;
        editingSettings.Brightness = SliderInt(brightnessSlider, editingSettings.Brightness);
        editingSettings.MasterVolume = SliderInt(masterVolumeSlider, editingSettings.MasterVolume);
        editingSettings.MusicVolume = SliderInt(musicVolumeSlider, editingSettings.MusicVolume);
        editingSettings.SfxVolume = SliderInt(sfxVolumeSlider, editingSettings.SfxVolume);
        editingSettings.Clamp();
    }

    private void PushSettingsToUi(GameSettings settings)
    {
        settings.Clamp();
        suppressEvents = true;

        SetDropdownValue(resolutionDropdown, settings.ResolutionIndex);
        SetDropdownValue(displayModeDropdown, settings.DisplayModeIndex);
        SetSliderValue(brightnessSlider, settings.Brightness);
        SetSliderValue(masterVolumeSlider, settings.MasterVolume);
        SetSliderValue(musicVolumeSlider, settings.MusicVolume);
        SetSliderValue(sfxVolumeSlider, settings.SfxVolume);

        suppressEvents = false;
        GameData.EnsureInstance().PreviewSettings(settings);
        RefreshValueLabels();
    }

    private void RefreshValueLabels()
    {
        SetValueText(brightnessValueText, brightnessSlider);
        SetValueText(masterVolumeValueText, masterVolumeSlider);
        SetValueText(musicVolumeValueText, musicVolumeSlider);
        SetValueText(sfxVolumeValueText, sfxVolumeSlider);
    }

    private GameObject FindChild(string childName)
    {
        var child = FindChildTransform(childName);
        return child != null ? child.gameObject : null;
    }

    private T FindChildComponent<T>(string childName) where T : Component
    {
        var child = FindChildTransform(childName);
        return child != null ? child.GetComponent<T>() : null;
    }

    private Transform FindChildTransform(string childName)
    {
        foreach (var child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
                return child;
        }

        return null;
    }

    private void EnsureControlsGuideUi()
    {
        if (settingsPage == null)
            return;

        var settingsRect = settingsPage.GetComponent<RectTransform>();
        var pageParent = settingsPage.transform.parent;

        if (controlsPage == null)
        {
            controlsPage = new GameObject("ControlsPage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            controlsPage.layer = gameObject.layer;
            controlsPage.transform.SetParent(pageParent, false);
            var controlsRect = controlsPage.GetComponent<RectTransform>();
            if (settingsRect != null)
            {
                controlsRect.anchorMin = settingsRect.anchorMin;
                controlsRect.anchorMax = settingsRect.anchorMax;
                controlsRect.pivot = settingsRect.pivot;
                controlsRect.sizeDelta = settingsRect.sizeDelta;
                controlsRect.anchoredPosition = settingsRect.anchoredPosition;
            }
            else
            {
                controlsRect.anchorMin = Vector2.zero;
                controlsRect.anchorMax = Vector2.one;
                controlsRect.offsetMin = Vector2.zero;
                controlsRect.offsetMax = Vector2.zero;
            }

            var bg = controlsPage.GetComponent<Image>();
            bg.color = new Color(0.01f, 0.035f, 0.055f, 0.40f);
            bg.raycastTarget = false;
            BuildControlsGuideContent(controlsPage.transform);
            controlsPage.SetActive(false);
        }

        if (controlsTabButton == null && settingsTabButton != null)
        {
            var tabParent = settingsTabButton.transform.parent;
            var tabObject = new GameObject("ControlsTabButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            tabObject.layer = settingsTabButton.gameObject.layer;
            tabObject.transform.SetParent(tabParent, false);
            var sourceRect = settingsTabButton.GetComponent<RectTransform>();
            var tabRect = tabObject.GetComponent<RectTransform>();
            if (sourceRect != null)
            {
                tabRect.anchorMin = sourceRect.anchorMin;
                tabRect.anchorMax = sourceRect.anchorMax;
                tabRect.pivot = sourceRect.pivot;
                tabRect.sizeDelta = sourceRect.sizeDelta;
                tabRect.anchoredPosition = sourceRect.anchoredPosition + new Vector2(0f, -68f);
            }

            controlsTabGraphic = tabObject.GetComponent<Image>();
            controlsTabGraphic.raycastTarget = true;
            controlsTabButton = tabObject.GetComponent<Button>();
            controlsTabButton.targetGraphic = controlsTabGraphic;

            var labelObject = new GameObject("LabelText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.layer = tabObject.layer;
            labelObject.transform.SetParent(tabObject.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 0f);
            labelRect.offsetMax = new Vector2(-12f, 0f);
            controlsTabLabel = labelObject.GetComponent<TextMeshProUGUI>();
            controlsTabLabel.text = "Ph\u00edm";
            controlsTabLabel.fontSize = 20f;
            controlsTabLabel.alignment = TextAlignmentOptions.Center;

        }
    }

    private void BuildControlsGuideContent(Transform parent)
    {
        AddGuideHeader(parent, "Di chuyển", -14f);
        AddGuideRow(parent, "W / A / S / D", "Di chuyển nhân vật", -58f);
        AddGuideRow(parent, "Chuột", "Nhìn / xoay camera", -98f);
        AddGuideRow(parent, "Shift", "Chạy", -138f);
        AddGuideRow(parent, "E", "Tương tác với vật thể", -178f);

        AddGuideHeader(parent, "Hành trang và vật phẩm", -246f);
        AddGuideRow(parent, "TAB", "Mở / đóng hành trang", -290f);
        AddGuideRow(parent, "Chuột trái", "Chọn vật phẩm trong hành trang", -330f);
        AddGuideRow(parent, "Chuột phải / nhấp đúp", "Sử dụng hoặc trang bị vật phẩm", -370f);
        AddGuideRow(parent, "Q / E", "Đổi tab khi đang mở hành trang", -410f);
        AddGuideRow(parent, "F", "Bật / tắt đèn pin", -450f);
        AddGuideRow(parent, "ESC", "Đóng UI hoặc quay lại", -490f);
    }

    private void AddGuideHeader(Transform parent, string text, float y)
    {
        var label = AddGuideText(parent, $"GuideHeader_{text}", text.ToUpperInvariant(), 23f, SelectedTextColor, TextAlignmentOptions.Left);
        var rect = label.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(620f, 34f);
        rect.anchoredPosition = new Vector2(4f, y);
    }

    private void AddGuideRow(Transform parent, string key, string description, float y)
    {
        var keyText = AddGuideText(parent, $"GuideKey_{key}", key, 20f, new Color(0.85f, 0.96f, 1f, 1f), TextAlignmentOptions.Left);
        var keyRect = keyText.rectTransform;
        keyRect.anchorMin = new Vector2(0f, 1f);
        keyRect.anchorMax = new Vector2(0f, 1f);
        keyRect.pivot = new Vector2(0f, 1f);
        keyRect.sizeDelta = new Vector2(220f, 30f);
        keyRect.anchoredPosition = new Vector2(28f, y);

        var descText = AddGuideText(parent, $"GuideDesc_{key}", description, 20f, NormalTextColor, TextAlignmentOptions.Left);
        var descRect = descText.rectTransform;
        descRect.anchorMin = new Vector2(0f, 1f);
        descRect.anchorMax = new Vector2(0f, 1f);
        descRect.pivot = new Vector2(0f, 1f);
        descRect.sizeDelta = new Vector2(480f, 30f);
        descRect.anchoredPosition = new Vector2(260f, y);
    }

    private TMP_Text AddGuideText(Transform parent, string objectName, string text, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    private static void SetTabVisual(Button button, Image image, TMP_Text label, bool selected)
    {
        if (image == null && button != null)
            image = button.targetGraphic as Image;
        if (image != null)
            image.color = selected ? SelectedTabColor : NormalTabColor;
        if (label != null)
            label.color = selected ? SelectedTextColor : NormalTextColor;
    }

    private void PositionSettingLabel(string objectName, int row)
    {
        var label = FindSettingsPageText(objectName);
        if (label == null)
            return;

        label.gameObject.SetActive(true);
        label.alignment = TextAlignmentOptions.Left;
        SetRect(label.rectTransform, new Vector2(0f, 1f), new Vector2(300f, 32f), new Vector2(70f, -126f - row * 62f));
    }

    private void PositionDropdownLikeMenu(TMP_Dropdown dropdown, int row)
    {
        if (dropdown == null)
            return;

        var rect = dropdown.GetComponent<RectTransform>();
        SetRect(rect, new Vector2(1f, 1f), new Vector2(360f, 46f), new Vector2(-84f, -124f - row * 62f));

        if (dropdown.captionText != null)
        {
            dropdown.captionText.alignment = TextAlignmentOptions.Center;
            SetStretch(dropdown.captionText.rectTransform, new Vector2(36f, 0f), new Vector2(-50f, 0f));
        }

        var arrow = FindChildText(dropdown.transform, "ArrowText");
        if (arrow != null)
        {
            arrow.alignment = TextAlignmentOptions.Center;
            SetRect(arrow.rectTransform, new Vector2(1f, 0.5f), new Vector2(32f, 32f), new Vector2(-22f, 0f));
        }

        if (dropdown.template != null)
            SetRect(dropdown.template, new Vector2(0.5f, 0f), new Vector2(360f, 176f), new Vector2(0f, -90f));
    }

    private void PositionSliderLikeMenu(Slider slider, TMP_Text valueText, int row)
    {
        if (slider == null)
            return;

        var rect = slider.GetComponent<RectTransform>();
        SetRect(rect, new Vector2(1f, 1f), new Vector2(400f, 32f), new Vector2(-128f, -130f - row * 62f));

        if (slider.handleRect != null)
            SetRect(slider.handleRect, new Vector2(0.5f, 0.5f), new Vector2(30f, 44f), Vector2.zero);

        if (valueText != null)
        {
            valueText.alignment = TextAlignmentOptions.Right;
            SetRect(valueText.rectTransform, new Vector2(1f, 1f), new Vector2(50f, 30f), new Vector2(-60f, -128f - row * 62f));
        }
    }

    private static void PositionButtonLikeMenu(Button button, Vector2 anchoredPosition)
    {
        if (button == null)
            return;

        SetRect(button.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(240f, 58f), anchoredPosition);
        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.alignment = TextAlignmentOptions.Center;
            SetStretch(label.rectTransform, new Vector2(12f, 0f), new Vector2(-12f, 0f));
        }
    }

    private void SetText(string objectName, string text)
    {
        var label = FindSettingsPageText(objectName);
        if (label != null)
            label.text = text;
    }

    private static void SetButtonText(Button button, string text)
    {
        var label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        if (label != null)
            label.text = text;
    }

    private TMP_Text FindSettingsPageText(string objectName)
    {
        return settingsPage != null ? FindChildText(settingsPage.transform, objectName) : null;
    }

    private static TMP_Text FindChildText(Transform parent, string objectName)
    {
        if (parent == null)
            return null;

        foreach (var text in parent.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text != null && text.name == objectName)
                return text;
        }

        return null;
    }

    private static void HideIfFound(Transform parent, string objectName)
    {
        if (parent == null)
            return;

        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child != null && child.name == objectName)
                child.gameObject.SetActive(false);
        }
    }

    private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
    {
        if (rect == null)
            return;

        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one;
    }

    private static void SetStretch(RectTransform rect, Vector2 minOffset, Vector2 maxOffset)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = minOffset;
        rect.offsetMax = maxOffset;
        rect.localScale = Vector3.one;
    }

    private static void SetOptions(TMP_Dropdown dropdown, string[] options)
    {
        if (dropdown == null || options == null)
            return;

        if (dropdown.options.Count == options.Length)
        {
            var same = true;
            for (var i = 0; i < options.Length; i++)
                same &= dropdown.options[i].text == options[i];
            if (same)
                return;
        }

        dropdown.options.Clear();
        foreach (var option in options)
            dropdown.options.Add(new TMP_Dropdown.OptionData(option));
        dropdown.RefreshShownValue();
    }

    private static void ConfigureDropdownForSettingsUi(TMP_Dropdown dropdown, bool useStaticDropView)
    {
        if (dropdown == null)
            return;

        dropdown.enabled = !useStaticDropView;
        dropdown.interactable = true;
        if (dropdown.targetGraphic is Image targetImage)
            targetImage.raycastTarget = true;

        var template = dropdown.template;
        if (template == null)
            return;

        template.gameObject.SetActive(false);
        if (useStaticDropView)
            return;

        template.SetAsLastSibling();

        var templateImage = template.GetComponent<Image>();
        if (templateImage != null)
            templateImage.raycastTarget = true;

        var group = template.GetComponent<CanvasGroup>();
        if (group == null)
            group = template.gameObject.AddComponent<CanvasGroup>();
        group.interactable = true;
        group.blocksRaycasts = true;
        group.ignoreParentGroups = false;

        var scrollRect = template.GetComponent<ScrollRect>();
        if (scrollRect != null)
        {
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 24f;
            scrollRect.inertia = false;

            var viewportImage = scrollRect.viewport != null ? scrollRect.viewport.GetComponent<Image>() : null;
            if (viewportImage != null)
                viewportImage.raycastTarget = true;
        }

        foreach (var selectable in template.GetComponentsInChildren<Selectable>(true))
            selectable.interactable = true;

        foreach (var graphic in template.GetComponentsInChildren<MaskableGraphic>(true))
        {
            if (graphic is TMP_Text)
                graphic.raycastTarget = false;
        }

        foreach (var toggle in template.GetComponentsInChildren<Toggle>(true))
        {
            if (toggle.targetGraphic != null)
                toggle.targetGraphic.raycastTarget = true;
            if (toggle.graphic != null)
                toggle.graphic.raycastTarget = false;
        }
    }

    public void OpenResolutionDropView()
    {
        ShowStaticDropView(StaticDropViewKind.Resolution);
    }

    public void OpenDisplayModeDropView()
    {
        ShowStaticDropView(StaticDropViewKind.DisplayMode);
    }

    public void CloseDropView()
    {
        HideStaticDropViews();
    }

    private void ResolveStaticDropViewReferences()
    {
        if (sharedDropView == null)
            sharedDropView = FindChild("DropView") ?? FindChild("Drop View") ?? FindChild("dropView") ?? FindChild("drop view") ?? FindChild("DropdownView") ?? FindChild("Dropdown View");
        if (resolutionDropView == null)
            resolutionDropView = FindChild("ResolutionDropView") ?? FindChild("Resolution Drop View") ?? FindChild("resolutionDropView") ?? FindChild("resolution drop view") ?? FindChild("ResolutionDropdownDropView");
        if (displayModeDropView == null)
            displayModeDropView = FindChild("DisplayModeDropView") ?? FindChild("Display Mode Drop View") ?? FindChild("displayModeDropView") ?? FindChild("display mode drop view") ?? FindChild("DisplayModeDropdownDropView");
    }

    private void WireStaticDropViewControls()
    {
        ConfigureStaticDropdownClickRelay(resolutionDropdown, StaticDropViewKind.Resolution);
        ConfigureStaticDropdownClickRelay(displayModeDropdown, StaticDropViewKind.DisplayMode);

        var wiredButtons = new System.Collections.Generic.HashSet<Button>();
        WireStaticDropViewButtons(sharedDropView, sharedDropViewButtons, wiredButtons);
        WireStaticDropViewButtons(resolutionDropView, resolutionDropViewButtons, wiredButtons);
        WireStaticDropViewButtons(displayModeDropView, displayModeDropViewButtons, wiredButtons);
    }

    private void ConfigureStaticDropdownClickRelay(TMP_Dropdown dropdown, StaticDropViewKind kind)
    {
        if (dropdown == null || !HasStaticDropView(kind))
            return;

        var relay = dropdown.GetComponent<StaticDropdownClickRelay>();
        if (relay == null)
            relay = dropdown.gameObject.AddComponent<StaticDropdownClickRelay>();
        relay.Initialize(this, kind);
    }

    private void WireStaticDropViewButtons(GameObject dropView, Button[] assignedButtons, System.Collections.Generic.HashSet<Button> wiredButtons)
    {
        var buttons = GetDropViewButtons(dropView, assignedButtons);
        for (int i = 0; i < buttons.Length; i++)
        {
            var button = buttons[i];
            if (button == null || wiredButtons.Contains(button))
                continue;

            int index = i;
            wiredButtons.Add(button);
            button.onClick.AddListener(() => SelectStaticDropViewItem(index));
        }
    }

    private void ConfigureStaticDropViewOptions(StaticDropViewKind kind)
    {
        if (!HasStaticDropView(kind))
            return;

        var options = GetStaticDropViewOptions(kind);
        var buttons = GetStaticDropViewButtons(kind);
        for (int i = 0; i < buttons.Length; i++)
        {
            var button = buttons[i];
            if (button == null)
                continue;

            bool visible = i < options.Length;
            button.gameObject.SetActive(visible);
            if (!visible)
                continue;

            button.interactable = true;
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = options[i];
        }
    }

    private void ToggleStaticDropView(StaticDropViewKind kind)
    {
        if (!HasStaticDropView(kind))
            return;

        if (activeDropViewKind == kind)
        {
            HideStaticDropViews();
            return;
        }

        ShowStaticDropView(kind);
    }

    private void ShowStaticDropView(StaticDropViewKind kind)
    {
        if (!HasStaticDropView(kind))
            return;

        HideStaticDropViews();
        ConfigureStaticDropViewOptions(kind);

        var dropView = GetStaticDropView(kind);
        if (dropView == null)
            return;

        activeDropViewKind = kind;
        dropView.SetActive(true);
        PrepareStaticDropViewForFront(dropView);
        dropView.transform.SetAsLastSibling();
    }

    private void SelectStaticDropViewItem(int index)
    {
        if (activeDropViewKind == StaticDropViewKind.None)
            return;

        var dropdown = GetStaticDropViewDropdown(activeDropViewKind);
        var options = GetStaticDropViewOptions(activeDropViewKind);
        if (dropdown == null || index < 0 || index >= options.Length)
            return;

        dropdown.SetValueWithoutNotify(index);
        dropdown.RefreshShownValue();
        RefreshPreview();
        HideStaticDropViews();
    }

    private void HideStaticDropViews()
    {
        activeDropViewKind = StaticDropViewKind.None;
        SetDropViewActive(sharedDropView, false);
        SetDropViewActive(resolutionDropView, false);
        SetDropViewActive(displayModeDropView, false);
    }

    private void HandleStaticDropViewPointerClose()
    {
        if (activeDropViewKind == StaticDropViewKind.None || !PointerPressedThisFrame(out Vector2 screenPosition))
            return;

        if (PointerIsOverActiveDropViewOrDropdown(screenPosition))
            return;

        HideStaticDropViews();
    }

    private bool PointerIsOverActiveDropViewOrDropdown(Vector2 screenPosition)
    {
        var dropView = GetStaticDropView(activeDropViewKind);
        var dropdown = GetStaticDropViewDropdown(activeDropViewKind);

        if (EventSystem.current != null)
        {
            var eventData = new PointerEventData(EventSystem.current) { position = screenPosition };
            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            foreach (var result in results)
            {
                if (IsSelfOrChildOf(result.gameObject, dropView) || IsSelfOrChildOf(result.gameObject, dropdown != null ? dropdown.gameObject : null))
                    return true;
            }
        }

        return IsScreenPointInside(dropView, screenPosition) || IsScreenPointInside(dropdown != null ? dropdown.gameObject : null, screenPosition);
    }

    private bool HasStaticDropView(StaticDropViewKind kind)
    {
        return GetStaticDropView(kind) != null;
    }

    private GameObject GetStaticDropView(StaticDropViewKind kind)
    {
        return kind switch
        {
            StaticDropViewKind.Resolution => resolutionDropView != null ? resolutionDropView : sharedDropView != null ? sharedDropView : displayModeDropView,
            StaticDropViewKind.DisplayMode => displayModeDropView != null ? displayModeDropView : sharedDropView != null ? sharedDropView : resolutionDropView,
            _ => null
        };
    }

    private TMP_Dropdown GetStaticDropViewDropdown(StaticDropViewKind kind)
    {
        return kind switch
        {
            StaticDropViewKind.Resolution => resolutionDropdown,
            StaticDropViewKind.DisplayMode => displayModeDropdown,
            _ => null
        };
    }

    private string[] GetStaticDropViewOptions(StaticDropViewKind kind)
    {
        return kind switch
        {
            StaticDropViewKind.Resolution => GameSettings.ResolutionLabels,
            StaticDropViewKind.DisplayMode => GameSettings.DisplayModeLabels,
            _ => System.Array.Empty<string>()
        };
    }

    private Button[] GetStaticDropViewButtons(StaticDropViewKind kind)
    {
        if (kind == StaticDropViewKind.Resolution && resolutionDropView != null)
            return GetDropViewButtons(resolutionDropView, resolutionDropViewButtons);
        if (kind == StaticDropViewKind.DisplayMode && displayModeDropView != null)
            return GetDropViewButtons(displayModeDropView, displayModeDropViewButtons);
        if (sharedDropView == null && kind == StaticDropViewKind.DisplayMode && resolutionDropView != null)
            return GetDropViewButtons(resolutionDropView, resolutionDropViewButtons);
        if (sharedDropView == null && kind == StaticDropViewKind.Resolution && displayModeDropView != null)
            return GetDropViewButtons(displayModeDropView, displayModeDropViewButtons);

        return GetDropViewButtons(sharedDropView, sharedDropViewButtons);
    }

    private static void PrepareStaticDropViewForFront(GameObject dropView)
    {
        if (dropView == null)
            return;

        var canvas = dropView.GetComponent<Canvas>();
        if (canvas == null)
            canvas = dropView.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = SettingsDropdownSortingOrder;

        var raycaster = dropView.GetComponent<GraphicRaycaster>();
        if (raycaster == null)
            raycaster = dropView.AddComponent<GraphicRaycaster>();
        raycaster.enabled = true;

        var group = dropView.GetComponent<CanvasGroup>();
        if (group == null)
            group = dropView.AddComponent<CanvasGroup>();
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
        group.ignoreParentGroups = true;

        foreach (var graphic in dropView.GetComponentsInChildren<MaskableGraphic>(true))
        {
            if (graphic == null)
                continue;

            graphic.maskable = false;
        }
    }

    private void NormalizeSettingsCanvasLayers()
    {
        foreach (var canvas in GetComponentsInChildren<Canvas>(true))
        {
            if (canvas == null)
                continue;

            if (IsSettingsDropdownCanvas(canvas.gameObject))
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = SettingsDropdownSortingOrder;
                EnsureRaycaster(canvas.gameObject);
                continue;
            }

            if (canvas.overrideSorting && canvas.sortingOrder >= SettingsDropdownSortingOrder)
                canvas.sortingOrder = SettingsUiSortingOrder;
        }
    }

    private bool IsSettingsDropdownCanvas(GameObject target)
    {
        if (target == null)
            return false;

        return target.name.Contains("Dropdown List")
            || target.name.Contains("DropView")
            || target.name.Contains("Drop View")
            || target == sharedDropView
            || target == resolutionDropView
            || target == displayModeDropView;
    }

    private static void EnsureRaycaster(GameObject target)
    {
        if (target == null)
            return;

        var raycaster = target.GetComponent<GraphicRaycaster>();
        if (raycaster == null)
            raycaster = target.AddComponent<GraphicRaycaster>();
        raycaster.enabled = true;
    }

    private static Button[] GetDropViewButtons(GameObject dropView, Button[] assignedButtons)
    {
        if (assignedButtons != null && assignedButtons.Length > 0)
            return assignedButtons;

        return dropView != null ? dropView.GetComponentsInChildren<Button>(true) : System.Array.Empty<Button>();
    }

    private static void SetDropViewActive(GameObject dropView, bool active)
    {
        if (dropView != null && dropView.activeSelf != active)
            dropView.SetActive(active);
    }

    private static bool PointerPressedThisFrame(out Vector2 screenPosition)
    {
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            screenPosition = mouse.position.ReadValue();
            return true;
        }

        var touch = Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
        {
            screenPosition = touch.primaryTouch.position.ReadValue();
            return true;
        }

        screenPosition = default;
        return false;
    }

    private static bool IsSelfOrChildOf(GameObject candidate, GameObject root)
    {
        if (candidate == null || root == null)
            return false;

        return candidate == root || candidate.transform.IsChildOf(root.transform);
    }

    private static bool IsScreenPointInside(GameObject target, Vector2 screenPosition)
    {
        if (target == null)
            return false;

        var rect = target.GetComponent<RectTransform>();
        if (rect == null)
            return false;

        return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, GetUiCamera(rect));
    }

    private static Camera GetUiCamera(RectTransform rect)
    {
        var canvas = rect != null ? rect.GetComponentInParent<Canvas>(true) : null;
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
    }

    private void CacheDropdownParentScrollRects()
    {
        var scrollRects = new System.Collections.Generic.List<ScrollRect>();
        AddParentScrollRects(resolutionDropdown, scrollRects);
        AddParentScrollRects(displayModeDropdown, scrollRects);

        dropdownParentScrollRects = scrollRects.ToArray();
        dropdownParentScrollRectStates = new bool[dropdownParentScrollRects.Length];
        for (int i = 0; i < dropdownParentScrollRects.Length; i++)
            dropdownParentScrollRectStates[i] = dropdownParentScrollRects[i] != null && dropdownParentScrollRects[i].enabled;
    }

    private static void AddParentScrollRects(TMP_Dropdown dropdown, System.Collections.Generic.List<ScrollRect> scrollRects)
    {
        if (dropdown == null)
            return;

        foreach (var scrollRect in dropdown.GetComponentsInParent<ScrollRect>(true))
        {
            if (scrollRect == null || scrollRects.Contains(scrollRect))
                continue;

            scrollRects.Add(scrollRect);
        }
    }

    private void UpdateDropdownParentScrollLock()
    {
        if (dropdownParentScrollRects == null || dropdownParentScrollRects.Length == 0)
            return;

        bool dropdownOpen = activeDropViewKind != StaticDropViewKind.None;
        for (int i = 0; i < dropdownParentScrollRects.Length; i++)
        {
            var scrollRect = dropdownParentScrollRects[i];
            if (scrollRect == null)
                continue;

            bool targetEnabled = dropdownOpen ? false : dropdownParentScrollRectStates[i];
            if (scrollRect.enabled != targetEnabled)
                scrollRect.enabled = targetEnabled;
        }
    }

    private static int SliderInt(Slider slider, int fallback)
    {
        return slider != null ? Mathf.RoundToInt(slider.value) : fallback;
    }

    private static void SetValueText(TMP_Text text, Slider slider)
    {
        if (text != null && slider != null)
            text.text = Mathf.RoundToInt(slider.value).ToString();
    }

    private static void SetSliderValue(Slider slider, float value)
    {
        if (slider == null)
            return;

        slider.minValue = 0f;
        slider.maxValue = 100f;
        slider.wholeNumbers = true;
        slider.value = Mathf.Clamp(value, 0f, 100f);
    }

    private static void SetDropdownValue(TMP_Dropdown dropdown, int value)
    {
        if (dropdown == null)
            return;

        if (dropdown.options == null || dropdown.options.Count == 0)
            return;

        dropdown.value = Mathf.Clamp(value, 0, dropdown.options.Count - 1);
        dropdown.RefreshShownValue();
    }

    private enum StaticDropViewKind
    {
        None,
        Resolution,
        DisplayMode
    }

    private sealed class StaticDropdownClickRelay : MonoBehaviour, IPointerClickHandler, ISubmitHandler
    {
        private SettingsUIController owner;
        private StaticDropViewKind kind;

        public void Initialize(SettingsUIController owner, StaticDropViewKind kind)
        {
            this.owner = owner;
            this.kind = kind;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
                return;

            owner?.ToggleStaticDropView(kind);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            owner?.ToggleStaticDropView(kind);
        }
    }
}
