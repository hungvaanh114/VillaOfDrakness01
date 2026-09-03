using System.Collections;
using UnityEngine;

namespace MainGame.P2
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class P2MirrorJumpscare : MonoBehaviour
    {
        [Header("Mirror")]
        [SerializeField] private MirrorReflectionCamera reflection;
        [SerializeField] private Transform mirrorRaycastTarget;
        [SerializeField] private bool triggerOnlyOnce = true;

        [Header("Trigger")]
        [SerializeField, Min(0f)] private float requiredMirrorLookSeconds = 1.35f;
        [SerializeField, Min(0.1f)] private float requiredPlayerDistance = 5f;
        [SerializeField] private bool requirePlayerInsideTrigger;
        [SerializeField] private bool requirePlayerInFront = true;
        [SerializeField] private bool requireMirrorRaycast = true;
        [SerializeField, Min(0.1f)] private float mirrorRaycastDistance = 8f;
        [SerializeField, Min(0f)] private float mirrorRaycastRadius = 0.08f;
        [SerializeField, Range(0.1f, 1f)] private float mirrorAimFallbackDot = 0.94f;

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

        private BoxCollider triggerCollider;
        private FpsHorrorKit.FpsController playerController;
        private bool hasTriggered;
        private bool isRunning;
        private bool playerInsideTrigger;
        private float mirrorLookTimer;

        private void Awake()
        {
            triggerCollider = GetComponent<BoxCollider>();
            triggerCollider.isTrigger = true;

            if (reflection == null)
                reflection = GetComponent<MirrorReflectionCamera>();
            if (sfxSource == null)
                sfxSource = GetComponent<AudioSource>();

            ResolveMirrorRaycastTarget();
            ResolveP2References();
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

            if (requirePlayerInFront && Vector3.Dot(GetMirrorForward(), toPlayer.normalized) < 0f)
                return false;

            return !requireMirrorRaycast || IsPlayerLookingAtMirror(candidate);
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

            reflection?.SetBloodStained();
            PlayMirrorEventSound();
            ShowRevealObject(true, candidate);
            ghostDirector?.ForceApparitionNearPlayer();

            if (mirrorHoldSeconds > 0f)
                yield return new WaitForSeconds(mirrorHoldSeconds);

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
            if (mirrorEventClip == null)
                return;

            if (sfxSource != null)
                sfxSource.PlayOneShot(mirrorEventClip);
            else
                AudioSource.PlayClipAtPoint(mirrorEventClip, transform.position);
        }

        private bool IsPlayerLookingAtMirror(FpsHorrorKit.FpsController candidate)
        {
            Transform source = ResolveLookSource(candidate);
            if (source == null)
                return false;

            if (IsRaycastHittingMirror(candidate, source.position, source.forward, out bool blocked))
                return true;
            if (blocked)
                return false;

            Vector3 toMirror = GetMirrorLookPoint() - source.position;
            if (toMirror.magnitude > mirrorRaycastDistance)
                return false;

            return Vector3.Dot(source.forward.normalized, toMirror.normalized) >= mirrorAimFallbackDot;
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
            return target != null && (hitTransform == target || hitTransform.IsChildOf(target));
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
            return target != null ? target.forward : transform.forward;
        }

        private static Transform ResolveLookSource(FpsHorrorKit.FpsController candidate)
        {
            if (candidate != null && candidate.followTarget != null)
                return candidate.followTarget;

            Camera camera = Camera.main;
            if (camera != null && camera.isActiveAndEnabled)
                return camera.transform;

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
