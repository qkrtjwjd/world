using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 화면 하단 아이템 퀵슬롯 바.
/// - 인벤토리 아이템을 아이콘으로 표시
/// - 마우스를 가까이 대면 팝업(이름 + 일러스트) 표시
/// - ScrollRect 로 스크롤 가능
/// </summary>
public class ItemQuickSlotUI : MonoBehaviour
{
    [Header("퀵슬롯 바")]
    public Transform     slotContainer;    // 슬롯들이 들어갈 부모 (ScrollRect > Content)
    public GameObject    slotPrefab;       // 슬롯 프리팹 (Button + Image + ItemSlotHover)

    [Header("호버 팝업")]
    public GameObject    popupPanel;       // 흰 배경 팝업 패널
    public Image         popupItemImage;   // 팝업 아이템 일러스트
    public Text          popupItemName;    // 팝업 아이템 이름
    public ScrollRect    popupScrollRect;  // 팝업 내 스크롤
    public float         showDelay = 0.1f; // 팝업 표시 지연

    private List<GameObject> _slots = new List<GameObject>();
    private Coroutine        _showCoroutine;
    private WaitForSecondsRealtime _waitPopup;

    void Start()
    {
        _waitPopup = new WaitForSecondsRealtime(showDelay);
        if (popupPanel != null) popupPanel.SetActive(false);
        Refresh();
    }

    /// <summary>인벤토리 변경 시 슬롯 재생성.</summary>
    public void Refresh()
    {
        // 기존 슬롯 제거
        foreach (var s in _slots) Destroy(s);
        _slots.Clear();

        if (InventoryManager.Instance == null || slotContainer == null || slotPrefab == null)
            return;

        foreach (var item in InventoryManager.Instance.inventoryItems)
        {
            GameObject slot = Instantiate(slotPrefab, slotContainer);
            _slots.Add(slot);

            // 아이콘 이미지
            Image icon = slot.GetComponentInChildren<Image>();
            if (icon != null && item.itemIcon != null)
                icon.sprite = item.itemIcon;

            // 호버 이벤트 연결
            var hover = slot.AddComponent<QuickSlotHover>();
            hover.Init(item, this);
        }
    }

    // ─────────────────────────────────────────────
    //  팝업 show/hide (QuickSlotHover에서 호출)
    // ─────────────────────────────────────────────

    public void ShowPopup(ItemData item)
    {
        if (_showCoroutine != null) StopCoroutine(_showCoroutine);
        _showCoroutine = StartCoroutine(ShowAfterDelay(item));
    }

    public void HidePopup()
    {
        if (_showCoroutine != null) StopCoroutine(_showCoroutine);
        if (popupPanel != null) popupPanel.SetActive(false);
    }

    IEnumerator ShowAfterDelay(ItemData item)
    {
        yield return _waitPopup;

        if (popupItemImage != null) popupItemImage.sprite = item.itemIcon;
        if (popupItemName  != null) popupItemName.text   = item.DisplayName;
        if (popupPanel     != null) popupPanel.SetActive(true);
    }
}

/// <summary>퀵슬롯 개별 슬롯에 붙어서 마우스 호버를 감지합니다.</summary>
public class QuickSlotHover : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler
{
    private ItemData       _item;
    private ItemQuickSlotUI _parent;

    public void Init(ItemData item, ItemQuickSlotUI parent)
    {
        _item   = item;
        _parent = parent;
    }

    public void OnPointerEnter(PointerEventData _) => _parent?.ShowPopup(_item);
    public void OnPointerExit(PointerEventData _)  => _parent?.HidePopup();
}
