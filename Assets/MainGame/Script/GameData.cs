using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class GameData : MonoBehaviour
{
    private const string PrefPrefix = "MainGame.Settings.";
    private const string ManagerName = "GameData";

    private const string ResolutionKey = PrefPrefix + "ResolutionIndex";
    private const string DisplayModeKey = PrefPrefix + "DisplayModeIndex";
    private const string BrightnessKey = PrefPrefix + "Brightness";
    private const string MasterVolumeKey = PrefPrefix + "MasterVolume";
    private const string MusicVolumeKey = PrefPrefix + "MusicVolume";
    private const string SfxVolumeKey = PrefPrefix + "SfxVolume";

    private const int BrightnessSortingOrder = -100;

    public static GameData Instance { get; private set; }
    public static event Action<GameSettings> SettingsChanged;

    public GameSettings Settings { get; private set; }

    private Image brightnessOverlay;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static GameData EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        var existing = FindFirstObjectByType<GameData>();
        if (existing != null)
        {
            existing.InitializeAsSingleton();
            return existing;
        }

        var gameObject = new GameObject(ManagerName);
        var data = gameObject.AddComponent<GameData>();
        data.InitializeAsSingleton();
        return data;
    }

    private void Awake()
    {
        InitializeAsSingleton();
    }

    public void SaveSettings(GameSettings settings, bool applyNow = true)
    {
        settings.Clamp();
        Settings = settings;

        PlayerPrefs.SetInt(ResolutionKey, Settings.ResolutionIndex);
        PlayerPrefs.SetInt(DisplayModeKey, Settings.DisplayModeIndex);
        PlayerPrefs.SetInt(BrightnessKey, Settings.Brightness);
        PlayerPrefs.SetInt(MasterVolumeKey, Settings.MasterVolume);
        PlayerPrefs.SetInt(MusicVolumeKey, Settings.MusicVolume);
        PlayerPrefs.SetInt(SfxVolumeKey, Settings.SfxVolume);
        PlayerPrefs.Save();

        if (applyNow)
            ApplySettings(Settings);

        SettingsChanged?.Invoke(Settings);
    }

    public void PreviewSettings(GameSettings settings)
    {
        settings.Clamp();
        ApplyBrightness(settings.Brightness);
        AudioManager.Instance?.ApplySettings(settings);
    }

    public void ResetSettings()
    {
        SaveSettings(GameSettings.Default);
    }

    public void ApplySettings(GameSettings settings)
    {
        settings.Clamp();

        var resolution = settings.Resolution;
        Screen.SetResolution(resolution.x, resolution.y, settings.FullScreenMode);
        ApplyBrightness(settings.Brightness);
        AudioManager.Instance?.ApplySettings(settings);
    }

    private void InitializeAsSingleton()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        gameObject.name = ManagerName;
        DontDestroyOnLoad(gameObject);
        Settings = LoadSettings();
        EnsureBrightnessOverlay();
        ApplySettings(Settings);
    }

    private static GameSettings LoadSettings()
    {
        var defaults = GameSettings.Default;
        var settings = new GameSettings
        {
            ResolutionIndex = PlayerPrefs.GetInt(ResolutionKey, defaults.ResolutionIndex),
            DisplayModeIndex = PlayerPrefs.GetInt(DisplayModeKey, defaults.DisplayModeIndex),
            Brightness = PlayerPrefs.GetInt(BrightnessKey, defaults.Brightness),
            MasterVolume = PlayerPrefs.GetInt(MasterVolumeKey, defaults.MasterVolume),
            MusicVolume = PlayerPrefs.GetInt(MusicVolumeKey, defaults.MusicVolume),
            SfxVolume = PlayerPrefs.GetInt(SfxVolumeKey, defaults.SfxVolume)
        };
        settings.Clamp();
        return settings;
    }

    private void ApplyBrightness(int brightness)
    {
        EnsureBrightnessOverlay();
        var normalized = Mathf.Clamp01(brightness / 100f);
        brightnessOverlay.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.48f, 0f, normalized));
    }

    private void EnsureBrightnessOverlay()
    {
        if (brightnessOverlay != null)
            return;

        var canvasObject = new GameObject("BrightnessOverlayCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = BrightnessSortingOrder;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var imageObject = new GameObject("BrightnessOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(canvasObject.transform, false);

        var rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        brightnessOverlay = imageObject.GetComponent<Image>();
        brightnessOverlay.raycastTarget = false;
    }
}
