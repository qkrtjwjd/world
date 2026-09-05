using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 세라의 마을 순찰 (C-14-3-1 / 수치 F-6).
///
/// 세라는 루를 쫓지 않는다. 결계를 구역별로 점검하러 나와 있을 뿐이다.
/// 구역을 하나씩 돌며, 머무는 동안 그 구역이 위험 구역이 된다. 집으로 돌아가지 않는 무한 루프다.
///
/// 구역: 광장(가장 오래) · 빵집 앞 · 상점 · 마을 출구.
/// ※ 꽃집은 순찰 대상이 아니다 — 아모는 이미 완성된 상태라 점검할 이유가 없다.
///   결과적으로 마을의 유일한 안전 구역이 되며, 그 이유 자체가 연출이다.
/// </summary>
public class SeraPatrol : MonoBehaviour
{
    [Serializable]
    public class PatrolZone
    {
        [Tooltip("구역 이름. 로그·디버그용.")]
        public string name = "";
        [Tooltip("세라가 서 있을 지점.")]
        public Transform point;
        [Tooltip("이 구역에 머무는 시간(초). F-6 「광장 30초 · 그 외 20초」 — 고정값이다.\n" +
                 "⚠ 20~30 난수는 폐기된 값이다(탈출 압박 v1.0 표지 2절). 되돌리지 말 것. " +
                 "라운드 합계가 110초로 떨어져야 하므로 흔들면 안 된다.")]
        public float dwellSeconds = 20f;
        [Tooltip("세라가 이 구역에 들어오면 솔이 사라진다. 상점 구역에만 체크한다.")]
        public SeraApproachTrigger approachTrigger;

        [Tooltip("이 구역으로 오는 도중 거쳐 갈 지점들. 비어 있으면 직선으로 간다.\n" +
                 "⚠ 건물을 뚫고 지나가지 않게 하려고 둔다. 세라가 벽을 통과하면 몰입이 깨지고, " +
                 "통과하는 동안에는 시야가 콜라이더 안에 갇혀 전방향이 막힌다.\n" +
                 "⚠ 경유점을 넣어도 구간 전체 이동 시간은 moveDuration 그대로다(F-6 「구역 간 이동 5초」). " +
                 "거리에 비례해 배분한다.")]
        public Transform[] waypoints;
    }

    public static SeraPatrol Instance { get; private set; }

    [Header("순찰 구역 (꽃집은 넣지 않는다)")]
    public PatrolZone[] zones;

    [Header("이동")]
    [Tooltip("구역 간 이동 시간(초). F-6 초안값 5초. " +
             "세라는 걸어서 접근한다 — 뛰지 않고, 발각 후에도 같은 속도다(D-2 15-A).")]
    public float moveDuration = 5f;

    [Header("연출")]
    [Tooltip("1회차 종료 예고. 걸음을 멈추고 손끝이 결계 쪽으로 당겨지는 시간(초).")]
    public float roundEndPauseDuration = 2.5f;
    [Tooltip("Animator 가 있으면 이동/정지 상태를 넘긴다. 비워도 순찰은 동작한다.")]
    public Animator animator;
    public string animatorSpeedParam = "Speed";
    [Tooltip("바라보는 방향을 넘길 int 파라미터. 0=아래 1=옆 2=위. 비우면 방향 전환을 하지 않는다.")]
    public string animatorDirParam = "dir";

    /// <summary>현재 순찰 회차. 1부터 시작한다. 1회차는 결계의 신호가 아직 닿지 않은 구간이다.</summary>
    public int RoundNumber { get; private set; } = 1;

    /// <summary>현재 머물고 있는 구역. 이동 중이면 null.</summary>
    public PatrolZone CurrentZone { get; private set; }

    /// <summary>한 회차를 다 돌았을 때 발행된다. 인자는 방금 끝난 회차 번호.</summary>
    public static event Action<int> OnRoundCompleted;

    /// <summary>
    /// 순찰 1라운드에 걸리는 시간(초). F-6 초안값은 110초다
    /// (광장 30 + 빵집 20 + 상점 20 + 출구 20 + 이동 5×4).
    /// </summary>
    public float RoundSeconds
    {
        get
        {
            if (zones == null) return 0f;
            float total = 0f;
            foreach (var z in zones)
                if (z != null) total += Mathf.Max(0f, z.dwellSeconds) + Mathf.Max(0f, moveDuration);
            return total;
        }
    }

    /// <summary>세라의 시야. 자식에 붙어 있다.</summary>
    public SeraVision Vision
    {
        get
        {
            if (_vision == null) _vision = GetComponentInChildren<SeraVision>();
            return _vision;
        }
    }

    Coroutine  _routine;
    SeraVision _vision;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable()
    {
        if (Vision != null) Vision.OnTurnedToSound += HandleTurnedToSound;
    }

