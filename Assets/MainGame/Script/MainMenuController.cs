using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FpsHorrorKit;

public sealed class MainMenuController : MonoBehaviour
{
    private const string PartOneLabel = "PH\u1EA6N 1";
    private const string PartTwoLabel = "PH\u1EA6N 2";
    private const string PartTwoLockedLabel = "PH\u1EA6N 2 - KH\u00D3A";

    [Header("Scene")]
    [SerializeField] private string gameSceneName = "Game";
    [SerializeField] private string partTwoSceneName = "GameP2";

    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Main Buttons")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    [Header("Part Selector")]
    [SerializeField] private Button previousPartButton;
    [SerializeField] private Button nextPartButton;
    [SerializeField] private TMP_Text selectedPartText;

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
        ResolvePartButtons();
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
        if (!ChapterOneCheckpointManager.HasContinueSave)
        {
            if (selectedChapter == 2 && PlayerPrefsMainMenuSaveService.IsPartTwoUnlocked)
            {
                LoadSelectedChapter();
                return;
            }

            return;
        }

        if (saveService.TryLoad(out var latestSaveData))
            saveData = latestSaveData;

        LoadSelectedChapter();
    }

    public void ContinueFromCheckpoint()
    {
        ContinueGame();
    }

    public void StartNewGame()
    {
        if (!IsSelectedChapterUnlocked())
            return;

        if (selectedChapter == 2)
        {
            saveData = PlayerPrefsMainMenuSaveService.StartNewPart(2);
            RefreshChapterCard();
            LoadSelectedChapter();
            return;
        }

        ChapterOneCheckpointManager.ClearSavedCheckpoint();
        saveData = saveService.StartNewGame();
        selectedChapter = 1;
        unlockedChapterCount = Mathf.Max(unlockedChapterCount, PlayerPrefsMainMenuSaveService.HighestUnlockedPart);
        RefreshChapterCard();
        LoadSelectedChapter();
    }

    public void StartNewGameFromBeginning()
    {
        StartNewGame();
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

    public void StartPartOne()
    {
        selectedChapter = 1;
        RefreshChapterCard();
    }

    public void StartPartTwo()
    {
        selectedChapter = 2;
        RefreshChapterCard();
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

        if (previousPartButton != null) previousPartButton.onClick.AddListener(SelectPreviousChapter);
        if (nextPartButton != null) nextPartButton.onClick.AddListener(SelectNextChapter);
    }

    private void RefreshSaveCard()
    {
        if (!saveService.TryLoad(out saveData))
            saveData = new MainMenuSaveData(1, "--/--/----  --:--", "00:00:00");

        unlockedChapterCount = Mathf.Max(unlockedChapterCount, PlayerPrefsMainMenuSaveService.HighestUnlockedPart);
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

        RefreshPartSelector(isUnlocked);

        if (continueButton != null)
        {
            bool hasContinueSave = CanContinueSelectedPart();
            continueButton.gameObject.SetActive(hasContinueSave);
            continueButton.interactable = hasContinueSave;
        }

        if (newGameButton != null)
            newGameButton.interactable = isUnlocked;
    }

    private void ShowPanel(GameObject panel)
    {
        if (mainPanel != null) mainPanel.SetActive(panel == mainPanel);
        if (settingsPanel != null) settingsPanel.SetActive(panel == settingsPanel);
    }

    private void LoadSelectedChapter()
    {
        if (!IsSelectedChapterUnlocked())
            return;

        AudioManager.Instance?.PlayButtonClick();
        SceneManager.LoadScene(selectedChapter == 2 && !string.IsNullOrWhiteSpace(partTwoSceneName) ? partTwoSceneName : gameSceneName);
    }

    private void ResolvePartButtons()
    {
        if (previousPartButton == null)
            previousPartButton = FindButtonByName("PartPreviousButton");
        if (nextPartButton == null)
            nextPartButton = FindButtonByName("PartNextButton");
        if (selectedPartText == null)
            selectedPartText = FindTextByName("PartSelectorLabel");
    }

    private static Button FindButtonByName(string buttonName)
    {
        var activeObject = GameObject.Find(buttonName);
        if (activeObject != null && activeObject.TryGetComponent(out Button activeButton))
            return activeButton;

        foreach (var button in Resources.FindObjectsOfTypeAll<Button>())
        {
            if (button == null || button.gameObject.name != buttonName || !button.gameObject.scene.IsValid())
                continue;

            return button;
        }

        return null;
    }

    private static TMP_Text FindTextByName(string textName)
    {
        var activeObject = GameObject.Find(textName);
        if (activeObject != null && activeObject.TryGetComponent(out TMP_Text activeText))
            return activeText;

        foreach (var text in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            if (text == null || text.gameObject.name != textName || !text.gameObject.scene.IsValid())
                continue;

            return text;
        }

        return null;
    }

    private void RefreshPartSelector(bool isSelectedPartUnlocked)
    {
        if (selectedPartText != null)
            selectedPartText.text = selectedChapter == 1
                ? PartOneLabel
                : (isSelectedPartUnlocked ? PartTwoLabel : PartTwoLockedLabel);

        if (previousPartButton != null)
            previousPartButton.gameObject.SetActive(selectedChapter > 1);
        if (nextPartButton != null)
            nextPartButton.gameObject.SetActive(selectedChapter < chapterCount);
    }

    private bool IsSelectedChapterUnlocked()
    {
        return selectedChapter <= unlockedChapterCount;
    }

    private bool CanContinueSelectedPart()
    {
        if (selectedChapter == 1)
            return ChapterOneCheckpointManager.HasContinueSave;

        if (selectedChapter == 2)
            return PlayerPrefsMainMenuSaveService.IsPartTwoUnlocked;

        return false;
    }
}
