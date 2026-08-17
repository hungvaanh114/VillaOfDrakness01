using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MainMenuController : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string gameSceneName = "Game";

    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Main Buttons")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    [Header("Chapter Card")]
    [SerializeField] private int chapterCount = 4;
    [SerializeField] private int unlockedChapterCount = 1;
    [SerializeField] private Button previousChapterButton;
    [SerializeField] private Button nextChapterButton;
    [SerializeField] private Button chapterPlayButton;
    [SerializeField] private TMP_Text chapterText;
    [SerializeField] private TMP_Text lastSaveText;
    [SerializeField] private TMP_Text playTimeText;
    [SerializeField] private GameObject lockedChapterOverlay;
    [SerializeField] private TMP_Text lockedChapterText;

    [Header("Settings")]
    [SerializeField] private SettingsUIController settingsController;

    private IMainMenuSaveService saveService;
    private MainMenuSaveData saveData;
    private int selectedChapter = 1;

    private void Awake()
    {
        GameData.EnsureInstance();
        AudioManager.EnsureInstance();
        saveService = new PlayerPrefsMainMenuSaveService();
        WireButtons();
    }

    private void Start()
    {
        RefreshSaveCard();
        ShowPanel(mainPanel);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            ShowPanel(mainPanel);
    }

    public void ContinueGame()
    {
        LoadSelectedChapter();
    }

    public void StartNewGame()
    {
        saveData = saveService.StartNewGame();
        selectedChapter = 1;
        RefreshChapterCard();
        LoadSelectedChapter();
    }

    public void SelectPreviousChapter()
    {
        selectedChapter = Mathf.Max(1, selectedChapter - 1);
        RefreshChapterCard();
    }

    public void SelectNextChapter()
    {
        selectedChapter = Mathf.Min(chapterCount, selectedChapter + 1);
        RefreshChapterCard();
    }

    public void OpenSettings()
    {
        settingsController?.LoadFromGameData();
        ShowPanel(settingsPanel);
        AudioManager.Instance?.PlayButtonClick();
    }

    public void ApplySettings()
    {
        settingsController?.Apply();
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void WireButtons()
    {
        if (continueButton != null) continueButton.onClick.AddListener(ContinueGame);
        if (newGameButton != null) newGameButton.onClick.AddListener(StartNewGame);
        if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
        if (quitButton != null) quitButton.onClick.AddListener(QuitGame);

        if (previousChapterButton != null) previousChapterButton.onClick.AddListener(SelectPreviousChapter);
        if (nextChapterButton != null) nextChapterButton.onClick.AddListener(SelectNextChapter);
        if (chapterPlayButton != null) chapterPlayButton.onClick.AddListener(LoadSelectedChapter);

    }

    private void RefreshSaveCard()
    {
        if (!saveService.TryLoad(out saveData))
            saveData = new MainMenuSaveData(1, "25/05/2025  22:47", "01:32:18");

        selectedChapter = Mathf.Clamp(saveData.Chapter, 1, chapterCount);
        RefreshChapterCard();
    }

    private void RefreshChapterCard()
    {
        var isUnlocked = selectedChapter <= unlockedChapterCount;

        if (chapterText != null)
            chapterText.text = $"Chương {selectedChapter}";

        if (lastSaveText != null)
            lastSaveText.text = isUnlocked && selectedChapter == saveData.Chapter ? saveData.LastSaveAt : "--/--/----  --:--";

        if (playTimeText != null)
            playTimeText.text = isUnlocked && selectedChapter == saveData.Chapter ? saveData.PlayTime : "Đang khóa";

        if (lockedChapterOverlay != null)
            lockedChapterOverlay.SetActive(!isUnlocked);

        if (lockedChapterText != null)
            lockedChapterText.text = $"Chương {selectedChapter} đang bị khóa";

        if (previousChapterButton != null)
            previousChapterButton.interactable = selectedChapter > 1;

        if (nextChapterButton != null)
            nextChapterButton.interactable = selectedChapter < chapterCount;

        if (chapterPlayButton != null)
            chapterPlayButton.interactable = isUnlocked;
    }

    private void ShowPanel(GameObject panel)
    {
        if (mainPanel != null) mainPanel.SetActive(panel == mainPanel);
        if (settingsPanel != null) settingsPanel.SetActive(panel == settingsPanel);
    }

    private void LoadSelectedChapter()
    {
        if (selectedChapter > unlockedChapterCount)
            return;

        AudioManager.Instance?.PlayButtonClick();
        SceneManager.LoadScene(gameSceneName);
    }
}
