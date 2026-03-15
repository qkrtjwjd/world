using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [Header("UI")]
    public GameObject inventoryPanel;

    [Header("시작 아이템")]
    public List<ItemData> startingItems = new List<ItemData>();

    [Header("슬롯 부모 오브젝트")]
    public Transform slotGrid;

    // 런타임 인벤토리 (GameState 와 공유)
    public List<ItemData> inventoryItems = new List<ItemData>();

    private List<ItemSlotUI> _slots = new List<ItemSlotUI>();

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // 슬롯 캐싱
        _slots.AddRange(slotGrid.GetComponentsInChildren<ItemSlotUI>());

        // GameState 에 저장된 인벤토리가 없으면 시작 아이템으로 초기화
        if (GameState.inventoryItems == null)
            GameState.inventoryItems = new List<ItemData>(startingItems);

        // 런타임 리스트를 GameState 와 동일한 참조로 연결
        inventoryItems = GameState.inventoryItems;

        UpdateSlotUI();
        Close();
    }

    // ─────────────────────────────────────────────
    //  인벤토리 조작
    // ─────────────────────────────────────────────
    /// <returns>추가 성공 여부</returns>
    public bool AddItem(ItemData item)
    {
        if (item == null) return false;

        if (inventoryItems.Count >= _slots.Count)
        {
            Debug.Log("[InventoryManager] 인벤토리가 가득 찼습니다.");
            return false;
        }

        inventoryItems.Add(item);
        UpdateSlotUI();
        return true;
    }

    public void RemoveItem(ItemData item)
    {
        if (item == null || !inventoryItems.Contains(item)) return;
        inventoryItems.Remove(item);
        UpdateSlotUI();
    }

    public bool HasItem(string itemName)
    {
        foreach (var item in inventoryItems)
            if (item != null && item.itemName == itemName) return true;
        return false;
    }

    // ─────────────────────────────────────────────
    //  UI
    // ─────────────────────────────────────────────
    public void UpdateSlotUI()
    {
        for (int i = 0; i < _slots.Count; i++)
            _slots[i].Setup(i < inventoryItems.Count ? inventoryItems[i] : null);
    }

    public void Open()
    {
        if (inventoryPanel != null) inventoryPanel.SetActive(true);
    }

    public void Close()
    {
        if (inventoryPanel != null) inventoryPanel.SetActive(false);
    }
}