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
        if (slotGrid != null)
            _slots.AddRange(slotGrid.GetComponentsInChildren<ItemSlotUI>());
        else
        {
            _slots.AddRange(Object.FindObjectsByType<ItemSlotUI>());
            if (_slots.Count == 0)
                Debug.LogWarning("[InventoryManager] slotGrid가 연결되지 않았고 ItemSlotUI를 찾을 수 없습니다. 인스펙터에서 slotGrid를 연결해주세요.");
        }

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
    public int FreeSlots => _slots.Count - inventoryItems.Count;

    /// <returns>추가 성공 여부</returns>
    public bool AddItem(ItemData item)
    {
        if (item == null) return false;

        // 씬 전환(DontDestroyOnLoad) 후 슬롯이 비어있으면 재탐색
        if (_slots.Count == 0 && slotGrid == null)
            _slots.AddRange(Object.FindObjectsByType<ItemSlotUI>(FindObjectsInactive.Include));

        if (inventoryItems.Count >= _slots.Count)
        {
            Debug.Log("[InventoryManager] 인벤토리가 가득 찼습니다.");
            ShowInventoryFullNotice();
            return false;
        }

        inventoryItems.Add(item);
        UpdateSlotUI();
        ItemAcquisitionUI.Instance?.ShowNotification(item);
        return true;
    }

    /// <summary>여러 아이템을 한 번에 추가. UI는 마지막에 한 번만 갱신.</summary>
    /// <returns>실제로 추가된 아이템 수</returns>
    public int AddItems(List<ItemData> items)
    {
        if (_slots.Count == 0 && slotGrid == null)
            _slots.AddRange(Object.FindObjectsByType<ItemSlotUI>(FindObjectsInactive.Include));

        int added = 0;
        var addedItems = new List<ItemData>();
        foreach (var item in items)
        {
            if (item == null) continue;
            if (inventoryItems.Count >= _slots.Count)
            {
                Debug.Log("[InventoryManager] 인벤토리가 가득 찼습니다.");
                ShowInventoryFullNotice();
                break;
            }
            inventoryItems.Add(item);
            addedItems.Add(item);
            added++;
        }
        if (added > 0)
        {
            UpdateSlotUI();
            ItemAcquisitionUI.Instance?.ShowNotifications(addedItems);
        }
        return added;
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
        // 아이템 종류별 갯수 집계
        var stacks = new List<(ItemData item, int count)>();
        foreach (var item in inventoryItems)
        {
            bool found = false;
            for (int i = 0; i < stacks.Count; i++)
            {
                if (stacks[i].item == item)
                {
                    stacks[i] = (item, stacks[i].count + 1);
                    found = true;
                    break;
                }
            }
            if (!found) stacks.Add((item, 1));
        }

        for (int i = 0; i < _slots.Count; i++)
        {
            if (i < stacks.Count)
                _slots[i].Setup(stacks[i].item, stacks[i].count);
            else
                _slots[i].Setup(null);
        }
    }

    void ShowInventoryFullNotice()
    {
        ItemNotificationUI.Instance?.Show("아이템 창이 가득 찼습니다.");
    }

    public void Open()
    {
        if (inventoryPanel != null) inventoryPanel.SetActive(true);
    }

    public void Close()
    {
        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        ItemDetailUI.Instance?.Hide();

        // 인벤토리 닫을 때 대화 중이 아니면 플레이어 이동 복구
        if (DialogueManager.Instance == null || !DialogueManager.Instance.isTalking)
        {
            var ctrl = Object.FindAnyObjectByType<ClearSky.SimplePlayerController>();
            ctrl?.Unlock();
        }
    }
}