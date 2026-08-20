using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FpsHorrorKit
{
    public sealed class InventorySlotUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image frame;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI amountText;
        [SerializeField] private Button button;
        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Sprite selectedSprite;

        private ItemData itemData;
        private InventoryUI owner;

        public void Setup(InventoryUI ownerUI, Sprite normal, Sprite selected)
        {
            owner = ownerUI;
            normalSprite = normal;
            selectedSprite = selected;
            button = GetComponent<Button>();
            frame = GetComponent<Image>();
            icon = transform.Find("Icon")?.GetComponent<Image>();
            itemNameText = transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
            amountText = transform.Find("Amount")?.GetComponent<TextMeshProUGUI>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => owner?.SelectInventoryItem(itemData));
            }
        }

        public void SetItem(InventoryItem item, bool selected)
        {
            itemData = item?.Data;
            bool hasItem = itemData != null;
            if (icon != null)
            {
                icon.enabled = hasItem && itemData.icon != null;
                icon.sprite = hasItem ? itemData.icon : null;
            }
            if (itemNameText != null) itemNameText.text = hasItem ? itemData.itemName : "";
            if (amountText != null) amountText.text = hasItem && item.Amount > 1 ? $"x{item.Amount}" : "";
            SetSelected(selected);
        }

        public void SetSelected(bool selected)
        {
            if (frame != null)
                frame.sprite = selected && selectedSprite != null ? selectedSprite : normalSprite;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (itemData == null || owner == null)
                return;

            owner.SelectInventoryItem(itemData);
            if (eventData.button == PointerEventData.InputButton.Right || eventData.clickCount >= 2)
                owner.UseInventoryItem(itemData);
        }
    }
}
