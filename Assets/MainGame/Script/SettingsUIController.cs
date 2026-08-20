using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class SettingsUIController : MonoBehaviour
{
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

    private GameSettings editingSettings;
    private bool suppressEvents;
    private bool wired;
    private int enabledFrame = -1;

    private void Awake()
    {
        ResolveOptionalReferences();
        PopulateDropdowns();
        WireEvents();
        LoadFromGameData();
        ShowSettingsPage();
    }

    private void OnEnable()
    {
        enabledFrame = Time.frameCount;
        ResolveOptionalReferences();
        PopulateDropdowns();
        LoadFromGameData();
        ShowSettingsPage();
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (Time.frameCount == enabledFrame)
            return;

        if (keyboard.escapeKey.wasPressedThisFrame)
            Back();
        else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            Apply();
        else if (keyboard.rKey.wasPressedThisFrame)
            ResetDefaults();
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

        dropdown.value = Mathf.Clamp(value, 0, dropdown.options.Count - 1);
        dropdown.RefreshShownValue();
    }

}
