using System.Collections;
using FpsHorrorKit;
using Unity.Cinemachine;
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
        [SerializeField] private CinemachineCamera targetVirtualCamera;
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
        private CameraTarget previousVirtualCameraTarget;
        private LensSettings previousVirtualCameraLens;
        private Transform runtimeTrackingTarget;
        private Transform runtimeLookTarget;

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
            {
                RestoreVirtualCameraDriver();
                UnlockPlayer();
            }
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
            var virtualCamera = ResolveVirtualCamera();
            if (cameraTransform == null && virtualCamera == null)
            {
                sequenceRoutine = null;
                yield break;
            }

            var originalLocalPosition = cameraTransform != null ? cameraTransform.localPosition : Vector3.zero;
            var originalLocalRotation = cameraTransform != null ? cameraTransform.localRotation : Quaternion.identity;
            var originalWorldPosition = cameraTransform != null ? cameraTransform.position : virtualCamera.transform.position;
            var originalWorldRotation = cameraTransform != null ? cameraTransform.rotation : virtualCamera.transform.rotation;
            var originalLookPosition = originalWorldPosition + originalWorldRotation * Vector3.forward * 4f;
            var originalFieldOfView = GetCurrentFieldOfView();

            LockPlayer();
            PrepareVirtualCameraDriver(originalWorldPosition, originalLookPosition);

            var targetPosition = cameraZoomPoint != null
                ? cameraZoomPoint.position
                : cameraTransform != null
                    ? cameraTransform.position
                    : virtualCamera.transform.position;
            var targetLookPosition = lookTarget != null
                ? lookTarget.position
                : targetPosition + GetZoomRotation(cameraTransform != null ? cameraTransform : virtualCamera.transform) * Vector3.forward * 4f;
            yield return MoveCamera(
                cameraTransform,
                cameraTransform != null ? cameraTransform.position : virtualCamera.transform.position,
                cameraTransform != null ? cameraTransform.rotation : virtualCamera.transform.rotation,
                originalLookPosition,
                originalFieldOfView,
                targetPosition,
                GetZoomRotation(cameraTransform != null ? cameraTransform : virtualCamera.transform),
                targetLookPosition,
                zoomFieldOfView,
                enterZoomSeconds);

            puzzleCompleted = false;
            puzzle.BeginZoomInteraction(targetCamera, () => puzzleCompleted = true);
            while (!puzzleCompleted && puzzle != null && puzzle.IsActive)
            {
                FpsAssetsInputs.Instance?.ClearGameplayInput();
                yield return null;
            }

            var restoreWorldPosition = cameraTransform != null && cameraTransform.parent != null
                ? cameraTransform.parent.TransformPoint(originalLocalPosition)
                : originalWorldPosition;
            var restoreWorldRotation = cameraTransform != null && cameraTransform.parent != null
                ? cameraTransform.parent.rotation * originalLocalRotation
                : originalWorldRotation;

            yield return MoveCamera(
                cameraTransform,
                cameraTransform != null ? cameraTransform.position : originalWorldPosition,
                cameraTransform != null ? cameraTransform.rotation : originalWorldRotation,
                targetLookPosition,
                GetCurrentFieldOfView(),
                restoreWorldPosition,
                restoreWorldRotation,
                originalLookPosition,
                originalFieldOfView,
                exitZoomSeconds);

            RestoreVirtualCameraDriver();
            if (cameraTransform != null)
            {
                cameraTransform.localPosition = originalLocalPosition;
                cameraTransform.localRotation = originalLocalRotation;
            }
            SetFieldOfView(originalFieldOfView);

            UnlockPlayer();
            sequenceRoutine = null;
        }

        private IEnumerator MoveCamera(
            Transform cameraTransform,
            Vector3 fromPosition,
            Quaternion fromRotation,
            Vector3 fromLookPosition,
            float fromFov,
            Vector3 toPosition,
            Quaternion toRotation,
            Vector3 toLookPosition,
            float toFov,
            float seconds)
        {
            float timer = 0f;
            seconds = Mathf.Max(0.01f, seconds);
            while (timer < seconds)
            {
                timer += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / seconds));
                ApplyCameraPose(
                    cameraTransform,
                    Vector3.Lerp(fromPosition, toPosition, t),
                    Quaternion.Slerp(fromRotation, toRotation, t),
                    Vector3.Lerp(fromLookPosition, toLookPosition, t),
                    Mathf.Lerp(fromFov, toFov, t));
                FpsAssetsInputs.Instance?.ClearGameplayInput();
                yield return null;
            }

            ApplyCameraPose(cameraTransform, toPosition, toRotation, toLookPosition, toFov);
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

        private void ApplyCameraPose(Transform cameraTransform, Vector3 position, Quaternion rotation, Vector3 lookPosition, float fieldOfView)
        {
            if (runtimeTrackingTarget != null)
            {
                runtimeTrackingTarget.position = position;
                runtimeTrackingTarget.rotation = rotation;
            }

            if (runtimeLookTarget != null)
                runtimeLookTarget.position = lookPosition;

            if (runtimeTrackingTarget == null && cameraTransform != null)
            {
                cameraTransform.position = position;
                cameraTransform.rotation = rotation;
            }

            SetFieldOfView(fieldOfView);
        }

        private void PrepareVirtualCameraDriver(Vector3 startPosition, Vector3 startLookPosition)
        {
            var virtualCamera = ResolveVirtualCamera();
            if (virtualCamera == null)
                return;

            previousVirtualCameraTarget = virtualCamera.Target;
            previousVirtualCameraLens = virtualCamera.Lens;

            runtimeTrackingTarget = new GameObject("P2_WallPlankZoom_RuntimeCameraTarget").transform;
            runtimeTrackingTarget.hideFlags = HideFlags.HideAndDontSave;
            runtimeTrackingTarget.SetPositionAndRotation(startPosition, virtualCamera.transform.rotation);

            runtimeLookTarget = new GameObject("P2_WallPlankZoom_RuntimeLookTarget").transform;
            runtimeLookTarget.hideFlags = HideFlags.HideAndDontSave;
            runtimeLookTarget.position = startLookPosition;

            virtualCamera.Target.TrackingTarget = runtimeTrackingTarget;
            virtualCamera.Target.LookAtTarget = runtimeLookTarget;
            virtualCamera.Target.CustomLookAtTarget = true;
        }

        private void RestoreVirtualCameraDriver()
        {
            var virtualCamera = ResolveVirtualCamera();
            if (virtualCamera != null)
            {
                virtualCamera.Target = previousVirtualCameraTarget;
                virtualCamera.Lens = previousVirtualCameraLens;
            }

            if (runtimeTrackingTarget != null)
                Destroy(runtimeTrackingTarget.gameObject);
            if (runtimeLookTarget != null)
                Destroy(runtimeLookTarget.gameObject);

            runtimeTrackingTarget = null;
            runtimeLookTarget = null;
        }

        private float GetCurrentFieldOfView()
        {
            var virtualCamera = ResolveVirtualCamera();
            if (virtualCamera != null)
                return virtualCamera.Lens.FieldOfView;
            return targetCamera != null ? targetCamera.fieldOfView : 60f;
        }

        private void SetFieldOfView(float fieldOfView)
        {
            var virtualCamera = ResolveVirtualCamera();
            if (virtualCamera != null)
            {
                var lens = virtualCamera.Lens;
                lens.FieldOfView = fieldOfView;
                virtualCamera.Lens = lens;
            }

            if (targetCamera != null)
                targetCamera.fieldOfView = fieldOfView;
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

        private CinemachineCamera ResolveVirtualCamera()
        {
            if (targetVirtualCamera != null)
                return targetVirtualCamera;

            var fpsController = GameController.Instance != null && GameController.Instance.playerController != null
                ? GameController.Instance.playerController
                : FindFirstObjectByType<FpsController>();
            if (fpsController != null)
                targetVirtualCamera = fpsController.virtualCamera;

            return targetVirtualCamera;
        }

        private void ResolveReferences()
        {
            if (audioLog == null)
                audioLog = GetComponent<P2AudioLogItem>();
            if (targetCamera == null)
                targetCamera = Camera.main;
            ResolveVirtualCamera();
            if (puzzle == null)
                puzzle = FindFirstObjectByType<P2KnockPlankPuzzle>(FindObjectsInactive.Include);
        }
    }
}
