namespace FpsHorrorKit
{
    using System;
    using UnityEngine;

    public class ItemUsageSystem : MonoBehaviour
    {
        public static ItemUsageSystem Instance { get; private set; }
        public static event Action<bool> FlashlightLightChanged;

        [Header("Items")]
        [SerializeField] private Item itemLantern;
        [SerializeField] private Item itemCamera;

        [Header("Item Objects Flaslight")]
        public GameObject lantern;
        public GameObject _light;
        public GameObject _lanternCanvas;
        [SerializeField, Min(1f)] private float forcedFlashlightEnergyLevel = 78f;

        [Header("Item Objects Camera")]
        public GameObject photoCaptureSystem;
        public GameObject cameraFrameUI;
        public GameObject cameraCanvas;
        public bool isAlbumActive = false;

        private FpsAssetsInputs _input;
        private bool isFirstCameraOpen = false;
        private bool cutsceneFlashlightForced;
        private bool savedCutsceneFlashlightState;
        private bool savedLanternActive;
        private bool savedLanternCanvasActive;
        private bool savedLightActive;
        private bool savedHasLantern;
        private bool savedCanUseLantern;
        private bool savedUsingLantern;
        private bool savedLanternEnergyEnough;
        private MainGame.P2.P2OilLamp p2OilLamp;

        public bool IsCutsceneFlashlightForced => cutsceneFlashlightForced;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _input = FindAnyObjectByType<FpsAssetsInputs>();
        }

        private void Start()
        {
            ResolveFlashlightLight();

            itemLantern.canUseItem = false;
            itemLantern.isUsingItem = IsFlashlightLightActive();
            itemLantern.isEnergyEnough = itemLantern.energyLevel > 0f;

            itemCamera.canUseItem = false;
            itemCamera.isUsingItem = false;
            itemCamera.isEnergyEnough = itemCamera.energyLevel > 0f;
        }

        private void Update()
        {
            if (global::GameController.IsGameplayInputLocked())
            {
                _input?.ClearGameplayInput();
                return;
            }

            CheckInputSelect();
            CheckInputUse();
        }

        private void CheckInputSelect()
        {
            if (_input == null)
                return;

            if (isAlbumActive) { return; }

            if (_input.itemIndex == 1 && _input.isPressed)
            {
                SelectFlashlight();
                DiSelectCamera();
                _input.isPressed = false;
                return;
            }

            if (_input.itemIndex == 2 && _input.isPressed)
            {
                SelectCamera();
                DiSelectFlashlight();
                _input.isPressed = false;
                return;
            }

            if (_input.itemIndex == 3 && _input.isPressed)
            {
                DiSelectFlashlight();
                DiSelectCamera();
                _input.isPressed = false;
                return;
            }

            if (_input.itemIndex == 4 && _input.isPressed)
            {
                DiSelectFlashlight();
                DiSelectCamera();
                _input.isPressed = false;
            }
        }

        public void CheckInputUse()
        {
            if (_input == null)
                return;

            if (_input.useFlashlight)
            {
                UseFlashlight();
                _input.useFlashlight = false;
            }

            if (_input.useCamera)
            {
                UseCamera();
                _input.useCamera = false;
            }
        }

        public void SelectFlashlight()
        {
            if (lantern == null) { Debug.LogError("Flashlight Object not found!"); return; }
            if (!EnsureFlashlightLight()) { return; }
            if (_lanternCanvas == null) { Debug.LogError("Flashlight Canvas not found!"); return; }

            if (itemLantern.hasItem)
            {
                itemLantern.canUseItem = _input.isSelectedItem;
                itemLantern.isUsingItem = false;

                lantern.SetActive(_input.isSelectedItem);
                _lanternCanvas.SetActive(_input.isSelectedItem);
                SetFlashlightLightActive(false);
            }
        }

        public void DiSelectFlashlight()
        {
            if (lantern == null) { Debug.LogError("Flashlight Object not found!"); return; }
            if (!EnsureFlashlightLight()) { return; }
            if (_lanternCanvas == null) { Debug.LogError("Flashlight Canvas not found!"); return; }

            if (itemLantern.hasItem)
            {
                itemLantern.canUseItem = false;
                itemLantern.isUsingItem = false;

                lantern.SetActive(false);
                _lanternCanvas.SetActive(false);
                SetFlashlightLightActive(false);
            }
        }

