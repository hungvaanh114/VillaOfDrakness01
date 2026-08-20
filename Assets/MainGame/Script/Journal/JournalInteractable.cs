using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FpsHorrorKit
{
    public sealed class JournalInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private JournalEntryData journalEntry;
        [SerializeField] private string interactText = "[E] Đọc nhật ký";
        [SerializeField] private string missingContentMessage = "Nhật ký chưa có nội dung.";
        [SerializeField] private GramophoneTapePlayer gramophoneTapePlayer;
        [SerializeField] private JournalPaperUI paperUi;

        private Renderer[] renderers;
        private Collider[] colliders;
        private GameController.GameState previousGameState;
        private bool wasPlayerRaycastEnabled = true;
        private bool isReading;
        private bool hasBeenRead;
        private int openedFrame;

        public static bool IsAnyPaperOpen { get; private set; }

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
            ResolvePaperUi();
        }

        private void Update()
        {
            if (!isReading || Time.frameCount <= openedFrame + 1)
                return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
                ClosePaper();
        }

        public void Interact()
        {
            if (hasBeenRead || isReading)
                return;

            if (journalEntry == null)
            {
                InteractMessageScript.Instance?.ShowMessage(missingContentMessage);
                return;
            }

            OpenPaper();
        }

        public void Highlight()
        {
            if (!hasBeenRead)
                PlayerInteract.Instance?.ChangeInteractText(interactText);
        }

        public void HoldInteract() { }
        public void UnHighlight() { }

        private void OpenPaper()
        {
            ResolvePaperUi();
            if (paperUi == null)
            {
                InteractMessageScript.Instance?.ShowMessage("Chưa tìm thấy UI nhật ký trong MainGameUICanvas.");
                return;
            }

            isReading = true;
            IsAnyPaperOpen = true;
            openedFrame = Time.frameCount;
            AudioManager.Instance?.PlayPaperPickup();
            SetModelVisible(false);
            paperUi.Show(journalEntry);

            if (GameController.Instance != null)
            {
                previousGameState = GameController.Instance.currentGameState;
                GameController.Instance.SetGameState(GameController.GameState.Cutscene);
            }

            if (PlayerInteract.Instance != null)
            {
                wasPlayerRaycastEnabled = PlayerInteract.Instance.sendRaycast;
                PlayerInteract.Instance.sendRaycast = false;
            }
        }

        private void ClosePaper()
        {
            if (!isReading)
                return;

            isReading = false;
            hasBeenRead = true;
            IsAnyPaperOpen = false;
            paperUi?.Hide();

            SetModelVisible(true);
            if (GameController.Instance != null)
                GameController.Instance.SetGameState(previousGameState);
            if (PlayerInteract.Instance != null)
                PlayerInteract.Instance.sendRaycast = wasPlayerRaycastEnabled;

            AudioManager.Instance?.PlayGenericInteract();
            var tapePlayer = ResolveGramophoneTapePlayer();
            if (!global::ChapterOneStoryFlow.TryBeginAfterStudyLetter(tapePlayer))
                tapePlayer?.PlayTape();
            Destroy(this);
        }

        private void SetModelVisible(bool visible)
        {
            foreach (var itemRenderer in renderers)
            {
                if (itemRenderer != null)
                    itemRenderer.enabled = visible;
            }

            foreach (var itemCollider in colliders)
            {
                if (itemCollider != null)
                    itemCollider.enabled = visible && !hasBeenRead;
            }
        }

        private void ResolvePaperUi()
        {
            if (paperUi != null)
                return;

            var root = GameObject.Find("JournalPaperUIRoot");
            paperUi = root != null
                ? root.GetComponent<JournalPaperUI>()
                : FindFirstObjectByType<JournalPaperUI>(FindObjectsInactive.Include);
        }

        private GramophoneTapePlayer ResolveGramophoneTapePlayer()
        {
            if (gramophoneTapePlayer != null)
                return gramophoneTapePlayer;

            gramophoneTapePlayer = FindFirstObjectByType<GramophoneTapePlayer>();
            if (gramophoneTapePlayer != null)
                return gramophoneTapePlayer;

            var gramophone = GameObject.Find("gramophone");
            return gramophone != null ? gramophone.GetComponent<GramophoneTapePlayer>() : null;
        }
    }

    public sealed class JournalPaperUI : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image dimImage;
        [SerializeField] private Image paperImage;
        [SerializeField] private TextMeshProUGUI contentText;
        [SerializeField] private Color dimColor = new(0f, 0f, 0f, 0.78f);
        [SerializeField] private Color paperColor = new(0.86f, 0.78f, 0.62f, 0.98f);
        [SerializeField] private Color inkColor = new(0.13f, 0.09f, 0.055f, 1f);

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            ResolveReferences();
            ApplyStyle();
            HideImmediate();
        }

        public void Show(JournalEntryData entry)
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            ResolveReferences();
            ApplyStyle();

            if (contentText != null)
                contentText.text = entry != null ? entry.content : string.Empty;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            IsOpen = true;
        }

        public void Hide()
        {
            HideImmediate();
        }

        private void HideImmediate()
        {
            IsOpen = false;
            if (contentText != null)
                contentText.text = string.Empty;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            gameObject.SetActive(false);
        }

        private void ResolveReferences()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

            if (dimImage == null)
                dimImage = transform.Find("JournalDim")?.GetComponent<Image>();

            if (paperImage == null)
                paperImage = transform.Find("JournalPaper")?.GetComponent<Image>();

            if (contentText == null)
                contentText = transform.Find("JournalPaper/Content")?.GetComponent<TextMeshProUGUI>();
        }

        private void ApplyStyle()
        {
            if (dimImage != null)
                dimImage.color = dimColor;

            if (paperImage != null)
                paperImage.color = paperColor;

            if (contentText != null)
            {
                contentText.color = inkColor;
                contentText.textWrappingMode = TextWrappingModes.Normal;
                contentText.overflowMode = TextOverflowModes.Overflow;
            }
        }
    }
}
