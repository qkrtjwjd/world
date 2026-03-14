using UnityEngine;
using UnityEngine.UI; // 이미지 컴포넌트 제어용

public class ItemSlotUI : MonoBehaviour
{
    [Header("슬롯 설정")]
    public Image iconImage;     // 아이템 아이콘을 표시할 UI 이미지
    private ItemData _itemData; // 현재 이 슬롯에 담긴 아이템 데이터

    // 외부(인벤토리 매니저 등)에서 이 슬롯에 아이템을 세팅해주는 함수
    public void Setup(ItemData newItem)
    {
        _itemData = newItem;

        if (_itemData != null)
        {
            iconImage.sprite = _itemData.itemIcon; // 아이콘 변경
            iconImage.enabled = true; // 보이게 켜기
        }
        else
        {
            iconImage.enabled = false; // 데이터 없으면 안 보이게
        }
    }

    // 이 슬롯 버튼을 클릭했을 때 실행
    public void OnClick()
    {
        if (_itemData == null) return; // 아이템 없으면 무시

        // 씬에서 MysteriousObject 찾아서 먹이기 (최신 유니티 권장 방식 사용)
        MysteriousObject feeder = Object.FindFirstObjectByType<MysteriousObject>();
        
        if (feeder != null)
        {
            // 1. 아이템 먹이기
            feeder.EatItem(_itemData);
            
            // 2. 인벤토리 닫기 (자동 닫기 기능)
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.Close();
            }
        }
        else
        {
            Debug.LogWarning("씬에 MysteriousObject가 없습니다!");
        }
    }
}