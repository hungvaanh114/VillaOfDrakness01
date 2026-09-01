using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class MirrorJumpscare : MonoBehaviour
    {
        private const string DefaultScreenJumpscareTexturePath = "Assets/MainGame/UI/anhHuMa.png";
        private const string MirrorLayerName = "Guong";

        [Header("Mirror")]
        [SerializeField] private MirrorReflectionCamera reflection;
        [SerializeField] private Transform mirrorRaycastTarget;
        [SerializeField] private bool triggerOnlyOnce = true;

        [Header("Trigger")]
        [SerializeField, Min(0f)] private float requiredMirrorLookSeconds = 3f;
        [SerializeField] private float requiredPlayerDistance = 4.5f;
        [SerializeField] private bool requirePlayerInsideTrigger = false;
        [SerializeField] private bool requirePlayerInFront = true;
        [SerializeField] private bool requireFlashlightOff = false;
        [SerializeField] private bool requireMirrorRaycast = true;
        [SerializeField, Min(0.1f)] private float mirrorRaycastDistance = 8f;
        [SerializeField, Min(0f)] private float mirrorRaycastRadius = 0.08f;
        [SerializeField, Range(0.1f, 1f)] private float mirrorAimFallbackDot = 0.94f;
        [SerializeField] private bool fallbackToPlayerRaycastWhenReflectionCameraMisses = true;
        [SerializeField] private bool triggerWhenFlashlightTurnsOff = false;

        [Header("Jumpscare")]
        [SerializeField, Min(0f)] private float fallRoll = 62f;
        [SerializeField, Min(0f)] private float fallPitch = 28f;
        [SerializeField, Min(0f)] private float deathTriggerDelay = 0f;

        [Header("Screen Image Jumpscare")]
        [SerializeField] private Texture2D screenJumpscareTexture;
        [SerializeField, Min(0.05f)] private float screenImagePopDuration = 0.22f;
        [SerializeField, Min(0f)] private float screenImageHoldDuration = 2.5f;
        [SerializeField, Min(0.05f)] private float screenImageStartScale = 0.18f;
        [SerializeField, Min(0.5f)] private float screenImageImpactScale = 1.25f;
        [SerializeField, Range(0f, 1f)] private float screenImageOpacity = 1f;
        [SerializeField, Range(0f, 1f)] private float screenDarkBackdropOpacity = 0.72f;

        private BoxCollider triggerCollider;
        private FpsHorrorKit.FpsController playerController;
        private Transform playerTransform;
        private Transform playerFollowTarget;
        private Vector3 followTargetStartPosition;
        private Quaternion followTargetStartRotation;
        private bool hasTriggered;
        private bool isRunning;
        private bool playerInsideTrigger;
        private bool previousRaycastState;
        private bool hasChangedRaycastState;
        private float mirrorLookTimer;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (screenJumpscareTexture == null)
                screenJumpscareTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(DefaultScreenJumpscareTexturePath);

            ResolveMirrorRaycastTarget();
        }
