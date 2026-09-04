using System.Collections;
using FpsHorrorKit;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MainGame.P2
{
    public sealed class P2JournalPaperInteractable : MonoBehaviour, IInteractable
    {
        [Header("Paper View")]
        [SerializeField] private Transform paperRoot;
        [SerializeField] private TextMeshProUGUI paperText;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private string interactText = "[E] Đọc nhật ký";
        [SerializeField] private Vector3 heldLocalPosition = new(0f, -0.16f, 0.58f);
        [SerializeField] private Vector3 heldLocalEulerAngles = new(68f, 0f, 0f);
        [SerializeField] private bool overrideHeldScale;
        [SerializeField] private Vector3 heldLocalScale = new(0.55f, 0.002f, 0.75f);
        [SerializeField, Min(0.01f)] private float moveSeconds = 0.22f;

        [Header("Reaction")]
        [SerializeField] private AudioClip ngocReturnVoiceClip;
        [SerializeField, TextArea(1, 3)] private string ngocReturnSubtitle = "Gió không đổi hướng. Phòng bé Linh. Tường phía tây.";
        [SerializeField, Min(0.1f)] private float fallbackSubtitleSeconds = 4f;
        [SerializeField] private bool playReactionOnce = true;

        private Transform originalParent;
        private Vector3 originalLocalPosition;
        private Quaternion originalLocalRotation;
        private Vector3 originalLocalScale;
        private Collider[] colliders;
        private GameController.GameState previousGameState;
        private bool previousRaycastState = true;
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
            if (paperRoot == null)
                yield break;

            var cameraTransform = ResolveCameraTransform();
            if (cameraTransform == null)
            {
                InteractMessageScript.Instance?.ShowMessage("Chưa tìm thấy camera để xem nhật ký.");
                yield break;
            }

            isReading = true;
            IsAnyPaperOpen = true;
            isMoving = true;
            openedFrame = Time.frameCount;
            StoreOriginalPose();
            SetCollidersEnabled(false);

            if (paperText != null)
                paperText.gameObject.SetActive(true);

            AudioManager.Instance?.PlayPaperPickup();
            LockPlayer(true);

            paperRoot.SetParent(cameraTransform, true);
            yield return MoveLocal(
                paperRoot.localPosition,
                Quaternion.Euler(paperRoot.localEulerAngles),
                paperRoot.localScale,
                heldLocalPosition,
                Quaternion.Euler(heldLocalEulerAngles),
                overrideHeldScale ? heldLocalScale : paperRoot.localScale);

            isMoving = false;
        }

        private IEnumerator ClosePaperRoutine()
        {
            if (!isReading || paperRoot == null)
                yield break;

            isMoving = true;
            AudioManager.Instance?.PlayGenericInteract();
            paperRoot.SetParent(originalParent, true);
            yield return MoveLocal(
                paperRoot.localPosition,
                paperRoot.localRotation,
                paperRoot.localScale,
                originalLocalPosition,
                originalLocalRotation,
                originalLocalScale);

            isReading = false;
            IsAnyPaperOpen = false;
            isMoving = false;
            SetCollidersEnabled(true);
            LockPlayer(false);
            PlayReturnReaction();
        }

        private IEnumerator MoveLocal(
            Vector3 startPosition,
            Quaternion startRotation,
            Vector3 startScale,
            Vector3 targetPosition,
            Quaternion targetRotation,
            Vector3 targetScale)
        {
            float timer = 0f;
            while (timer < moveSeconds)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / moveSeconds));
                paperRoot.localPosition = Vector3.Lerp(startPosition, targetPosition, t);
                paperRoot.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);
                paperRoot.localScale = Vector3.Lerp(startScale, targetScale, t);
                yield return null;
            }

            paperRoot.localPosition = targetPosition;
            paperRoot.localRotation = targetRotation;
            paperRoot.localScale = targetScale;
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

        private void StoreOriginalPose()
        {
            originalParent = paperRoot.parent;
            originalLocalPosition = paperRoot.localPosition;
            originalLocalRotation = paperRoot.localRotation;
            originalLocalScale = paperRoot.localScale;
        }

        private void RestoreImmediately()
        {
            if (paperRoot != null)
            {
                paperRoot.SetParent(originalParent, false);
                paperRoot.localPosition = originalLocalPosition;
                paperRoot.localRotation = originalLocalRotation;
                paperRoot.localScale = originalLocalScale;
            }

            isReading = false;
            IsAnyPaperOpen = false;
            isMoving = false;
            SetCollidersEnabled(true);
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
                return;
            }

            if (GameController.Instance != null)
                GameController.Instance.SetGameState(previousGameState);

            if (PlayerInteract.Instance != null)
                PlayerInteract.Instance.sendRaycast = previousRaycastState;

            P2GameController.Instance?.LockInput(false);
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

        private Transform ResolveCameraTransform()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
            if (targetCamera == null)
                targetCamera = FindFirstObjectByType<Camera>();
            return targetCamera != null ? targetCamera.transform : null;
        }

        private void ResolveReferences()
        {
            if (paperRoot == null)
                paperRoot = transform;
            if (paperText == null)
                paperText = GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }
}
