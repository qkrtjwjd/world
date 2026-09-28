using System.Collections;
using UnityEngine;

/// <summary>
/// 솔(상인) NPC. 광장/마을 출구 두 위치에 배치.
/// 플레이어 접근 시 자동 인사 대화 1회 시작.
///
/// 2026-09-27 (개정 D S#14 · S#15 15-E · C-14-3-1 · F-7-3):
///   · <b>세라가 광장에 있는 동안 솔은 없다.</b> 세라가 오면 창을 즉시 닫고 사라지며(F-7-3), 떠나면 돌아온다
///     (돌아오는 것은 사용자 결정 — 순찰이 매 라운드 광장을 지나므로 한 번 사라지고 끝나면 S#14 가 성립하지 않는다).
///     마을에서 세라를 인식하고 반응하는 유일한 사례다. 전에는 구현돼 있지 않아 세라 옆에서 인사했다(배치 실측).
///   · S#14 는 솔이 보일 때만 발동한다. 인사 도중 세라가 오면 인사를 끊고, 다음에 다시 발동한다.
///   · [CAM] 루의 걸음이 멈추는 순간 카메라도 멈춘다(D 541) — 인사 동안 고정, 끝나면 추적.
///   · 세이브 로드 뒤 인사를 다시 하지 않는다(GameState.hasMerchantMetAtSquare 를 읽는다).
/// </summary>
public class SolNPC : MonoBehaviour
{
    [Header("Yarn 노드")]
    [SerializeField] private string autoGreetNode   = "Village_Sol_Square";
    [SerializeField] private string breadRejectNode = "Sol_BreadDoughReject";

    [Header("근접 감지")]
    [SerializeField] private float     autoTriggerRadius = 1.125f;
    [SerializeField] private LayerMask playerLayer;

    [Header("세라가 오면 사라진다 (C-14-3-1 · F-7-3)")]
    [Tooltip("세라가 이 이름의 순찰 구역에 머무는 동안 솔은 없다.")]
    [SerializeField] private string seraZoneName = "광장";
    [Tooltip("세라가 솔에게서 이 거리(월드 유닛) 안에 들어오면 — 광장으로 걸어 들어오는 중에도 — 사라진다. 0 이면 구역만 본다.")]
    [SerializeField] private float seraVanishRadius = 2.5f;   // 2026-09-27 실측: 씬의 「상점」 구역이 솔에게서 3.2 — 그보다 작게 둬야 광장에만 반응한다

    private bool _hasGreeted;
    private bool _greeting;
    private bool _hidden;

    SpriteRenderer[] _renderers;
    Collider2D[]     _colliders;
    Behaviour[]      _interactions;

    void Start()
    {
        _hasGreeted   = GameState.hasMerchantMetAtSquare;
        _renderers    = GetComponentsInChildren<SpriteRenderer>(true);
        _colliders    = GetComponentsInChildren<Collider2D>(true);
        _interactions = new Behaviour[]
        {
            GetComponent<InteractionTrigger>(),
            GetComponent<SolTradeInteraction>(),
        };
    }

    void Update()
    {
        bool seraHere = SeraIsHere();
        if (seraHere != _hidden) SetHidden(seraHere);
        if (_hidden) return;

        if (_hasGreeted || YarnDialogue.IsRunning) return;

        Collider2D hit = Physics2D.OverlapCircle(transform.position, autoTriggerRadius, playerLayer);
        if (hit == null) return;

        StartCoroutine(GreetRoutine(hit.transform));
    }

    IEnumerator GreetRoutine(Transform lu)
    {
        _hasGreeted = true;
        _greeting   = true;

        // 루의 걸음이 멈추는 순간 카메라도 멈춘다(D 541). 루는 솔 쪽을 본다 — 솔은 처음부터 루를 보고 있었다.
        CameraDirector.Instance?.Hold();
        FaceToward(lu, transform.position);

        yield return YarnDialogue.PlayAndWait(autoGreetNode, true);

        bool interrupted = !_greeting;
        _greeting = false;
        CameraDirector.Instance?.Track();

        if (interrupted)
        {
            // 세라가 와서 솔이 사라졌다. 인사는 없었던 것으로 하고, 솔이 돌아오면 다시 발동한다.
            _hasGreeted = false;
            yield break;
        }
        GameState.hasMerchantMetAtSquare = true;
    }

    bool SeraIsHere()
    {
        var patrol = SeraPatrol.Instance;
        if (patrol == null || !patrol.isActiveAndEnabled) return false;
        if (patrol.CurrentZone != null && patrol.CurrentZone.name == seraZoneName) return true;
        return seraVanishRadius > 0f &&
               Vector2.Distance(patrol.transform.position, transform.position) <= seraVanishRadius;
    }

    /// <summary>
    /// 순간적으로 자리에서 사라진다/돌아온다. 오브젝트를 끄지 않는다 — 이 컴포넌트가 계속 세라를 봐야 돌아올 수 있다.
    /// 소멸 연출(F-6-1 에셋)이 오면 여기서 재생한다.
    /// </summary>
    void SetHidden(bool hidden)
    {
        _hidden = hidden;

        if (hidden)
        {
            // 창을 즉시 닫는다. 확인창을 띄우지 않는다(F-7-3).
            SolTradeUI.Instance?.ForceClose();
            if (_greeting && YarnDialogue.IsRunning)
            {
                _greeting = false;
                YarnDialogue.Runner.Stop();
            }
        }

        if (_renderers != null) foreach (var r in _renderers) if (r != null) r.enabled = !hidden;
        if (_colliders != null) foreach (var c in _colliders) if (c != null) c.enabled = !hidden;
        if (_interactions != null) foreach (var b in _interactions) if (b != null) b.enabled = !hidden;

        Dbg.Log(hidden ? "[솔] 세라가 광장에 — 사라짐" : "[솔] 세라가 떠남 — 돌아옴");
    }

    /// <summary>0=아래 1=옆 2=위, 옆은 localScale.x 부호만 뒤집는다(양수가 왼쪽 — CLAUDE.md §11).</summary>
    static void FaceToward(Transform who, Vector2 target)
    {
        if (who == null) return;
        var anim = who.GetComponent<Animator>();
        Vector2 d = target - (Vector2)who.position;
        if (d.sqrMagnitude < 0.0001f) return;
        int dir = Mathf.Abs(d.x) >= Mathf.Abs(d.y) ? 1 : (d.y > 0f ? 2 : 0);
        Vector3 s = who.localScale;
        s.x = dir == 1 ? Mathf.Abs(s.x) * (d.x > 0f ? -1f : 1f) : Mathf.Abs(s.x);
        who.localScale = s;
        if (anim != null) { anim.SetInteger("dir", dir); anim.SetBool("isRun", false); }
    }

    /// <summary>빵 반죽 거래 시도 시 호출 (InteractionTrigger.onInteract에 연결)</summary>
    public void OnBreadDoughTradeAttempt()
    {
        if (YarnDialogue.IsRunning || _hidden) return;
        StartCoroutine(YarnDialogue.PlayAndWait(breadRejectNode, true));
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, autoTriggerRadius);
        Gizmos.color = new Color(1f, 0.6f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, seraVanishRadius);
    }
}
