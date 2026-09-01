using UnityEngine;

namespace FpsHorrorKit
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class UpperFloorBlocker : MonoBehaviour
    {
        [SerializeField] private GameProgress requiredProgressToPass = GameProgress.PianoCompleted;
        [SerializeField] private string blockedMessage = "Chua kham pha xong tang tret.";
        [SerializeField, Min(0.1f)] private float messageDuration = 2.4f;
        [SerializeField] private bool showMessageOnlyOnce = true;

        private BoxCollider blockerCollider;
        private BoxCollider messageTriggerCollider;
        private bool hasShownMessage;
        private FpsController player;
        private bool blockerTemporarilyOpenForPlayerExit;

        private void Awake()
        {
            ResolveColliders();
            IgnoreMonsterColliders();
        }

        private void Update()
        {
            if (CanPass())
            {
                if (blockerCollider != null && blockerCollider.enabled)
                    blockerCollider.enabled = false;

                if (messageTriggerCollider != null && messageTriggerCollider.enabled)
                    messageTriggerCollider.enabled = false;

                return;
            }

            UpdatePlayerExitUnstuck();
        }

        private void OnTriggerEnter(Collider other)
        {
            TryShowBlockedMessageFromTrigger(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryShowBlockedMessageFromTrigger(other);
        }

        private void TryShowBlockedMessageFromTrigger(Collider other)
        {
            if (other == null || CanPass())
                return;

            var triggerPlayer = other.GetComponentInParent<FpsController>();
            if (triggerPlayer == null)
                return;

            ShowBlockedMessageOnce();
        }

        private void ShowBlockedMessageOnce()
        {
            if (!showMessageOnlyOnce || !hasShownMessage)
            {
                hasShownMessage = true;
                InteractMessageScript.Instance?.ShowMessage(blockedMessage, messageDuration);
            }
        }

        private bool CanPass()
        {
            var progress = GameProgressManager.Instance;
            return progress != null && progress.CurrentProgress >= requiredProgressToPass;
        }

        private void UpdatePlayerExitUnstuck()
        {
            if (blockerCollider == null)
                return;

            if (player == null)
                player = FindFirstObjectByType<FpsController>();

            if (player == null)
                return;

            bool playerInsideBlocker = IsPlayerOverlappingBlocker(player);
            if (playerInsideBlocker)
            {
                if (blockerCollider.enabled)
                    blockerCollider.enabled = false;

                blockerTemporarilyOpenForPlayerExit = true;
                return;
            }

            if (blockerTemporarilyOpenForPlayerExit)
            {
                blockerCollider.enabled = true;
                blockerTemporarilyOpenForPlayerExit = false;
                IgnoreMonsterColliders();
            }
        }

        private bool IsPlayerOverlappingBlocker(FpsController targetPlayer)
        {
            if (targetPlayer == null || blockerCollider == null)
                return false;

            var characterController = targetPlayer.GetComponent<CharacterController>();
            Vector3 playerCenter = characterController != null
                ? targetPlayer.transform.TransformPoint(characterController.center)
                : targetPlayer.transform.position;

            Vector3 localPlayerCenter = blockerCollider.transform.InverseTransformPoint(playerCenter) - blockerCollider.center;
            Vector3 blockerExtents = blockerCollider.size * 0.5f;

            Vector3 totalExtents = blockerExtents + Vector3.one * 0.05f;

            return Mathf.Abs(localPlayerCenter.x) <= totalExtents.x
                && Mathf.Abs(localPlayerCenter.y) <= totalExtents.y
                && Mathf.Abs(localPlayerCenter.z) <= totalExtents.z;
        }

        private void ResolveColliders()
        {
            var boxColliders = GetComponents<BoxCollider>();
            for (int i = 0; i < boxColliders.Length; i++)
            {
                if (boxColliders[i] == null)
                    continue;

                if (boxColliders[i].isTrigger)
                {
                    messageTriggerCollider ??= boxColliders[i];
                }
                else
                {
                    blockerCollider ??= boxColliders[i];
                }
            }

            if (blockerCollider == null && boxColliders.Length > 0)
            {
                blockerCollider = boxColliders[0];
                blockerCollider.isTrigger = false;
            }
        }

        private void IgnoreMonsterColliders()
        {
            if (blockerCollider == null)
                return;

            var monsters = FindObjectsByType<MonsterAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int monsterIndex = 0; monsterIndex < monsters.Length; monsterIndex++)
            {
                if (monsters[monsterIndex] == null)
                    continue;

                var monsterColliders = monsters[monsterIndex].GetComponentsInChildren<Collider>(true);
                for (int colliderIndex = 0; colliderIndex < monsterColliders.Length; colliderIndex++)
                {
                    if (monsterColliders[colliderIndex] != null)
                        Physics.IgnoreCollision(blockerCollider, monsterColliders[colliderIndex], true);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null)
                return;

            Gizmos.color = new Color(1f, 0.25f, 0.1f, 0.35f);
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.matrix = oldMatrix;
        }
    }
}
