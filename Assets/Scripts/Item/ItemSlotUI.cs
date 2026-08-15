using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class ItemSlotUI : MonoBehaviour, ISubmitHandler
{
    [Header("아이콘 이미지")]
    public Image iconImage;

    [Header("갯수 텍스트 (선택)")]
    public TMP_Text countText;

    private ItemData _item;

    public void Setup(ItemData newItem, int count = 1)
    {
        _item = newItem;

        if (_item == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (iconImage != null)
        {
            iconImage.sprite  = _item.CurrentIcon;
            iconImage.enabled = _item.CurrentIcon != null;
        }

        if (countText != null)
        {
            bool showCount = count > 1;
            countText.gameObject.SetActive(showCount);
            if (showCount) countText.text = $"x{count}";
        }
    }

    /// <summary>슬롯 클릭 시 상세 패널에 아이템 정보를 표시합니다.</summary>
    public void OnClick()
    {
        if (_item == null) return;
        ItemDetailUI.Instance?.Show(_item);
    }

    /// <summary>키보드 포커스 상태에서 Enter 키를 누를 때 호출됩니다.</summary>
    public void OnSubmit(BaseEventData eventData)
    {
        OnClick();
    }
}