    void Start()
    {
        if (zones == null || zones.Length == 0)
        {
            Debug.LogWarning("[SeraPatrol] 순찰 구역이 하나도 배치되지 않았습니다. 순찰을 시작하지 않습니다.");
            return;
        }

        // 1라운드 길이는 엄폐물 소실 속도를 정하는 값이라 어긋나면 난도가 통째로 흔들린다.
        if (Mathf.Abs(RoundSeconds - 110f) > 0.5f)
            Debug.LogWarning($"[SeraPatrol] 순찰 1라운드가 {RoundSeconds:F1}초입니다. " +
                             "F-6 초안값은 110초(광장 30 + 나머지 20×3 + 이동 5×4)입니다.");

        SnapToStart();
        _routine = StartCoroutine(PatrolRoutine());
    }

    /// <summary>
    /// 첫 구역(광장)에 점검 상태로 세워 둔다. 광장은 결계의 중심이며 가장 오래 머무는 곳이다(C-14-3-1).
    /// 순찰 경로가 광장에서 시작하므로 시작 위치도 거기여야 한다 — 아니면 첫 이동이 경로 밖에서 생긴다.
    /// </summary>
    void SnapToStart()
    {
        if (zones == null || zones.Length == 0 || zones[0]?.point == null) return;

        transform.position = zones[0].point.position;
        CurrentZone        = null;
        SetAnimatorSpeed(0f);
        Vision?.SetState(SeraVisionState.Inspecting);
    }

