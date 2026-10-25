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

    static readonly Color LockedTint = new Color(0.45f, 0.45f, 0.45f, 0.55f);

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
            // 섭취 제한에 걸린 마시멜로는 잠금 상태로 보인다(C-17-3). 필터 고정(C-17-2)과 달리 표시를 둔다.
            iconImage.color = MarshmallowRules.IsLockedForUse(_item) ? LockedTint : Color.white;
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
