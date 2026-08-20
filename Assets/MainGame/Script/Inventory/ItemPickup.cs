using UnityEngine;

namespace FpsHorrorKit
{
    public sealed class ItemPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private ItemData itemData;
        [SerializeField] private int amount = 1;
        [SerializeField] private string interactText = "Nhấn E để nhặt vật phẩm";

        public ItemData ItemData => itemData;

        public void Interact()
        {
            if (InventoryManager.Instance == null || itemData == null)
                return;

            if (InventoryManager.Instance.AddItem(itemData, amount))
            {
                AudioManager.Instance?.PlayItemPickup(itemData);
                InteractMessageScript.Instance?.ShowMessage($"Đã nhặt {itemData.itemName}.");
                Destroy(gameObject);
            }
            else
            {
                InteractMessageScript.Instance?.ShowMessage("Hành trang đã đầy.");
            }
        }

        public void Highlight()
        {
            string itemName = itemData != null ? itemData.itemName : "vật phẩm";
            PlayerInteract.Instance?.ChangeInteractText(string.IsNullOrWhiteSpace(interactText) ? $"[E] Nhặt {itemName}" : interactText);
        }

        public void HoldInteract() { }
        public void UnHighlight() { }
    }
}
