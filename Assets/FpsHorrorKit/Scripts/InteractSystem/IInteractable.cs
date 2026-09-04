namespace FpsHorrorKit
{
    using UnityEngine;

    public interface IInteractable
    {
        public void Interact();
        public void HoldInteract();
        public void Highlight();
        public void UnHighlight();
    }

    public interface IInteractionRaycastFilter
    {
        public bool BlocksInteractionRaycast(Collider hitCollider);
    }
}
