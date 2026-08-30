using System;
using UnityEngine;

[Serializable]
public struct GameSettings
{
    public int ResolutionIndex;
    public int DisplayModeIndex;
    public int Brightness;
    public int MasterVolume;
    public int MusicVolume;
    public int SfxVolume;

    public static readonly Vector2Int[] SupportedResolutions =
    {
        new(1920, 1080),
        new(1600, 900),
        new(1366, 768),
        new(1280, 720),
        new(2560, 1440),
        new(3840, 2160)
    };

    public static readonly string[] ResolutionLabels =
    {
        "1920x1080",
        "1600x900",
        "1366x768",
        "1280x720",
        "2560x1440",
        "3840x2160"
    };

    public static readonly string[] DisplayModeLabels =
    {
        "Toàn màn hình",
        "Cửa sổ",
        "Không viền"
    };

    public static GameSettings Default => new()
    {
        ResolutionIndex = 0,
        DisplayModeIndex = 0,
        Brightness = 60,
        MasterVolume = 80,
        MusicVolume = 40,
        SfxVolume = 70
    };

    public readonly FullScreenMode FullScreenMode => DisplayModeIndex switch
    {
        1 => FullScreenMode.Windowed,
        2 => FullScreenMode.FullScreenWindow,
        _ => FullScreenMode.FullScreenWindow
    };

    public readonly Vector2Int Resolution => SupportedResolutions[Mathf.Clamp(ResolutionIndex, 0, SupportedResolutions.Length - 1)];

    public void Clamp()
    {
        ResolutionIndex = Mathf.Clamp(ResolutionIndex, 0, SupportedResolutions.Length - 1);
        DisplayModeIndex = Mathf.Clamp(DisplayModeIndex, 0, DisplayModeLabels.Length - 1);
        Brightness = Mathf.Clamp(Brightness, 0, 100);
        MasterVolume = Mathf.Clamp(MasterVolume, 0, 100);
        MusicVolume = Mathf.Clamp(MusicVolume, 0, 100);
        SfxVolume = Mathf.Clamp(SfxVolume, 0, 100);
    }
}
