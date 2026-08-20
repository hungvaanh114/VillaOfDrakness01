namespace FpsHorrorKit
{
    using UnityEngine;

    public class ITOLantern : MonoBehaviour, IInteractable
    {
        [SerializeField] private Item item;
        [SerializeField] private string interactText = "Nhặt đèn pin [E]";
        [SerializeField] private ITOLightSwitch mainLightSwitch;

        public void Interact()
        {
            item.hasItem = true;
            if(mainLightSwitch != null)
                mainLightSwitch.Interact(); // Close the all lights
            InteractMessageScript.Instance?.ShowMessage("Đã nhặt đèn pin. Nhấn 1 rồi nhấn F để dùng.");
            Destroy(gameObject);
        }
        public void Highlight()
        {
            PlayerInteract.Instance.ChangeInteractText(interactText);
        }

        public void HoldInteract() { }
        public void UnHighlight() { }
    }
}
