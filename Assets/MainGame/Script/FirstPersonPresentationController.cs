using UnityEngine;

namespace FpsHorrorKit
{
    public sealed class FirstPersonPresentationController : MonoBehaviour
    {
        private const string PlayerModelLayerName = "Player";
        private const string ViewModelLayerName = "Viewmodel";

        [Header("Scene References")]
        [SerializeField] private GameObject playerModelRoot;
        [SerializeField] private GameObject firstPersonFlashlightViewModel;

        [Header("State Rules")]
        [SerializeField] private bool hidePlayerRenderersInFirstPerson = true;
        [SerializeField] private bool showFlashlightViewModelInFirstPerson = true;

        private Renderer[] playerModelRenderers;
        private GameController.GameState lastAppliedState;
        private bool hasAppliedState;
        private int playerModelLayer = -1;
        private int viewModelLayer = -1;
        private CameraMaskState[] cameraMaskStates = System.Array.Empty<CameraMaskState>();

        private void Awake()
        {
            ResolveMissingReferences();
            ResolveLayer();
            CachePlayerRenderers();
            PrepareFlashlightViewModel();
            AssignPlayerModelRenderLayer();
            AssignFlashlightViewModelLayer();
        }

        private void OnEnable()
        {
            ApplyCurrentState(true);
        }

        private void PrepareFlashlightViewModel()
        {
            if (firstPersonFlashlightViewModel == null)
                return;

            foreach (var collider in firstPersonFlashlightViewModel.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            foreach (var light in firstPersonFlashlightViewModel.GetComponentsInChildren<Light>(true))
                light.enabled = false;
        }

        private void Start()
        {
            ApplyCurrentState(true);
        }

        private void LateUpdate()
        {
            ApplyCurrentState(false);
        }

        private void ResolveMissingReferences()
        {
            if (playerModelRoot == null)
            {
                var model = transform.Find("PlayerNew");
                if (model == null)
                    model = FindSceneTransform("PlayerNew");
                if (model != null)
                    playerModelRoot = model.gameObject;
            }

            if (firstPersonFlashlightViewModel == null)
            {
                var viewModel = transform.Find("FlashlightPivot/Electric torch/FirstPersonFlashlightViewModel");
                if (viewModel == null)
                    viewModel = FindSceneTransform("FirstPersonFlashlightViewModel");
                if (viewModel != null)
                    firstPersonFlashlightViewModel = viewModel.gameObject;
            }
        }

        private void CachePlayerRenderers()
        {
            playerModelRenderers = playerModelRoot != null
                ? playerModelRoot.GetComponentsInChildren<Renderer>(true)
                : System.Array.Empty<Renderer>();
        }

        private void ApplyCurrentState(bool force)
        {
            var controller = GameController.Instance;
            var state = controller != null ? controller.currentGameState : GameController.GameState.Gameplay;
            bool cutscenePresentation = IsCutscenePresentationState(state);

            AssignPlayerModelRenderLayer();
            AssignFlashlightViewModelLayer();
            ApplyCameraMasks(cutscenePresentation);

            if (!force && hasAppliedState && state == lastAppliedState)
                return;

            KeepPlayerModelRenderersEnabled();

            if (firstPersonFlashlightViewModel != null)
                firstPersonFlashlightViewModel.SetActive(!cutscenePresentation && showFlashlightViewModelInFirstPerson);

            lastAppliedState = state;
            hasAppliedState = true;
        }

        private void KeepPlayerModelRenderersEnabled()
        {
            if (!hidePlayerRenderersInFirstPerson)
                return;

            if (playerModelRenderers == null || playerModelRenderers.Length == 0)
                CachePlayerRenderers();

            foreach (var renderer in playerModelRenderers)
            {
                if (renderer != null)
                    renderer.enabled = true;
            }
        }

        private void ResolveLayer()
        {
            playerModelLayer = LayerMask.NameToLayer(PlayerModelLayerName);
            if (playerModelLayer < 0)
                playerModelLayer = gameObject.layer;

            viewModelLayer = LayerMask.NameToLayer(ViewModelLayerName);
        }

        private void AssignPlayerModelRenderLayer()
        {
            if (!hidePlayerRenderersInFirstPerson || playerModelLayer < 0)
                return;

            if (playerModelRenderers == null || playerModelRenderers.Length == 0)
                CachePlayerRenderers();

            foreach (var renderer in playerModelRenderers)
            {
                if (renderer == null)
                    continue;

                renderer.enabled = true;
                renderer.gameObject.layer = playerModelLayer;
            }
        }

        private void AssignFlashlightViewModelLayer()
        {
            if (firstPersonFlashlightViewModel == null || viewModelLayer < 0)
                return;

            foreach (var child in firstPersonFlashlightViewModel.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = viewModelLayer;
        }

        private void ApplyCameraMasks(bool cutscenePresentation)
        {
            if (!hidePlayerRenderersInFirstPerson || playerModelLayer < 0)
                return;

            int playerModelMask = 1 << playerModelLayer;
            int viewModelMask = viewModelLayer >= 0 ? 1 << viewModelLayer : 0;
            var cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            EnsureCameraMaskCache(cameras);

            foreach (var camera in cameras)
            {
                if (camera == null || IsMirrorReflectionCamera(camera))
                    continue;

                int baseMask = GetOriginalCameraMask(camera);
                camera.cullingMask = cutscenePresentation
                    ? (baseMask | playerModelMask) & ~viewModelMask
                    : (baseMask & ~playerModelMask) | viewModelMask;
            }

            foreach (var camera in cameras)
            {
                if (camera != null && IsMirrorReflectionCamera(camera))
                    camera.cullingMask = (camera.cullingMask | playerModelMask) & ~viewModelMask;
            }
        }

        private void EnsureCameraMaskCache(Camera[] cameras)
        {
            if (cameras == null)
                return;

            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera == null || HasCachedCamera(camera))
                    continue;

                System.Array.Resize(ref cameraMaskStates, cameraMaskStates.Length + 1);
                cameraMaskStates[^1] = new CameraMaskState(camera, camera.cullingMask);
            }
        }

        private bool HasCachedCamera(Camera camera)
        {
            for (int i = 0; i < cameraMaskStates.Length; i++)
            {
                if (cameraMaskStates[i].Camera == camera)
                    return true;
            }

            return false;
        }

        private int GetOriginalCameraMask(Camera camera)
        {
            for (int i = 0; i < cameraMaskStates.Length; i++)
            {
                if (cameraMaskStates[i].Camera == camera)
                    return cameraMaskStates[i].OriginalCullingMask;
            }

            return camera.cullingMask;
        }

        private static bool IsMirrorReflectionCamera(Camera camera)
        {
            return camera != null && camera.GetComponentInParent<MirrorReflectionCamera>(true) != null;
        }

        private static bool IsCutscenePresentationState(GameController.GameState state)
        {
            return state == GameController.GameState.Cutscene
                || state == GameController.GameState.Ending
                || state == GameController.GameState.Dead;
        }

        private static Transform FindSceneTransform(string objectName)
        {
            foreach (var candidate in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (candidate.name == objectName && candidate.gameObject.scene.IsValid())
                    return candidate;
            }

            return null;
        }

        private readonly struct CameraMaskState
        {
            public readonly Camera Camera;
            public readonly int OriginalCullingMask;

            public CameraMaskState(Camera camera, int originalCullingMask)
            {
                Camera = camera;
                OriginalCullingMask = originalCullingMask;
            }
        }
    }
}
