using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FpsHorrorKit
{
    using System;
    public sealed class ClosetHiding : MonoBehaviour, IInteractable
    {
        public static bool IsAnyPlayerHidden { get; private set; }
        public static event Action<ClosetHiding> PlayerEnteredCloset;
        public static event Action<ClosetHiding> PlayerExitedCloset;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetHiddenState()
        {
            IsAnyPlayerHidden = false;
        }

        [Header("References")]
        [SerializeField] private Transform doorTransform;
        [SerializeField] private Transform hidingPoint;
        [SerializeField] private Transform exitPoint;
        [SerializeField] private Collider interactionCollider;
        [SerializeField] private AudioSource doorAudioSource;

        [Header("Interaction")]
        [SerializeField] private string enterText = "[E] Trốn vào tủ";
        [SerializeField] private string exitText = "[E] Ra khỏi tủ";
        [SerializeField, Min(1f)] private float openAngle = -105f;
        [SerializeField, Min(0.1f)] private float doorRotationSpeed = 180f;
        [SerializeField, Min(0.05f)] private float moveDuration = 0.45f;

        private FpsController playerController;
        private CharacterController playerCharacterController;
        private Renderer[] playerRenderers;
        private float closedDoorRotation;
        private bool isOccupied;
        private bool isBusy;
        private bool exitAllowed = true;
        private int interactionFrame;
        private bool previousRaycastState = true;
        private GameController.GameState previousGameState;

        private void Awake()
        {
            if (doorTransform == null)
                doorTransform = transform.Find("DoorHinge");
            if (hidingPoint == null)
                hidingPoint = transform.Find("HidingPoint");
            if (exitPoint == null)
                exitPoint = transform.Find("ExitPoint");
            if (interactionCollider == null)
                interactionCollider = GetComponentInChildren<Collider>();

            if (doorTransform != null)
                closedDoorRotation = doorTransform.localEulerAngles.y;
        }

        private void Start()
        {
            ResolvePlayer();
        }

        private void Update()
        {
            if (!isOccupied || isBusy || Time.frameCount <= interactionFrame)
                return;

            if (exitAllowed && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                StartCoroutine(ExitClosetRoutine());
        }

        public void Interact()
        {
            if (isBusy)
                return;

            if (isOccupied)
            {
                if (!exitAllowed)
                    return;

                StartCoroutine(ExitClosetRoutine());
                return;
            }

            StartCoroutine(EnterClosetRoutine());
        }

        public void Highlight()
        {
            if (isBusy)
                return;

            if (isOccupied && !exitAllowed)
                return;

            PlayerInteract.Instance?.ChangeInteractText(isOccupied ? exitText : enterText);
        }

        public void HoldInteract() { }
        public void UnHighlight() { }

        private IEnumerator EnterClosetRoutine()
        {
            ResolvePlayer();
            if (playerController == null || hidingPoint == null || doorTransform == null)
                yield break;

            isBusy = true;
            interactionFrame = Time.frameCount;
            DisableInteractionRaycast();
            previousGameState = GameController.Instance != null
                ? GameController.Instance.currentGameState
                : GameController.GameState.Gameplay;

            yield return RotateDoor(closedDoorRotation, closedDoorRotation + openAngle);
            yield return MovePlayer(hidingPoint);

            isOccupied = true;
            IsAnyPlayerHidden = true;
            PlayerEnteredCloset?.Invoke(this);
            SetPlayerVisible(false);
            if (playerCharacterController != null)
                playerCharacterController.enabled = false;

            if (GameController.Instance != null)
                GameController.Instance.StartHiding();
            else
                playerController.isInteracting = true;

            yield return RotateDoor(closedDoorRotation + openAngle, closedDoorRotation);
            isBusy = false;
        }

        private IEnumerator ExitClosetRoutine()
        {
            if (!isOccupied || playerController == null || exitPoint == null || doorTransform == null)
                yield break;

            isBusy = true;
            interactionFrame = Time.frameCount;
            yield return RotateDoor(closedDoorRotation, closedDoorRotation + openAngle);

            if (playerCharacterController != null)
                playerCharacterController.enabled = false;
            SetPlayerVisible(true);
            yield return MovePlayer(exitPoint);
            if (playerCharacterController != null)
                playerCharacterController.enabled = true;

            isOccupied = false;
            IsAnyPlayerHidden = false;
            PlayerExitedCloset?.Invoke(this);
            yield return RotateDoor(closedDoorRotation + openAngle, closedDoorRotation);

            if (GameController.Instance != null)
                GameController.Instance.SetGameState(previousGameState == GameController.GameState.Hiding
                    ? GameController.GameState.Gameplay
                    : previousGameState);
            else
                playerController.isInteracting = false;

            RestoreInteractionRaycast();
            isBusy = false;
        }

        private IEnumerator MovePlayer(Transform target)
        {
            if (target == null || playerController == null)
                yield break;

            if (playerCharacterController != null)
                playerCharacterController.enabled = false;

            Vector3 startPosition = playerController.transform.position;
            Quaternion startRotation = playerController.transform.rotation;
            float elapsed = 0f;

            while (elapsed < moveDuration)
            {
                float t = elapsed / moveDuration;
                playerController.transform.SetPositionAndRotation(
                    Vector3.Lerp(startPosition, target.position, t),
                    Quaternion.Slerp(startRotation, target.rotation, t));
                elapsed += Time.deltaTime;
                yield return null;
            }

            playerController.transform.SetPositionAndRotation(target.position, target.rotation);
        }

        private IEnumerator RotateDoor(float fromRotation, float targetRotation)
        {
            if (doorTransform == null)
                yield break;

            if (doorAudioSource != null)
                doorAudioSource.Play();
            else
                AudioManager.Instance?.PlayDoorOpenSlow();

            doorTransform.localEulerAngles = new Vector3(
                doorTransform.localEulerAngles.x,
                fromRotation,
                doorTransform.localEulerAngles.z);

            while (Mathf.Abs(Mathf.DeltaAngle(doorTransform.localEulerAngles.y, targetRotation)) > 0.1f)
            {
                float nextY = Mathf.MoveTowardsAngle(
                    doorTransform.localEulerAngles.y,
                    targetRotation,
                    doorRotationSpeed * Time.deltaTime);
                doorTransform.localEulerAngles = new Vector3(
                    doorTransform.localEulerAngles.x,
                    nextY,
                    doorTransform.localEulerAngles.z);
                yield return null;
            }

            doorTransform.localEulerAngles = new Vector3(
                doorTransform.localEulerAngles.x,
                targetRotation,
                doorTransform.localEulerAngles.z);
        }

        private void ResolvePlayer()
        {
            if (playerController != null)
                return;

            playerController = FindFirstObjectByType<FpsController>();
            if (playerController == null)
                return;

            playerCharacterController = playerController.GetComponent<CharacterController>();
            playerRenderers = playerController.GetComponentsInChildren<Renderer>(true);
        }

        private void SetPlayerVisible(bool visible)
        {
            if (playerRenderers == null)
                return;

            foreach (Renderer renderer in playerRenderers)
            {
                if (renderer != null)
                    renderer.enabled = visible;
            }
        }

        private void DisableInteractionRaycast()
        {
            if (PlayerInteract.Instance != null)
            {
                previousRaycastState = PlayerInteract.Instance.sendRaycast;
                PlayerInteract.Instance.sendRaycast = false;
            }
        }

        private void RestoreInteractionRaycast()
        {
            if (PlayerInteract.Instance != null)
                PlayerInteract.Instance.sendRaycast = previousRaycastState;
        }

        public void SetExitAllowed(bool allowed)
        {
            exitAllowed = allowed;
        }
    }
}