    /// <summary>
    /// 순찰을 처음 상태로 되돌립니다 — 라운드 카운터 0(= 1회차), 광장에서 점검 상태로 재시작 (C-14-3-6).
    ///
    /// 라운드 카운터를 초기화하지 않으면 복귀 직후 2회차부터 시작해 한 번만 걸려도 다시 감금된다.
    /// 되감기가 처벌이 되는 것이며, 이중 처벌 금지와 같은 근거다.
    /// </summary>
    public void ResetPatrol()
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }

        RoundNumber = 1;
        SnapToStart();

        if (isActiveAndEnabled && zones != null && zones.Length > 0)
            _routine = StartCoroutine(PatrolRoutine());

        Dbg.Log("[마을순찰] 초기화 — 1회차 · 광장 점검부터 다시 시작");
    }

    IEnumerator PatrolRoutine()
    {
        // 집으로 돌아가지 않는 무한 루프 (C-14-3-1)
        while (true)
        {
            for (int i = 0; i < zones.Length; i++)
            {
                PatrolZone zone = zones[i];
                if (zone?.point == null) continue;

                yield return MoveVia(zone);

                CurrentZone = zone;
                SetAnimatorSpeed(0f);

                // 세라가 오면 솔은 순간적으로 자리에서 사라진다 (C-14-3-1).
                // 마을에서 세라를 인식하고 반응하는 유일한 사례다.
                zone.approachTrigger?.NotifyApproach();

                // 멈춰 서서 결계를 점검한다 — 시야가 넓고 짧아지며 느리게 회전한다 (C-14-3-2).
                Vision?.SetState(SeraVisionState.Inspecting);

                yield return new WaitForSeconds(Mathf.Max(0f, zone.dwellSeconds));

                CurrentZone = null;
            }

            // 회차 종료 — 1회차 종료 예고 연출이 여기 붙는다.
            int finished = RoundNumber;
            RoundNumber++;

            SetAnimatorSpeed(0f);
            OnRoundCompleted?.Invoke(finished);

            // 걸음을 멈추고 손끝이 결계 쪽으로 당겨진다 (C-14-3-4).
            if (roundEndPauseDuration > 0f)
                yield return new WaitForSeconds(roundEndPauseDuration);
        }
    }

    /// <summary>
    /// 구역까지 경유점을 거쳐 이동합니다. 경유점이 없으면 직선입니다.
    ///
    /// ⚠ 구간 전체가 <see cref="moveDuration"/> 안에 끝난다 (F-6 「구역 간 이동 5초」).
    ///   경유점마다 5초씩 쓰면 라운드가 110초에서 벗어난다. 거리에 비례해 나눠 쓴다.
    /// </summary>
    IEnumerator MoveVia(PatrolZone zone)
    {
        // 이미 그 자리에 서 있으면 움직이지 않는다. 재시작 직후(SnapToStart·ResetPatrol)가 그렇다.
        // ⚠ 이 검사를 경유점 처리보다 먼저 해야 한다. 뒤에 두면 광장에 서 있는 세라가
        //   광장의 경유점까지 걸어 나갔다 돌아오게 되어 「광장에서 점검 상태로 재시작」(C-14-3-6)이 깨진다.
        if ((zone.point.position - transform.position).sqrMagnitude < 0.0001f)
        {
            SetAnimatorSpeed(0f);
            yield break;
        }

        var legs = new List<Vector3>();
        if (zone.waypoints != null)
            foreach (var w in zone.waypoints)
                if (w != null) legs.Add(w.position);
        legs.Add(zone.point.position);

        // 구간별 거리로 시간을 배분한다.
        float total = 0f;
        Vector3 from = transform.position;
        var lengths = new float[legs.Count];
        for (int i = 0; i < legs.Count; i++)
        {
            lengths[i] = Vector3.Distance(from, legs[i]);
            total += lengths[i];
            from = legs[i];
        }

        for (int i = 0; i < legs.Count; i++)
        {
            float share = total > 0.0001f ? moveDuration * (lengths[i] / total) : 0f;
            yield return MoveTo(legs[i], share);
        }
    }

    IEnumerator MoveTo(Vector3 target) => MoveTo(target, moveDuration);

    IEnumerator MoveTo(Vector3 target, float duration)
    {
        Vector3 start   = transform.position;
        float   elapsed = 0f;
        float   dur     = Mathf.Max(0.01f, duration);

        // 이미 그 자리에 서 있으면 이동하지 않는다. 시야도 점검 상태로 둔다.
        // 재시작 직후(SnapToStart·ResetPatrol)가 이 경우다 — 여기서 걸어버리면
        // 「광장에서 점검 상태로 재시작」(C-14-3-6)이 5초 동안 깨진다.
        if ((target - start).sqrMagnitude < 0.0001f)
        {
            SetAnimatorSpeed(0f);
            yield break;
        }

        SetFacing(target - start);
        SetAnimatorSpeed(1f);

        // 이동 중에는 시야가 진행 방향으로 좁고 길어지며 회전하지 않는다 (C-14-3-2).
        Vision?.SetState(SeraVisionState.Moving);
        Vision?.SetFacing(((Vector2)(target - start)).normalized);

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(start, target, Mathf.Clamp01(elapsed / dur));
            yield return null;
        }
        transform.position = target;
    }

    void SetAnimatorSpeed(float v)
    {
        if (animator != null && !string.IsNullOrEmpty(animatorSpeedParam))
            animator.SetFloat(animatorSpeedParam, v);
    }

    /// <summary>
    /// 이동 방향을 Animator 의 dir(0=아래 1=옆 2=위)로 넘기고, 좌우는 flipX 로 처리한다.
    /// right 스프라이트는 만들지 않고 left 를 뒤집는다(CLAUDE.md §11).
    /// ⚠ 부호 규약은 루와 같다 — left 가 왼쪽을 보므로 **왼쪽이 +, 오른쪽이 −** 다.
    /// ⚠ localScale 을 통째로 대입하지 않는다. 씬에 박힌 크기가 지워진다(§11).
    /// </summary>
    void SetFacing(Vector3 delta)
    {
        if (Mathf.Abs(delta.x) < 0.001f && Mathf.Abs(delta.y) < 0.001f) return;

        Vector3 s = transform.localScale;
        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
        {
            if (animator != null && !string.IsNullOrEmpty(animatorDirParam))
                animator.SetInteger(animatorDirParam, 1);
            s.x = Mathf.Abs(s.x) * (delta.x > 0f ? -1f : 1f);
        }
        else
        {
            if (animator != null && !string.IsNullOrEmpty(animatorDirParam))
                animator.SetInteger(animatorDirParam, delta.y > 0f ? 2 : 0);
            s.x = Mathf.Abs(s.x);   // 위·아래 스프라이트는 뒤집지 않는다
        }
        transform.localScale = s;
    }

    /// <summary>
    /// 딱딱 소리가 난 방향으로 돌아본다. 발각 판정 전 단계다 (C-14-3-2 · F-6).
    ///
    /// 소리 → 회전(지연 1초) → 돌아봄 유지(3초) → 원복 순서는 <see cref="SeraVision"/> 이 관리한다.
    /// 여기서는 스프라이트가 같은 쪽을 보게만 한다.
    /// </summary>
    public void LookToward(Vector3 worldPosition)
    {
        Vector3 dir = worldPosition - transform.position;
        if (dir.sqrMagnitude < 0.0001f) return;

        // 스프라이트는 시야가 실제로 돌아본 순간에 맞춰 돌린다(OnTurnedToSound).
        // 여기서 바로 돌리면 지연 1초 동안 그림만 먼저 돌아 시야와 어긋난다.
        Vision?.NoticeSound(((Vector2)dir).normalized);
    }

    /// <summary>시야가 소리 쪽으로 돌아본 순간 스프라이트도 같은 쪽을 보게 한다.</summary>
    void HandleTurnedToSound(Vector2 direction) => SetFacing(direction);

    void OnDisable()
    {
        if (_vision != null) _vision.OnTurnedToSound -= HandleTurnedToSound;
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
    }
}
