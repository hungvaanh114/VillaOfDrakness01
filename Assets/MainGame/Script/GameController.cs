using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
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
        if (cutSceneManager == null) cutSceneManager = FindFirstObjectByType<CutSceneManager>();
        ResolveUiReferences();
        ResolveDeathUI();

        if (playIntroOnStart)
            ResetForIntroStart();

        EnsureGameUiCanRender();
        if (gameUI != null) gameUI.SetActive(true);
        if (pauseUI != null) pauseUI.SetActive(false);
        if (settingsUI != null) settingsUI.SetActive(false);
        if (deathUI != null) deathUI.SetActive(false);
        if (endingUI != null) endingUI.SetActive(false);

        ApplyChapterAudio();
        SetGameState(currentGameState);

        bool shouldPlayIntro = playIntroOnStart && currentChapterPhase == ChapterPhase.Intro && cutSceneManager != null;
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

        if (JournalInteractable.IsAnyPaperOpen)
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
            FpsAssetsInputs.Instance?.ClearGameplayInput();
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
        if (isDead)
            return;

        isDead = true;
        AudioManager.Instance?.StopMonsterVoice();
        if (playJumpscareAudio)
            AudioManager.Instance?.PlayWellJumpscare();
        AudioManager.Instance?.PlayDeathVoice(3);
        AudioManager.Instance?.PlayDeathMusic();
        SetGameState(GameState.Dead);
        onDeath?.Invoke();

        CancelInvoke(nameof(ShowDeathUI));
        if (uiShowDelay <= 0f)
            ShowDeathUI();
        else
            Invoke(nameof(ShowDeathUI), uiShowDelay);
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
        if (deathUI == null)
            deathUI = CreateRuntimeDeathUI();

        if (deathUI != null)
            deathUI.SetActive(false);
    }

    private GameObject CreateRuntimeDeathUI()
    {
        var canvasObject = new GameObject("DeathUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var background = CreateUiImage("DeathBackground", canvasObject.transform, new Color(0.005f, 0.015f, 0.03f, 0.96f));
        StretchFullScreen(background.rectTransform);

        var title = CreateUiText("DeathTitle", canvasObject.transform, "BẠN ĐÃ CHẾT", 76, TextAlignmentOptions.Center);
        title.rectTransform.anchorMin = new Vector2(0.1f, 0.58f);
        title.rectTransform.anchorMax = new Vector2(0.9f, 0.78f);
        title.rectTransform.offsetMin = Vector2.zero;
        title.rectTransform.offsetMax = Vector2.zero;
        title.color = new Color(0.78f, 0.89f, 1f, 1f);
        title.fontStyle = FontStyles.Bold;
        title.outlineWidth = 0.18f;
        title.outlineColor = new Color(0.02f, 0.08f, 0.14f, 1f);

        var line = CreateUiImage("TitleLine", canvasObject.transform, new Color(0.54f, 0.75f, 0.9f, 0.85f));
        line.rectTransform.anchorMin = new Vector2(0.39f, 0.555f);
        line.rectTransform.anchorMax = new Vector2(0.61f, 0.56f);
        line.rectTransform.offsetMin = Vector2.zero;
        line.rectTransform.offsetMax = Vector2.zero;

        var restartButton = CreateDeathButton(canvasObject.transform, "Chơi lại", 0.42f, 0.43f);
        restartButton.onClick.AddListener(RestartGame);
        var menuButton = CreateDeathButton(canvasObject.transform, "Về menu chính", 0.42f, 0.32f);
        menuButton.onClick.AddListener(LoadMainMenu);

        return canvasObject;
    }

    private static Image CreateUiImage(string objectName, Transform parent, Color color)
    {
        var objectRoot = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        objectRoot.transform.SetParent(parent, false);
        var image = objectRoot.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TextMeshProUGUI CreateUiText(string objectName, Transform parent, string text, int fontSize, TextAlignmentOptions alignment)
    {
        var objectRoot = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        objectRoot.transform.SetParent(parent, false);
        var label = objectRoot.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.enableWordWrapping = false;
        label.raycastTarget = false;
        return label;
    }

    private static Button CreateDeathButton(Transform parent, string labelText, float centerX, float centerY)
    {
        var buttonImage = CreateUiImage(labelText + "Button", parent, new Color(0.015f, 0.06f, 0.1f, 0.94f));
        var rect = buttonImage.rectTransform;
        rect.anchorMin = new Vector2(centerX, centerY);
        rect.anchorMax = new Vector2(centerX + 0.16f, centerY + 0.075f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var button = buttonImage.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        var colors = button.colors;
        colors.normalColor = new Color(0.015f, 0.06f, 0.1f, 0.94f);
        colors.highlightedColor = new Color(0.06f, 0.2f, 0.3f, 1f);
        colors.pressedColor = new Color(0.1f, 0.3f, 0.42f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        var outline = buttonImage.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.47f, 0.75f, 0.9f, 0.8f);
        outline.effectDistance = new Vector2(2f, 2f);

        var label = CreateUiText("Label", buttonImage.transform, labelText, 28, TextAlignmentOptions.Center);
        StretchFullScreen(label.rectTransform);
        label.color = new Color(0.83f, 0.92f, 1f, 1f);
        return button;
    }

    private static void StretchFullScreen(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
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
