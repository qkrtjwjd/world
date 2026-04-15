using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상인 물물교환 UI.
/// MerchantInteraction 컴포넌트에서 Open() 을 호출합니다.
///
/// [Unity 에디터 세팅]
/// 1. Canvas 하위에 Panel 생성 → MerchantUI 컴포넌트 추가
/// 2. panel              : 루트 패널 GameObject
/// 3. merchantNameText   : 상인 이름 Text
/// 4. merchantPortrait   : 상인 초상화 Image
/// 5. dealListContainer  : 거래 목록 버튼들의 부모 Transform (VerticalLayoutGroup 권장)
/// 6. dealButtonPrefab   : 거래 항목 버튼 프리팹 (Button + Text 포함)
/// 7. playerOfferContainer  : 플레이어 제공 아이템 아이콘 부모
/// 8. merchantOfferContainer: 상인 제공 아이템 아이콘 부모
/// 9. offerIconPrefab    : 아이템 아이콘 표시용 프리팹 (Image + Text 포함)
/// 10. confirmButton     : "거래 제안" 버튼
/// 11. cancelButton      : "닫기" 버튼
/// </summary>
public class MerchantUI : MonoBehaviour
{
    public static MerchantUI Instance { get; private set; }

    [Header("UI 연결")]
    public GameObject panel;
    public Text       merchantNameText;
    public Image      merchantPortrait;

    [Header("거래 목록")]
    public Transform  dealListContainer;
    public GameObject dealButtonPrefab;

    [Header("아이템 표시 패널")]
    public Transform  playerOfferContainer;
    public Transform  merchantOfferContainer;
    public GameObject offerIconPrefab; // Image(아이콘) + Text(수량) 포함 프리팹

    [Header("버튼")]
    public Button confirmButton;
    public Button cancelButton;

    [Header("텍스트")]
    public string confirmLabel  = "거래 제안";
    public string cancelLabel   = "닫기";
    public string noDealsLabel  = "거래 가능한 물건이 없습니다.";
    public string notEnoughItemsLabel = "보유한 아이템이 부족합니다.";

