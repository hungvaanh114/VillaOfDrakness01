using FpsHorrorKit;
using UnityEngine;

namespace MainGame.P2
{
    [RequireComponent(typeof(Collider))]
    public sealed class P2LockedCabinet : MonoBehaviour, IInteractable
    {
        [SerializeField] private ItemData requiredKey;
        [SerializeField] private GameObject closedVisual;
        [SerializeField] private GameObject openVisual;
        [SerializeField] private GameObject contentsRoot;
        [SerializeField] private string lockedText = "Tủ bị khóa. Cần chìa khóa tủ.";
        [SerializeField] private string openText = "[E] Mở tủ";
        [SerializeField] private string openedText = "Đã mở tủ. Bên trong có một con búp bê.";
        [SerializeField] private bool consumeKey;

        private bool opened;

        public void Configure(ItemData key, GameObject closed, GameObject openedObject, GameObject contents)
        {
            requiredKey = key;
            closedVisual = closed;
            openVisual = openedObject;
            contentsRoot = contents;
            ApplyVisualState();
        }

        public void Interact()
        {
            if (opened)
                return;

            var inventory = InventoryManager.Instance;
            if (requiredKey != null && (inventory == null || !inventory.Contains(requiredKey)))
            {
                InteractMessageScript.Instance?.ShowMessage(lockedText);
                AudioManager.Instance?.PlayDoorLocked();
                return;
            }

            opened = true;
            if (consumeKey && inventory != null && requiredKey != null)
                inventory.RemoveItem(requiredKey, 1);

            ApplyVisualState();
            InteractMessageScript.Instance?.ShowMessage(openedText, 2.8f);
            AudioManager.Instance?.PlayDoorUnlock();
        }

        public void Highlight()
        {
            PlayerInteract.Instance?.ChangeInteractText(opened ? string.Empty : openText);
        }

        public void HoldInteract()
        {
        }

        public void UnHighlight()
        {
        }

        private void Awake()
        {
            ApplyVisualState();
        }

        private void ApplyVisualState()
        {
            if (closedVisual != null)
                closedVisual.SetActive(!opened);
            if (openVisual != null)
                openVisual.SetActive(opened);
            if (contentsRoot != null)
                contentsRoot.SetActive(opened);
        }
    }
}
