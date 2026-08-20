namespace FpsHorrorKit
{
    using UnityEngine;

    public class ITOCamera : MonoBehaviour, IInteractable
    {
        public Item photoCamera;
        [SerializeField] private string interactText = "Nhặt camera [E]";

        public void Interact()
        {
            InteractMessageScript.Instance?.ShowMessage("Đã nhặt camera. Nhấn 2 rồi nhấn T để dùng.");
            photoCamera.hasItem = true;
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
