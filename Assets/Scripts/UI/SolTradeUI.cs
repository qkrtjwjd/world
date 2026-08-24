using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 솔 거래창. SolTradeInteraction 또는 &lt;&lt;open_sol_trade&gt;&gt; 에서 Open() 을 호출합니다.
///
/// [상태 흐름]
///   Closed → Choice(대화 / 거래) → Talk 또는 Trade → Choice 복귀
///   - 대화: Choice 를 닫고 Yarn 대화 노드 실행, 종료 시 Choice 복귀
///   - 거래: 품목 목록 → 품목 선택 시 설명 + 루의 소지품을 같은 화면에
///   - 이전 키(backKey): Choice 복귀 / 취소 키(설정의 일시정지 키): 전체 종료
///
/// [금지 — 정본 기준]
///   가격·수치·등급을 표시하지 않는다. 잔액·통화·되판매 감가 개념이 없다.
///   등급 표시용 아이콘·텍스트를 만들지 않는다.
///
/// [Unity 에디터 세팅]
///   1. Canvas 하위 Panel 에 이 컴포넌트 추가
///   2. panel / choiceGroup / tradeGroup / itemFocusGroup : 각 단계의 루트 GameObject
///   3. talkButton, tradeButton : Choice 단계 버튼
///   4. slotContainer + openSlotPrefab + coveredSlotPrefab : 품목 목록
///   5. focusIcon / focusNameText / focusDescriptionText : 품목 상세
///   6. offerContainer + offerSlotPrefab : 루가 내밀 수 있는 소지품
///   7. sfx* : AudioManager 에 이미 등록된 이름만 넣는다. 비워두면 무음.
/// </summary>
public class SolTradeUI : MonoBehaviour
{
    public enum State { Closed, Choice, Talk, Trade, ItemFocus }

    public static SolTradeUI Instance { get; private set; }

    /// <summary>
    /// 거래창이 화면에 떠 있는지. PauseSystem 이 취소 키를 가로채지 않도록,
    /// YarnCommandBridge 가 패널 위 입력을 대사 진행으로 넘기지 않도록 사용한다.
    ///
    /// ⚠ 반드시 '패널이 실제로 보이는지' 기준이어야 한다.
    ///   대사 재생 중에는 패널을 숨기는데, 이때 true 로 남으면
    ///   YarnCommandBridge.Update 가 대사 진행 입력을 계속 차단해 대사가 넘어가지 않는다.
    ///   (Dialogue.prefab 의 LinePresenter 는 autoAdvance=0 이고 continue 버튼도 없어
    ///    Space/클릭 말고는 대사를 넘길 수단이 없다.)
    /// </summary>
    public static bool IsOpen => Instance != null
                                 && Instance._state != State.Closed
                                 && Instance.panel != null
                                 && Instance.panel.activeSelf;

    [Header("루트")]
    public GameObject panel;

    [Header("Choice — 대화 / 거래")]
    public GameObject choiceGroup;
    public Button     talkButton;
    public Button     tradeButton;

    [Header("Trade — 품목 목록")]
    public GameObject tradeGroup;
    public Transform  slotContainer;
    [Tooltip("Button + Image(아이콘) + TMP_Text(이름) 를 포함한 프리팹")]
    public GameObject openSlotPrefab;
    [Tooltip("천으로 덮인 칸 프리팹. 클릭되지 않는다.")]
    public GameObject coveredSlotPrefab;

    [Header("ItemFocus — 품목 상세 + 루의 소지품")]
    public GameObject itemFocusGroup;
    public Image      focusIcon;
    public TMP_Text   focusNameText;
    public TMP_Text   focusDescriptionText;
    public Transform  offerContainer;
    [Tooltip("Button + Image(아이콘) + TMP_Text(개수) 를 포함한 프리팹")]
    public GameObject offerSlotPrefab;

    [Header("마을 모드에서 이름·설명 자리에 넣을 표기")]
    [Tooltip("비워두면 아무 것도 표시되지 않는다. 회색 비활성 처리는 하지 않는다.")]
    public string hiddenNameLabel        = "";
    [TextArea(1, 3)]
    public string hiddenDescriptionLabel = "";

    [Header("입력")]
    [Tooltip("이전 단계(Choice)로 돌아가는 키. 취소 키는 설정의 일시정지 키를 따른다.")]
    public KeyCode backKey = KeyCode.Q;

    [Header("SFX — AudioManager 에 등록된 이름만. 비워두면 무음")]
    public string sfxOpen;
    public string sfxSelect;
    public string sfxBack;
    public string sfxTradeSuccess;
    public string sfxTradeReject;

