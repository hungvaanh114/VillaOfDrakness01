using UnityEngine;
using UnityEngine.UI;

public sealed class PauseMenuController : MonoBehaviour
{
    [SerializeField] private Button continueButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button mainMenuButton;

    private void Awake()
    {
        if (continueButton != null) continueButton.onClick.AddListener(Continue);
        if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(GoToMainMenu);
    }

    private static void Continue()
    {
        AudioManager.Instance?.PlayButtonClick();
        GameController.Instance?.ResumeGame();
    }

    private static void OpenSettings()
    {
        AudioManager.Instance?.PlayButtonClick();
        GameController.Instance?.OpenSettingsFromPause();
    }

    private static void GoToMainMenu()
    {
        AudioManager.Instance?.PlayButtonClick();
        GameController.Instance?.LoadMainMenu();
    }
}
