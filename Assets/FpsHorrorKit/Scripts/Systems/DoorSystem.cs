namespace FpsHorrorKit
{
    using System.Collections;
    using UnityEngine;


    public class DoorSystem : MonoBehaviour, IInteractable
    {
        [Header("Highlight UI")]
        [SerializeField] private string interactText = "Mở/Đóng cửa [E]";
        [SerializeField] private string doorLockedText = "Tìm chìa khóa";
        [SerializeField] private string useKeyText = "Dùng chìa khóa";

        [Header("Door Settings")]
        [Tooltip("Kapı kilitli mi?")] public bool isLocked;
        [Tooltip("Kapının anahtarına sahip mi?")] public bool hasKey;
        [Header("Main Game Key Settings")]
        public string requiredKeyID;
        public bool consumeKeyOnUse;
        [Tooltip("Khi gramophone phát tape, cửa này sẽ bị đóng và khóa cứng.")]
        public bool closeAndLockWhenGramophoneTapePlays;
        [Header("Piano Unlock")]
        public bool openWhenPianoCompleted;
        public float pianoOpenDelay = 0.25f;
        public float pianoLookAtDoorTime = 1.2f;
        public float pianoThoughtTextTime = 4f;
        [Tooltip("Kapının menteşe etrafında dnme hızı")] public float rotationSpeed = 100f;
        public float endRotation;
        public AudioSource doorAudioSource;
        public System.Action<DoorSystem> OnDoorUnlocked;
        public System.Action<DoorSystem> OnPlayerOpened;


        private float startRotation = 0;
        private bool isFinished = false;
        private bool isOpen;
        private bool subscribedToPiano;
        private bool lockedByGramophoneTape;
        private static bool gramophoneTapeLockActive;
        private static bool pianoCompletionSequenceRunning;

        private bool IsLockedByGramophoneTape => gramophoneTapeLockActive && lockedByGramophoneTape;
        public bool IsOpen => isOpen;
        public bool IsBusy => !isFinished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetRuntimeLocks()
        {
            gramophoneTapeLockActive = false;
            pianoCompletionSequenceRunning = false;
        }

        private void OnValidate()
        {
            if (openWhenPianoCompleted)
                isLocked = true;
        }

        private void Start()
        {
            if (!gramophoneTapeLockActive)
                lockedByGramophoneTape = false;

            if (openWhenPianoCompleted)
                isLocked = true;

            isFinished = true;
            startRotation = transform.localEulerAngles.y;
            if (openWhenPianoCompleted)
                StartCoroutine(SubscribeToPianoCompletion());
        }

        private void OnDestroy()
        {
            if (subscribedToPiano && PianoPuzzle.Instance != null)
                PianoPuzzle.Instance.OnPianoCompleted -= OpenAfterPianoCompleted;
        }

        public void Interact()
        {
            if (IsLockedByGramophoneTape)
            {
                AudioManager.Instance?.PlayDoorLocked();
                InteractMessageScript.Instance?.ShowMessage("Cửa bị khóa chặt.");
                return;
            }

            if (hasKey)
            {
                isLocked = false;
            }
            if (isLocked && !TryUnlockWithEquippedKey()) { return; }

            if (!isOpen && isFinished)
            {
                StartCoroutine(OpenDoor(endRotation));
                isOpen = true;
            }
            else if (isOpen && isFinished)
            {
                StartCoroutine(OpenDoor(startRotation));
                isOpen = false;
            }

        }

        public bool TryOpenForMonster()
        {
            if (isOpen)
                return true;

            if (!isFinished)
                return false;

            // The monster can force open a normal or key-locked door during its search.
            // The tape lock still prevents normal player interaction afterward.
            isLocked = false;
            StartCoroutine(OpenDoor(endRotation));
            isOpen = true;
            return true;
        }

        public void CloseAndLockFromStory()
        {
            lockedByGramophoneTape = true;
            gramophoneTapeLockActive = true;
            isLocked = true;
            hasKey = false;

            if (isOpen && isFinished)
            {
                StartCoroutine(OpenDoor(startRotation));
                isOpen = false;
            }
        }

        public void UnlockAndOpenFromStory()
        {
            lockedByGramophoneTape = false;
            isLocked = false;
            hasKey = true;
            AudioManager.Instance?.PlayDoorUnlock();
            OnDoorUnlocked?.Invoke(this);

            if (!isOpen && isFinished)
            {
                StartCoroutine(OpenDoor(endRotation, () => OnPlayerOpened?.Invoke(this)));
                isOpen = true;
            }
        }

        public void Highlight()
        {
            if (IsLockedByGramophoneTape)
            {
                PlayerInteract.Instance.ChangeInteractText("Cửa bị khóa chặt.");
            }
            else if (isLocked && !string.IsNullOrWhiteSpace(requiredKeyID))
            {
                PlayerInteract.Instance.ChangeInteractText("Cửa đã khóa.");
            }
            else if (hasKey && isLocked)
            {
                PlayerInteract.Instance.ChangeInteractText(useKeyText);
            }
            else if (isLocked)
            {
                PlayerInteract.Instance.ChangeInteractText(doorLockedText);
            }
            else
            {
                PlayerInteract.Instance.ChangeInteractText(interactText);
            }
        }

        private bool TryUnlockWithEquippedKey()
        {
            if (string.IsNullOrWhiteSpace(requiredKeyID))
            {
                AudioManager.Instance?.PlayDoorLocked();
                return false;
            }

            var inventory = InventoryManager.Instance;
            var equippedKey = inventory != null ? inventory.CurrentEquippedKey : null;
            if (equippedKey == null)
            {
                AudioManager.Instance?.PlayDoorLocked();
                InteractMessageScript.Instance?.ShowMessage("Cửa đã khóa.");
                return false;
            }

            if (equippedKey.keyID != requiredKeyID)
            {
                AudioManager.Instance?.PlayDoorLocked();
                InteractMessageScript.Instance?.ShowMessage("Chìa khóa này không mở được cửa.");
                return false;
            }

            isLocked = false;
            hasKey = true;
            if (consumeKeyOnUse && equippedKey.itemType != ItemType.Key)
                inventory.RemoveItem(equippedKey, 1);
            AudioManager.Instance?.PlayDoorUnlock();
            InteractMessageScript.Instance?.ShowMessage("Đã mở khóa cửa.");
            OnDoorUnlocked?.Invoke(this);
            return true;
        }

        IEnumerator OpenDoor(float targetRotation, System.Action onComplete = null)
        {
            isFinished = false;
            if (doorAudioSource != null && doorAudioSource.clip != null) doorAudioSource.Play();
            else AudioManager.Instance?.PlayDoorOpenSlow();

            while (Mathf.Abs(Mathf.DeltaAngle(transform.localEulerAngles.y, targetRotation)) > 0.1f)
            {
                float step = rotationSpeed * Time.deltaTime;
                float newY = Mathf.MoveTowardsAngle(transform.localEulerAngles.y, targetRotation, step);
                transform.localEulerAngles = new Vector3(transform.localEulerAngles.x, newY, transform.localEulerAngles.z);
                yield return null;
            }
            // Son rotasyonu kesinleştir
            transform.localEulerAngles = new Vector3(transform.localEulerAngles.x, targetRotation, transform.localEulerAngles.z);
            isFinished = true;
            onComplete?.Invoke();
            Debug.Log("Door opened");
        }
        public void HoldInteract() { }
        public void UnHighlight() { }

        public static void LockDoorsMarkedForGramophoneTape()
        {
            gramophoneTapeLockActive = true;

            var doors = FindObjectsByType<DoorSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var door in doors)
            {
                if (door != null && door.closeAndLockWhenGramophoneTapePlays)
                    door.LockFromGramophoneTape();
            }
        }

        private void LockFromGramophoneTape()
        {
            CloseAndLockFromStory();
        }

        private IEnumerator SubscribeToPianoCompletion()
        {
            while (PianoPuzzle.Instance == null)
                yield return null;

            PianoPuzzle.Instance.OnPianoCompleted -= OpenAfterPianoCompleted;
            PianoPuzzle.Instance.OnPianoCompleted += OpenAfterPianoCompleted;
            subscribedToPiano = true;
        }

        private void OpenAfterPianoCompleted()
        {
            if (!openWhenPianoCompleted || pianoCompletionSequenceRunning)
                return;

            var nearestDoor = FindNearestPianoUnlockDoor();
            if (nearestDoor != this)
                return;

            pianoCompletionSequenceRunning = true;
            StartCoroutine(OpenAfterPianoCompletedRoutine());
        }

        private IEnumerator OpenAfterPianoCompletedRoutine()
        {
            if (pianoOpenDelay > 0f)
                yield return new WaitForSeconds(pianoOpenDelay);

            var gameController = GameController.Instance;
            var playerController = gameController != null && gameController.playerController != null
                ? gameController.playerController
                : FindFirstObjectByType<FpsController>();

            if (gameController != null)
            {
                gameController.SetPlayerControl(false);
                if (gameController.gameUI != null)
                    gameController.gameUI.SetActive(true);
            }
            else if (playerController != null)
            {
                playerController.isCutScene = true;
                playerController.isInteracting = true;
            }

            if (PlayerInteract.Instance != null)
                PlayerInteract.Instance.sendRaycast = false;

            if (playerController != null)
            {
                playerController.SetCutSceneCameraPitch(0f);
                yield return RotatePlayerTowardDoor(playerController);
            }

            UnlockAndOpenFromPiano();

            InteractMessageScript.Instance?.ShowMessage("\"C\u1eeda... t\u1ef1 m\u1edf? C\u00e1i... c\u00e1i g\u00ec v\u1eeba x\u1ea3y ra v\u1eady?\"", pianoThoughtTextTime);

            if (pianoThoughtTextTime > 0f)
                yield return new WaitForSeconds(pianoThoughtTextTime);

            if (gameController != null)
                gameController.StartGameplay();
            else if (playerController != null)
            {
                playerController.isCutScene = false;
                playerController.isInteracting = false;
            }

            if (PlayerInteract.Instance != null)
                PlayerInteract.Instance.sendRaycast = true;

            pianoCompletionSequenceRunning = false;
        }

        private void UnlockAndOpenFromPiano()
        {
            if (IsLockedByGramophoneTape)
                return;

            isLocked = false;
            hasKey = true;
            AudioManager.Instance?.PlayDoorUnlock();
            OnDoorUnlocked?.Invoke(this);

            if (!isOpen && isFinished)
            {
                StartCoroutine(OpenDoor(endRotation));
                isOpen = true;
            }
        }

        private IEnumerator RotatePlayerTowardDoor(FpsController playerController)
        {
            float elapsed = 0f;
            float duration = Mathf.Max(0.1f, pianoLookAtDoorTime);
            Vector3 targetPosition = transform.position;

            while (elapsed < duration)
            {
                Vector3 direction = targetPosition - playerController.transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude <= 0.001f)
                    break;

                playerController.RotateCutSceneTowards(direction, 540f);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private static DoorSystem FindNearestPianoUnlockDoor()
        {
            var doors = FindObjectsByType<DoorSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var player = FindFirstObjectByType<FpsController>();
            Vector3 origin = player != null ? player.transform.position : Vector3.zero;
            DoorSystem nearestDoor = null;
            float nearestDistance = float.MaxValue;

            foreach (var door in doors)
            {
                if (door == null || !door.openWhenPianoCompleted)
                    continue;

                float distance = player != null ? (door.transform.position - origin).sqrMagnitude : 0f;
                if (nearestDoor == null || distance < nearestDistance)
                {
                    nearestDoor = door;
                    nearestDistance = distance;
                }
            }

            return nearestDoor;
        }
    }
}
