using System.Collections;
using FpsHorrorKit;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.P2
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class P2MirrorJumpscare : MonoBehaviour, IInteractable
    {
        [Header("Mirror")]
        [SerializeField] private MirrorReflectionCamera reflection;
        [SerializeField] private Transform mirrorRaycastTarget;
        [SerializeField] private GameObject clothCover;
        [SerializeField] private bool triggerOnlyOnce = true;

        [Header("Trigger")]
        [SerializeField, Min(0f)] private float requiredMirrorLookSeconds;
        [SerializeField, Min(0.1f)] private float requiredPlayerDistance = 5f;
        [SerializeField] private bool requirePlayerInsideTrigger = true;
        [SerializeField] private bool requirePlayerInFront;
        [SerializeField] private bool invertMirrorFrontDirection = true;
        [SerializeField] private bool acceptEitherMirrorSide = true;
        [SerializeField] private bool requireMirrorRaycast;
        [SerializeField, Min(0.1f)] private float mirrorRaycastDistance = 8f;
        [SerializeField, Min(0f)] private float mirrorRaycastRadius = 0.08f;

        [Header("Cloth Cover")]
        [SerializeField] private bool startCovered = true;
        [SerializeField] private bool triggerImmediatelyAfterClothRemoved = true;
        [SerializeField] private string coveredInteractText = "[E] Kéo tấm vải";
        [SerializeField] private string uncoveredInteractText = "Nhìn vào gương";
        [SerializeField, Min(0.01f)] private float clothPullSeconds = 0.8f;
        [SerializeField] private Vector3 clothPulledLocalOffset = new(0f, -1.7f, 0.08f);
        [SerializeField] private Vector3 clothPulledLocalEulerOffset = new(0f, 0f, -12f);
        [SerializeField] private AudioClip clothPullClip;

        [Header("Chapter 2 Event")]
        [SerializeField] private P2GhostDoorApparitionDirector ghostDirector;
        [SerializeField] private P2GhostController ghostController;
        [SerializeField] private Transform ghostRevealPoint;
        [SerializeField] private GameObject ghostRevealObject;
        [SerializeField] private bool awakenGhostAfterEvent = true;
        [SerializeField] private bool triggerP2MirrorBreakEvent = true;
        [SerializeField, Min(0f)] private float mirrorHoldSeconds = 1.6f;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioClip mirrorEventClip;

        [Header("Screen Image Jumpscare")]
        [SerializeField] private Texture2D screenJumpscareTexture;
        [SerializeField, Min(0.05f)] private float screenImagePopDuration = 0.22f;
        [SerializeField, Min(0f)] private float screenImageHoldDuration = 2.5f;
        [SerializeField, Min(0.05f)] private float screenImageStartScale = 0.18f;
        [SerializeField, Min(0.5f)] private float screenImageImpactScale = 1.25f;
        [SerializeField, Range(0f, 1f)] private float screenImageOpacity = 1f;
        [SerializeField, Range(0f, 1f)] private float screenDarkBackdropOpacity = 0.72f;
        [SerializeField, Min(0f)] private float fallRoll = 62f;
        [SerializeField, Min(0f)] private float fallPitch = 28f;

        private BoxCollider triggerCollider;
        private FpsHorrorKit.FpsController playerController;
        private Transform playerFollowTarget;
        private Vector3 followTargetStartPosition;
        private Quaternion followTargetStartRotation;
        private bool hasTriggered;
        private bool isRunning;
        private bool playerInsideTrigger;
        private bool isUncovered;
        private bool isPullingCloth;
        private float mirrorLookTimer;
        private Vector3 clothCoveredLocalPosition;
        private Quaternion clothCoveredLocalRotation;

        private void Awake()
        {
            triggerCollider = GetComponent<BoxCollider>();
            triggerCollider.isTrigger = true;

            if (reflection == null)
                reflection = GetComponent<MirrorReflectionCamera>();
            if (sfxSource == null)
                sfxSource = GetComponent<AudioSource>();

            ResolveMirrorRaycastTarget();
            EnsureMirrorRaycastCollider();
            ResolveP2References();
            ResolveClothCover();
            CaptureClothCoveredPose();
            ApplyClothStateInstant();
        }

        private void Update()
        {
            if (isRunning || (triggerOnlyOnce && hasTriggered))
                return;
            if (!isUncovered)
            {
                mirrorLookTimer = 0f;
                return;
            }

            var candidate = playerController != null
                ? playerController
                : FindFirstObjectByType<FpsHorrorKit.FpsController>();
            TickLookTrigger(candidate);
        }

        private void OnTriggerEnter(Collider other)
        {
            TryTrackPlayer(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryTrackPlayer(other);
        }

        private void OnTriggerExit(Collider other)
        {
            var candidate = other.GetComponentInParent<FpsHorrorKit.FpsController>();
            if (candidate == null)
                return;

            if (playerController == null || playerController == candidate)
            {
                playerInsideTrigger = false;
                mirrorLookTimer = 0f;
            }
        }

        private void TryTrackPlayer(Collider other)
        {
            if (isRunning || (triggerOnlyOnce && hasTriggered))
                return;

            var candidate = other.GetComponentInParent<FpsHorrorKit.FpsController>();
            if (candidate == null)
                return;

            playerInsideTrigger = true;
            playerController = candidate;
        }

        public void Interact()
        {
            if (isUncovered || isPullingCloth || isRunning || (triggerOnlyOnce && hasTriggered))
                return;

            StartCoroutine(PullClothRoutine());
        }

        public void Highlight()
        {
            PlayerInteract.Instance?.ChangeInteractText(isUncovered ? uncoveredInteractText : coveredInteractText);
        }

        public void HoldInteract()
        {
        }

        public void UnHighlight()
        {
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
            StartCoroutine(MirrorEventRoutine(candidate));
        }

        private bool CanTrigger(FpsHorrorKit.FpsController candidate)
        {
            if (candidate == null || FpsHorrorKit.ClosetHiding.IsAnyPlayerHidden)
                return false;
            if (!isUncovered)
                return false;

            var gameController = GameController.Instance;
            if (gameController != null
                && (gameController.currentGameState == GameController.GameState.Cutscene
                    || gameController.currentGameState == GameController.GameState.Ending
                    || gameController.currentGameState == GameController.GameState.Dead))
                return false;

            if (requirePlayerInsideTrigger && !playerInsideTrigger)
                return false;

            Vector3 mirrorPoint = GetMirrorLookPoint();
            Vector3 toPlayer = candidate.transform.position - mirrorPoint;
            if (toPlayer.magnitude > requiredPlayerDistance)
                return false;

            if (requirePlayerInFront && !IsPlayerOnMirrorSide(toPlayer.normalized))
                return false;

            return !requireMirrorRaycast || IsPlayerLookingAtMirror(candidate);
        }

        private IEnumerator PullClothRoutine()
        {
            ResolveClothCover();
            CaptureClothCoveredPose();
            isPullingCloth = true;
            AudioManager.Instance?.PlayGenericInteract();
            PlayClothPullSound();

            if (clothCover == null)
            {
                isUncovered = true;
                isPullingCloth = false;
                TryTriggerAfterClothRemoved();
                yield break;
            }

            Transform cloth = clothCover.transform;
            Vector3 startPosition = cloth.localPosition;
            Quaternion startRotation = cloth.localRotation;
            Vector3 endPosition = clothCoveredLocalPosition + clothPulledLocalOffset;
            Quaternion endRotation = clothCoveredLocalRotation * Quaternion.Euler(clothPulledLocalEulerOffset);

            float timer = 0f;
            while (timer < clothPullSeconds)
            {
                timer += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / clothPullSeconds));
                cloth.localPosition = Vector3.Lerp(startPosition, endPosition, t);
                cloth.localRotation = Quaternion.Slerp(startRotation, endRotation, t);
                yield return null;
            }

            clothCover.SetActive(false);
            isUncovered = true;
            isPullingCloth = false;
            mirrorLookTimer = 0f;
            TryTriggerAfterClothRemoved();
        }

        private void TryTriggerAfterClothRemoved()
        {
            if (!triggerImmediatelyAfterClothRemoved || isRunning || (triggerOnlyOnce && hasTriggered))
                return;

            var candidate = playerController != null
                ? playerController
                : FindFirstObjectByType<FpsHorrorKit.FpsController>();
            if (candidate == null)
                return;

            hasTriggered = true;
            mirrorLookTimer = 0f;
            StartCoroutine(MirrorEventRoutine(candidate));
        }

        private IEnumerator MirrorEventRoutine(FpsHorrorKit.FpsController candidate)
        {
            isRunning = true;
            playerController = candidate;
            ResolveP2References();

            var gameController = GameController.Instance;
            if (gameController != null)
                gameController.SetGameState(GameController.GameState.Cutscene);
            else
                SetFallbackPlayerLocked(candidate, true);

            playerFollowTarget = candidate != null ? candidate.followTarget : null;
            followTargetStartPosition = playerFollowTarget != null ? playerFollowTarget.localPosition : Vector3.zero;
            followTargetStartRotation = playerFollowTarget != null ? playerFollowTarget.localRotation : Quaternion.identity;

            reflection?.SetBloodStained();
            PlayMirrorEventSound();
            ShowRevealObject(true, candidate);
            ghostDirector?.ForceApparitionNearPlayer();

            Camera playerCamera = Camera.main;
            yield return PlayScreenImageJumpscare(playerCamera);
            AnimatePlayerFall(0f, playerCamera);

            ShowRevealObject(false, candidate);

            if (awakenGhostAfterEvent)
            {
                ghostController?.Awaken();
                ghostDirector?.Awaken();
            }

            if (triggerP2MirrorBreakEvent)
                P2GameController.Instance?.TriggerMirrorBreakEvent();

            if (gameController != null
                && gameController.currentGameState == GameController.GameState.Cutscene)
            {
                gameController.StartGameplay();
            }
            else if (gameController == null)
            {
                SetFallbackPlayerLocked(candidate, false);
            }

            isRunning = false;
        }

        private void ShowRevealObject(bool visible, FpsHorrorKit.FpsController candidate)
        {
            if (ghostRevealObject == null)
                return;

            if (visible && ghostRevealPoint != null)
                ghostRevealObject.transform.SetPositionAndRotation(ghostRevealPoint.position, ghostRevealPoint.rotation);

            if (visible && candidate != null)
                FaceTarget(ghostRevealObject.transform, candidate.transform.position);

            ghostRevealObject.SetActive(visible);
        }

        private void PlayMirrorEventSound()
        {
            if (mirrorEventClip != null && sfxSource != null)
                sfxSource.PlayOneShot(mirrorEventClip);
            else if (mirrorEventClip != null)
                AudioSource.PlayClipAtPoint(mirrorEventClip, transform.position);
            else
                AudioManager.Instance?.PlayGhostJumpscare();
        }

        private void PlayClothPullSound()
        {
            if (clothPullClip == null)
                return;

            if (sfxSource != null)
                sfxSource.PlayOneShot(clothPullClip);
            else
                AudioSource.PlayClipAtPoint(clothPullClip, transform.position);
        }

        private IEnumerator PlayScreenImageJumpscare(Camera playerCamera)
        {
            RectTransform imageRect = null;
            CanvasGroup canvasGroup = null;
            GameObject canvasObject = BuildScreenJumpscareUi(out imageRect, out canvasGroup);
            if (imageRect == null || canvasObject == null)
            {
                if (mirrorHoldSeconds > 0f)
                    yield return new WaitForSeconds(mirrorHoldSeconds);
                yield break;
            }

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
                "P2MirrorJumpscareImageUI",
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

        private bool IsPlayerLookingAtMirror(FpsHorrorKit.FpsController candidate)
        {
            Transform source = ResolveLookSource(candidate);
            if (source == null)
                return false;

            if (IsRaycastHittingMirror(candidate, source.position, source.forward, out bool blocked))
                return true;
            return false;
        }

        private bool IsRaycastHittingMirror(
            FpsHorrorKit.FpsController candidate,
            Vector3 origin,
            Vector3 direction,
            out bool blocked)
        {
            blocked = false;
            if (direction.sqrMagnitude <= 0.001f)
                return false;

            RaycastHit[] hits = mirrorRaycastRadius > 0f
                ? Physics.SphereCastAll(origin, mirrorRaycastRadius, direction.normalized, mirrorRaycastDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)
                : Physics.RaycastAll(origin, direction.normalized, mirrorRaycastDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

            if (hits == null || hits.Length == 0)
                return false;

            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (var hit in hits)
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

        private bool IsMirrorHit(Transform hitTransform)
        {
            var target = ResolveMirrorRaycastTarget();
            return target != null
                && (hitTransform == transform
                    || hitTransform.IsChildOf(transform)
                    || hitTransform == target
                    || hitTransform.IsChildOf(target)
                    || target.IsChildOf(hitTransform));
        }

        private void EnsureMirrorRaycastCollider()
        {
            var target = ResolveMirrorRaycastTarget();
            if (target == null || target == transform)
                return;

            var colliders = target.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && !colliders[i].isTrigger)
                    return;
            }

            var box = target.GetComponent<BoxCollider>();
            if (box == null)
                box = target.gameObject.AddComponent<BoxCollider>();

            box.isTrigger = false;
            var renderer = target.GetComponentInChildren<Renderer>(true);
            if (renderer == null)
            {
                box.center = Vector3.zero;
                box.size = Vector3.one;
                return;
            }

            Bounds bounds = renderer.bounds;
            Vector3 localSize = target.InverseTransformVector(bounds.size);
            box.center = target.InverseTransformPoint(bounds.center);
            box.size = new Vector3(
                Mathf.Max(0.04f, Mathf.Abs(localSize.x)),
                Mathf.Max(0.04f, Mathf.Abs(localSize.y)),
                Mathf.Max(0.04f, Mathf.Abs(localSize.z)));
        }

        private Transform ResolveMirrorRaycastTarget()
        {
            if (mirrorRaycastTarget != null)
                return mirrorRaycastTarget;

            mirrorRaycastTarget = transform.Find("MirrorSurface");
            if (mirrorRaycastTarget != null)
                return mirrorRaycastTarget;

            foreach (Transform child in transform)
            {
                string lowerName = child.name.ToLowerInvariant();
                if (lowerName.Contains("mirror") || lowerName.Contains("guong") || lowerName.Contains("surface"))
                {
                    mirrorRaycastTarget = child;
                    return mirrorRaycastTarget;
                }
            }

            return transform;
        }

        private void ResolveClothCover()
        {
            if (clothCover != null)
                return;

            var child = transform.Find("P2_MirrorClothCover");
            if (child != null)
                clothCover = child.gameObject;
        }

        private void CaptureClothCoveredPose()
        {
            if (clothCover == null)
                return;

            clothCoveredLocalPosition = clothCover.transform.localPosition;
            clothCoveredLocalRotation = clothCover.transform.localRotation;
        }

        private void ApplyClothStateInstant()
        {
            isUncovered = !startCovered;
            if (clothCover != null)
            {
                clothCover.SetActive(startCovered);
                clothCover.transform.localPosition = clothCoveredLocalPosition;
                clothCover.transform.localRotation = clothCoveredLocalRotation;
            }
        }

        private void ResolveP2References()
        {
            if (ghostDirector == null)
                ghostDirector = FindFirstObjectByType<P2GhostDoorApparitionDirector>(FindObjectsInactive.Include);
            if (ghostController == null)
                ghostController = FindFirstObjectByType<P2GhostController>(FindObjectsInactive.Include);
        }

        private Vector3 GetMirrorLookPoint()
        {
            Transform target = ResolveMirrorRaycastTarget();
            if (target == null)
                return transform.position + Vector3.up * 1.5f;

            var renderer = target.GetComponentInChildren<Renderer>();
            return renderer != null ? renderer.bounds.center : target.position;
        }

        private Vector3 GetMirrorForward()
        {
            Transform target = ResolveMirrorRaycastTarget();
            Vector3 forward = target != null ? target.forward : transform.forward;
            if (invertMirrorFrontDirection)
                forward = -forward;

            return forward.sqrMagnitude > 0.001f ? forward.normalized : transform.forward;
        }

        private bool IsPlayerOnMirrorSide(Vector3 directionFromMirrorToPlayer)
        {
            float dot = Vector3.Dot(GetMirrorForward(), directionFromMirrorToPlayer);
            return acceptEitherMirrorSide ? Mathf.Abs(dot) > 0.05f : dot >= 0f;
        }

        private static Transform ResolveLookSource(FpsHorrorKit.FpsController candidate)
        {
            Camera camera = Camera.main;
            if (camera != null && camera.isActiveAndEnabled)
                return camera.transform;

            if (candidate != null && candidate.followTarget != null)
                return candidate.followTarget;

            return candidate != null ? candidate.transform : null;
        }

        private static void FaceTarget(Transform subject, Vector3 targetPosition)
        {
            if (subject == null)
                return;

            Vector3 direction = targetPosition - subject.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.001f)
                return;

            subject.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private static void SetFallbackPlayerLocked(FpsHorrorKit.FpsController candidate, bool locked)
        {
            if (candidate == null)
                return;

            candidate.isCutScene = locked;
            candidate.isInteracting = locked;
        }
    }
}
