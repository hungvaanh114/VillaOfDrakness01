using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FpsHorrorKit
{
    public sealed class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }
        public static event Action<bool> FallbackFlashlightChanged;

        [SerializeField] private int inventorySize = 12;
        [SerializeField] private ItemData startingFlashlight;
        [SerializeField, Range(0f, 100f)] private float flashlightBatteryPercent = 78f;
        [SerializeField, Min(0f)] private float flashlightBatteryDrainPerSecond = 0.5f;

        private readonly List<InventoryItem> items = new();
        private readonly Image[] fallbackBatteryCells = new Image[6];

        private GameObject fallbackFlashlightObject;
        private Image fallbackLanternFuelFill;
        private TextMeshProUGUI fallbackBatteryPercentText;
        private MainGame.P2.P2OilLamp p2OilLamp;
        private bool fallbackFlashlightOn;
        private bool hasSyncedFallbackFlashlight;
        private bool cutsceneFallbackFlashlightForced;
        private bool savedFallbackFlashlightOn;
        private bool savedFallbackFlashlightActive;

        public event Action OnInventoryChanged;
        public event Action<ItemData, int> OnItemAdded;
        public event Action<ItemData, int> OnItemRemoved;
        public event Action<ItemData> OnItemSelected;
        public event Action<ItemData> OnItemUsed;
        public event Action<ItemData> OnItemEquipped;

        public IReadOnlyList<InventoryItem> Items => items;
        public ItemData CurrentEquippedItem { get; private set; }
        public ItemData CurrentEquippedKey => CurrentEquippedItem != null && CurrentEquippedItem.itemType == ItemType.Key ? CurrentEquippedItem : null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            if (startingFlashlight != null && !Contains(startingFlashlight))
                AddItem(startingFlashlight, 1);

            ItemUsageSystem.Instance?.GrantFlashlightItem(false);
            InitializeFallbackFlashlight();
            SyncFallbackFlashlightStateFromScene();
            UpdateFallbackFlashlightUi();
        }

        private void Update()
        {
            UpdateFallbackFlashlight();
        }

        public bool AddItem(ItemData item, int amount = 1)
        {
            if (item == null || amount <= 0)
                return false;

            if (item.itemType == ItemType.MusicSheet)
                return false;

            var existing = Find(item);
            if (existing != null)
            {
                if (!item.canStack)
                    return false;

                int room = Mathf.Max(0, item.maxStack - existing.Amount);
                int accepted = Mathf.Min(room, amount);
                if (accepted <= 0)
                    return false;

                existing.Add(accepted);
                OnItemAdded?.Invoke(item, accepted);
                OnInventoryChanged?.Invoke();
                return accepted == amount;
            }

            if (items.Count >= inventorySize)
                return false;

            int addAmount = item.canStack ? Mathf.Clamp(amount, 1, Mathf.Max(1, item.maxStack)) : 1;
            items.Add(new InventoryItem(item, addAmount));
            OnItemAdded?.Invoke(item, addAmount);
            OnInventoryChanged?.Invoke();
            return addAmount == amount || !item.canStack;
        }

        public bool RemoveItem(ItemData item, int amount = 1)
        {
            var existing = Find(item);
            if (existing == null)
                return false;

            existing.Remove(amount);
            if (existing.Amount <= 0)
            {
                items.Remove(existing);
                if (CurrentEquippedItem == item)
                    UnequipCurrent();
            }

            OnItemRemoved?.Invoke(item, amount);
            OnInventoryChanged?.Invoke();
            return true;
        }

        public bool Contains(ItemData item)
        {
            return Find(item) != null;
        }

        public void ResetToStartingItems()
        {
            items.Clear();
            CurrentEquippedItem = null;
            HeldItemController.Instance?.Clear();

            if (startingFlashlight != null)
                AddItem(startingFlashlight, 1);

            ItemUsageSystem.Instance?.GrantFlashlightItem(false);
            OnItemEquipped?.Invoke(null);
            OnInventoryChanged?.Invoke();
        }

        public InventoryItem Find(ItemData item)
        {
            return items.Find(entry => entry.Data == item);
        }

        public InventoryItem FindById(string id)
        {
            return items.Find(entry => entry.Data != null && entry.Data.id == id);
        }

        public void SelectItem(ItemData item)
        {
            OnItemSelected?.Invoke(item);
        }

        public bool UseItem(ItemData item)
        {
            if (item == null || !Contains(item) || !item.canUse)
                return false;

            bool used = false;
            bool playGenericUseSound = true;
            if (DebtBookUI.IsDebtBook(item))
            {
                used = DebtBookUI.Open(item);
                playGenericUseSound = false;
            }
            else
            {
                switch (item.itemType)
                {
                    case ItemType.Battery:
                        used = UseBattery(item);
                        playGenericUseSound = false;
                        break;
                    case ItemType.Key:
                        EquipItem(item);
                        used = true;
                        break;
                    case ItemType.Flashlight:
                        if (ItemUsageSystem.Instance != null)
                            ItemUsageSystem.Instance.ForceFlashlightOn(true);
                        else
                            ToggleFallbackFlashlight();
                        used = true;
                        break;
                    default:
                        used = true;
                        break;
                }
            }

            if (used)
            {
                if (playGenericUseSound)
                    AudioManager.Instance?.PlayGenericInteract();

                if (item.itemType != ItemType.Flashlight && CurrentEquippedItem?.itemType != ItemType.Flashlight)
                    HeldItemController.Instance?.HideCurrentVisual();

                OnItemUsed?.Invoke(item);
                OnInventoryChanged?.Invoke();
            }

            return used;
        }

        public void EquipItem(ItemData item)
        {
            if (item == null || !Contains(item))
                return;

            CurrentEquippedItem = item;
            HeldItemController.Instance?.Equip(item);
            OnItemEquipped?.Invoke(item);
            OnInventoryChanged?.Invoke();
        }

        public void UnequipCurrent()
        {
            CurrentEquippedItem = null;
            HeldItemController.Instance?.Clear();
            OnItemEquipped?.Invoke(null);
            OnInventoryChanged?.Invoke();
        }

        private bool UseBattery(ItemData item)
        {
            if (!RechargeFlashlightBattery())
            {
                InteractMessageScript.Instance?.ShowMessage("Pin đèn đã đầy.");
                return false;
            }

            RemoveItem(item, 1);
            AudioManager.Instance?.PlayFlashlightBatteryUse();
            InteractMessageScript.Instance?.ShowMessage("Đã sạc đầy pin đèn pin.");
            return true;
        }

        private bool RechargeFlashlightBattery()
        {
            bool recharged = false;

            var updater = FindFirstObjectByType<FlashLightUpdater>();
            if (updater != null)
                recharged = updater.TryRecharge();

            if (flashlightBatteryPercent < 100f)
            {
                flashlightBatteryPercent = 100f;
                recharged = true;
            }

            UpdateFallbackFlashlightUi();
            return recharged;
        }

        private void UpdateFallbackFlashlight()
        {
            if (ItemUsageSystem.Instance != null)
                return;

            InitializeFallbackFlashlight();
            SyncFallbackFlashlightStateFromScene();

            if (TryResolveP2OilLamp())
            {
                if (cutsceneFallbackFlashlightForced)
                    SetFallbackFlashlightActive(true);
                else if (!GameController.IsGameplayInputLocked() && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
                    ToggleFallbackFlashlight();

                return;
            }

            if (cutsceneFallbackFlashlightForced)
            {
                SetFallbackFlashlightActive(true);
                UpdateFallbackFlashlightUi();
                return;
            }

            if (GameController.IsGameplayInputLocked())
            {
                UpdateFallbackFlashlightUi();
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
                ToggleFallbackFlashlight();

            if (!fallbackFlashlightOn)
            {
                UpdateFallbackFlashlightUi();
                return;
            }

            flashlightBatteryPercent = Mathf.Max(0f, flashlightBatteryPercent - flashlightBatteryDrainPerSecond * Time.deltaTime);
            if (flashlightBatteryPercent <= 0f)
            {
                fallbackFlashlightOn = false;
                SetFallbackFlashlightActive(false);
                InteractMessageScript.Instance?.ShowMessage("Pin đèn đã hết.");
            }

            UpdateFallbackFlashlightUi();
        }

        private void ToggleFallbackFlashlight()
        {
            if (GameController.IsGameplayInputLocked())
                return;

            if (ItemUsageSystem.Instance != null)
            {
                ItemUsageSystem.Instance.UseFlashlight();
                return;
            }

            if (flashlightBatteryPercent <= 0f)
            {
                SetFallbackFlashlightActive(false);
                InteractMessageScript.Instance?.ShowMessage("Pin đèn đã hết.");
                return;
            }

            fallbackFlashlightOn = !IsFallbackFlashlightLightActive();
            SetFallbackFlashlightActive(fallbackFlashlightOn);
            AudioManager.Instance?.PlayFlashlightToggle();
        }

        public void SetCutsceneFallbackFlashlightForced(bool forced)
        {
            if (ItemUsageSystem.Instance != null)
                return;

            InitializeFallbackFlashlight();

            if (forced)
            {
                if (!cutsceneFallbackFlashlightForced)
                {
                    savedFallbackFlashlightOn = fallbackFlashlightOn;
                    savedFallbackFlashlightActive = IsFallbackFlashlightLightActive();
                }

                cutsceneFallbackFlashlightForced = true;
                SetFallbackFlashlightActive(true);
                UpdateFallbackFlashlightUi();
                return;
            }

            if (!cutsceneFallbackFlashlightForced)
                return;

            cutsceneFallbackFlashlightForced = false;
            fallbackFlashlightOn = savedFallbackFlashlightOn;
            SetFallbackFlashlightActive(savedFallbackFlashlightActive);
            UpdateFallbackFlashlightUi();
        }

        private void InitializeFallbackFlashlight()
        {
            if (fallbackFlashlightObject == null)
            {
                var controller = FindFirstObjectByType<FpsController>();
                if (controller != null && controller.flashlightLight != null && controller.flashlightLight.name != "Spot Light_1")
                    fallbackFlashlightObject = controller.flashlightLight.gameObject;

                var followTarget = controller != null && controller.followTarget != null
                    ? controller.followTarget.gameObject
                    : GameObject.Find("FollowTarget");
                var spotLight = fallbackFlashlightObject == null && followTarget != null
                    ? followTarget.transform.Find("Spot Light")
                    : null;
                if (spotLight != null)
                    fallbackFlashlightObject = spotLight.gameObject;
            }

            if (fallbackBatteryPercentText == null)
            {
                var textObject = GameObject.Find("BatteryPercentText");
                if (textObject != null)
                    fallbackBatteryPercentText = textObject.GetComponent<TextMeshProUGUI>();
            }

            if (fallbackLanternFuelFill == null)
            {
                var fillObject = GameObject.Find("LanternFuelFill");
                if (fillObject != null)
                    fallbackLanternFuelFill = fillObject.GetComponent<Image>();
            }

            for (int i = 0; i < fallbackBatteryCells.Length; i++)
            {
                if (fallbackBatteryCells[i] != null)
                    continue;

                var cellObject = GameObject.Find($"BatteryCell{i + 1}");
                if (cellObject != null)
                    fallbackBatteryCells[i] = cellObject.GetComponent<Image>();
            }
        }

        private void SetFallbackFlashlightActive(bool active)
        {
            InitializeFallbackFlashlight();

            bool wasActive = IsFallbackFlashlightLightActive();
            if (TryResolveP2OilLamp())
            {
                p2OilLamp.SetLit(active);
                fallbackFlashlightOn = p2OilLamp.IsLit;
                if (wasActive != fallbackFlashlightOn)
                    FallbackFlashlightChanged?.Invoke(fallbackFlashlightOn);
                return;
            }

            if (fallbackFlashlightObject != null && fallbackFlashlightObject.name != "Spot Light_1")
                fallbackFlashlightObject.SetActive(active);

            fallbackFlashlightOn = fallbackFlashlightObject != null
                ? fallbackFlashlightObject.activeInHierarchy
                : active;

            if (wasActive != fallbackFlashlightOn)
                FallbackFlashlightChanged?.Invoke(fallbackFlashlightOn);
        }

        public bool IsFallbackFlashlightLightActive()
        {
            InitializeFallbackFlashlight();

            if (TryResolveP2OilLamp())
                return p2OilLamp.IsLit;

            return fallbackFlashlightObject != null
                ? fallbackFlashlightObject.activeInHierarchy
                : fallbackFlashlightOn;
        }

        private bool TryResolveP2OilLamp()
        {
            if (p2OilLamp == null || !p2OilLamp.ControlsGameplaySystems)
                p2OilLamp = FindGameplayP2OilLamp();

            return p2OilLamp != null;
        }

        private static MainGame.P2.P2OilLamp FindGameplayP2OilLamp()
        {
            MainGame.P2.P2OilLamp fallback = null;
            foreach (var lamp in FindObjectsByType<MainGame.P2.P2OilLamp>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (lamp == null)
                    continue;

                fallback ??= lamp;
                if (lamp.ControlsGameplaySystems)
                    return lamp;
            }

            return fallback;
        }

        private void SyncFallbackFlashlightStateFromScene()
        {
            if (hasSyncedFallbackFlashlight || fallbackFlashlightObject == null)
                return;

            fallbackFlashlightOn = fallbackFlashlightObject.activeInHierarchy;
            hasSyncedFallbackFlashlight = true;
        }

        private void UpdateFallbackFlashlightUi()
        {
            flashlightBatteryPercent = Mathf.Clamp(flashlightBatteryPercent, 0f, 100f);

            if (fallbackBatteryPercentText != null)
                fallbackBatteryPercentText.text = $"{Mathf.CeilToInt(flashlightBatteryPercent)}%";

            if (fallbackLanternFuelFill != null)
                fallbackLanternFuelFill.fillAmount = flashlightBatteryPercent / 100f;

            int activeCells = Mathf.CeilToInt(flashlightBatteryPercent / 100f * fallbackBatteryCells.Length);
            for (int i = 0; i < fallbackBatteryCells.Length; i++)
            {
                if (fallbackBatteryCells[i] != null)
                    fallbackBatteryCells[i].gameObject.SetActive(i < activeCells);
            }
        }
    }
}
