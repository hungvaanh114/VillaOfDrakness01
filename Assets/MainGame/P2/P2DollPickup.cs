using System.Collections;
using FpsHorrorKit;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MainGame.P2
{
    [RequireComponent(typeof(Collider))]
    public sealed class P2DollPickup : MonoBehaviour, IInteractable
    {
        [Header("Audio")]
        [SerializeField] private ItemData dollItem;
        [SerializeField] private AudioClip linhVoiceClip;
        [SerializeField] private AudioSource dollAudioSource;
        [SerializeField, Range(0f, 1f)] private float audioSpatialBlend = 1f;
        [SerializeField, Min(0.1f)] private float audioMinDistance = 6f;
        [SerializeField, Min(1f)] private float audioMaxDistance = 35f;
        [SerializeField] private string audioPlayingMessage = "Đang phát nhạc...";
        [SerializeField, Min(0.1f)] private float audioMessageRefreshSeconds = 1.1f;

        [Header("Inspect")]
        [SerializeField] private Transform inspectRoot;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private string interactText = "[E] Cầm búp bê";
        [SerializeField] private Vector3 heldLocalPosition = new(0f, -0.18f, 0.68f);
        [SerializeField] private Vector3 heldLocalEulerAngles = new(12f, 0f, 0f);
        [SerializeField] private bool overrideHeldScale;
        [SerializeField] private Vector3 heldLocalScale = Vector3.one;
        [SerializeField, Min(0.01f)] private float moveSeconds = 0.18f;
        [SerializeField, Min(0f)] private float mouseRotationDegreesPerPixel = 0.18f;

        private Transform originalParent;
        private Vector3 originalLocalPosition;
        private Quaternion originalLocalRotation;
        private Vector3 originalLocalScale;
        private Collider[] colliders;
        private FpsController fpsController;
        private bool previousRaycastState = true;
        private bool previousPlayerInteracting;
        private bool isHeld;
        private bool isMoving;
        private int openedFrame;
        private bool hasPlayedDollAudio;
        private Coroutine audioMessageRoutine;

        public static bool IsAnyDollHeld { get; private set; }

        public void Configure(ItemData item, AudioClip voiceClip)
        {
            dollItem = item;
            linhVoiceClip = voiceClip;
            EnsureAudioSource();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnDisable()
        {
            if (isHeld)
                RestoreImmediately();

            StopAudioMessageRoutine();
        }

        private void Update()
        {
            if (!isHeld || isMoving)
                return;

            RotateHeldDoll();

            if (Time.frameCount <= openedFrame + 1)
                return;

            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.eKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
                StartCoroutine(PutDownRoutine());
        }

        public void Interact()
        {
            if (isMoving)
                return;

            if (isHeld)
            {
                StartCoroutine(PutDownRoutine());
                return;
            }

            StartCoroutine(PickUpRoutine());
        }

        public void Highlight()
        {
            if (!isHeld && !isMoving)
                PlayerInteract.Instance?.ChangeInteractText(interactText);
        }

        public void HoldInteract() { }
        public void UnHighlight() { }

        private IEnumerator PickUpRoutine()
        {
            ResolveReferences();
            if (inspectRoot == null)
                yield break;

            var cameraTransform = ResolveCameraTransform();
            if (cameraTransform == null)
            {
                InteractMessageScript.Instance?.ShowMessage("Chưa tìm thấy camera để cầm búp bê.");
                yield break;
            }

            isHeld = true;
            IsAnyDollHeld = true;
            isMoving = true;
            openedFrame = Time.frameCount;

            StoreOriginalPose();
            SetCollidersEnabled(false);
            LockPlayer(true);
            AudioManager.Instance?.PlayItemPickup(dollItem);
            PlayDollAudio();

            inspectRoot.SetParent(cameraTransform, true);
            yield return MoveLocal(
                inspectRoot.localPosition,
                inspectRoot.localRotation,
                inspectRoot.localScale,
                heldLocalPosition,
                Quaternion.Euler(heldLocalEulerAngles),
                overrideHeldScale ? heldLocalScale : inspectRoot.localScale);

            isMoving = false;
        }

        private IEnumerator PutDownRoutine()
        {
            if (!isHeld || inspectRoot == null)
                yield break;

            isMoving = true;
            AudioManager.Instance?.PlayGenericInteract();

            inspectRoot.SetParent(originalParent, true);
            yield return MoveLocal(
                inspectRoot.localPosition,
                inspectRoot.localRotation,
                inspectRoot.localScale,
                originalLocalPosition,
                originalLocalRotation,
                originalLocalScale);

            isHeld = false;
            IsAnyDollHeld = false;
            isMoving = false;
            SetCollidersEnabled(true);
            LockPlayer(false);
        }

        private void RotateHeldDoll()
        {
            var mouse = Mouse.current;
            var cameraTransform = ResolveCameraTransform();
            if (mouse == null || cameraTransform == null || inspectRoot == null)
                return;

            Vector2 delta = mouse.delta.ReadValue();
            if (delta.sqrMagnitude < 0.01f)
                return;

            inspectRoot.Rotate(cameraTransform.up, -delta.x * mouseRotationDegreesPerPixel, Space.World);
            inspectRoot.Rotate(cameraTransform.right, delta.y * mouseRotationDegreesPerPixel, Space.World);
        }

        private void PlayDollAudio()
        {
            EnsureAudioSource();
            if (dollAudioSource == null || linhVoiceClip == null)
                return;

            if (hasPlayedDollAudio || dollAudioSource.isPlaying)
                return;

            hasPlayedDollAudio = true;
            dollAudioSource.clip = linhVoiceClip;
            dollAudioSource.Play();
            StartAudioMessageRoutine();
        }

        private void StartAudioMessageRoutine()
        {
            StopAudioMessageRoutine();
            if (!string.IsNullOrWhiteSpace(audioPlayingMessage))
                audioMessageRoutine = StartCoroutine(AudioMessageRoutine());
        }

        private void StopAudioMessageRoutine()
        {
            if (audioMessageRoutine == null)
                return;

            StopCoroutine(audioMessageRoutine);
            audioMessageRoutine = null;
        }

        private IEnumerator AudioMessageRoutine()
        {
            while (dollAudioSource != null && dollAudioSource.isPlaying)
            {
                InteractMessageScript.Instance?.ShowMessage(audioPlayingMessage, audioMessageRefreshSeconds + 0.2f);
                yield return new WaitForSecondsRealtime(audioMessageRefreshSeconds);
            }

            audioMessageRoutine = null;
        }

        private void EnsureAudioSource()
        {
            if (dollAudioSource == null)
                dollAudioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

            dollAudioSource.playOnAwake = false;
            dollAudioSource.loop = false;
            dollAudioSource.spatialBlend = audioSpatialBlend;
            dollAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            dollAudioSource.minDistance = Mathf.Max(0.1f, audioMinDistance);
            dollAudioSource.maxDistance = Mathf.Max(dollAudioSource.minDistance + 1f, audioMaxDistance);
            if (linhVoiceClip != null && dollAudioSource.clip == null)
                dollAudioSource.clip = linhVoiceClip;
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
                inspectRoot.localPosition = Vector3.Lerp(startPosition, targetPosition, t);
                inspectRoot.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);
                inspectRoot.localScale = Vector3.Lerp(startScale, targetScale, t);
                yield return null;
            }

            inspectRoot.localPosition = targetPosition;
            inspectRoot.localRotation = targetRotation;
            inspectRoot.localScale = targetScale;
        }

        private void StoreOriginalPose()
        {
            originalParent = inspectRoot.parent;
            originalLocalPosition = inspectRoot.localPosition;
            originalLocalRotation = inspectRoot.localRotation;
            originalLocalScale = inspectRoot.localScale;
        }

        private void RestoreImmediately()
        {
            if (inspectRoot != null)
            {
                inspectRoot.SetParent(originalParent, false);
                inspectRoot.localPosition = originalLocalPosition;
                inspectRoot.localRotation = originalLocalRotation;
                inspectRoot.localScale = originalLocalScale;
            }

            isHeld = false;
            IsAnyDollHeld = false;
            isMoving = false;
            SetCollidersEnabled(true);
            LockPlayer(false);
        }

        private void LockPlayer(bool locked)
        {
            if (locked)
            {
                fpsController = GameController.Instance != null && GameController.Instance.playerController != null
                    ? GameController.Instance.playerController
                    : FindFirstObjectByType<FpsController>();

                if (fpsController != null)
                {
                    previousPlayerInteracting = fpsController.isInteracting;
                    fpsController.isInteracting = true;
                    fpsController.ForceIdleState();
                }

                if (PlayerInteract.Instance != null)
                {
                    previousRaycastState = PlayerInteract.Instance.sendRaycast;
                    PlayerInteract.Instance.sendRaycast = false;
                }

                FpsAssetsInputs.Instance?.ClearGameplayInput();
                return;
            }

            if (fpsController != null)
                fpsController.isInteracting = previousPlayerInteracting;

            if (PlayerInteract.Instance != null)
                PlayerInteract.Instance.sendRaycast = previousRaycastState;

            FpsAssetsInputs.Instance?.ClearGameplayInput();
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
            if (inspectRoot == null)
                inspectRoot = transform;
            if (colliders == null || colliders.Length == 0)
                colliders = GetComponentsInChildren<Collider>(true);
            EnsureAudioSource();
        }
    }
}
