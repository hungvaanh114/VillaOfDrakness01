using System;
using System.Collections;
using FpsHorrorKit;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.P2
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class P2WaterMirrorJumpscare : MonoBehaviour
    {
        [Header("Water Mirror")]
        [SerializeField] private Transform waterSurface;
        [SerializeField] private bool triggerOnlyOnce = true;

        [Header("Look Trigger")]
        [SerializeField, Min(0f)] private float requiredLookSeconds = 3f;
        [SerializeField, Min(0.1f)] private float requiredPlayerDistance = 4.5f;
        [SerializeField] private bool requirePlayerInFront = true;
        [SerializeField] private bool requireRaycastHit = true;
        [SerializeField, Min(0.1f)] private float lookRaycastDistance = 7f;
        [SerializeField, Min(0f)] private float lookRaycastRadius = 0.08f;
        [SerializeField, Range(0.1f, 1f)] private float aimFallbackDot = 0.965f;

        [Header("Jumpscare")]
        [SerializeField] private Texture2D screenJumpscareTexture;
        [SerializeField, Min(0.05f)] private float popDuration = 0.2f;
        [SerializeField, Min(0f)] private float holdDuration = 2.35f;
        [SerializeField, Min(0.05f)] private float startScale = 0.18f;
        [SerializeField, Min(0.5f)] private float impactScale = 1.24f;
        [SerializeField, Range(0f, 1f)] private float imageOpacity = 1f;
        [SerializeField, Range(0f, 1f)] private float darkBackdropOpacity = 0.72f;
        [SerializeField, Min(0f)] private float respawnDelayPadding = 0.08f;
        [SerializeField] private bool playDeathVoiceImmediately;
        [SerializeField] private int deathVoiceIndex = 3;

        [Header("Debug")]
        [SerializeField] private bool drawDebugGizmos = true;
        [SerializeField] private Color debugRangeColor = new(0.35f, 0.8f, 1f, 0.22f);

        private BoxCollider triggerCollider;
        private FpsController playerController;
        private Transform playerFollowTarget;
        private Vector3 followTargetStartPosition;
        private Quaternion followTargetStartRotation;
        private bool hasTriggered;
        private bool isRunning;
        private bool previousRaycastState;
        private bool changedRaycastState;
        private float lookTimer;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (screenJumpscareTexture == null)
                screenJumpscareTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/MainGame/UI/anhHuMa.png");
            ResolveSurface();
        }