        public void UseFlashlight()
        {
            if (lantern == null) { Debug.LogError("Flashlight Object not found!"); return; }
            if (!EnsureFlashlightLight()) { return; }
            if (_lanternCanvas == null) { Debug.LogError("Flashlight Canvas not found!"); return; }

            if (!itemLantern.hasItem)
                return;

            itemLantern.canUseItem = true;
            lantern.SetActive(true);
            _lanternCanvas.SetActive(true);

            if (!itemLantern.isEnergyEnough)
            {
                itemLantern.isUsingItem = false;
                SetFlashlightLightActive(false);
                InteractMessageScript.Instance?.ShowMessage("Pin đèn đã hết.");
                return;
            }

            bool turnOn = !IsFlashlightLightActive();
            itemLantern.isUsingItem = turnOn;
            SetFlashlightLightActive(turnOn);
            AudioManager.Instance?.PlayFlashlightToggle();
        }

        public void GrantFlashlightItem(bool turnOn = false)
        {
            if (itemLantern == null)
                return;

            itemLantern.hasItem = true;
            if (itemLantern.energyLevel <= 0f)
                itemLantern.energyLevel = forcedFlashlightEnergyLevel;
            itemLantern.isEnergyEnough = itemLantern.energyLevel > 0f;
            itemLantern.canUseItem = true;

            if (turnOn)
                ForceFlashlightOn(true);
        }

        public void ForceFlashlightOn(bool playToggleSound = false)
        {
            if (lantern == null || _lanternCanvas == null || itemLantern == null || !EnsureFlashlightLight())
                return;

            if (!itemLantern.hasItem)
                itemLantern.hasItem = true;
            if (itemLantern.energyLevel <= 0f)
                itemLantern.energyLevel = forcedFlashlightEnergyLevel;

            itemLantern.canUseItem = true;
            itemLantern.isUsingItem = true;
            itemLantern.isEnergyEnough = itemLantern.energyLevel > 0f;

            lantern.SetActive(true);
            _lanternCanvas.SetActive(true);

            if (itemLantern.isEnergyEnough)
            {
                SetFlashlightLightActive(true);
                if (playToggleSound)
                    AudioManager.Instance?.PlayFlashlightToggle();
            }
        }

        public void SetCutsceneFlashlightForced(bool forced)
        {
            if (forced)
            {
                ForceCutsceneFlashlightOn();
                return;
            }

            RestoreCutsceneFlashlight();
        }

        private void ForceCutsceneFlashlightOn()
        {
            if (lantern == null || _lanternCanvas == null || itemLantern == null || !EnsureFlashlightLight())
                return;

            if (!cutsceneFlashlightForced)
            {
                savedCutsceneFlashlightState = true;
                savedLanternActive = lantern.activeSelf;
                savedLanternCanvasActive = _lanternCanvas.activeSelf;
                savedLightActive = IsFlashlightLightActive();
                savedHasLantern = itemLantern.hasItem;
                savedCanUseLantern = itemLantern.canUseItem;
                savedUsingLantern = itemLantern.isUsingItem;
                savedLanternEnergyEnough = itemLantern.isEnergyEnough;
            }

            cutsceneFlashlightForced = true;
            itemLantern.hasItem = true;
            itemLantern.canUseItem = true;
            itemLantern.isUsingItem = true;
            itemLantern.isEnergyEnough = itemLantern.energyLevel > 0f;

            lantern.SetActive(false);
            _lanternCanvas.SetActive(false);
            SetFlashlightLightActive(true);
        }

        private void RestoreCutsceneFlashlight()
        {
            if (!cutsceneFlashlightForced)
                return;

            cutsceneFlashlightForced = false;

            if (lantern != null)
                lantern.SetActive(savedCutsceneFlashlightState && savedLanternActive);
            if (_lanternCanvas != null)
                _lanternCanvas.SetActive(savedCutsceneFlashlightState && savedLanternCanvasActive);
            if (itemLantern != null)
            {
                itemLantern.hasItem = savedCutsceneFlashlightState && savedHasLantern;
                itemLantern.canUseItem = savedCutsceneFlashlightState && savedCanUseLantern;
                itemLantern.isUsingItem = savedCutsceneFlashlightState && savedUsingLantern;
                itemLantern.isEnergyEnough = savedCutsceneFlashlightState && savedLanternEnergyEnough;
            }

            SetFlashlightLightActive(savedCutsceneFlashlightState && savedLightActive);
            savedCutsceneFlashlightState = false;
        }

