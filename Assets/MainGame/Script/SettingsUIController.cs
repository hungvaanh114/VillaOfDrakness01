using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class SettingsUIController : MonoBehaviour
{
    private const string SettingsPageTitle = "C\u00c0I \u0110\u1eb6T";

    private static readonly Color SelectedTabColor = new(0.04f, 0.18f, 0.26f, 0.72f);
    private static readonly Color SelectedTextColor = new(0.78f, 0.94f, 1f, 1f);

    [Header("Pages")]
    public GameObject settingsPage;
    public Button settingsTabButton;
    public Image settingsTabGraphic;
    public TMP_Text settingsTabLabel;
    public TMP_Text pageTitleText;

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
        if (pageTitleText != null)
            pageTitleText.text = SettingsPageTitle;
        SetTabVisual(settingsTabButton, settingsTabGraphic, settingsTabLabel);
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

    private static void SetTabVisual(Button button, Image image, TMP_Text label)
    {
        if (image == null && button != null)
            image = button.targetGraphic as Image;
        if (image != null)
            image.color = SelectedTabColor;
        if (label != null)
            label.color = SelectedTextColor;
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
