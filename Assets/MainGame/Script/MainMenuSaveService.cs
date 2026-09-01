using System;
using UnityEngine;

public readonly struct MainMenuSaveData
{
    public readonly int Chapter;
    public readonly string LastSaveAt;
    public readonly string PlayTime;

    public MainMenuSaveData(int chapter, string lastSaveAt, string playTime)
    {
        Chapter = chapter;
        LastSaveAt = lastSaveAt;
        PlayTime = playTime;
    }
}

public interface IMainMenuSaveService
{
    bool TryLoad(out MainMenuSaveData saveData);
    MainMenuSaveData StartNewGame();
}

public sealed class PlayerPrefsMainMenuSaveService : IMainMenuSaveService
{
    private const string HasSaveKey = "MainMenu.HasSave";
    private const string ChapterKey = "MainMenu.Chapter";
    private const string LastSaveAtKey = "MainMenu.LastSaveAt";
    private const string PlaySecondsKey = "MainMenu.PlaySeconds";
    private const string PartTwoUnlockedKey = "MainMenu.PartTwoUnlocked";

    public static bool IsPartTwoUnlocked => PlayerPrefs.GetInt(PartTwoUnlockedKey, 0) == 1;
    public static int HighestUnlockedPart => IsPartTwoUnlocked ? 2 : 1;

    public static void UnlockPartTwo()
    {
        PlayerPrefs.SetInt(PartTwoUnlockedKey, 1);
        PlayerPrefs.SetInt(HasSaveKey, 1);
        PlayerPrefs.SetInt(ChapterKey, Mathf.Max(PlayerPrefs.GetInt(ChapterKey, 1), 2));
        PlayerPrefs.SetString(LastSaveAtKey, DateTime.Now.ToString("dd/MM/yyyy  HH:mm"));
        PlayerPrefs.Save();
    }

    public bool TryLoad(out MainMenuSaveData saveData)
    {
        if (PlayerPrefs.GetInt(HasSaveKey, 0) == 0)
        {
            saveData = new MainMenuSaveData(1, "--/--/----  --:--", "00:00:00");
            return false;
        }

        saveData = new MainMenuSaveData(
            PlayerPrefs.GetInt(ChapterKey, 1),
            PlayerPrefs.GetString(LastSaveAtKey, DateTime.Now.ToString("dd/MM/yyyy  HH:mm")),
            FormatTime(PlayerPrefs.GetInt(PlaySecondsKey, 0)));
        return true;
    }

    public MainMenuSaveData StartNewGame()
    {
        return StartNewPart(1);
    }

    public static MainMenuSaveData StartNewPart(int part)
    {
        int clampedPart = Mathf.Clamp(part, 1, HighestUnlockedPart);
        var saveData = new MainMenuSaveData(clampedPart, DateTime.Now.ToString("dd/MM/yyyy  HH:mm"), "00:00:00");

        PlayerPrefs.SetInt(HasSaveKey, 1);
        PlayerPrefs.SetInt(ChapterKey, saveData.Chapter);
        PlayerPrefs.SetString(LastSaveAtKey, saveData.LastSaveAt);
        PlayerPrefs.SetInt(PlaySecondsKey, 0);
        PlayerPrefs.Save();

        return saveData;
    }

    private static string FormatTime(int seconds)
    {
        var span = TimeSpan.FromSeconds(Mathf.Max(0, seconds));
        return $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}";
    }
}
