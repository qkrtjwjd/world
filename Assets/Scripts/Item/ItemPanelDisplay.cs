using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ItemPanelDisplay : MonoBehaviour
{
    [Header("UI References")]
    public Transform contentParent;
    public GameObject slotPrefab;

    [Header("Inventory Data")]
    public List<ItemData> inventoryContents = new List<ItemData>();

    public void UpdateUI()
    {
        // 1. 기존 생성된 슬롯(자식 오브젝트) 모두 제거
        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }

        // 2. 인벤토리 개수만큼 프리팹 동적 생성
        foreach (ItemData item in inventoryContents)
        {
            GameObject newSlot = Instantiate(slotPrefab, contentParent);

            // 3. 프리팹 내부의 Text와 Image 컴포넌트 찾기
            // 프리팹 구조에 따라 자식 오브젝트의 이름("ItemName", "ItemIcon")은 일치해야 합니다.
            Text nameText = newSlot.transform.Find("ItemName")?.GetComponent<Text>();
            Image iconImage = newSlot.transform.Find("ItemIcon")?.GetComponent<Image>();

            // 4. 아이템 데이터 할당
            if (nameText != null)
            {
                nameText.text = item.itemName;
            }

            if (iconImage != null)
            {
                iconImage.sprite = item.itemIcon;
                iconImage.enabled = item.itemIcon != null;
            }
        }
    }
}
