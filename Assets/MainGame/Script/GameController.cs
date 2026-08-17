using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using FpsHorrorKit;

public class GameController : MonoBehaviour
{
    public static GameController Instance;

    public enum GameState
    {
        Gameplay,
        Cutscene,
        Dialogue,
        Puzzle,
        Hiding,
        Paused,
        Dead,
        Ending
    }

    public enum ChapterPhase
    {
        Intro,
        EnterHouse,
        HouseExploration,
        PianoPuzzle,
        StudyRadio,
        HallEncounter,
        Escape,
        Well,
        Ending
    }

    [Header("Game State")]
    public GameState currentGameState = GameState.Gameplay;
    public ChapterPhase currentChapterPhase = ChapterPhase.Intro;

    [Header("Player")]
    public FpsController playerController;

    [Header("Pause")]
    public GameObject gameUI;
    public GameObject pauseUI;
    public GameObject settingsUI;
    public string mainMenuSceneName = "Menu";

    [Header("Death")]
    public GameObject deathUI;
    public float deathUIShowDelay = 1.5f;

    [Header("Ending")]
    public GameObject endingUI;

    [Header("Events")]
    public UnityEvent onPause;
    public UnityEvent onResume;
    public UnityEvent onDeath;
    public UnityEvent onEnding;

    private GameState previousGameState;
    private bool isPaused = false;
    private bool isDead = false;

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
        if (playerController == null) playerController = FindFirstObjectByType<FpsController>();
        if (gameUI == null) gameUI = GameObject.Find("GameUI");
        if (settingsUI == null) settingsUI = GameObject.Find("SettingUI");

        if (gameUI != null) gameUI.SetActive(true);
        if (pauseUI != null) pauseUI.SetActive(false);
        if (settingsUI != null) settingsUI.SetActive(false);
        if (deathUI != null) deathUI.SetActive(false);
        if (endingUI != null) endingUI.SetActive(false);

        SetGameState(currentGameState);
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame && !isDead && currentGameState != GameState.Ending)
        {
            if (isPaused && settingsUI != null && settingsUI.activeSelf) ShowPauseMenu();
            else if (isPaused) ResumeGame();
            else PauseGame();
        }
    }

    public void SetGameState(GameState newState)
    {
        currentGameState = newState;

        switch (currentGameState)
        {
            case GameState.Gameplay:
                SetPlayerControl(true);
                SetCursor(false);
                break;

            case GameState.Cutscene:
            case GameState.Dialogue:
            case GameState.Puzzle:
                SetPlayerControl(false);
                SetCursor(false);
                break;

            case GameState.Hiding:
                SetPlayerControl(false);
                SetCursor(false);
                break;

            case GameState.Paused:
                SetPlayerControl(false);
                SetCursor(true);
                break;

            case GameState.Dead:
                SetPlayerControl(false);
                SetCursor(true);
                break;

            case GameState.Ending:
                SetPlayerControl(false);
                SetCursor(false);
                break;
        }
    }

    public void SetChapterPhase(ChapterPhase newPhase)
    {
        currentChapterPhase = newPhase;
    }

    public void SetPlayerControl(bool canControl)
    {
        if (playerController == null)
            return;

        playerController.isInteracting = !canControl;
    }

    public void PauseGame()
    {
        if (isPaused || isDead)
            return;

        previousGameState = currentGameState;
        isPaused = true;

        SetGameState(GameState.Paused);
        Time.timeScale = 0f;

        if (gameUI != null) gameUI.SetActive(false);
        if (pauseUI != null) pauseUI.SetActive(true);
        if (settingsUI != null) settingsUI.SetActive(false);

        onPause?.Invoke();
    }

    public void ResumeGame()
    {
        if (!isPaused)
            return;

        isPaused = false;
        Time.timeScale = 1f;

        if (pauseUI != null) pauseUI.SetActive(false);
        if (settingsUI != null) settingsUI.SetActive(false);
        if (gameUI != null) gameUI.SetActive(true);

        SetGameState(previousGameState);

        onResume?.Invoke();
    }

    public void ShowPauseMenu()
    {
        if (!isPaused)
            PauseGame();

        if (settingsUI != null) settingsUI.SetActive(false);
        if (pauseUI != null) pauseUI.SetActive(true);
    }

    public void OpenSettingsFromPause()
    {
        if (!isPaused)
            PauseGame();

        if (pauseUI != null) pauseUI.SetActive(false);
        if (settingsUI != null)
        {
            var settingsController = settingsUI.GetComponent<SettingsUIController>();
            if (settingsController != null)
            {
                settingsController.panelToHide = settingsUI;
                settingsController.backTargetPanel = pauseUI;
                settingsController.LoadFromGameData();
            }
            settingsUI.SetActive(true);
        }
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void StartGameplay()
    {
        SetGameState(GameState.Gameplay);
    }

    public void StartCutscene()
    {
        SetGameState(GameState.Cutscene);
    }

    public void StartDialogue()
    {
        SetGameState(GameState.Dialogue);
    }

    public void StartPuzzle()
    {
        SetGameState(GameState.Puzzle);
    }

    public void StartHiding()
    {
        SetGameState(GameState.Hiding);
    }

    public void TriggerDeath()
    {
        if (isDead)
            return;

        isDead = true;
        SetGameState(GameState.Dead);
        onDeath?.Invoke();

        Invoke(nameof(ShowDeathUI), deathUIShowDelay);
    }

    private void ShowDeathUI()
    {
        if (deathUI != null) deathUI.SetActive(true);
        SetCursor(true);
    }

    public void StartEnding()
    {
        SetGameState(GameState.Ending);

        if (endingUI != null) endingUI.SetActive(true);

        onEnding?.Invoke();
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void LoadScene(string sceneName)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    private void SetCursor(bool visible)
    {
        Cursor.visible = visible;
        Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
    }
}