#endif

        private void Awake()
        {
            triggerCollider = GetComponent<BoxCollider>();
            triggerCollider.isTrigger = true;
            ResolveSurface();
        }

        private void Update()
        {
            if (isRunning || (triggerOnlyOnce && hasTriggered))
                return;

            playerController ??= FindFirstObjectByType<FpsController>();
            TickLookTrigger(playerController);
        }

        private void TickLookTrigger(FpsController candidate)
        {
            if (candidate == null || !CanTrigger(candidate))
            {
                lookTimer = 0f;
                return;
            }

            lookTimer += Time.deltaTime;
            if (lookTimer < requiredLookSeconds)
                return;

            hasTriggered = true;
            lookTimer = 0f;
            StartCoroutine(JumpscareRoutine(candidate));
        }

        private bool CanTrigger(FpsController candidate)
        {
            if (candidate == null || ClosetHiding.IsAnyPlayerHidden)
                return false;

            var gameController = GameController.Instance;
            if (gameController != null
                && (gameController.currentGameState == GameController.GameState.Cutscene
                    || gameController.currentGameState == GameController.GameState.Dead
                    || gameController.currentGameState == GameController.GameState.Ending))
            {
                return false;
            }

            Vector3 lookPoint = GetWaterLookPoint();
            Vector3 toPlayer = candidate.transform.position - lookPoint;
            if (toPlayer.magnitude > requiredPlayerDistance)
                return false;

            if (requirePlayerInFront && Vector3.Dot(GetWaterNormal(), toPlayer.normalized) < 0.05f)
                return false;

            return !requireRaycastHit || IsPlayerLookingAtWater(candidate);
        }

        private bool IsPlayerLookingAtWater(FpsController candidate)
        {
            Transform source = ResolveLookSource(candidate);
            if (source == null)
                return false;

            if (IsRaycastHittingWater(candidate, source.position, source.forward, out bool blocked))
                return true;
            if (blocked)
                return false;

            Vector3 toWater = GetWaterLookPoint() - source.position;
            if (toWater.magnitude > lookRaycastDistance)
                return false;

            return Vector3.Dot(source.forward.normalized, toWater.normalized) >= aimFallbackDot;
        }

        private bool IsRaycastHittingWater(FpsController candidate, Vector3 origin, Vector3 direction, out bool blocked)
        {
            blocked = false;
            if (direction.sqrMagnitude <= 0.001f)
                return false;

            RaycastHit[] hits = lookRaycastRadius > 0f
                ? Physics.SphereCastAll(origin, lookRaycastRadius, direction.normalized, lookRaycastDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)
                : Physics.RaycastAll(origin, direction.normalized, lookRaycastDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

            if (hits == null || hits.Length == 0)
                return false;

            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (var hit in hits)
            {
                Transform hitTransform = hit.transform;
                if (hitTransform == null)
                    continue;
                if (candidate != null && hitTransform.IsChildOf(candidate.transform))
                    continue;

                if (IsWaterHit(hitTransform))
                    return true;
                if (hit.collider != null && hit.collider.isTrigger)
                    continue;

                blocked = true;
                return false;
            }

            return false;
        }

        private IEnumerator JumpscareRoutine(FpsController candidate)
        {
            isRunning = true;
            playerController = candidate;
            playerFollowTarget = candidate.followTarget;
            followTargetStartPosition = playerFollowTarget != null ? playerFollowTarget.localPosition : Vector3.zero;
            followTargetStartRotation = playerFollowTarget != null ? playerFollowTarget.localRotation : Quaternion.identity;

            DisablePlayerControl(candidate);
            float audioDuration = AudioManager.Instance?.PlayGhostJumpscare() ?? 0f;
            float respawnDelay = Mathf.Max(popDuration + holdDuration + respawnDelayPadding, audioDuration, 2.5f);
            GameController.Instance?.TriggerJumpscareCheckpointRespawn(respawnDelay, playDeathVoiceImmediately, deathVoiceIndex);

            yield return PlayScreenImageJumpscare(Camera.main);

            if (GameController.Instance == null)
                enabled = false;
        }

        private IEnumerator PlayScreenImageJumpscare(Camera playerCamera)
        {
            var canvasObject = BuildScreenJumpscareUi(out RectTransform imageRect, out CanvasGroup canvasGroup);
            if (canvasObject == null || imageRect == null)
                yield break;

            var canvasRect = canvasObject.GetComponent<RectTransform>();
            Vector2 startPosition = GetWaterScreenPosition(canvasRect, playerCamera);
            Vector2 impactPosition = Vector2.zero;
            imageRect.anchoredPosition = startPosition;
            imageRect.localScale = Vector3.one * startScale;

            float elapsed = 0f;
            while (elapsed < popDuration)
            {
                float t = Mathf.Clamp01(elapsed / popDuration);
                float eased = 1f - Mathf.Pow(1f - t, 4f);
                imageRect.anchoredPosition = Vector2.LerpUnclamped(startPosition, impactPosition, eased);
                imageRect.localScale = Vector3.one * Mathf.Lerp(startScale, impactScale, eased);
                if (canvasGroup != null)
                    canvasGroup.alpha = Mathf.Lerp(0.2f, imageOpacity, eased);

                AnimatePlayerFall(Mathf.Min(eased * 0.8f, 0.8f), playerCamera);
                elapsed += Time.deltaTime;
                yield return null;
            }

            imageRect.anchoredPosition = impactPosition;
            imageRect.localScale = Vector3.one * impactScale;
            if (canvasGroup != null)
                canvasGroup.alpha = imageOpacity;

            elapsed = 0f;
            while (elapsed < holdDuration)
            {
                float shake = Mathf.Sin(Time.unscaledTime * 88f) * 8f;
                imageRect.anchoredPosition = impactPosition + new Vector2(shake, -shake * 0.45f);
                elapsed += Time.deltaTime;
                yield return null;
            }

            Destroy(canvasObject);
        }

        private GameObject BuildScreenJumpscareUi(out RectTransform imageRect, out CanvasGroup canvasGroup)
        {
            imageRect = null;
            canvasGroup = null;

            var canvasObject = new GameObject(
                "P2WaterMirrorJumpscareUI",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 920;

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
            backdrop.color = new Color(0f, 0f, 0f, darkBackdropOpacity);

            imageRect = new GameObject("WaterMirrorJumpscareImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
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

        private void AnimatePlayerFall(float amount, Camera playerCamera)
        {
            if (playerFollowTarget != null)
            {
                playerFollowTarget.localRotation = Quaternion.Slerp(
                    followTargetStartRotation,
                    followTargetStartRotation * Quaternion.Euler(30f * amount, 0f, -58f * amount),
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
                    playerCamera.transform.rotation * Quaternion.Euler(30f, 0f, -58f),
                    amount);
            }
        }

        private void DisablePlayerControl(FpsController candidate)
        {
            if (GameController.Instance != null)
                GameController.Instance.SetPlayerControl(false);
            else if (candidate != null)
            {
                candidate.isCutScene = true;
                candidate.isInteracting = true;
            }

            if (PlayerInteract.Instance != null)
            {
                previousRaycastState = PlayerInteract.Instance.sendRaycast;
                PlayerInteract.Instance.sendRaycast = false;
                changedRaycastState = true;
            }

            candidate?.StopCutSceneMovement();
        }

        private Vector2 GetWaterScreenPosition(RectTransform canvasRect, Camera playerCamera)
        {
            Vector3 waterPoint = GetWaterLookPoint();
            Vector2 screenPoint = playerCamera != null
                ? RectTransformUtility.WorldToScreenPoint(playerCamera, waterPoint)
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            if (playerCamera != null)
            {
                Vector3 viewport = playerCamera.WorldToViewportPoint(waterPoint);
                if (viewport.z < 0f)
                    screenPoint = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out var anchoredPosition)
                ? anchoredPosition
                : Vector2.zero;
        }

        private Sprite CreateScreenSprite()
        {
            if (screenJumpscareTexture == null)
                return null;

            return Sprite.Create(
                screenJumpscareTexture,
                new Rect(0f, 0f, screenJumpscareTexture.width, screenJumpscareTexture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        private Vector3 GetWaterLookPoint()
        {
            var target = ResolveSurface();
            if (target == null)
                return transform.position;

            var renderer = target.GetComponentInChildren<Renderer>();
            return renderer != null ? renderer.bounds.center : target.position;
        }

        private Vector3 GetWaterNormal()
        {
            var target = ResolveSurface();
            return target != null ? target.up : transform.up;
        }

        private bool IsWaterHit(Transform hitTransform)
        {
            var target = ResolveSurface();
            return target != null && (hitTransform == target || hitTransform.IsChildOf(target) || target.IsChildOf(hitTransform));
        }

        private Transform ResolveSurface()
        {
            if (waterSurface != null)
                return waterSurface;

            var child = transform.Find("WaterMirrorSurface");
            waterSurface = child != null ? child : transform;
            return waterSurface;
        }

        private static Transform ResolveLookSource(FpsController candidate)
        {
            if (candidate != null && candidate.followTarget != null)
                return candidate.followTarget;

            var camera = Camera.main;
            if (camera != null && camera.isActiveAndEnabled)
                return camera.transform;

            return candidate != null ? candidate.transform : null;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void OnDestroy()
        {
            if (changedRaycastState && PlayerInteract.Instance != null)
                PlayerInteract.Instance.sendRaycast = previousRaycastState;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDebugGizmos)
                return;

            Gizmos.color = debugRangeColor;
            Gizmos.DrawSphere(GetWaterLookPoint(), requiredPlayerDistance);
            Gizmos.color = new Color(0.35f, 0.8f, 1f, 0.9f);
            Vector3 point = GetWaterLookPoint();
            Gizmos.DrawLine(point, point + GetWaterNormal() * 0.75f);
        }
    }
}
