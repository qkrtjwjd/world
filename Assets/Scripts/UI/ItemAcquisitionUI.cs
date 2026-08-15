using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 아이템 획득 시 화면에 잠깐 표시되는 알림 UI.
/// - 단일 아이템: InventoryManager.AddItem() 에서 자동 호출
/// - 복수 아이템: InventoryManager.AddItems() 에서 한꺼번에 호출
/// iconSlots 에 Image 를 왼쪽부터 순서대로 연결해두면,
/// 획득한 아이템 개수만큼만 왼쪽부터 활성화되고 나머지는 비활성화된다.
/// </summary>
public class ItemAcquisitionUI : MonoBehaviour
{
    public static ItemAcquisitionUI Instance { get; private set; }

    [Header("UI 연결")]
    public GameObject notificationPanel;
    public TMP_Text   messageText;

    [Header("아이콘 슬롯 (왼쪽부터 순서대로 인스펙터에 연결)")]
    public Image[]    iconSlots;
    public TMP_Text[] countTexts;

    [Header("표시 시간 (초)")]
    public float displayDuration = 2f;

    private Coroutine _hideCoroutine;
    private string _pendingYarnNode;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);
        if (notificationPanel != null) notificationPanel.SetActive(false);
        HideAllSlots();
    }

    /// <summary>단일 아이템 획득 알림.</summary>
    public void ShowNotification(ItemData item)
    {
        if (item == null) return;
        ShowNotifications(new List<ItemData> { item });
    }

    /// <summary>복수 아이템 획득 알림 — 아이콘을 왼쪽부터, 이름을 한 줄에 표시.</summary>
    public void ShowNotifications(List<ItemData> items)
    {
        if (items == null || items.Count == 0 || notificationPanel == null) return;

        // 종류별 수량 집계
        var counts = new Dictionary<ItemData, int>();
        foreach (var item in items)
        {
            if (item == null) continue;
            if (!counts.ContainsKey(item)) counts[item] = 0;
            counts[item]++;
        }
        var uniqueItems = new List<ItemData>(counts.Keys);

        // 아이콘 + 수량 슬롯: 종류별로 왼쪽부터 하나씩 활성화
        if (iconSlots != null)
        {
            for (int i = 0; i < iconSlots.Length; i++)
            {
                if (iconSlots[i] == null) continue;
                if (i < uniqueItems.Count)
                {
                    var item = uniqueItems[i];
                    int qty  = counts[item];

                    iconSlots[i].gameObject.SetActive(true);
                    iconSlots[i].sprite  = item.CurrentIcon;
                    iconSlots[i].enabled = item.CurrentIcon != null;

                    // 수량 텍스트: 2개 이상이면 "x수량", 1개이면 숨김
                    if (countTexts != null && i < countTexts.Length && countTexts[i] != null)
                    {
                        countTexts[i].gameObject.SetActive(qty > 1);
                        countTexts[i].text = $"x{qty}";
                    }
                }
                else
                {
                    iconSlots[i].gameObject.SetActive(false);
                    if (countTexts != null && i < countTexts.Length && countTexts[i] != null)
                        countTexts[i].gameObject.SetActive(false);
                }
            }
        }

        // 텍스트: "핫초코 x3, 사과을(를) 획득했습니다"
        if (messageText != null)
        {
            var names = new System.Text.StringBuilder();
            foreach (var kv in counts)
            {
                if (names.Length > 0) names.Append(", ");
                names.Append(kv.Value > 1
                    ? $"{kv.Key.DisplayName} x{kv.Value}"
                    : kv.Key.DisplayName);
            }
            messageText.text = $"{names}을(를) 획득했습니다";
        }

        notificationPanel.SetActive(true);

        if (_hideCoroutine != null) StopCoroutine(_hideCoroutine);
        _hideCoroutine = StartCoroutine(AutoHide());
    }

    void HideAllSlots()
    {
        if (iconSlots != null)
            foreach (var slot in iconSlots)
                if (slot != null) slot.gameObject.SetActive(false);

        if (countTexts != null)
            foreach (var t in countTexts)
                if (t != null) t.gameObject.SetActive(false);
    }

    public bool IsShowing => notificationPanel != null && notificationPanel.activeSelf;

    public void SetPendingYarnNode(string nodeName) => _pendingYarnNode = nodeName;

    IEnumerator AutoHide()
    {
        yield return new WaitForSeconds(displayDuration);
        if (notificationPanel != null) notificationPanel.SetActive(false);
        HideAllSlots();
        _hideCoroutine = null;

        if (!string.IsNullOrEmpty(_pendingYarnNode))
        {
            var node = _pendingYarnNode;
            _pendingYarnNode = null;
            StartCoroutine(YarnDialogue.PlayAndWait(node));
        }
    }
}