    // ── 런타임 상태 ──
    private State     _state = State.Closed;
    private SolStock  _stock;
    private TradeMode _mode;
    private TradeItem _selectedWant;
    private bool      _lockedPlayer;

    // 창이 닫히거나 다시 열릴 때마다 증가한다.
    // 대사 코루틴은 시작 시점의 값을 들고 있다가 yield 후 달라졌으면 스스로 빠져나온다.
    // (코루틴이 이 컴포넌트가 아니라 YarnDialogue 의 상주 호스트에서 돌기 때문에
    //  StopAllCoroutines 로는 멈출 수 없다.)
    private int _session;

    private readonly List<GameObject> _slotObjects  = new List<GameObject>();
    private readonly List<GameObject> _offerObjects = new List<GameObject>();
    private readonly Dictionary<TradeItem, int> _offerCounts = new Dictionary<TradeItem, int>();
    private readonly Dictionary<ItemData, int>  _itemCountCache = new Dictionary<ItemData, int>();

    public State CurrentState => _state;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);

        if (talkButton  != null) talkButton.onClick.AddListener(OnTalkSelected);
        if (tradeButton != null) tradeButton.onClick.AddListener(OnTradeSelected);

        SetState(State.Closed);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ─────────────────────────────────────────────
    //  열기 / 닫기
    // ─────────────────────────────────────────────

    public void Open(SolStock stock, TradeMode mode)
    {
        if (stock == null)
        {
            Debug.LogWarning("[SolTradeUI] Open: SolStock 이 비어 있습니다.");
            return;
        }

        _session++;   // 이전 세션에 남은 대사 코루틴을 무효화한다

        _stock        = stock;
        _mode         = mode;
        _selectedWant = null;

        if (!_lockedPlayer)
        {
            PlayerInputLock.Instance?.Lock();
            _lockedPlayer = true;
        }

        PlaySfx(sfxOpen);
        EnterChoice();
    }

    /// <summary>취소 키 / 정상 종료.</summary>
    public void Close()
    {
        _session++;   // 진행 중인 대사 코루틴을 무효화한다

        ClearSlots();
        ClearOffers();

        _stock        = null;
        _selectedWant = null;
        SetState(State.Closed);

        if (_lockedPlayer)
        {
            PlayerInputLock.Instance?.Unlock();
            _lockedPlayer = false;
        }
    }

    /// <summary>
    /// 세라 접근 시 강제 종료. 확인창 없이 즉시 닫히고 선택 상태는 저장되지 않는다.
    /// SeraApproachTrigger.onSeraApproach 에 연결한다.
    /// </summary>
    public void ForceClose()
    {
        if (_state == State.Closed) return;
        Close();   // _session 증가로 진행 중인 대사 코루틴이 스스로 빠져나온다
    }

    // ─────────────────────────────────────────────
    //  상태 전이
    // ─────────────────────────────────────────────

    void SetState(State next)
    {
        _state = next;

        if (panel          != null) panel.SetActive(next != State.Closed);
        if (choiceGroup    != null) choiceGroup.SetActive(next == State.Choice);
        if (tradeGroup     != null) tradeGroup.SetActive(next == State.Trade);
        if (itemFocusGroup != null) itemFocusGroup.SetActive(next == State.ItemFocus);
    }

    void EnterChoice()
    {
        ClearSlots();
        ClearOffers();
        _selectedWant = null;
        SetState(State.Choice);
    }

    void OnTalkSelected()
    {
        if (_state != State.Choice) return;
        PlaySfx(sfxSelect);
        // 패널을 끄면 이 컴포넌트도 함께 꺼질 수 있으므로 상주 호스트에서 돌린다
        YarnDialogue.StartCoroutine(RunTalk(_session));
    }

    IEnumerator RunTalk(int session)
    {
        SetState(State.Talk);
        if (panel != null) panel.SetActive(false);

        // 이미 이 창이 입력을 잠그고 있으므로 대사에서 다시 잠그지 않는다
        yield return YarnDialogue.PlayIfExists(_stock != null ? _stock.talkNode : null, false);

        if (session != _session) yield break;   // 대사 도중 닫혔다면 여기서 끝
        EnterChoice();
    }

    void OnTradeSelected()
    {
        if (_state != State.Choice) return;
        PlaySfx(sfxSelect);
        EnterTrade();
    }

    void EnterTrade()
    {
        SetState(State.Trade);
        BuildSlotList();
    }

    void EnterItemFocus(TradeItem want)
    {
        _selectedWant = want;
        SetState(State.ItemFocus);

        bool reveal = _mode == TradeMode.ForestTrade;

        if (focusIcon != null)
        {
            focusIcon.sprite  = want.icon;
            focusIcon.enabled = want.icon != null;
        }
        if (focusNameText != null)
            focusNameText.text = reveal ? want.displayName : hiddenNameLabel;
        if (focusDescriptionText != null)
            focusDescriptionText.text = reveal ? want.description : hiddenDescriptionLabel;

        BuildOfferList();
    }

    // ─────────────────────────────────────────────
    //  품목 목록
    // ─────────────────────────────────────────────

    void BuildSlotList()
    {
        ClearSlots();
        if (slotContainer == null || _stock == null) return;

        if (openSlotPrefab == null)
        {
            Debug.LogError("[SolTradeUI] openSlotPrefab 이 연결되지 않았습니다. 인스펙터에서 설정해주세요.");
            return;
        }

        bool reveal = _mode == TradeMode.ForestTrade;

        foreach (var want in _stock.openSlots)
        {
            if (want == null) continue;

            var go = Instantiate(openSlotPrefab, slotContainer);
            _slotObjects.Add(go);

            SetIcon(go, want.icon);

            var txt = go.GetComponentInChildren<TMP_Text>();
            if (txt != null)
                txt.text = reveal ? want.displayName : hiddenNameLabel;

            // 마을에서도 선택은 정상 동작한다. interactable=false 로 회색 처리하지 않는다.
            var btn = go.GetComponent<Button>();
            var captured = want;
            if (btn != null)
                btn.onClick.AddListener(() => { PlaySfx(sfxSelect); EnterItemFocus(captured); });
        }

        // 천으로 덮인 칸 — 두 모드 모두 열리지 않는다
        if (coveredSlotPrefab == null)
        {
            if (_stock.coveredSlotCount > 0)
                Debug.LogWarning("[SolTradeUI] coveredSlotPrefab 이 없어 덮인 칸을 표시하지 못했습니다.");
            return;
        }

        for (int i = 0; i < _stock.coveredSlotCount; i++)
        {
            var go = Instantiate(coveredSlotPrefab, slotContainer);
            _slotObjects.Add(go);

            // 클릭 리스너를 붙이지 않는다. 프리팹에 Button 이 있어도 무반응이어야 한다.
            var btn = go.GetComponent<Button>();
            if (btn != null) btn.onClick.RemoveAllListeners();
        }
    }

    void ClearSlots()
    {
        foreach (var go in _slotObjects) if (go != null) Destroy(go);
        _slotObjects.Clear();
    }

    // ─────────────────────────────────────────────
    //  루가 내밀 수 있는 소지품
    // ─────────────────────────────────────────────

    void BuildOfferList()
    {
        ClearOffers();
        if (offerContainer == null || _selectedWant == null) return;

        if (offerSlotPrefab == null)
        {
            Debug.LogError("[SolTradeUI] offerSlotPrefab 이 연결되지 않았습니다. 인스펙터에서 설정해주세요.");
            return;
        }

        CountOfferables();

        foreach (var pair in _offerCounts)
        {
            var offer = pair.Key;
            int have  = pair.Value;

            var go = Instantiate(offerSlotPrefab, offerContainer);
            _offerObjects.Add(go);

            SetIcon(go, offer.icon);

            var txt = go.GetComponentInChildren<TMP_Text>();
            if (txt != null)
                txt.text = have > 1 ? $"x{have}" : "";

            var btn = go.GetComponent<Button>();
            var captured = offer;
            var capturedSlot = go;   // A안 트윈이 이 칸의 아이콘을 움직인다
            if (btn != null)
                btn.onClick.AddListener(() => OnOfferSelected(captured, capturedSlot));
        }
    }

    /// <summary>인벤토리의 ItemData 를 TradeItem 으로 되짚어 종류별 개수를 센다.</summary>
    void CountOfferables()
    {
        _offerCounts.Clear();
        if (InventoryManager.Instance == null) return;

        foreach (var item in InventoryManager.Instance.inventoryItems)
        {
            var trade = SolTradeRules.FromItemData(item);
            if (trade == null) continue;

            _offerCounts.TryGetValue(trade, out int count);
            _offerCounts[trade] = count + 1;
        }
    }

    void ClearOffers()
    {
        foreach (var go in _offerObjects) if (go != null) Destroy(go);
        _offerObjects.Clear();
    }

    // ─────────────────────────────────────────────
    //  거래 시도
    // ─────────────────────────────────────────────

    void OnOfferSelected(TradeItem offer, GameObject slot = null)
    {
        if (_state != State.ItemFocus) return;
        // 패널을 끄면 이 컴포넌트도 함께 꺼질 수 있으므로 상주 호스트에서 돌린다
        YarnDialogue.StartCoroutine(TryTrade(offer, _selectedWant, _session, slot));
    }

    IEnumerator TryTrade(TradeItem offer, TradeItem want, int session, GameObject slot = null)
    {
        var outcome = SolTradeRules.Resolve(_mode, offer, want);

        // ── 침묵 (C-15-4 마시멜로 · D-2 15-E-3 · F-7-2) ────────────────────
        // 솔은 아무 말도 하지 않는다. 대사도 효과음도 아이콘 움직임도 없고
        // 창 상태도 바뀌지 않는다. 다시 눌러도 같다. "입력은 정상 수신하되 아무 일도 없다" 가 전부다.
        if (!outcome.accepted && outcome.reason == RejectReason.Silence)
            yield break;

        // 성립 판정이 났어도 실제로 내밀 수량이 모자라면 성립하지 않는다
        if (outcome.accepted)
        {
            SolTradeRules.GetExchangeCounts(offer, want, out int need, out _);
            if (CountInInventory(offer.source) < need)
                outcome = new TradeOutcome
                {
                    accepted = false,
                    reason   = RejectReason.Empty,
                    yarnNode = YarnNodes.Sol_Trade_Reject_Empty,
                };
        }

        if (outcome.accepted) ExecuteExchange(offer, want);

        PlaySfx(outcome.accepted ? sfxTradeSuccess : sfxTradeReject);

        // ── 루가 스스로 거둔다 (C-15-4 각설탕 · D-2 15-E-1 · F-7-2 A안) ─────
        // 솔이 거절하는 것이 아니라 루가 내밀다 만 것이다. 그래서 아이콘만 짧게
        // 떠올랐다 제자리로 돌아오고, 창은 닫지도 어둡게 덮지도 않는다.
        bool withdraws = !outcome.accepted && outcome.reason == RejectReason.PlayerWithdraws;
        if (withdraws) yield return WithdrawTween(slot);

        // 대사 재생 동안만 패널을 감춘다. 창 자체는 닫히지 않는다.
        // ⚠ 루가 거두는 경우에는 감추지 않는다 — 정본이 "창은 닫지 않고 어둡게 덮지도 않으며"
        //   를 못박고 있다(F-7-2). 대화창과 겹쳐 보이면 거래창 프리팹 쪽에서 자리를 조정할 것.
        bool hidePanel = panel != null && !withdraws;
        if (hidePanel) panel.SetActive(false);
        yield return YarnDialogue.PlayIfExists(outcome.yarnNode, false);

        if (session != _session) yield break;   // 대사 도중 닫혔다면 여기서 끝
        if (hidePanel) panel.SetActive(true);

        if (outcome.accepted) EnterTrade();        // 성립하면 목록을 다시 그린다
        else                  BuildOfferList();    // 미성립이면 그 자리에 머무른다
    }

    /// <summary>
    /// A안 — 칸의 아이콘을 위로 짧게 띄웠다가 원위치로 되돌린다 (F-7-2 · D-2 15-E-1).
    ///
    /// <para>손 스프라이트나 클로즈업 컷을 만들지 않는다는 것이 A안의 요지다. 기존 아이콘에
    /// 트윈만 건다. <b>아이콘이 칸 밖으로 나가지 않아야</b> 하므로 이동량을 칸 높이에 비례해 잡는다.</para>
    ///
    /// <para><c>unscaledDeltaTime</c> 을 쓴다 — 거래창은 대화·일시정지와 겹칠 수 있어
    /// <c>Time.timeScale</c> 이 0 일 때도 움직여야 한다.</para>
    /// </summary>
    IEnumerator WithdrawTween(GameObject slot)
    {
        if (slot == null) yield break;

        var icon = FindIconRect(slot);
        if (icon == null) yield break;

        Vector2 home = icon.anchoredPosition;
        float   rise = Mathf.Max(4f, GetSlotHeight(slot) * WithdrawRiseRatio);

        yield return MoveIcon(icon, home, home + Vector2.up * rise, WithdrawRiseSeconds);
        yield return MoveIcon(icon, home + Vector2.up * rise, home, WithdrawFallSeconds);
        icon.anchoredPosition = home;   // 오차 보정 — 반드시 제자리로 돌아온다
    }

    IEnumerator MoveIcon(RectTransform icon, Vector2 from, Vector2 to, float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            if (icon == null) yield break;   // 창이 닫혀 칸이 파괴된 경우
            t += Time.unscaledDeltaTime;
            icon.anchoredPosition = Vector2.Lerp(from, to, Mathf.Clamp01(t / seconds));
            yield return null;
        }
    }

    /// <summary>칸 자신이 아니라 그 안의 아이콘을 움직인다. SetIcon 과 같은 규칙으로 찾는다.</summary>
    static RectTransform FindIconRect(GameObject slot)
    {
        var images = slot.GetComponentsInChildren<Image>(true);
        foreach (var img in images)
            if (img.transform != slot.transform) return img.rectTransform;
        return images.Length > 0 ? images[0].rectTransform : null;
    }

    static float GetSlotHeight(GameObject slot)
    {
        var rt = slot.transform as RectTransform;
        return rt != null ? rt.rect.height : 0f;
    }

    const float WithdrawRiseRatio   = 0.18f;  // 칸 높이의 18% 만 뜬다 — 칸 밖으로 안 나간다
    const float WithdrawRiseSeconds = 0.12f;
    const float WithdrawFallSeconds = 0.10f;

    void ExecuteExchange(TradeItem offer, TradeItem want)
    {
        if (InventoryManager.Instance == null) return;

        if (offer.source == null || want.source == null)
        {
            Debug.LogWarning($"[SolTradeUI] TradeItem 의 source 가 비어 있어 인벤토리 증감을 건너뜁니다 " +
                             $"(offer: {offer.name}, want: {want.name}). 대사만 재생됩니다.");
            return;
        }

        SolTradeRules.GetExchangeCounts(offer, want, out int offerCount, out int wantCount);

        for (int i = 0; i < offerCount; i++)
            InventoryManager.Instance.RemoveItem(offer.source);

        var toAdd = new List<ItemData>(wantCount);
        for (int i = 0; i < wantCount; i++)
            toAdd.Add(want.source);
        InventoryManager.Instance.AddItems(toAdd);
    }

    /// <summary>MerchantUI 가 쓰던 카운트 캐시 방식 (매 호출 new 를 피한다).</summary>
    int CountInInventory(ItemData item)
    {
        if (item == null || InventoryManager.Instance == null) return 0;

        _itemCountCache.Clear();
        foreach (var owned in InventoryManager.Instance.inventoryItems)
        {
            if (owned == null) continue;
            _itemCountCache.TryGetValue(owned, out int count);
            _itemCountCache[owned] = count + 1;
        }
        return _itemCountCache.TryGetValue(item, out int have) ? have : 0;
    }

    // ─────────────────────────────────────────────
    //  입력
    // ─────────────────────────────────────────────

    void Update()
    {
        if (_state == State.Closed || _state == State.Talk) return;

        // 대사 재생 중에는 패널을 숨겨둔다. 이때 이전/취소 키를 받으면
        // 대사가 끝난 뒤 돌아오는 코루틴과 상태가 어긋나므로 입력을 받지 않는다.
        if (panel == null || !panel.activeSelf) return;

        if (SettingsPanelUI.IsRebinding) return;

        KeyCode cancelKey = SettingsManager.Instance?.keyPause ?? KeyCode.Escape;

        if (Input.GetKeyDown(cancelKey))
        {
            PlaySfx(sfxBack);
            Close();
            return;
        }

        if (Input.GetKeyDown(backKey) && (_state == State.Trade || _state == State.ItemFocus))
        {
            PlaySfx(sfxBack);
            EnterChoice();
        }
    }

    /// <summary>
    /// 슬롯 프리팹의 아이콘 Image 에 스프라이트를 넣는다.
    /// 루트에는 Button 배경 Image 가 있는 경우가 많으므로 자식의 Image 를 우선한다.
    /// (자식이 없으면 루트를 쓴다 — 배경 하나로 아이콘까지 겸하는 프리팹 대응)
    /// </summary>
    static void SetIcon(GameObject go, Sprite sprite)
    {
        var images = go.GetComponentsInChildren<Image>(true);
        if (images.Length == 0) return;

        Image target = images[0];
        foreach (var img in images)
        {
            if (img.transform == go.transform) continue;
            target = img;
            break;
        }

        target.sprite  = sprite;
        target.enabled = sprite != null;
    }

    void PlaySfx(string soundName)
    {
        if (string.IsNullOrEmpty(soundName)) return;
        AudioManager.Instance?.Play(soundName);
    }
}
