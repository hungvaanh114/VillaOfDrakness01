using System.Collections;
using FpsHorrorKit;
using UnityEngine;

namespace MainGame.P2
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(P2AudioLogItem))]
    public sealed class P2KnockPlankZoomSequence : MonoBehaviour
    {
        [Header("Trigger")]
        [SerializeField] private P2AudioLogItem audioLog;
        [SerializeField] private bool triggerOnce = true;
        [SerializeField, Min(0f)] private float delayAfterAudio = 0.35f;

        [Header("Camera")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform cameraZoomPoint;
        [SerializeField] private Transform lookTarget;
        [SerializeField, Range(20f, 80f)] private float zoomFieldOfView = 38f;
        [SerializeField, Min(0.01f)] private float enterZoomSeconds = 0.65f;
        [SerializeField, Min(0.01f)] private float exitZoomSeconds = 0.5f;

        [Header("Puzzle")]
        [SerializeField] private P2KnockPlankPuzzle puzzle;

        private GameController.GameState previousGameState;
        private bool previousRaycastState = true;
        private bool hasTriggered;
        private bool puzzleCompleted;
        private Coroutine sequenceRoutine;

        public static bool IsAnyZoomActive { get; private set; }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (audioLog != null)
                audioLog.PlaybackCompleted += HandlePlaybackCompleted;
        }

        private void OnDisable()
        {
            if (audioLog != null)
                audioLog.PlaybackCompleted -= HandlePlaybackCompleted;

            if (IsAnyZoomActive)
                UnlockPlayer();
        }

        private void HandlePlaybackCompleted(P2AudioLogItem item)
        {
            if (triggerOnce && hasTriggered)
                return;
            if (sequenceRoutine != null)
                return;

            hasTriggered = true;
            sequenceRoutine = StartCoroutine(SequenceRoutine());
        }

        private IEnumerator SequenceRoutine()
        {
            if (delayAfterAudio > 0f)
                yield return new WaitForSeconds(delayAfterAudio);

            ResolveReferences();
            if (puzzle == null)
            {
                sequenceRoutine = null;
                yield break;
            }

            var cameraTransform = ResolveCameraTransform();
            if (cameraTransform == null)
            {
                sequenceRoutine = null;
                yield break;
            }

            var originalLocalPosition = cameraTransform.localPosition;
            var originalLocalRotation = cameraTransform.localRotation;
            var originalFieldOfView = targetCamera.fieldOfView;

            LockPlayer();

            var targetPosition = cameraZoomPoint != null ? cameraZoomPoint.position : cameraTransform.position;
            var targetRotation = GetZoomRotation(cameraTransform);
            yield return MoveCamera(
                cameraTransform,
                cameraTransform.position,
                cameraTransform.rotation,
                targetCamera.fieldOfView,
                targetPosition,
                targetRotation,
                zoomFieldOfView,
                enterZoomSeconds);

            puzzleCompleted = false;
            puzzle.BeginZoomInteraction(targetCamera, () => puzzleCompleted = true);
            while (!puzzleCompleted && puzzle != null && puzzle.IsActive)
            {
                FpsAssetsInputs.Instance?.ClearGameplayInput();
                yield return null;
            }

            var restoreWorldPosition = cameraTransform.parent != null
                ? cameraTransform.parent.TransformPoint(originalLocalPosition)
                : originalLocalPosition;
            var restoreWorldRotation = cameraTransform.parent != null
                ? cameraTransform.parent.rotation * originalLocalRotation
                : originalLocalRotation;

            yield return MoveCamera(
                cameraTransform,
                cameraTransform.position,
                cameraTransform.rotation,
                targetCamera.fieldOfView,
                restoreWorldPosition,
                restoreWorldRotation,
                originalFieldOfView,
                exitZoomSeconds);

            cameraTransform.localPosition = originalLocalPosition;
            cameraTransform.localRotation = originalLocalRotation;
            targetCamera.fieldOfView = originalFieldOfView;

            UnlockPlayer();
            sequenceRoutine = null;
        }

        private IEnumerator MoveCamera(
            Transform cameraTransform,
            Vector3 fromPosition,
            Quaternion fromRotation,
            float fromFov,
            Vector3 toPosition,
            Quaternion toRotation,
            float toFov,
            float seconds)
        {
            float timer = 0f;
            seconds = Mathf.Max(0.01f, seconds);
            while (timer < seconds)
            {
                timer += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / seconds));
                cameraTransform.position = Vector3.Lerp(fromPosition, toPosition, t);
                cameraTransform.rotation = Quaternion.Slerp(fromRotation, toRotation, t);
                targetCamera.fieldOfView = Mathf.Lerp(fromFov, toFov, t);
                FpsAssetsInputs.Instance?.ClearGameplayInput();
                yield return null;
            }

            cameraTransform.position = toPosition;
            cameraTransform.rotation = toRotation;
            targetCamera.fieldOfView = toFov;
        }

        private Quaternion GetZoomRotation(Transform cameraTransform)
        {
            if (cameraZoomPoint != null && lookTarget == null)
                return cameraZoomPoint.rotation;

            if (lookTarget == null)
                return cameraTransform.rotation;

            var direction = lookTarget.position - (cameraZoomPoint != null ? cameraZoomPoint.position : cameraTransform.position);
            return direction.sqrMagnitude > 0.001f ? Quaternion.LookRotation(direction.normalized, Vector3.up) : cameraTransform.rotation;
        }

        private void LockPlayer()
        {
            IsAnyZoomActive = true;
            if (GameController.Instance != null)
            {
                previousGameState = GameController.Instance.currentGameState;
                GameController.Instance.SetGameState(GameController.GameState.Puzzle);
            }

            if (PlayerInteract.Instance != null)
            {
                previousRaycastState = PlayerInteract.Instance.sendRaycast;
                PlayerInteract.Instance.sendRaycast = false;
            }

            P2GameController.Instance?.LockInput(true);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            FpsAssetsInputs.Instance?.ClearGameplayInput();
        }

        private void UnlockPlayer()
        {
            IsAnyZoomActive = false;
            if (PlayerInteract.Instance != null)
                PlayerInteract.Instance.sendRaycast = previousRaycastState;

            P2GameController.Instance?.LockInput(false);
            if (GameController.Instance != null)
                GameController.Instance.SetGameState(previousGameState);

            FpsAssetsInputs.Instance?.ClearGameplayInput();
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
            if (audioLog == null)
                audioLog = GetComponent<P2AudioLogItem>();
            if (targetCamera == null)
                targetCamera = Camera.main;
            if (puzzle == null)
                puzzle = FindFirstObjectByType<P2KnockPlankPuzzle>(FindObjectsInactive.Include);
        }
    }
}
