namespace FpsHorrorKit
{
    using TMPro;
    using UnityEngine;
    using UnityEngine.UI;

    public class FlashLightUpdater : MonoBehaviour
    {
        [SerializeField] private Item itemFlashLight;
        [SerializeField] private Image flashLightBatteryImage;
        [SerializeField] private TextMeshProUGUI batteryPercentText;
        [SerializeField] private float batteryDecraseSpeed = .25f;


        private void Start()
        {
            ResolveBatteryImage();
            ResolveBatteryPercentText();
            SetBatteryPercent(itemFlashLight != null ? itemFlashLight.energyLevel : 0f);
        }

        public void UpdateBattery(float energyLevel)
        {
            if (itemFlashLight == null)
                return;

            SetBatteryPercent(itemFlashLight.energyLevel + energyLevel);
        }

        public bool TryRecharge(float energyLevel = 100f)
        {
            if (itemFlashLight == null || itemFlashLight.energyLevel >= 100f)
                return false;

            SetBatteryPercent(Mathf.Max(itemFlashLight.energyLevel, energyLevel));
            return true;
        }

        public float GetBatteryPercent()
        {
            return itemFlashLight != null ? Mathf.Clamp(itemFlashLight.energyLevel, 0f, 100f) : 0f;
        }

        private void LateUpdate()
        {
            if (itemFlashLight == null)
                return;

            if (ItemUsageSystem.Instance != null && ItemUsageSystem.Instance.IsCutsceneFlashlightForced)
            {
                SetBatteryPercent(itemFlashLight.energyLevel);
                return;
            }

            if (!IsFlashlightLightOn())
            {
                SetBatteryPercent(itemFlashLight.energyLevel);
                return;
            }

            if (itemFlashLight.energyLevel > 0f)
            {
                SetBatteryPercent(itemFlashLight.energyLevel - Time.deltaTime * batteryDecraseSpeed);
                return;
            }

            itemFlashLight.isUsingItem = false;
            ItemUsageSystem.Instance?.SetFlashlightLightActive(false);
            SetBatteryPercent(0f);
        }

        private bool IsFlashlightLightOn()
        {
            var usageSystem = ItemUsageSystem.Instance;
            return usageSystem != null
                && itemFlashLight.isUsingItem
                && usageSystem.IsFlashlightLightActive();
        }

        private void SetBatteryPercent(float percent)
        {
            if (itemFlashLight == null)
                return;

            itemFlashLight.energyLevel = Mathf.Clamp(percent, 0f, 100f);
            itemFlashLight.isEnergyEnough = itemFlashLight.energyLevel > 0f;

            if (flashLightBatteryImage != null)
                flashLightBatteryImage.fillAmount = itemFlashLight.energyLevel / 100f;

            if (batteryPercentText != null)
                batteryPercentText.text = $"{Mathf.CeilToInt(itemFlashLight.energyLevel)}%";
        }

        private void ResolveBatteryImage()
        {
            if (flashLightBatteryImage != null)
                return;

            var fillObject = GameObject.Find("LanternFuelFill");
            if (fillObject != null)
                flashLightBatteryImage = fillObject.GetComponent<Image>();
        }

        private void ResolveBatteryPercentText()
        {
            if (batteryPercentText != null)
                return;

            var textObject = GameObject.Find("BatteryPercentText");
            if (textObject != null)
                batteryPercentText = textObject.GetComponent<TextMeshProUGUI>();
        }
    }
}
