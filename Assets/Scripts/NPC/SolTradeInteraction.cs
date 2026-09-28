using System.Collections;
using UnityEngine;

/// <summary>
/// 솔 거래 진입 컴포넌트. E키 상호작용 시 인사 대사(선택)를 재생한 뒤 거래창을 엽니다.
///
/// ※ SolNPC 의 자동 인사 트리거와는 별개입니다. SolNPC 는 접근 시 1회 인사만 하고,
///    이 컴포넌트는 E키를 눌렀을 때의 거래 진입을 담당합니다. 같은 오브젝트에 함께 붙일 수 있습니다.
///
/// [사용법]
/// 1. 솔 오브젝트에 InteractionTrigger 추가
/// 2. 이 컴포넌트 추가
/// 3. stock : SolStock ScriptableObject 연결 (Assets ▸ Create ▸ NPC ▸ Sol Stock)
/// 4. mode  : 마을이면 VillageBrowse, 숲이면 ForestTrade
/// 5. yarnNode_greeting : 처음 대화 시 재생할 노드 (선택, 없으면 바로 거래창)
/// </summary>
[RequireComponent(typeof(InteractionTrigger))]
public class SolTradeInteraction : MonoBehaviour
{
    [Header("좌판 데이터")]
    public SolStock stock;

    [Header("거래 모드")]
    [Tooltip("VillageBrowse: 이름·설명이 감춰지고 어떤 거래도 성립하지 않는다.\nForestTrade: 이름·설명이 열리고 성립한다.")]
    public TradeMode mode = TradeMode.VillageBrowse;

    [Header("인사 대사 (선택)")]
    [Tooltip("E키로 처음 접촉했을 때 재생할 Yarn 노드 이름. 비워두면 바로 거래창이 열립니다.\n예: Village_Sol_Square")]
    public string yarnNode_greeting;

    [Tooltip("인사 대사 중 플레이어 이동 잠금 여부")]
    public bool lockPlayerDuringGreeting = false;

    [Header("숲 재조우 (ForestTrade 전용 · S#20-A)")]
    [Tooltip("2차 전투(S#19)가 끝난 뒤 루가 이 거리 안에 들어오면 인사 대사가 자동으로 한 번 돈다(D 1053). " +
             "그 뒤로는 E키가 인사 없이 거래창을 바로 연다. 마을(VillageBrowse)에는 쓰지 않는다.")]
    public float forestAutoGreetRadius = 3.5f;

    private bool _isOpening = false;

    // S#20-A — 2차 전투(S#19) 종료 후 이동 중 자동 발동 · 한 번만(D 1053 · 1100).
    //   2026-09-28: 전에는 E키를 누를 때마다 재조우 대사가 처음부터 다시 돌았다.
    bool IsForestReunionPending =>
        mode == TradeMode.ForestTrade && !GameState.isForestSolMet && !string.IsNullOrEmpty(yarnNode_greeting);

    void Update()
    {
        if (!IsForestReunionPending || _isOpening || SolTradeUI.IsOpen || YarnDialogue.IsRunning) return;
        if (GameState.tutorialBattleStep < 2 || stock == null) return;
        var lu = PlayerStats.Instance != null ? PlayerStats.Instance.transform : null;
        if (lu == null || Vector2.Distance(lu.position, transform.position) > forestAutoGreetRadius) return;
        StartCoroutine(ForestReunion(lu));
    }

    /// <summary>
    /// [CAM] 「루를 따라간다. 솔이 먼저 고개를 돌리고, 루의 걸음이 그 뒤에 멈추면 카메라도 고정한다」(D 1054).
    /// 솔은 소리를 듣고 돌아보는 게 아니라 이미 그쪽을 보고 있다가 고개만 돌린다(D 1058).
    /// </summary>
    IEnumerator ForestReunion(Transform lu)
    {
        _isOpening = true;
        FaceToward(transform, lu.position);
        yield return new WaitForSeconds(0.3f);

        var ctrl = YarnDialogue.LockPlayer();
        CameraDirector.Instance?.Hold();
        yield return YarnDialogue.PlayIfExists(yarnNode_greeting, false);
        GameState.isForestSolMet = true;

        // 대화 종료 → 거래창 개방 · 숲 상시 대화(S#20-D) 개방(D 1100). 상시 대화는 거래창의 대화 항목이다.
        var entrance = FindAnyObjectByType<ForestEntranceDirector>();
        CameraDirector.Instance?.Track(entrance != null ? entrance.forwardOffset : Vector2.zero);
        YarnDialogue.UnlockPlayer(ctrl);
        if (SolTradeUI.Instance != null) SolTradeUI.Instance.Open(stock, mode);
        _isOpening = false;
    }

    /// <summary>0=아래 1=옆 2=위, 옆은 localScale.x 부호만 뒤집는다(CLAUDE.md §11 · KuruReunionDirector 와 같다).</summary>
    static void FaceToward(Transform who, Vector2 target)
    {
        Vector2 d = target - (Vector2)who.position;
        if (d.sqrMagnitude < 0.0001f) return;
        int dir = Mathf.Abs(d.x) >= Mathf.Abs(d.y) ? 1 : (d.y > 0f ? 2 : 0);
        Vector3 s = who.localScale;
        s.x = dir == 1 ? Mathf.Abs(s.x) * (d.x > 0f ? -1f : 1f) : Mathf.Abs(s.x);
        who.localScale = s;
        var anim = who.GetComponent<Animator>();
        if (anim != null) anim.SetInteger("dir", dir);
    }

    void Awake()
    {
        GetComponent<InteractionTrigger>().onInteract.AddListener(OnInteract);
    }

    void OnDestroy()
    {
        var trigger = GetComponent<InteractionTrigger>();
        if (trigger != null)
            trigger.onInteract.RemoveListener(OnInteract);
    }

    void OnInteract()
    {
        if (_isOpening || SolTradeUI.IsOpen) return;
        if (stock == null)
        {
            Debug.LogWarning($"[SolTradeInteraction] '{gameObject.name}': stock 이 비어 있습니다.");
            return;
        }
        if (YarnDialogue.IsRunning) return;
        // 숲에서는 재조우(S#20-A) 전에 거래창을 열지 않는다 — 자동 발동이 먼저다.
        if (IsForestReunionPending) return;

        StartCoroutine(OpenTrade());
    }

    IEnumerator OpenTrade()
    {
        _isOpening = true;

        // 숲은 재조우 대사를 한 번만 돈다 — 이미 만났으면 인사 없이 거래창.
        if (!string.IsNullOrEmpty(yarnNode_greeting) && mode != TradeMode.ForestTrade)
            yield return YarnDialogue.PlayIfExists(yarnNode_greeting, lockPlayerDuringGreeting);

        if (SolTradeUI.Instance != null)
            SolTradeUI.Instance.Open(stock, mode);
        else
            Debug.LogWarning("[SolTradeInteraction] SolTradeUI 인스턴스를 찾을 수 없습니다. Canvas 에 SolTradeUI 를 배치해주세요.");

        _isOpening = false;
    }
}