        public void SetFlashlightLightActive(bool active)
        {
            if (!EnsureFlashlightLight())
                return;

            if (TryResolveP2OilLamp())
            {
                bool wasLit = p2OilLamp.IsLit;
                p2OilLamp.SetLit(active);
                if (wasLit != p2OilLamp.IsLit)
                    FlashlightLightChanged?.Invoke(p2OilLamp.IsLit);
                return;
            }

            bool wasActive = IsFlashlightLightActive();
            _light.SetActive(active);
            bool isActive = _light.activeInHierarchy;

            if (wasActive != isActive)
                FlashlightLightChanged?.Invoke(isActive);
        }

        public bool IsFlashlightLightActive()
        {
            if (TryResolveP2OilLamp())
                return p2OilLamp.IsLit;

            return EnsureFlashlightLight() && _light.activeInHierarchy;
        }

        private bool EnsureFlashlightLight()
        {
            if (TryResolveP2OilLamp())
                return true;

            if (_light == null || _light.name == "Spot Light_1")
                ResolveFlashlightLight();

            if (_light != null)
                return true;

            Debug.LogError("Flashlight Spot Light under FollowTarget not found!");
            return false;
        }

        private void ResolveFlashlightLight()
        {
            var controller = FindFirstObjectByType<FpsController>();
            if (controller != null && controller.followTarget != null)
            {
                if (controller.flashlightLight != null && controller.flashlightLight.name != "Spot Light_1")
                {
                    _light = controller.flashlightLight.gameObject;
                    return;
                }

                var spotLight = controller.followTarget.Find("Spot Light");
                if (spotLight != null)
                {
                    _light = spotLight.gameObject;
                    return;
                }
            }

            var followTarget = GameObject.Find("FollowTarget");
            if (followTarget == null)
                return;

            var directSpotLight = followTarget.transform.Find("Spot Light");
            if (directSpotLight != null)
                _light = directSpotLight.gameObject;
        }

        private bool TryResolveP2OilLamp()
        {
            if (p2OilLamp == null || !p2OilLamp.ControlsGameplaySystems)
                p2OilLamp = FindGameplayP2OilLamp();

            if (p2OilLamp == null)
                return false;

            if (_light == null)
            {
                var controller = FindFirstObjectByType<FpsController>();
                if (controller != null && controller.flashlightLight != null)
                    _light = controller.flashlightLight.gameObject;
            }

            return true;
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

        public void SelectCamera()
        {
            if (photoCaptureSystem == null) { Debug.LogError("Camera Object not found!"); return; }
            if (cameraFrameUI == null) { Debug.LogError("Camera Frame UI not found!"); return; }
            if (cameraCanvas == null) { Debug.LogError("Camera Canvas not found!"); return; }

            if (itemCamera.hasItem)
            {
                itemCamera.canUseItem = _input.isSelectedItem;
                itemCamera.isUsingItem = _input.isSelectedItem;

                photoCaptureSystem.SetActive(_input.isSelectedItem);
                cameraFrameUI.SetActive(_input.isSelectedItem);
                cameraCanvas.SetActive(_input.isSelectedItem);

                if (isFirstCameraOpen == false)
                {
                    InteractMessageScript.Instance?.ShowMessage("Nhấn Tab để mở album!");
                    isFirstCameraOpen = true;
                }
            }
        }

        public void DiSelectCamera()
        {
            if (photoCaptureSystem == null) { Debug.LogError("Camera Object not found!"); return; }
            if (cameraFrameUI == null) { Debug.LogError("Camera Frame UI not found!"); return; }
            if (cameraCanvas == null) { Debug.LogError("Camera Canvas not found!"); return; }

            if (itemCamera.hasItem)
            {
                itemCamera.canUseItem = false;
                itemCamera.isUsingItem = false;

                photoCaptureSystem.SetActive(false);
                cameraFrameUI.SetActive(false);
                cameraCanvas.SetActive(false);
            }
        }

        public void UseCamera()
        {
            if (photoCaptureSystem == null) { Debug.LogError("Camera Object not found!"); return; }
            if (PhotoCaptureSystem.Instance == null) { Debug.LogError("PhotoCaptureSystem script not found!"); return; }

            if (itemCamera.hasItem && itemCamera.canUseItem && itemCamera.isEnergyEnough)
            {
                PhotoCaptureSystem.Instance.CapturePhoto();
                AudioManager.Instance?.PlayCameraShot();
            }
        }
    }
}
