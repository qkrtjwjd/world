using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour, IInventoryService
{
    public static InventoryManager Instance;

    // ── IInventoryService 구현 ──
    IReadOnlyList<ItemData> IInventoryService.Items => inventoryItems;

    public const int MaxSlots = 48; // 6열 × 8행

    [Header("UI")]
    public GameObject inventoryPanel;
    public TMPro.TMP_Text quantityText;

    [Header("시작 아이템")]
    public List<ItemData> startingItems = new List<ItemData>();

    [Header("슬롯 부모 오브젝트")]
    public Transform slotGrid;

    // 런타임 인벤토리 (GameState 와 공유)
    public List<ItemData> inventoryItems = new List<ItemData>();

    private List<ItemSlotUI> _slots = new List<ItemSlotUI>();
    private ItemCategory _currentCategory = ItemCategory.All;
    public  ItemCategory CurrentCategory => _currentCategory;

    // UpdateSlotUI 재사용 컨테이너 (매 호출 new 방지)
    private readonly List<ItemData> _uiSource   = new List<ItemData>();
    private readonly Dictionary<ItemData, int>  _stackIndex = new Dictionary<ItemData, int>();
    private readonly List<(ItemData item, int count)> _stacks = new List<(ItemData, int)>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    void Awake()
    {
        if (Instance != null && Instance != this) { SingletonGuard.DestroyDuplicate(this); return; }
        Instance = this;
        BattleServices.Register((IInventoryService)this);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
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
    public int FreeSlots => MaxSlots - inventoryItems.Count;

    /// <returns>추가 성공 여부</returns>
    public bool AddItem(ItemData item)
    {
        if (item == null) return false;

        // 씬 전환(DontDestroyOnLoad) 후 슬롯이 비어있으면 재탐색
        if (_slots.Count == 0 && slotGrid == null)
            _slots.AddRange(Object.FindObjectsByType<ItemSlotUI>(FindObjectsInactive.Include));

        if (inventoryItems.Count >= MaxSlots)
        {
            Dbg.Log("[InventoryManager] 인벤토리가 가득 찼습니다.");
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
            if (inventoryItems.Count >= MaxSlots)
            {
                Dbg.Log("[InventoryManager] 인벤토리가 가득 찼습니다.");
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
        // 카테고리 필터링
        _uiSource.Clear();
        foreach (var item in inventoryItems)
        {
            if (item == null) continue;
            if (_currentCategory == ItemCategory.All || item.category == _currentCategory)
                _uiSource.Add(item);
        }

        // 아이템 종류별 갯수 집계 — O(n), 삽입 순서 유지
        _stackIndex.Clear();
        _stacks.Clear();
        foreach (var item in _uiSource)
        {
            if (_stackIndex.TryGetValue(item, out int idx))
                _stacks[idx] = (item, _stacks[idx].count + 1);
            else
            {
                _stackIndex[item] = _stacks.Count;
                _stacks.Add((item, 1));
            }
        }

        for (int i = 0; i < _slots.Count; i++)
        {
            if (i < _stacks.Count)
                _slots[i].Setup(_stacks[i].item, _stacks[i].count);
            else
                _slots[i].Setup(null);
        }

        if (quantityText != null)
            quantityText.text = $"{inventoryItems.Count} / {Mathf.Min(_slots.Count, MaxSlots)}";
    }

    public void FilterByCategory(ItemCategory category)
    {
        _currentCategory = category;
        UpdateSlotUI();
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
        if (!YarnDialogue.IsRunning)
        {
            var ctrl = Object.FindAnyObjectByType<ClearSky.SimplePlayerController>();
            ctrl?.Unlock();
        }
    }
}