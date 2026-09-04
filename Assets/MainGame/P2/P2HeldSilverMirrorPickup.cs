using System.Collections;
using FpsHorrorKit;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MainGame.P2
{
    [RequireComponent(typeof(Collider))]
    public sealed class P2HeldSilverMirrorPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform heldRoot;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private string handRootName = "LeftHandProp";
        [SerializeField] private string fallbackHandRootName = "LeftHand";
        [SerializeField] private AudioClip pickupClip;
        [SerializeField] private string interactText = "[E] Cầm gương bạc";
        [SerializeField] private Vector3 heldLocalPosition = new Vector3(0.02f, 0.04f, 0.02f);
        [SerializeField] private Vector3 heldLocalEulerAngles = new Vector3(0f, 90f, 0f);
        [SerializeField] private bool overrideHeldScale;
        [SerializeField] private Vector3 heldLocalScale = Vector3.one;
        [SerializeField, Min(0.01f)] private float moveSeconds = 0.18f;
        [SerializeField, Min(0f)] private float mouseRotationDegreesPerPixel = 0.12f;
        [SerializeField] private bool breakHouseGlassWhenPickedUp = true;
        [SerializeField] private bool lockPlayerWhileHeld;
        [SerializeField] private bool allowPutDown;
        [SerializeField] private bool allowMouseRotation;
        [SerializeField] private bool matchHandLayerWhileHeld = true;
        [SerializeField] private bool showHeldMirrorOnlyInCutscene = true;

        private Transform originalParent;
        private Vector3 originalLocalPosition;
        private Quaternion originalLocalRotation;
        private Vector3 originalLocalScale;
        private Collider[] colliders;
        private Renderer[] heldRenderers;
        private bool[] originalRendererStates;
        private Transform[] layerTargets;
        private int[] originalLayers;
        private FpsController fpsController;
        private bool previousRaycastState = true;
        private bool previousPlayerInteracting;
        private bool isHeld;
        private bool isMoving;
        private bool hasTriggeredGlassBreak;
        private int pickedFrame;

        public static bool HasAnySilverMirrorBeenHeld { get; private set; }
        public bool IsHeld => isHeld;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ResetHeldMirrorState()
        {
            HasAnySilverMirrorBeenHeld = false;
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnDisable()
        {
            if (isHeld)
            {
                if (gameObject.activeInHierarchy)
                    RestoreImmediately();
                else
                    SetPlayerInspectLock(false);
            }
        }

        private void Update()
        {
            if (!isHeld)
                return;

            UpdateHeldVisibility();

            if (isMoving)
                return;

            if (allowMouseRotation)
                RotateHeldMirror();

            if (Time.frameCount <= pickedFrame + 1)
                return;

            var keyboard = Keyboard.current;
            if (allowPutDown && keyboard != null && (keyboard.eKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
                StartCoroutine(PutDownRoutine());
        }

        public void Interact()
        {
            if (isMoving)
                return;

            if (isHeld)
            {
                if (allowPutDown)
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

            var holdParent = ResolveHoldParent();
            if (holdParent == null)
            {
                InteractMessageScript.Instance?.ShowMessage("Chưa tìm thấy camera để cầm gương bạc.");
                yield break;
            }

            isHeld = true;
            isMoving = true;
            HasAnySilverMirrorBeenHeld = true;
            pickedFrame = Time.frameCount;

            StoreOriginalPose();
            SetCollidersEnabled(false);
            SetPlayerInspectLock(true);
            PlayPickupSound();

            heldRoot.SetParent(holdParent, true);
            ApplyHeldLayer(holdParent.gameObject.layer);
            UpdateHeldVisibility();
            yield return MoveLocal(
                heldRoot.localPosition,
                heldRoot.localRotation,
                heldRoot.localScale,
                heldLocalPosition,
                Quaternion.Euler(heldLocalEulerAngles),
                overrideHeldScale ? heldLocalScale : heldRoot.localScale);

            if (breakHouseGlassWhenPickedUp && !hasTriggeredGlassBreak)
            {
                hasTriggeredGlassBreak = true;
                if (P2GameController.Instance != null)
                    P2GameController.Instance.RegisterSilverMirrorHeld();
                else
                    P2BreakableWindowGlass.BreakAllHouseGlass();
            }

            isMoving = false;
        }

        private IEnumerator PutDownRoutine()
        {
            if (!isHeld || heldRoot == null)
                yield break;

            isMoving = true;
            AudioManager.Instance?.PlayGenericInteract();

            heldRoot.SetParent(originalParent, true);
            yield return MoveLocal(
                heldRoot.localPosition,
                heldRoot.localRotation,
                heldRoot.localScale,
                originalLocalPosition,
                originalLocalRotation,
                originalLocalScale);

            isHeld = false;
            isMoving = false;
            RestoreLayers();
            RestoreRendererStates();
            SetCollidersEnabled(true);
            SetPlayerInspectLock(false);
        }

        private void RotateHeldMirror()
        {
            var mouse = Mouse.current;
            var cameraTransform = ResolveCameraTransform();
            if (mouse == null || cameraTransform == null || heldRoot == null)
                return;

            Vector2 delta = mouse.delta.ReadValue();
            if (delta.sqrMagnitude < 0.01f)
                return;

            heldRoot.Rotate(cameraTransform.up, -delta.x * mouseRotationDegreesPerPixel, Space.World);
            heldRoot.Rotate(cameraTransform.right, delta.y * mouseRotationDegreesPerPixel, Space.World);
        }

        private void ResolveReferences()
        {
            if (heldRoot == null)
                heldRoot = transform;
            if (targetCamera == null)
                targetCamera = Camera.main;
            if (colliders == null || colliders.Length == 0)
                colliders = GetComponentsInChildren<Collider>(true);
            if (heldRenderers == null || heldRenderers.Length == 0)
                heldRenderers = heldRoot != null
                    ? heldRoot.GetComponentsInChildren<Renderer>(true)
                    : GetComponentsInChildren<Renderer>(true);
            if (fpsController == null)
                fpsController = FindFirstObjectByType<FpsController>();
        }

        private Transform ResolveHoldParent()
        {
            var handRoot = FindSceneTransform(handRootName);
            if (handRoot != null)
                return handRoot;

            handRoot = FindSceneTransform(fallbackHandRootName);
            if (handRoot != null)
                return handRoot;

            return ResolveCameraTransform();
        }

        private Transform ResolveCameraTransform()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
            return targetCamera != null ? targetCamera.transform : null;
        }

        private void StoreOriginalPose()
        {
            originalParent = heldRoot.parent;
            originalLocalPosition = heldRoot.localPosition;
            originalLocalRotation = heldRoot.localRotation;
            originalLocalScale = heldRoot.localScale;
            CaptureOriginalLayers();
            CaptureOriginalRendererStates();
        }

        private void CaptureOriginalLayers()
        {
            layerTargets = heldRoot != null
                ? heldRoot.GetComponentsInChildren<Transform>(true)
                : System.Array.Empty<Transform>();
            originalLayers = new int[layerTargets.Length];
            for (int i = 0; i < layerTargets.Length; i++)
                originalLayers[i] = layerTargets[i] != null ? layerTargets[i].gameObject.layer : 0;
        }

        private void ApplyHeldLayer(int layer)
        {
            if (!matchHandLayerWhileHeld || layer < 0 || heldRoot == null)
                return;

            if (layerTargets == null || layerTargets.Length == 0)
                layerTargets = heldRoot.GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < layerTargets.Length; i++)
            {
                if (layerTargets[i] != null)
                    layerTargets[i].gameObject.layer = layer;
            }
        }

        private void RestoreLayers()
        {
            if (layerTargets == null || originalLayers == null)
                return;

            int count = Mathf.Min(layerTargets.Length, originalLayers.Length);
            for (int i = 0; i < count; i++)
            {
                if (layerTargets[i] != null)
                    layerTargets[i].gameObject.layer = originalLayers[i];
            }
        }

        private void RestoreImmediately()
        {
            heldRoot.SetParent(originalParent, true);
            heldRoot.localPosition = originalLocalPosition;
            heldRoot.localRotation = originalLocalRotation;
            heldRoot.localScale = originalLocalScale;
            isHeld = false;
            isMoving = false;
            RestoreLayers();
            RestoreRendererStates();
            SetCollidersEnabled(true);
            SetPlayerInspectLock(false);
        }

        private void CaptureOriginalRendererStates()
        {
            heldRenderers = heldRoot != null
                ? heldRoot.GetComponentsInChildren<Renderer>(true)
                : System.Array.Empty<Renderer>();
            originalRendererStates = new bool[heldRenderers.Length];
            for (int i = 0; i < heldRenderers.Length; i++)
                originalRendererStates[i] = heldRenderers[i] != null && heldRenderers[i].enabled;
        }

        private void UpdateHeldVisibility()
        {
            if (!showHeldMirrorOnlyInCutscene || heldRenderers == null)
                return;

            bool visible = GameController.Instance != null
                && GameController.Instance.currentGameState == GameController.GameState.Cutscene;

            for (int i = 0; i < heldRenderers.Length; i++)
            {
                if (heldRenderers[i] != null)
                    heldRenderers[i].enabled = visible;
            }
        }

        private void RestoreRendererStates()
        {
            if (heldRenderers == null || originalRendererStates == null)
                return;

            int count = Mathf.Min(heldRenderers.Length, originalRendererStates.Length);
            for (int i = 0; i < count; i++)
            {
                if (heldRenderers[i] != null)
                    heldRenderers[i].enabled = originalRendererStates[i];
            }
        }

        private void SetCollidersEnabled(bool enabled)
        {
            if (colliders == null)
                return;

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    colliders[i].enabled = enabled;
            }
        }

        private void SetPlayerInspectLock(bool locked)
        {
            if (!lockPlayerWhileHeld)
                return;

            if (fpsController == null)
                fpsController = FindFirstObjectByType<FpsController>();

            if (fpsController == null)
                return;

            if (locked)
            {
                previousRaycastState = PlayerInteract.Instance == null || PlayerInteract.Instance.sendRaycast;
                previousPlayerInteracting = fpsController.isInteracting;
                if (PlayerInteract.Instance != null)
                    PlayerInteract.Instance.sendRaycast = false;
                fpsController.isInteracting = true;
            }
            else
            {
                if (PlayerInteract.Instance != null)
                    PlayerInteract.Instance.sendRaycast = previousRaycastState;
                fpsController.isInteracting = previousPlayerInteracting;
            }
        }

        private void PlayPickupSound()
        {
            if (pickupClip != null)
                AudioManager.Instance?.PlaySfx(pickupClip);
            else
                AudioManager.Instance?.PlayGenericInteract();
        }

        private IEnumerator MoveLocal(
            Vector3 startPosition,
            Quaternion startRotation,
            Vector3 startScale,
            Vector3 endPosition,
            Quaternion endRotation,
            Vector3 endScale)
        {
            float elapsed = 0f;
            while (elapsed < moveSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / moveSeconds));
                heldRoot.localPosition = Vector3.Lerp(startPosition, endPosition, t);
                heldRoot.localRotation = Quaternion.Slerp(startRotation, endRotation, t);
                heldRoot.localScale = Vector3.Lerp(startScale, endScale, t);
                yield return null;
            }

            heldRoot.localPosition = endPosition;
            heldRoot.localRotation = endRotation;
            heldRoot.localScale = endScale;
        }

        private static Transform FindSceneTransform(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
                return null;

            foreach (var transform in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (transform != null && transform.name == objectName && transform.gameObject.scene.IsValid())
                    return transform;
            }

            return null;
        }
    }
}