    private MerchantData   _currentMerchant;
    private BarterDeal     _selectedDeal;
    private readonly List<GameObject> _dealButtons  = new List<GameObject>();
    private readonly List<GameObject> _offerIcons   = new List<GameObject>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);
        if (panel != null) panel.SetActive(false);

        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnConfirm);
            var txt = confirmButton.GetComponentInChildren<Text>();
            if (txt != null) txt.text = confirmLabel;
        }
        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(Close);
            var txt = cancelButton.GetComponentInChildren<Text>();
            if (txt != null) txt.text = cancelLabel;
        }
    }

    // ─────────────────────────────────────────────
    //  열기 / 닫기
    // ─────────────────────────────────────────────

    public void Open(MerchantData data)
    {
        if (data == null) return;
        _currentMerchant = data;
        _selectedDeal    = null;

        if (merchantNameText != null) merchantNameText.text = data.merchantName;
        if (merchantPortrait != null)
        {
            merchantPortrait.sprite  = data.portrait;
            merchantPortrait.enabled = data.portrait != null;
        }

        BuildDealList();
        ClearOfferPanels();

        if (confirmButton != null) confirmButton.interactable = false;
        if (panel != null) panel.SetActive(true);
    }

    public void Close()
    {
        ClearDealList();
        ClearOfferPanels();
        _selectedDeal    = null;
        _currentMerchant = null;
        if (panel != null) panel.SetActive(false);

        // 인벤토리 닫을 때와 동일하게 플레이어 이동 복구
        if (DialogueManager.Instance == null || !DialogueManager.Instance.isTalking)
        {
            var ctrl = Object.FindAnyObjectByType<ClearSky.SimplePlayerController>();
            ctrl?.Unlock();
        }
    }

    // ─────────────────────────────────────────────
    //  거래 목록 구성
    // ─────────────────────────────────────────────

    void BuildDealList()
    {
        ClearDealList();
        if (_currentMerchant == null || dealListContainer == null) return;
        if (dealButtonPrefab == null)
        {
            Debug.LogError("[MerchantUI] dealButtonPrefab 이 연결되지 않았습니다. 인스펙터에서 설정해주세요.");
            return;
        }

        bool anyDeal = false;
        foreach (var deal in _currentMerchant.deals)
        {
            if (deal.isCompleted && deal.oneTimeOnly) continue;
            anyDeal = true;

            var go  = Instantiate(dealButtonPrefab, dealListContainer);
            _dealButtons.Add(go);

            var txt = go.GetComponentInChildren<Text>();
            if (txt != null) txt.text = string.IsNullOrEmpty(deal.dealName) ? "(거래)" : deal.dealName;

            var btn = go.GetComponent<Button>();
            var capturedDeal = deal;
            if (btn != null)
                btn.onClick.AddListener(() => SelectDeal(capturedDeal));
        }

        if (!anyDeal)
        {
            var go = Instantiate(dealButtonPrefab, dealListContainer);
            _dealButtons.Add(go);
            var txt = go.GetComponentInChildren<Text>();
            if (txt != null) txt.text = noDealsLabel;
            var btn = go.GetComponent<Button>();
            if (btn != null) btn.interactable = false;
        }
    }

    void ClearDealList()
    {
        foreach (var go in _dealButtons) if (go != null) Destroy(go);
        _dealButtons.Clear();
    }

    // ─────────────────────────────────────────────
    //  거래 선택
    // ─────────────────────────────────────────────

    void SelectDeal(BarterDeal deal)
    {
        _selectedDeal = deal;
        ShowOfferPanels(deal);
        if (confirmButton != null) confirmButton.interactable = true;
    }

    void ShowOfferPanels(BarterDeal deal)
    {
        ClearOfferPanels();
        if (offerIconPrefab == null) return;

        BuildOfferIcons(deal.playerOffer,   playerOfferContainer);
        BuildOfferIcons(deal.merchantOffer, merchantOfferContainer);
    }

    void BuildOfferIcons(List<BarterItemEntry> entries, Transform container)
    {
        if (container == null) return;
        foreach (var entry in entries)
        {
            if (entry.item == null) continue;
            var go  = Instantiate(offerIconPrefab, container);
            _offerIcons.Add(go);

            var img = go.GetComponentInChildren<Image>();
            if (img != null)
            {
                img.sprite  = entry.item.CurrentIcon;
                img.enabled = entry.item.CurrentIcon != null;
            }

            var txt = go.GetComponentInChildren<Text>();
            if (txt != null)
                txt.text = entry.quantity > 1 ? $"x{entry.quantity}" : "";
        }
    }

    void ClearOfferPanels()
    {
        foreach (var go in _offerIcons) if (go != null) Destroy(go);
        _offerIcons.Clear();
    }

    // ─────────────────────────────────────────────
    //  거래 실행
    // ─────────────────────────────────────────────

    void OnConfirm()
    {
        if (_selectedDeal == null) return;
        StartCoroutine(ProcessBarter(_selectedDeal));
    }

    IEnumerator ProcessBarter(BarterDeal deal)
    {
        if (confirmButton != null) confirmButton.interactable = false;

        // 플레이어 보유 아이템 확인
        if (!HasRequiredItems(deal.playerOffer))
        {
            ItemNotificationUI.Instance?.Show(notEnoughItemsLabel);
            if (confirmButton != null) confirmButton.interactable = true;
            yield break;
        }

        // UI 닫기 (대사 재생을 위해)
        if (panel != null) panel.SetActive(false);

        // 제안 대사
        if (deal.proposeDialogue != null)
            yield return DialogueRunner.PlayAndWait(deal.proposeDialogue);

        if (deal.merchantAccepts)
        {
            // 수락 대사
            if (deal.acceptDialogue != null)
                yield return DialogueRunner.PlayAndWait(deal.acceptDialogue);

            // 아이템 교환
            ExecuteTrade(deal);

            if (deal.oneTimeOnly) deal.isCompleted = true;
        }
        else
        {
            // 거절 대사
            if (deal.rejectDialogue != null)
                yield return DialogueRunner.PlayAndWait(deal.rejectDialogue);
        }

        // 거래창 다시 열기 (oneTimeOnly 거래 완료 시 목록 갱신)
        Open(_currentMerchant);
    }

    bool HasRequiredItems(List<BarterItemEntry> required)
    {
        if (InventoryManager.Instance == null) return false;
        var inv = InventoryManager.Instance.inventoryItems;

        // 종류별 보유 수량 계산
        var owned = new Dictionary<ItemData, int>();
        foreach (var item in inv)
        {
            if (item == null) continue;
            if (!owned.ContainsKey(item)) owned[item] = 0;
            owned[item]++;
        }

        foreach (var entry in required)
        {
            if (entry.item == null) continue;
            int have = owned.ContainsKey(entry.item) ? owned[entry.item] : 0;
            if (have < entry.quantity) return false;
        }
        return true;
    }

    void ExecuteTrade(BarterDeal deal)
    {
        if (InventoryManager.Instance == null) return;

        // 플레이어 아이템 제거
        foreach (var entry in deal.playerOffer)
        {
            if (entry.item == null) continue;
            for (int i = 0; i < entry.quantity; i++)
                InventoryManager.Instance.RemoveItem(entry.item);
        }

        // 상인 아이템 추가
        var toAdd = new List<ItemData>();
        foreach (var entry in deal.merchantOffer)
        {
            if (entry.item == null) continue;
            for (int i = 0; i < entry.quantity; i++)
                toAdd.Add(entry.item);
        }
        if (toAdd.Count > 0)
            InventoryManager.Instance.AddItems(toAdd);
    }
}
