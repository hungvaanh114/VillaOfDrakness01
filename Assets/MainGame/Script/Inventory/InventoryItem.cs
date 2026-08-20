using System;

namespace FpsHorrorKit
{
    [Serializable]
    public sealed class InventoryItem
    {
        public ItemData Data { get; private set; }
        public int Amount { get; private set; }

        public InventoryItem(ItemData data, int amount)
        {
            Data = data;
            Amount = amount;
        }

        public void Add(int amount)
        {
            Amount += amount;
        }

        public void Remove(int amount)
        {
            Amount -= amount;
            if (Amount < 0) Amount = 0;
        }
    }
}
