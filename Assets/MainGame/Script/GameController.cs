using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using UnityEngine.UI;
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

    [Header("Cut Scenes")]
    public CutSceneManager cutSceneManager;
    public bool playIntroOnStart = true;
    [SerializeField] private bool useChapterOneCheckpoints = true;

    [Header("Pause")]
    public GameObject gameUI;
    public GameObject pauseUI;
    public GameObject settingsUI;
    public string mainMenuSceneName = "Menu";

    [Header("Death")]
    public GameObject deathUI;
    public float deathUIShowDelay = 1.5f;
    [SerializeField, Min(0f)] private float jumpscareAutoRespawnDelay = 2.5f;

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
    private bool isJumpscareRespawnPending;
    private bool endingDeathScreenPresentation;
    private bool cutsceneFlashlightForced;

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
        if (cutSceneManager == null) cutSceneManager = FindFirstObjectByType<CutSceneManager>();
        ResolveUiReferences();
        ResolveDeathUI();

        var checkpointManager = useChapterOneCheckpoints
            ? ChapterOneCheckpointManager.Instance ?? FindFirstObjectByType<ChapterOneCheckpointManager>(FindObjectsInactive.Include)
            : null;
        bool restoredCheckpoint = checkpointManager != null && checkpointManager.ApplySavedState(this);
        bool hasCompletedIntro = useChapterOneCheckpoints && ChapterOneCheckpointManager.HasCompletedIntroCutscenes;

        if (playIntroOnStart && !restoredCheckpoint && !hasCompletedIntro)
            ResetForIntroStart();
        else if (hasCompletedIntro)
            playIntroOnStart = false;

        EnsureGameUiCanRender();
        if (gameUI != null) gameUI.SetActive(true);
        if (pauseUI != null) pauseUI.SetActive(false);
        if (settingsUI != null) settingsUI.SetActive(false);
        if (deathUI != null) deathUI.SetActive(false);
        if (endingUI != null) endingUI.SetActive(false);

        ApplyChapterAudio();
        SetGameState(currentGameState);

        bool shouldPlayIntro = playIntroOnStart
            && !hasCompletedIntro
            && currentChapterPhase == ChapterPhase.Intro
            && cutSceneManager != null;
        if (!shouldPlayIntro)
            HideStartupNarration();

        if (shouldPlayIntro)
            cutSceneManager.PlayIntro(this);
    }

    private void ResetForIntroStart()
    {
        isPaused = false;
        isDead = false;
        Time.timeScale = 1f;
        currentGameState = GameState.Gameplay;
        currentChapterPhase = ChapterPhase.Intro;
    }

    private void Update()
    {
        if (ShouldKeepCursorVisibleForUI() && (Cursor.lockState != CursorLockMode.None || !Cursor.visible))
            SetCursor(true);

        if (Keyboard.current == null)
            return;

        if (JournalInteractable.IsAnyPaperOpen
            || MainGame.P2.P2JournalPaperInteractable.IsAnyPaperOpen
            || MainGame.P2.P2DollPickup.IsAnyDollHeld
            || MainGame.P2.P2KnockPlankZoomSequence.IsAnyZoomActive)
            return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame && !isDead && currentGameState != GameState.Ending)
        {
            if (PhysicalPianoController.CloseActivePiano())
                return;

            if (InventoryUI.Instance != null && InventoryUI.Instance.IsOpen)
            {
                InventoryUI.Instance.Close();
                return;
            }

            if (PianoPuzzleUI.Instance != null && PianoPuzzleUI.Instance.IsOpen)
            {
                PianoPuzzleUI.Instance.Close();
                return;
            }

            if (isPaused && settingsUI != null && settingsUI.activeSelf) ShowPauseMenu();
            else if (isPaused) ResumeGame();
            else PauseGame();
        }

        if (IsCutsceneInputLocked())
            FpsAssetsInputs.Instance?.ClearGameplayInput();
    }

    private void LateUpdate()
    {
        UpdateGameUIVisibility();
    }

    public void SetGameState(GameState newState)
    {
        currentGameState = newState;
        UpdateGameUIVisibility();

        switch (currentGameState)
        {
            case GameState.Gameplay:
                SetPlayerControl(true);
                SetCursor(false);
                break;

            case GameState.Cutscene:
            case GameState.Dialogue:
                SetPlayerControl(false);
                SetCursor(false);
                break;

            case GameState.Puzzle:
                SetPlayerControl(false);
                SetCursor(true);
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

        ApplyCutsceneFlashlightState(currentGameState == GameState.Cutscene || currentGameState == GameState.Ending);
    }

    private void ApplyCutsceneFlashlightState(bool forceOn)
    {
        if (cutsceneFlashlightForced == forceOn)
            return;

        cutsceneFlashlightForced = forceOn;
        if (ItemUsageSystem.Instance != null)
            ItemUsageSystem.Instance.SetCutsceneFlashlightForced(forceOn);
        else
            InventoryManager.Instance?.SetCutsceneFallbackFlashlightForced(forceOn);
    }

    public void SetChapterPhase(ChapterPhase newPhase)
    {
        currentChapterPhase = newPhase;
        ApplyChapterAudio();
    }

    public void SetPlayerControl(bool canControl)
    {
        if (playerController == null)
            return;

        playerController.isCutScene = !canControl;
        playerController.isInteracting = !canControl;

        if (!canControl)
        {
            playerController.ForceIdleState();
            FpsAssetsInputs.Instance?.ClearGameplayInput();
        }
    }

    public bool CanUseGameplayInput()
    {
        return currentGameState == GameState.Gameplay && !isPaused && !isDead;
    }

    public bool IsCutsceneInputLocked()
    {
        return currentGameState == GameState.Cutscene
            || currentGameState == GameState.Ending
            || currentGameState == GameState.Dead
            || isDead;
    }

    public static bool IsGameplayInputLocked()
    {
        return Instance != null && !Instance.CanUseGameplayInput();
    }

    public static bool IsCutsceneOrEndInputLocked()
    {
        return Instance != null && Instance.IsCutsceneInputLocked();
    }

    public void PauseGame()
    {
        if (isPaused || isDead)
            return;

        previousGameState = currentGameState;
        isPaused = true;

        SetGameState(GameState.Paused);
        Time.timeScale = 0f;
        cutSceneManager?.SetPaused(true);

        if (gameUI != null) gameUI.SetActive(false);
        if (pauseUI != null && pauseUI != settingsUI) pauseUI.SetActive(true);
        if (settingsUI != null) settingsUI.SetActive(false);

        onPause?.Invoke();
    }

    public void ResumeGame()
    {
        if (!isPaused)
            return;

        isPaused = false;
        Time.timeScale = 1f;
        cutSceneManager?.SetPaused(false);

        if (pauseUI != null && pauseUI != settingsUI) pauseUI.SetActive(false);
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
        if (pauseUI != null && pauseUI != settingsUI) pauseUI.SetActive(true);
    }

    public void OpenSettingsFromPause()
    {
        if (!isPaused)
            PauseGame();

        if (pauseUI != null && pauseUI != settingsUI) pauseUI.SetActive(false);
        if (settingsUI != null)
        {
            var settingsController = settingsUI.GetComponent<SettingsUIController>();
            if (settingsController != null)
            {
                settingsController.panelToHide = settingsUI;
                settingsController.backTargetPanel = pauseUI != settingsUI ? pauseUI : null;
                settingsController.LoadFromGameData();
            }
            settingsUI.SetActive(true);
            SetCursor(true);
        }
    }

    private void UpdateGameUIVisibility()
    {
        if (gameUI == null)
            ResolveUiReferences();

        if (gameUI == null)
            return;

        bool isEndingOrDeath = currentChapterPhase == ChapterPhase.Ending
            || currentGameState == GameState.Ending
            || currentGameState == GameState.Dead
            || isDead;
        bool shouldShow = !isEndingOrDeath
            && (currentGameState == GameState.Gameplay
                || currentGameState == GameState.Hiding
                || currentGameState == GameState.Dialogue
                || currentGameState == GameState.Cutscene);

        if (shouldShow)
            EnsureGameUiCanRender();

        if (gameUI.activeSelf != shouldShow)
            gameUI.SetActive(shouldShow);
    }

    private void ResolveUiReferences()
    {
        if (gameUI == null) gameUI = FindSceneGameObject("GameUI");
        if (pauseUI == null) pauseUI = FindSceneGameObject("PauseMenuUI");
        if (settingsUI == null) settingsUI = FindSceneGameObject("SettingUI");
    }

    private void EnsureGameUiCanRender()
    {
        if (gameUI == null)
            return;

        var canvas = gameUI.GetComponentInParent<Canvas>(true);
        if (canvas == null)
            return;

        if (!canvas.gameObject.activeSelf)
            canvas.gameObject.SetActive(true);

        canvas.enabled = true;
        var rectTransform = canvas.GetComponent<RectTransform>();
        if (rectTransform != null && rectTransform.localScale.sqrMagnitude < 0.001f)
            rectTransform.localScale = Vector3.one;

        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
            scaler.enabled = true;
    }

    private void HideStartupNarration()
    {
        foreach (var transform in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (transform == null || transform.name != "NarrationPanel")
                continue;

            foreach (var text in transform.GetComponentsInChildren<TMPro.TMP_Text>(true))
                text.text = "";

            transform.gameObject.SetActive(false);
        }
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void StartGameplay()
    {
        ApplyChapterAudio();
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
        TriggerDeath(true);
    }

    public void TriggerDeath(bool playJumpscareAudio)
    {
        TriggerDeathWithUIDelay(playJumpscareAudio, deathUIShowDelay);
    }

    public void TriggerDeathWithUIDelay(bool playJumpscareAudio, float uiShowDelay)
    {
        if (isDead || isJumpscareRespawnPending)
            return;

        isDead = true;
        AudioManager.Instance?.StopMonsterVoice();
        float deathSequenceDelay = Mathf.Max(0f, uiShowDelay);
        if (playJumpscareAudio)
        {
            float jumpscareDuration = AudioManager.Instance?.PlayWellJumpscare() ?? 0f;
            if (jumpscareDuration > 0f)
                deathSequenceDelay = jumpscareDuration;
        }
        SetGameState(GameState.Dead);
        onDeath?.Invoke();

        CancelInvoke(nameof(FinishDeathSequence));
        CancelInvoke(nameof(ShowDeathUI));
        if (deathSequenceDelay <= 0f)
            FinishDeathSequence();
        else
            Invoke(nameof(FinishDeathSequence), deathSequenceDelay);
    }

    public void TriggerDeathAfterExternalJumpscare(float remainingJumpscareSeconds)
    {
        TriggerDeathWithUIDelay(false, Mathf.Max(0f, remainingJumpscareSeconds));
    }

    public void TriggerJumpscareCheckpointRespawn()
    {
        TriggerJumpscareCheckpointRespawn(jumpscareAutoRespawnDelay, true);
    }

    public void TriggerJumpscareCheckpointRespawn(bool playDeathVoiceImmediately, int deathVoiceIndex = 3)
    {
        TriggerJumpscareCheckpointRespawn(jumpscareAutoRespawnDelay, playDeathVoiceImmediately, deathVoiceIndex);
    }

    public void TriggerJumpscareCheckpointRespawn(float delaySeconds)
    {
        TriggerJumpscareCheckpointRespawn(delaySeconds, true);
    }

    public void TriggerJumpscareCheckpointRespawn(float delaySeconds, bool playDeathVoiceImmediately, int deathVoiceIndex = 3)
    {
        if (isJumpscareRespawnPending)
            return;

        isJumpscareRespawnPending = true;
        isDead = true;
        Time.timeScale = 1f;

        AudioManager.Instance?.StopMonsterVoice();
        if (playDeathVoiceImmediately)
            AudioManager.Instance?.PlayDeathVoice(deathVoiceIndex);

        ResolveUiReferences();
        ResolveDeathUI();
        HideSceneObject("InventoryOverlay");
        HideSceneObject("PianoPuzzleOverlay");

        if (gameUI != null)
            gameUI.SetActive(false);
        if (pauseUI != null)
            pauseUI.SetActive(false);
        if (settingsUI != null)
            settingsUI.SetActive(false);
        if (deathUI != null)
            deathUI.SetActive(false);

        HideFirstPersonFlashlightViewModel();
        SetGameState(GameState.Dead);
        SetCursor(false);

        CancelInvoke(nameof(FinishDeathSequence));
        CancelInvoke(nameof(ShowDeathUI));
        CancelInvoke(nameof(RestartGame));
        Invoke(nameof(RestartGame), Mathf.Max(0f, delaySeconds));
    }

    public void ShowEndingDeathScreenPresentation()
    {
        isJumpscareRespawnPending = false;
        endingDeathScreenPresentation = true;
        isDead = true;
        Time.timeScale = 1f;

        ResolveUiReferences();
        ResolveDeathUI();
        HideSceneObject("InventoryOverlay");
        HideSceneObject("PianoPuzzleOverlay");
        HideSceneObject("DebtBookOverlay");

        if (gameUI != null)
            gameUI.SetActive(false);
        if (pauseUI != null)
            pauseUI.SetActive(false);
        if (settingsUI != null)
            settingsUI.SetActive(false);

        SetGameState(GameState.Dead);
        ShowDeathUI();
        SetCursor(false);
    }

    public void HideEndingDeathScreenPresentation()
    {
        endingDeathScreenPresentation = false;
        if (deathUI != null)
            deathUI.SetActive(false);
        SetCursor(false);
    }

    private void FinishDeathSequence()
    {
        AudioManager.Instance?.PlayDeathVoice(3);
        AudioManager.Instance?.PlayDeathMusic();
        ShowDeathUI();
    }

    private void ShowDeathUI()
    {
        ResolveDeathUI();
        if (deathUI != null) deathUI.SetActive(true);
        SetCursor(true);
    }

    private void ResolveDeathUI()
    {
        if (deathUI != null)
            return;

        deathUI = FindSceneGameObject("DeathUI");

        if (deathUI != null)
            deathUI.SetActive(false);
    }

    private static GameObject FindSceneGameObject(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        var activeObject = GameObject.Find(objectName);
        if (activeObject != null)
            return activeObject;

        foreach (var transform in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (transform != null && transform.name == objectName)
                return transform.gameObject;
        }

        return null;
    }

    public void StartEnding()
    {
        currentChapterPhase = ChapterPhase.Ending;
        AudioManager.Instance?.PlayDeathMusic();
        SetGameState(GameState.Ending);

        if (endingUI != null) endingUI.SetActive(true);

        onEnding?.Invoke();
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        CancelInvoke(nameof(FinishDeathSequence));
        CancelInvoke(nameof(ShowDeathUI));
        CancelInvoke(nameof(RestartGame));
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

    public bool ShouldKeepCursorVisibleForUI()
    {
        if (isJumpscareRespawnPending || endingDeathScreenPresentation)
            return false;
        if (isPaused || isDead)
            return true;
        if (InventoryUI.Instance != null && InventoryUI.Instance.IsOpen)
            return true;
        if (PianoPuzzleUI.Instance != null && PianoPuzzleUI.Instance.IsOpen)
            return true;
        if (settingsUI != null && settingsUI.activeInHierarchy)
            return true;
        if (pauseUI != null && pauseUI.activeInHierarchy)
            return true;
        if (deathUI != null && deathUI.activeInHierarchy)
            return true;
        if (endingUI != null && endingUI.activeInHierarchy)
            return true;

        return false;
    }

    private static void HideFirstPersonFlashlightViewModel()
    {
        var viewModel = FindSceneGameObject("FirstPersonFlashlightViewModel");
        if (viewModel != null)
            viewModel.SetActive(false);
    }

    private static void HideSceneObject(string objectName)
    {
        var sceneObject = FindSceneGameObject(objectName);
        if (sceneObject != null)
            sceneObject.SetActive(false);
    }

    private void ApplyChapterAudio()
    {
        var audio = AudioManager.EnsureInstance();
        switch (currentChapterPhase)
        {
            case ChapterPhase.Intro:
                audio.PlayIntroAmbience();
                break;
            case ChapterPhase.HallEncounter:
            case ChapterPhase.Escape:
                break;
            case ChapterPhase.Well:
                break;
            case ChapterPhase.Ending:
                audio.PlayDeathMusic();
                break;
            default:
                audio.StopMusic();
                audio.PlayGameplayAmbience();
                break;
        }
    }
}
