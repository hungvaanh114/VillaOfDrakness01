namespace FpsHorrorKit
{
    using UnityEngine;

    public class ITOCameraBattery : MonoBehaviour, IInteractable
    {
        [SerializeField] private Item itemCameraBattery;
        [SerializeField] private string interactText = "Nhặt pin camera [E]";

        public void Interact()
        {
            bool result = Inventory.Instance.AddItem(itemCameraBattery, 1);
            if (result)
            {
                InteractMessageScript.Instance?.ShowMessage("Đã nhặt pin camera. Nhấn I mở hành trang rồi bấm dùng.");
                UIInventory.Instance.UpdateUI();
                Destroy(gameObject);
            }
            else
            {
                Debug.Log("Inventory is full!");
            }
        }
        public void UpdateBattery()
        {
            Debug.Log("Camera Battery updated!");
        }
        public void Highlight()
        {
            PlayerInteract.Instance.ChangeInteractText(interactText);
        }

        public void HoldInteract() { }
        public void UnHighlight() { }
    }
}