#endif

        private void Awake()
        {
            triggerCollider = GetComponent<BoxCollider>();
            triggerCollider.isTrigger = true;

            if (reflection == null)
                reflection = GetComponent<MirrorReflectionCamera>();

            ResolveMirrorRaycastTarget();
        }

        private void OnEnable()
        {
            FpsHorrorKit.ItemUsageSystem.FlashlightLightChanged += HandleFlashlightLightChanged;
            FpsHorrorKit.InventoryManager.FallbackFlashlightChanged += HandleFlashlightLightChanged;
        }

        private void OnDisable()
        {
            FpsHorrorKit.ItemUsageSystem.FlashlightLightChanged -= HandleFlashlightLightChanged;
            FpsHorrorKit.InventoryManager.FallbackFlashlightChanged -= HandleFlashlightLightChanged;
        }

        private void Update()
        {
            if (isRunning || (triggerOnlyOnce && hasTriggered))
                return;

            var candidate = playerController != null
                ? playerController
                : FindFirstObjectByType<FpsHorrorKit.FpsController>();
            TickLookTrigger(candidate);
        }

        private void OnTriggerEnter(Collider other)
        {
            TryTrigger(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryTrigger(other);
        }

        private void OnTriggerExit(Collider other)
        {
            var candidate = other.GetComponentInParent<FpsHorrorKit.FpsController>();
            if (candidate == null)
                return;

            if (playerController == null || candidate == playerController)
            {
                playerInsideTrigger = false;
                mirrorLookTimer = 0f;
            }
        }

        private void TryTrigger(Collider other)
        {
            if (isRunning || (triggerOnlyOnce && hasTriggered))
                return;

            var candidate = other.GetComponentInParent<FpsHorrorKit.FpsController>();
            if (candidate != null)
            {
                playerInsideTrigger = true;
                playerController = candidate;
            }
        }

        private void TickLookTrigger(FpsHorrorKit.FpsController candidate)
        {
            if (candidate == null || !CanTrigger(candidate))
            {
                mirrorLookTimer = 0f;
                return;
            }

            mirrorLookTimer += Time.deltaTime;
            if (mirrorLookTimer < requiredMirrorLookSeconds)
                return;

            hasTriggered = true;
            mirrorLookTimer = 0f;
            StartCoroutine(JumpscareRoutine(candidate));
        }

        private void HandleFlashlightLightChanged(bool active)
        {
            if (!triggerWhenFlashlightTurnsOff || active)
                return;

            mirrorLookTimer = 0f;
        }

        private bool CanTrigger(FpsHorrorKit.FpsController candidate)
        {
            if (candidate == null || FpsHorrorKit.ClosetHiding.IsAnyPlayerHidden)
                return false;

            if (requirePlayerInsideTrigger && !playerInsideTrigger)
                return false;

            var controller = GameController.Instance;
            if (controller != null)
            {
                if (controller.currentGameState == GameController.GameState.Dead
                    || controller.currentGameState == GameController.GameState.Cutscene)
                    return false;
            }

            if (requireFlashlightOff && IsFlashlightLightActive())
                return false;

            Vector3 mirrorPoint = GetMirrorLookPoint();
            Vector3 toPlayer = candidate.transform.position - mirrorPoint;
            if (toPlayer.magnitude > requiredPlayerDistance)
                return false;

            if (requirePlayerInFront && Vector3.Dot(GetMirrorForward(), toPlayer.normalized) < 0f)
                return false;

            if (requireMirrorRaycast && !IsPlayerCameraSeeingMirror(candidate))
                return false;

            if (requireMirrorRaycast && !IsReflectionCameraSeeingPlayer(candidate))
            {
                if (!fallbackToPlayerRaycastWhenReflectionCameraMisses || !IsPlayerRaycastLookingAtMirror(candidate))
                    return false;
            }

            return true;
        }

        private bool IsPlayerCameraSeeingMirror(FpsHorrorKit.FpsController candidate)
        {
            Camera playerCamera = ResolvePlayerCamera(candidate);
            if (playerCamera == null)
                return IsPlayerRaycastLookingAtMirror(candidate);

            Bounds mirrorBounds = GetMirrorBounds();
            return IsBoundsVisibleFromCamera(
                playerCamera,
                mirrorBounds,
                ResolveMirrorRaycastTarget(),
                candidate != null ? candidate.transform : null,
                false);
        }

        private bool IsReflectionCameraSeeingPlayer(FpsHorrorKit.FpsController candidate)
        {
            if (candidate == null)
                return false;

            Camera playerCamera = ResolvePlayerCamera(candidate);
            Camera mirrorCamera = reflection != null
                ? reflection.RefreshReflection(playerCamera)
                : null;

            if (mirrorCamera == null)
                return false;

            Bounds playerBounds = GetPlayerVisibilityBounds(candidate);
            return IsBoundsVisibleFromCamera(
                mirrorCamera,
                playerBounds,
                candidate.transform,
                transform,
                true);
        }

        private Camera ResolvePlayerCamera(FpsHorrorKit.FpsController candidate)
        {
            Camera playerCamera = Camera.main;
            if (playerCamera != null && playerCamera.isActiveAndEnabled)
                return playerCamera;

            return candidate != null ? candidate.GetComponentInChildren<Camera>(true) : null;
        }

        private Bounds GetMirrorBounds()
        {
            Transform target = ResolveMirrorRaycastTarget();
            if (target == null)
                return new Bounds(transform.position + Vector3.up * 1.65f, Vector3.one);

            Renderer targetRenderer = target.GetComponentInChildren<Renderer>();
            if (targetRenderer != null)
                return targetRenderer.bounds;

            return new Bounds(target.position, new Vector3(1.6f, 2.4f, 0.25f));
        }

        private static Bounds GetPlayerVisibilityBounds(FpsHorrorKit.FpsController candidate)
        {
            var characterController = candidate.GetComponent<CharacterController>();
            if (characterController != null)
            {
                Vector3 center = candidate.transform.TransformPoint(characterController.center);
                Vector3 size = new(
                    characterController.radius * 2f,
                    characterController.height,
                    characterController.radius * 2f);
                return new Bounds(center, size);
            }

            var colliders = candidate.GetComponentsInChildren<Collider>(true);
            bool hasBounds = false;
            Bounds bounds = default;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider itemCollider = colliders[i];
                if (itemCollider == null || itemCollider.isTrigger)
                    continue;

                if (!hasBounds)
                {
                    bounds = itemCollider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(itemCollider.bounds);
                }
            }

            if (hasBounds)
                return bounds;

            return new Bounds(candidate.transform.position + Vector3.up * 1f, new Vector3(0.7f, 1.8f, 0.7f));
        }

        private bool IsBoundsVisibleFromCamera(
            Camera camera,
            Bounds bounds,
            Transform targetRoot,
            Transform ignoredRoot,
            bool ignoreMirrorRoot)
        {
            Vector3[] samplePoints = GetBoundsSamplePoints(bounds);
            for (int i = 0; i < samplePoints.Length; i++)
            {
                Vector3 point = samplePoints[i];
                if (!IsPointInsideCamera(camera, point))
                    continue;

                if (HasClearLineToPoint(
                    camera.transform.position,
                    point,
                    targetRoot,
                    ignoredRoot,
                    ignoreMirrorRoot))
                    return true;
            }

            return false;
        }

        private static Vector3[] GetBoundsSamplePoints(Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            Vector3 center = bounds.center;
            return new[]
            {
                center,
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z)
            };
        }

        private static bool IsPointInsideCamera(Camera camera, Vector3 point)
        {
            Vector3 viewport = camera.WorldToViewportPoint(point);
            return viewport.z > camera.nearClipPlane
                && viewport.x >= 0f
                && viewport.x <= 1f
                && viewport.y >= 0f
                && viewport.y <= 1f;
        }

        private bool HasClearLineToPoint(
            Vector3 origin,
            Vector3 point,
            Transform targetRoot,
            Transform ignoredRoot,
            bool ignoreMirrorRoot)
        {
            Vector3 direction = point - origin;
            float distance = direction.magnitude;
            if (distance <= 0.05f)
                return true;

            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                direction / distance,
                distance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide);

            if (hits == null || hits.Length == 0)
                return true;

            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (RaycastHit hit in hits)
            {
                Transform hitTransform = hit.transform;
                if (hitTransform == null)
                    continue;

                if (hit.collider != null && hit.collider.isTrigger)
                    continue;

                if (ignoredRoot != null && hitTransform.IsChildOf(ignoredRoot))
                    continue;

                if (ignoreMirrorRoot && hitTransform.IsChildOf(transform))
                    continue;

                if (targetRoot != null && (hitTransform == targetRoot || hitTransform.IsChildOf(targetRoot)))
                    return true;

                return false;
            }

            return true;
        }

        private bool IsPlayerRaycastLookingAtMirror(FpsHorrorKit.FpsController candidate)
        {
            Transform raySource = ResolveLookRaySource(candidate);
            if (raySource == null)
                return false;

            Vector3 origin = raySource.position;
            Vector3 direction = raySource.forward;
            if (direction.sqrMagnitude < 0.001f)
                return false;

            if (IsRaycastHittingMirror(candidate, origin, direction.normalized, out bool rayBlocked))
                return true;

            if (rayBlocked)
                return false;

            Vector3 mirrorPoint = GetMirrorLookPoint();
            Vector3 toMirror = mirrorPoint - origin;
            if (toMirror.magnitude > mirrorRaycastDistance)
                return false;

            return Vector3.Dot(direction.normalized, toMirror.normalized) >= mirrorAimFallbackDot;
        }

        private Transform ResolveLookRaySource(FpsHorrorKit.FpsController candidate)
        {
            if (candidate != null && candidate.followTarget != null)
                return candidate.followTarget;

            Camera playerCamera = Camera.main;
            if (playerCamera != null && playerCamera.isActiveAndEnabled)
                return playerCamera.transform;

            return candidate != null ? candidate.transform : null;
        }

        private bool IsMirrorHit(Transform hitTransform)
        {
            if (hitTransform == null)
                return false;

            int mirrorLayer = LayerMask.NameToLayer(MirrorLayerName);
            if (mirrorLayer >= 0 && hitTransform.gameObject.layer != mirrorLayer)
                return false;

            Transform target = ResolveMirrorRaycastTarget();
            return target != null && (hitTransform == target || hitTransform.IsChildOf(target));
        }

        private bool IsRaycastHittingMirror(
            FpsHorrorKit.FpsController candidate,
            Vector3 origin,
            Vector3 direction,
            out bool blocked)
        {
            blocked = false;

            RaycastHit[] hits = mirrorRaycastRadius > 0f
                ? Physics.SphereCastAll(origin, mirrorRaycastRadius, direction, mirrorRaycastDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)
                : Physics.RaycastAll(origin, direction, mirrorRaycastDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

            if (hits == null || hits.Length == 0)
                return false;

            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            foreach (RaycastHit hit in hits)
            {
                Transform hitTransform = hit.transform;
                if (hitTransform == null)
                    continue;

                if (candidate != null && hitTransform.IsChildOf(candidate.transform))
                    continue;

                if (IsMirrorHit(hitTransform))
                    return true;

                if (hit.collider != null && hit.collider.isTrigger)
                    continue;

                blocked = true;
                return false;
            }

            return false;
        }

        private Transform ResolveMirrorRaycastTarget()
        {
            if (mirrorRaycastTarget != null)
                return mirrorRaycastTarget;

            mirrorRaycastTarget = transform.Find("MirrorSurface");
            if (mirrorRaycastTarget != null)
                return mirrorRaycastTarget;

            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                string lowerName = child.name.ToLowerInvariant();
                if (lowerName.Contains("mirror") || lowerName.Contains("guong") || lowerName.Contains("surface"))
                {
                    mirrorRaycastTarget = child;
                    return mirrorRaycastTarget;
                }
            }

            return null;
        }

        private Vector3 GetMirrorLookPoint()
        {
            Transform target = ResolveMirrorRaycastTarget();
            if (target == null)
                return transform.position + Vector3.up * 1.65f;

            Renderer targetRenderer = target.GetComponentInChildren<Renderer>();
            return targetRenderer != null ? targetRenderer.bounds.center : target.position;
        }

        private Vector3 GetMirrorForward()
        {
            Transform target = ResolveMirrorRaycastTarget();
            return target != null ? target.forward : transform.forward;
        }

        private bool IsFlashlightLightActive()
        {
            var usageSystem = FpsHorrorKit.ItemUsageSystem.Instance;
            if (usageSystem != null)
                return usageSystem.IsFlashlightLightActive();

            var inventory = FpsHorrorKit.InventoryManager.Instance;
            if (inventory != null)
                return inventory.IsFallbackFlashlightLightActive();

            var controller = FindFirstObjectByType<FpsHorrorKit.FpsController>();
            if (controller != null && controller.flashlightLight != null && controller.flashlightLight.name != "Spot Light_1")
                return controller.flashlightLight.gameObject.activeInHierarchy;

            var followTarget = GameObject.Find("FollowTarget");
            var spotLight = followTarget != null ? followTarget.transform.Find("Spot Light") : null;
            return spotLight != null && spotLight.gameObject.activeInHierarchy;
        }

        private IEnumerator JumpscareRoutine(FpsHorrorKit.FpsController candidate)
        {
            isRunning = true;
            playerController = candidate;
            playerTransform = candidate.transform;
            playerFollowTarget = candidate.followTarget;
            followTargetStartPosition = playerFollowTarget != null
                ? playerFollowTarget.localPosition
                : Vector3.zero;
            followTargetStartRotation = playerFollowTarget != null
                ? playerFollowTarget.localRotation
                : Quaternion.identity;

            DisablePlayerInteraction();
            reflection?.SetBloodStained();
            float jumpscareAudioStartedAt = Time.time;
            float jumpscareAudioDuration = AudioManager.Instance?.PlayGhostJumpscare() ?? 0f;
            float respawnDelay = Mathf.Max(
                screenImagePopDuration + screenImageHoldDuration + deathTriggerDelay,
                2.5f);
            GameController.Instance?.TriggerJumpscareCheckpointRespawn(respawnDelay);

            Camera playerCamera = Camera.main;
            yield return PlayScreenImageJumpscare(playerCamera);

            AnimatePlayerFall(1f, playerCamera);
            if (deathTriggerDelay > 0f)
                yield return new WaitForSeconds(deathTriggerDelay);

            if (GameController.Instance == null)
            {
                float remainingJumpscareAudio = Mathf.Max(0f, jumpscareAudioDuration - (Time.time - jumpscareAudioStartedAt));
                if (remainingJumpscareAudio > 0f)
                    yield return new WaitForSeconds(remainingJumpscareAudio);
                enabled = false;
            }
        }

        private void AnimatePlayerFall(float amount, Camera playerCamera)
        {
            if (playerFollowTarget != null)
            {
                Quaternion fallRotation = followTargetStartRotation
                    * Quaternion.Euler(fallPitch * amount, 0f, -fallRoll * amount);
                playerFollowTarget.localRotation = Quaternion.Slerp(
                    followTargetStartRotation,
                    fallRotation,
                    amount);
                playerFollowTarget.localPosition = Vector3.Lerp(
                    followTargetStartPosition,
                    followTargetStartPosition + new Vector3(0f, -0.12f, 0f),
                    amount);
            }
            else if (playerCamera != null)
            {
                playerCamera.transform.rotation = Quaternion.Slerp(
                    playerCamera.transform.rotation,
                    playerCamera.transform.rotation * Quaternion.Euler(fallPitch, 0f, -fallRoll),
                    amount);
            }
        }

        private IEnumerator PlayScreenImageJumpscare(Camera playerCamera)
        {
            RectTransform imageRect = null;
            CanvasGroup canvasGroup = null;
            GameObject canvasObject = BuildScreenJumpscareUi(out imageRect, out canvasGroup);
            if (imageRect == null || canvasObject == null)
                yield break;

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            Vector2 startPosition = GetMirrorScreenAnchoredPosition(canvasRect, playerCamera);
            Vector2 impactPosition = Vector2.zero;
            imageRect.anchoredPosition = startPosition;
            imageRect.localScale = Vector3.one * screenImageStartScale;

            float elapsed = 0f;
            while (elapsed < screenImagePopDuration)
            {
                float t = Mathf.Clamp01(elapsed / screenImagePopDuration);
                float eased = 1f - Mathf.Pow(1f - t, 4f);
                imageRect.anchoredPosition = Vector2.LerpUnclamped(startPosition, impactPosition, eased);
                imageRect.localScale = Vector3.one * Mathf.Lerp(screenImageStartScale, screenImageImpactScale, eased);
                if (canvasGroup != null)
                    canvasGroup.alpha = Mathf.Lerp(0.2f, screenImageOpacity, eased);

                AnimatePlayerFall(Mathf.Min(eased * 0.8f, 0.8f), playerCamera);
                elapsed += Time.deltaTime;
                yield return null;
            }

            imageRect.anchoredPosition = impactPosition;
            imageRect.localScale = Vector3.one * screenImageImpactScale;
            if (canvasGroup != null)
                canvasGroup.alpha = screenImageOpacity;

            elapsed = 0f;
            while (elapsed < screenImageHoldDuration)
            {
                float shake = Mathf.Sin(Time.unscaledTime * 85f) * 8f;
                imageRect.anchoredPosition = impactPosition + new Vector2(shake, -shake * 0.45f);
                elapsed += Time.deltaTime;
                yield return null;
            }

            canvasObject.SetActive(false);
            Destroy(canvasObject);
        }

        private GameObject BuildScreenJumpscareUi(out RectTransform imageRect, out CanvasGroup canvasGroup)
        {
            imageRect = null;
            canvasGroup = null;

            var canvasObject = new GameObject(
                "MirrorJumpscareImageUI",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGroup = canvasObject.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            var backdropRect = new GameObject("DarkBackdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
            backdropRect.SetParent(canvasObject.transform, false);
            Stretch(backdropRect);
            var backdrop = backdropRect.GetComponent<Image>();
            backdrop.raycastTarget = false;
            backdrop.color = new Color(0f, 0f, 0f, screenDarkBackdropOpacity);

            imageRect = new GameObject("AnhHuMa", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
            imageRect.SetParent(canvasObject.transform, false);
            imageRect.anchorMin = new Vector2(0.5f, 0.5f);
            imageRect.anchorMax = new Vector2(0.5f, 0.5f);
            imageRect.pivot = new Vector2(0.5f, 0.5f);

            var image = imageRect.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.sprite = CreateScreenSprite();
            image.color = Color.white;

            float aspect = screenJumpscareTexture != null && screenJumpscareTexture.height > 0
                ? (float)screenJumpscareTexture.width / screenJumpscareTexture.height
                : 1f;
            float height = 1180f;
            imageRect.sizeDelta = new Vector2(height * aspect, height);

            return canvasObject;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private Sprite CreateScreenSprite()
        {
            Texture2D texture = screenJumpscareTexture;
            if (texture == null)
                return null;

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        private Vector2 GetMirrorScreenAnchoredPosition(RectTransform canvasRect, Camera playerCamera)
        {
            Vector3 mirrorPoint = GetMirrorLookPoint();
            Vector2 screenPoint = playerCamera != null
                ? RectTransformUtility.WorldToScreenPoint(playerCamera, mirrorPoint)
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            if (playerCamera != null)
            {
                Vector3 viewportPoint = playerCamera.WorldToViewportPoint(mirrorPoint);
                if (viewportPoint.z < 0f)
                    screenPoint = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out var anchoredPosition))
                return anchoredPosition;

            return Vector2.zero;
        }

        private void DisablePlayerInteraction()
        {
            if (GameController.Instance != null)
                GameController.Instance.SetPlayerControl(false);
            else if (playerController != null)
            {
                playerController.isCutScene = true;
                playerController.isInteracting = true;
            }

            if (FpsHorrorKit.PlayerInteract.Instance != null)
            {
                previousRaycastState = FpsHorrorKit.PlayerInteract.Instance.sendRaycast;
                FpsHorrorKit.PlayerInteract.Instance.sendRaycast = false;
                hasChangedRaycastState = true;
            }

            playerController?.StopCutSceneMovement();
        }

        private void OnDestroy()
        {
            if (hasChangedRaycastState && FpsHorrorKit.PlayerInteract.Instance != null)
                FpsHorrorKit.PlayerInteract.Instance.sendRaycast = previousRaycastState;
        }
    }
}
