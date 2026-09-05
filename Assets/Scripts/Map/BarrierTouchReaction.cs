using System.Collections;
using UnityEngine;

/// <summary>
/// 결계의 접촉 반응 (A-13-3 문단 389 · C-14-2-3 문단 1067).
///
/// 나가려는 의도를 가지고 경계에 닿으면 <b>접촉면이 뜨거워진다.</b>
/// 상해를 입히지는 않는다 — 물러서게 만드는 것이 목적이다.
///
/// ⚠ <b>현관문에는 붙이지 않는다.</b> C-14-2-3 문단 1069 가 못박았다 —
///   「현관문에 접촉 반응을 걸면 나가려는 손을 결계가 밀어내게 되어 진행 자체가 막힌다.
///   이 구간의 접촉 반응은 <b>옆길을 닫는 용도로만</b> 쓴다.」
///   붙일 곳은 <b>마당 담장과 창문</b>뿐이다.
///
/// ⚠ S#06 의 「손잡이가 뜨거워진다」와 혼동하지 말 것. 그쪽은 압박 구간 이전의 별개 연출이며
///   <see cref="FrontDoorInteraction"/> 이 맡는다.
///
/// ⚠ 체력을 깎지 않는다. 인형화도 올리지 않는다. 정본이 「상해를 입히지는 않는다」로 못박았고,
///   실패에 페널티를 붙이지 않는다는 원칙(CLAUDE.md §2)과도 같은 축이다.
///
/// 감지용 트리거는 자동으로 만든다 — 대상의 콜라이더를 조금 부풀린 자식이다.
/// 통행 콜라이더는 손대지 않으므로 C-14-2-2 와도 어긋나지 않는다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BarrierTouchReaction : MonoBehaviour
{
    [Header("발동 조건")]
    [Tooltip("탈출 압박이 도는 동안에만 반응한다. C-14-2-3 의 「이 구간에서」가 그 뜻이다.\n" +
             "끄면 언제나 반응한다 — 결계 자체를 상시 표현하고 싶을 때만.")]
    public bool onlyDuringPressure = true;

    [Tooltip("감지 트리거를 대상 콜라이더보다 얼마나 부풀릴지(월드 유닛). 몸이 닿기 직전에 반응하게 한다.")]
    public float detectPadding = 0.12f;

    [Tooltip("같은 곳에서 연달아 터지지 않도록 두는 간격(초).")]
    public float cooldown = 1.2f;

    [Header("연출 — 뜨거워짐")]
    [Tooltip("화면 가장자리에 번지는 색. 뜨거운 쪽이므로 따뜻한 색이다.")]
    public Color heatColor = new Color(0.85f, 0.45f, 0.25f, 0.45f);
    [Tooltip("번짐이 이어지는 시간(초).")]
    public float heatDuration = 0.45f;

    [Tooltip("AudioManager 에 등록된 이름. 비어 있으면 무음이다 — 화면 쪽이 주 전달 수단이라 " +
             "소리가 없어도 정보가 빠지지 않는다. (CLAUDE.md §0-4: 이름을 지어내지 않는다)")]
    public string sfxName = "";

    [Header("물러섬")]
    [Tooltip("닿은 반대쪽으로 밀어내는 거리(월드 유닛). 0 이면 밀지 않는다.\n" +
             "⚠ 크게 주지 말 것. 상해가 아니라 '물러서게 하는' 정도다.")]
    public float pushBack = 0.18f;
    [Tooltip("밀어내는 데 걸리는 시간(초).")]
    public float pushDuration = 0.15f;

    float _nextTime;
    Collider2D _detector;

    void Awake()
    {
        BuildDetector();
    }

    /// <summary>
    /// 감지용 트리거를 자식으로 만든다. 대상의 통행 콜라이더는 건드리지 않는다 —
    /// 크기를 바꾸면 통행 판정이 달라지고, 그건 C-14-2-2 가 금지한다.
    /// </summary>
    void BuildDetector()
    {
        var src = GetComponent<Collider2D>();
        var go = new GameObject("접촉감지");
        go.transform.SetParent(transform, false);
        go.layer = gameObject.layer;

        if (src is BoxCollider2D box)
        {
            var b = go.AddComponent<BoxCollider2D>();
            Vector3 ls = transform.lossyScale;
            float px = Mathf.Approximately(ls.x, 0f) ? 0f : detectPadding / Mathf.Abs(ls.x);
            float py = Mathf.Approximately(ls.y, 0f) ? 0f : detectPadding / Mathf.Abs(ls.y);
            b.size      = box.size + new Vector2(px * 2f, py * 2f);
            b.offset    = box.offset;
            b.isTrigger = true;
            _detector = b;
        }
        else
        {
            // 박스가 아니면 대상 크기를 감싸는 원으로 대신한다.
            var c = go.AddComponent<CircleCollider2D>();
            var e = src.bounds.extents;
            float r = Mathf.Max(e.x, e.y) + detectPadding;
            Vector3 ls = transform.lossyScale;
            float s = Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.y)));
            c.radius    = r / s;
            c.isTrigger = true;
            _detector = c;
        }

        var relay = go.AddComponent<BarrierTouchRelay>();
        relay.owner = this;
    }

    internal void NotifyTouch(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (Time.time < _nextTime) return;
        if (onlyDuringPressure && !HouseEscapePressureController.IsActive) return;

        _nextTime = Time.time + cooldown;
        StartCoroutine(React(other.transform));
    }

    IEnumerator React(Transform player)
    {
        // 접촉면이 뜨거워진다 (A-13-3). 상해는 없다 — 체력도 인형화도 건드리지 않는다.
        ScreenEdgeEffectController.ShowEdge(heatColor, heatDuration);

        if (!string.IsNullOrEmpty(sfxName)) AudioManager.Instance?.Play(sfxName);

        Dbg.Log($"[결계] 접촉 반응 — '{name}' 에 닿았다 (뜨거워짐 · 상해 없음)");

        if (pushBack <= 0f || player == null) yield break;

        // 물러서게 만든다. 경계에서 바깥이 아니라 '안쪽'으로 민다 — 나가려는 것을 되돌리는 방향이다.
        Vector2 away = (Vector2)(player.position - transform.position);
        if (away.sqrMagnitude < 0.0001f) yield break;
        away.Normalize();

        Vector3 from = player.position;
        Vector3 to   = from + (Vector3)(away * pushBack);
        var rb = player.GetComponent<Rigidbody2D>();

        float t = 0f;
        while (t < pushDuration && player != null)
        {
            t += Time.deltaTime;
            Vector3 p = Vector3.Lerp(from, to, Mathf.Clamp01(t / pushDuration));
            if (rb != null) rb.MovePosition(p); else player.position = p;
            yield return null;
        }
    }
}

/// <summary>
/// 자동 생성한 감지 트리거에서 부모로 접촉을 넘긴다.
/// 부모가 직접 받으면 통행 콜라이더의 충돌까지 섞여 들어온다.
/// </summary>
public class BarrierTouchRelay : MonoBehaviour
{
    internal BarrierTouchReaction owner;

    void OnTriggerEnter2D(Collider2D other) => owner?.NotifyTouch(other);
    void OnTriggerStay2D(Collider2D other)  => owner?.NotifyTouch(other);
}
