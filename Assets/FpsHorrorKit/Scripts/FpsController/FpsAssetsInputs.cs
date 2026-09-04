namespace FpsHorrorKit
{
    using UnityEngine;
    using UnityEngine.InputSystem;

    public class FpsAssetsInputs : MonoBehaviour
    {
        public static FpsAssetsInputs Instance { get; private set; }

        [Header("Character Input Values")]
        public Vector2 move;
        public Vector2 look;
        public bool jump;
        public bool sprint;

        [Header("Interaction Values")]
        public bool interact;
        public bool stopInteract;
        public bool useFlashlight;
        public bool useCamera;
        public bool fire;
        public bool inventory;

        [Header("Item Usage Values")]
        public bool isPressed;
        public bool isSelectedItem;
        public int itemIndex;

        [Header("Mouse Cursor Settings")]
        public bool cursorLocked = true;
        public bool cursorInputForLook = true;

        int currentItemIndex = -1;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void OnMove(InputValue value)
        {
            if (IsGameplayInputLocked())
            {
                move = Vector2.zero;
                return;
            }

            MoveInput(value.Get<Vector2>());
        }
        public void OnLook(InputValue value)
        {
            if (IsGameplayInputLocked())
            {
                look = Vector2.zero;
                return;
            }

            LookInput(value.Get<Vector2>());
        }
        public void OnJump(InputValue value)
        {
            if (IsGameplayInputLocked() || global::MainGame.P2.P2AudioLogItem.IsSpaceSkipActive)
            {
                jump = false;
                return;
            }

            JumpInput(value.isPressed);
        }
        public void OnSprint(InputValue value)
        {
            if (IsGameplayInputLocked())
            {
                sprint = false;
                return;
            }

            SprintInput(value.isPressed);
        }

        public void OnFire(InputValue value)
        {
            if (IsGameplayInputLocked())
            {
                fire = false;
                return;
            }

            FireInput(value.isPressed);
        }

        public void OnInteract(InputValue value)
        {
            if (IsGameplayInputLocked())
            {
                interact = false;
                return;
            }

            InteractInput(value.isPressed);
        }

        public void OnStopInteract(InputValue value)
        {
            if (IsGameplayInputLocked())
            {
                stopInteract = false;
                return;
            }

            StopInteractInput(value.isPressed);
        }

        public void OnUseFlashlight(InputValue value)
        {
            if (IsGameplayInputLocked())
            {
                useFlashlight = false;
                return;
            }

            UseFlashlightInput(value.isPressed);
        }

        public void OnUseCamera(InputValue value)
        {
            if (IsGameplayInputLocked())
            {
                useCamera = false;
                return;
            }

            UseCameraInput(value.isPressed);
        }
        public void OnInventory(InputValue value)
        {
            if (IsGameplayInputLocked())
            {
                inventory = false;
                return;
            }

            InventoryInput(value.isPressed);
        }
        public void OnKey1(InputValue value)
        {
            if (IsGameplayInputLocked())
                return;

            UseItem(1);
        }
        public void OnKey2(InputValue value)
        {
            if (IsGameplayInputLocked())
                return;

            UseItem(2);
        }
        public void OnKey3(InputValue value)
        {
            if (IsGameplayInputLocked())
                return;

            UseItem(3);
        }
        public void OnKey4(InputValue value)
        {
            if (IsGameplayInputLocked())
                return;

            UseItem(4);
        }

        public void UseItem(int newItemIndex)
        {
            if (IsGameplayInputLocked())
            {
                ClearGameplayInput();
                return;
            }

            isPressed = true;

            if (currentItemIndex != newItemIndex)
            {
                isSelectedItem = true;
                itemIndex = newItemIndex;
                currentItemIndex = newItemIndex;
            }
            else
            {
                currentItemIndex = -1;
                isSelectedItem = false;
            }
        }

        // Metotlar
        private void MoveInput(Vector2 moveInput) => move = moveInput;
        private void LookInput(Vector2 lookInput) => look = lookInput;
        private void JumpInput(bool jumpInput)
        {
            if (jumpInput)
                jump = true;
        }
        private void SprintInput(bool sprintInput) => sprint = sprintInput;
        private void FireInput(bool fireInput) => fire = fireInput;
        private void InteractInput(bool interactInput) => interact = interactInput;
        private void StopInteractInput(bool stopInteractInput) => stopInteract = stopInteractInput;
        private void UseFlashlightInput(bool useFlashlightInput) => useFlashlight = useFlashlightInput;
        private void UseCameraInput(bool useCameraInput) => useCamera = useCameraInput;
        private void InventoryInput(bool inventoryInput) => inventory = inventoryInput;

        public void ClearGameplayInput()
        {
            move = Vector2.zero;
            look = Vector2.zero;
            jump = false;
            sprint = false;
            interact = false;
            stopInteract = false;
            useFlashlight = false;
            useCamera = false;
            fire = false;
            inventory = false;
            isPressed = false;
            isSelectedItem = false;
            itemIndex = 0;
            currentItemIndex = -1;
        }

        private static bool IsGameplayInputLocked()
        {
            return global::GameController.IsGameplayInputLocked();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                return;

            if (global::GameController.Instance != null && global::GameController.Instance.ShouldKeepCursorVisibleForUI())
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            SetCursorState(cursorLocked);
        }

        private void SetCursorState(bool newState)
        {
            Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
        }
    }
}
