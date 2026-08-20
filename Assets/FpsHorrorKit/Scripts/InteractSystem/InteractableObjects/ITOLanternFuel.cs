using UnityEngine;

namespace FpsHorrorKit
{
    public class ITOLanternFuel : MonoBehaviour, IInteractable
    {
        [SerializeField] private Item itemLanternFuel;
        [SerializeField] private string interactText = "Nhặt pin đèn [E]";

        public void Interact()
        {
            bool result = Inventory.Instance.AddItem(itemLanternFuel, 1);
            if (result)
            {
                InteractMessageScript.Instance?.ShowMessage("Đã nhặt pin đèn. Nhấn I mở hành trang rồi bấm dùng.");
                UIInventory.Instance.UpdateUI();
                Destroy(gameObject);
            }
            else
            {
                Debug.Log("Inventory is full!");
            }
        }
        public void Highlight()
        {
            PlayerInteract.Instance.ChangeInteractText(interactText);
        }

        public void HoldInteract() { }
        public void UnHighlight() { }
    }
}
