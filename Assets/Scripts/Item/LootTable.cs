using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LootTable_", menuName = "Battle/Loot Table")]
public class LootTable : ScriptableObject
{
    [System.Serializable]
    public class LootEntry
    {
        public ItemData item;
        [Range(0f, 1f)] public float dropChance = 0.5f;
        [Min(1)] public int minQuantity = 1;
        [Min(1)] public int maxQuantity = 1;
    }

    public List<LootEntry> entries = new List<LootEntry>();

    /// <summary>각 항목의 dropChance를 굴려 드랍 아이템 목록을 반환합니다.</summary>
    public List<ItemData> RollDrops()
    {
        var result = new List<ItemData>();
        foreach (var entry in entries)
        {
            if (entry == null || entry.item == null) continue;
            if (Random.value <= entry.dropChance)
            {
                int qty = Random.Range(entry.minQuantity, entry.maxQuantity + 1);
                for (int i = 0; i < qty; i++)
                    result.Add(entry.item);
            }
        }
        return result;
    }
}
