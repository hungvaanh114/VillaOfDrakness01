using System.Collections;
using FpsHorrorKit;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MainGame.P2
{
    public sealed class P2JournalPaperInteractable : MonoBehaviour, IInteractable
    {
        [Header("Paper View")]
        [SerializeField] private Transform paperRoot;
        [SerializeField] private TextMeshProUGUI paperText;
        [SerializeField] private CinemachineCamera journalCamera;
        [SerializeField] private string interactText = "[E] \u0110\u1ecdc nh\u1eadt k\u00fd";
        [SerializeField] private int readingCameraPriority = 100;
        [SerializeField, Min(0f)] private float cameraBlendSeconds = 0.25f;

        [Header("Reaction")]
        [SerializeField] private AudioClip ngocReturnVoiceClip;
        [SerializeField, TextArea(1, 3)] private string ngocReturnSubtitle = "Gi\u00f3 kh\u00f4ng \u0111\u1ed5i h\u01b0\u1edbng. Ph\u00f2ng b\u00e9 Linh. T\u01b0\u1eddng ph\u00eda t\u00e2y.";
        [SerializeField, Min(0.1f)] private float fallbackSubtitleSeconds = 4f;
        [SerializeField] private bool playReactionOnce = true;

        private Collider[] colliders;
        private GameController.GameState previousGameState;
        private bool previousRaycastState = true;
        private PrioritySettings previousJournalPriority;
        private bool previousJournalCameraEnabled;
        private bool previousJournalCameraActive;
        private bool previousPaperTextActive;
        private bool isReading;
        private bool isMoving;
        private bool reactionPlayed;
        private int openedFrame;

        public static bool IsAnyPaperOpen { get; private set; }

        private void Awake()
        {
            ResolveReferences();
            colliders = GetComponentsInChildren<Collider>(true);
        }

        private void OnDisable()
        {
            if (isReading)
                RestoreImmediately();
        }

        private void Update()
        {
            if (!isReading || isMoving || Time.frameCount <= openedFrame + 1)
                return;

            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.eKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
                StartCoroutine(ClosePaperRoutine());
        }

        public void Interact()
        {
            if (isMoving)
                return;

            if (isReading)
            {
                StartCoroutine(ClosePaperRoutine());
                return;
            }

            StartCoroutine(OpenPaperRoutine());
        }

        public void Highlight()
        {
            if (!isReading && !isMoving)
                PlayerInteract.Instance?.ChangeInteractText(interactText);
        }

        public void HoldInteract() { }
        public void UnHighlight() { }

        private IEnumerator OpenPaperRoutine()
        {
            ResolveReferences();
            if (journalCamera == null)
            {
                InteractMessageScript.Instance?.ShowMessage("Ch\u01b0a g\u00e1n camera \u0111\u1ec3 xem nh\u1eadt k\u00fd.");
                yield break;
            }

            isReading = true;
            IsAnyPaperOpen = true;
            isMoving = true;
            openedFrame = Time.frameCount;
            StoreCameraState();
            SetCollidersEnabled(false);
            SetPaperTextReading(true);

            AudioManager.Instance?.PlayPaperPickup();
            LockPlayer(true);
            SwitchJournalCamera(true);

            yield return WaitRealtime(cameraBlendSeconds);
            isMoving = false;
        }

        private IEnumerator ClosePaperRoutine()
        {
            if (!isReading)
                yield break;

            isMoving = true;
            AudioManager.Instance?.PlayGenericInteract();
            SwitchJournalCamera(false);

            yield return WaitRealtime(cameraBlendSeconds);

            RestoreJournalCameraState();
            isReading = false;
            IsAnyPaperOpen = false;
            isMoving = false;
            SetCollidersEnabled(true);
            SetPaperTextReading(false);
            LockPlayer(false);
            PlayReturnReaction();
        }

        private static IEnumerator WaitRealtime(float seconds)
        {
            float timer = 0f;
            seconds = Mathf.Max(0f, seconds);
            while (timer < seconds)
            {
                timer += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void PlayReturnReaction()
        {
            if (playReactionOnce && reactionPlayed)
                return;

            reactionPlayed = true;
            var clip = ngocReturnVoiceClip != null
                ? ngocReturnVoiceClip
                : Resources.Load<AudioData>("Audio/AudioData")?.p2Ngoc04;

            float duration = AudioManager.Instance != null
                ? AudioManager.Instance.PlayPlayerVoice(clip)
                : 0f;

            if (!string.IsNullOrWhiteSpace(ngocReturnSubtitle))
                InteractMessageScript.Instance?.ShowMessage($"\"{ngocReturnSubtitle}\"", duration > 0f ? duration : fallbackSubtitleSeconds);
        }

        private void RestoreImmediately()
        {
            SwitchJournalCamera(false);
            RestoreJournalCameraState();
            isReading = false;
            IsAnyPaperOpen = false;
            isMoving = false;
            SetCollidersEnabled(true);
            SetPaperTextReading(false);
            LockPlayer(false);
        }

        private void LockPlayer(bool locked)
        {
            if (locked)
            {
                if (GameController.Instance != null)
                {
                    previousGameState = GameController.Instance.currentGameState;
                    GameController.Instance.SetGameState(GameController.GameState.Cutscene);
                }

                if (PlayerInteract.Instance != null)
                {
                    previousRaycastState = PlayerInteract.Instance.sendRaycast;
                    PlayerInteract.Instance.sendRaycast = false;
                }

                P2GameController.Instance?.LockInput(true);
                FpsAssetsInputs.Instance?.ClearGameplayInput();
                return;
            }

            if (GameController.Instance != null)
                GameController.Instance.SetGameState(previousGameState);

            if (PlayerInteract.Instance != null)
                PlayerInteract.Instance.sendRaycast = previousRaycastState;

            P2GameController.Instance?.LockInput(false);
            FpsAssetsInputs.Instance?.ClearGameplayInput();
        }

        private void StoreCameraState()
        {
            if (journalCamera == null)
                return;

            previousJournalPriority = journalCamera.Priority;
            previousJournalCameraEnabled = journalCamera.enabled;
            previousJournalCameraActive = journalCamera.gameObject.activeSelf;
        }

        private void SwitchJournalCamera(bool reading)
        {
            if (journalCamera == null)
                return;

            if (reading)
            {
                if (!journalCamera.gameObject.activeSelf)
                    journalCamera.gameObject.SetActive(true);

                journalCamera.enabled = true;
                journalCamera.Priority = readingCameraPriority;
                return;
            }

            journalCamera.Priority = previousJournalPriority;
        }

        private void RestoreJournalCameraState()
        {
            if (journalCamera == null)
                return;

            journalCamera.enabled = previousJournalCameraEnabled;
            if (journalCamera.gameObject.activeSelf != previousJournalCameraActive)
                journalCamera.gameObject.SetActive(previousJournalCameraActive);
        }

        private void SetPaperTextReading(bool reading)
        {
            if (paperText == null)
                return;

            if (reading)
                previousPaperTextActive = paperText.gameObject.activeSelf;

            paperText.gameObject.SetActive(reading || previousPaperTextActive);
        }

        private void SetCollidersEnabled(bool enabled)
        {
            if (colliders == null)
                return;

            foreach (var item in colliders)
            {
                if (item != null)
                    item.enabled = enabled;
            }
        }

        private void ResolveReferences()
        {
            if (paperRoot == null)
                paperRoot = transform;
            if (paperText == null)
                paperText = GetComponentInChildren<TextMeshProUGUI>(true);
            if (journalCamera == null)
                journalCamera = GetComponentInChildren<CinemachineCamera>(true);
        }
    }
}
