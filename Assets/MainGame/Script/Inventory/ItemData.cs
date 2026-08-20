using UnityEngine;

namespace FpsHorrorKit
{
    public enum ItemType
    {
        Flashlight,
        Battery,
        Key,
        MusicSheet,
        QuestItem,
        NormalItem
    }

    [CreateAssetMenu(fileName = "ItemData", menuName = "MainGame/Inventory/Item Data")]
    public sealed class ItemData : ScriptableObject
    {
        [Header("Identity")]
        public string id;
        public string itemName;
        [TextArea(3, 8)] public string description;
        public Sprite icon;
        public ItemType itemType;

        [Header("Inventory")]
        public bool canStack = true;
        public int maxStack = 1;
        public bool canUse;

        [Header("Key")]
        public string keyID;

        [Header("Prefabs")]
        public GameObject worldPrefab;
        public GameObject heldPrefab;
    }
}
