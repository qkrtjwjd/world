using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    // 어디서든 접근할 수 있게 싱글톤 패턴 적용 (선택사항이지만 편의를 위해)
    public static InventoryManager Instance;

    [Header("인벤토리 UI 전체 패널")]
    public GameObject inventoryPanel; // 껐다 켰다 할 패널

    [Header("시작 아이템 목록")]
    public List<ItemData> startingItems = new List<ItemData>();

    // 실제 런타임에 사용할 인벤토리 리스트
    public List<ItemData> inventoryItems = new List<ItemData>();

    [Header("슬롯들이 모여있는 부모 오브젝트 (Grid Layout Group)")]
    public Transform slotGrid; 

    private List<ItemSlotUI> slots = new List<ItemSlotUI>();

    void Awake()
    {
        Instance = this; // 나 자신을 전역 변수에 저장
    }

    void Start()
    {
        // 1. 슬롯들을 다 찾아서 리스트에 저장
        ItemSlotUI[] foundSlots = slotGrid.GetComponentsInChildren<ItemSlotUI>();
        slots.AddRange(foundSlots);

        // 2. 전역 상태(GameState)에서 인벤토리 불러오기
        if (GameState.inventoryItems == null)
        {
            // 게임 처음 시작: 초기 아이템으로 리스트 생성
            GameState.inventoryItems = new List<ItemData>(startingItems);
        }

        // 현재 인벤토리가 전역 리스트를 참조하도록 연결
        inventoryItems = GameState.inventoryItems;

        // 3. UI 업데이트
        UpdateSlotUI();

        // 시작할 땐 꺼두기 (원하면 켜놔도 됨)
        Close(); 
    }

    public void UpdateSlotUI()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (i < inventoryItems.Count)
            {
                slots[i].Setup(inventoryItems[i]);
            }
            else
            {
                slots[i].Setup(null);
            }
        }
    }

    // 아이템 추가 메서드
    public bool AddItem(ItemData item)
    {
        // 슬롯 개수보다 아이템이 적을 때만 추가 가능
        if (inventoryItems.Count < slots.Count)
        {
            inventoryItems.Add(item);
            UpdateSlotUI(); // 화면 갱신
            return true;
        }
        
        Debug.Log("인벤토리가 가득 찼습니다.");
        return false;
    }

    // 아이템 제거 메서드
    public void RemoveItem(ItemData item)
    {
        if (inventoryItems.Contains(item))
        {
            inventoryItems.Remove(item);
            UpdateSlotUI();
        }
    }

    // 아이템 보유 확인 (이름으로)
    public bool HasItem(string itemName)
    {
        foreach (var item in inventoryItems)
        {
            if (item.itemName == itemName) return true;
        }
        return false;
    }

    // 인벤토리 열기
    public void Open()
    {
        inventoryPanel.SetActive(true);
        // 필요하면 열 때마다 아이템 상태 갱신
        // UpdateSlotUI(); 
    }

    // 인벤토리 닫기
    public void Close()
    {
        inventoryPanel.SetActive(false);
    }
}
