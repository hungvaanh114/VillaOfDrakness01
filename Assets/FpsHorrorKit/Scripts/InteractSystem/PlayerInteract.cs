namespace FpsHorrorKit
{
    using System;
    using TMPro;
    using UnityEngine;
    using UnityEngine.UI;

    public class PlayerInteract : MonoBehaviour
    {
        public static PlayerInteract Instance { get; private set; }

        [Header("Raycast Settings")]
        public bool sendRaycast = true;
        public float interactRange = 2.0f;

        [Header("Highlight Settings")]
        public GameObject higlightObject;
        public TextMeshProUGUI interactTextUI;
        public Image interactImageUI;
        public bool showHiglight = true;

        [SerializeField] private bool canDragDoor;

        private FpsAssetsInputs input;
        private IInteractable currentInteractable;
        private GameObject defaultHighlightObj;
        private string defaultInteractText;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            input = FindAnyObjectByType<FpsAssetsInputs>();
            ResolveHudReferences();

            defaultInteractText = "Nhấn E để tương tác";
            if (interactTextUI != null)
                interactTextUI.text = defaultInteractText;

            defaultHighlightObj = higlightObject;
            SetHighlightActive(false);
        }

        private void Update()
        {
            if (global::GameController.IsGameplayInputLocked())
            {
                input?.ClearGameplayInput();
                UnHighlight();
                return;
            }

            if (currentInteractable != null)
            {
                if (Input.GetMouseButton(0) && canDragDoor)
                {
                    SetHighlightActive(false);
                    currentInteractable.HoldInteract();
                    sendRaycast = false;
                }
                else if (Input.GetMouseButtonUp(0))
                {
                    UnHighlight();
                    sendRaycast = true;
                }
            }

            if (sendRaycast)
            {
                showHiglight = true;
                SendRaycast();
            }
            else
            {
                showHiglight = false;
            }
        }

        private void SendRaycast()
        {
            if (Camera.main == null)
            {
                UnHighlight();
                return;
            }

            var ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
            var hits = Physics.RaycastAll(ray, interactRange, ~0, QueryTriggerInteraction.Ignore);
            if (hits.Length == 0)
            {
                UnHighlight();
                return;
            }

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                var filter = hit.collider.GetComponent<IInteractionRaycastFilter>() ?? hit.collider.GetComponentInParent<IInteractionRaycastFilter>();
                if (filter != null && !filter.BlocksInteractionRaycast(hit.collider))
                    continue;

                var interactable = hit.collider.GetComponent<IInteractable>() ?? hit.collider.GetComponentInParent<IInteractable>();
                if (interactable == null)
                {
                    UnHighlight();
                    return;
                }

                if (currentInteractable != null && currentInteractable != interactable)
                    currentInteractable.UnHighlight();

                currentInteractable = interactable;
                canDragDoor = CanHoldInteract(currentInteractable);
                Highlight();

                if (input != null && input.interact && higlightObject != null && higlightObject.activeSelf)
                {
                    currentInteractable.Interact();
                    input.interact = false;
                    UnHighlight();
                }

                return;
            }

            UnHighlight();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactRange);
        }

        private void Highlight()
        {
            currentInteractable?.Highlight();
            SetHighlightActive(showHiglight);
        }

        private void UnHighlight()
        {
            canDragDoor = false;
            currentInteractable?.UnHighlight();
            currentInteractable = null;

            higlightObject = defaultHighlightObj;
            if (interactTextUI != null)
                interactTextUI.text = defaultInteractText;

            SetHighlightActive(false);
        }

        public void ChangeInteractText(string interactText)
        {
            if (interactTextUI != null)
                interactTextUI.text = string.IsNullOrWhiteSpace(interactText) ? defaultInteractText : interactText;
        }

        public void ChangeInteractImage(Sprite interactImage)
        {
            if (interactImageUI == null)
                return;

            interactImageUI.sprite = interactImage;
        }

        private void ResolveHudReferences()
        {
            if (higlightObject == null)
                higlightObject = FindSceneObject("InteractPrompt");

            if (interactTextUI == null)
            {
                var textObject = FindSceneObject("InteractText");
                if (textObject != null)
                    interactTextUI = textObject.GetComponent<TextMeshProUGUI>();
            }
        }

        private static GameObject FindSceneObject(string objectName)
        {
            foreach (var transform in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (transform.name == objectName && transform.gameObject.scene.IsValid())
                    return transform.gameObject;
            }

            return null;
        }

        private void SetHighlightActive(bool active)
        {
            if (higlightObject != null)
                higlightObject.SetActive(active);
        }

        private static bool CanHoldInteract(IInteractable interactable)
        {
            return interactable is DragToOpenSystem || interactable is DrawerSystem;
        }
    }
}
