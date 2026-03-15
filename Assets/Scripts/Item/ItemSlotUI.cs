using UnityEngine;
using UnityEngine.UI;

public class ItemSlotUI : MonoBehaviour
{
    [Header("아이콘 이미지")]
    public Image iconImage;

    private ItemData _item;

    public void Setup(ItemData newItem)
    {
        _item = newItem;

        if (iconImage == null) return;

        if (_item != null && _item.itemIcon != null)
        {
            iconImage.sprite  = _item.itemIcon;
            iconImage.enabled = true;
        }
        else
        {
            iconImage.enabled = false;
        }
    }

    /// <summary>인벤토리 슬롯 클릭 시 MysteriousObject 에게 아이템을 먹입니다.</summary>
    public void OnClick()
    {
        if (_item == null) return;

        MysteriousObject feeder = Object.FindFirstObjectByType<MysteriousObject>();
        if (feeder == null)
        {
            Debug.LogWarning("[ItemSlotUI] 씬에 MysteriousObject 가 없습니다.");
            return;
        }

        feeder.EatItem(_item);
        InventoryManager.Instance?.Close();
    }
}