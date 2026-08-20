namespace FpsHorrorKit
{
    using UnityEngine;

    public class ITOKey : MonoBehaviour, IInteractable
    {
        public DoorSystem compatibleDoor;
        [SerializeField] private string interactText = "Nhặt chìa khóa [E]";

        public void Interact()
        {
            compatibleDoor.hasKey = true;
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
