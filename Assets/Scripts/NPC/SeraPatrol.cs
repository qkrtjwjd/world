using System;
using System.Collections;
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
        [Tooltip("이 구역에 머무는 시간(초). F-6 초안값 20~30. 광장은 가장 길게 잡는다.")]
        public float dwellMin = 20f;
        public float dwellMax = 30f;
        [Tooltip("세라가 이 구역에 들어오면 솔이 사라진다. 상점 구역에만 체크한다.")]
        public SeraApproachTrigger approachTrigger;
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

    Coroutine _routine;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (zones == null || zones.Length == 0)
        {
            Debug.LogWarning("[SeraPatrol] 순찰 구역이 하나도 배치되지 않았습니다. 순찰을 시작하지 않습니다.");
            return;
        }
        _routine = StartCoroutine(PatrolRoutine());
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

                yield return MoveTo(zone.point.position);

                CurrentZone = zone;
                SetAnimatorSpeed(0f);

                // 세라가 오면 솔은 순간적으로 자리에서 사라진다 (C-14-3-1).
                // 마을에서 세라를 인식하고 반응하는 유일한 사례다.
                zone.approachTrigger?.NotifyApproach();

                float dwell = UnityEngine.Random.Range(zone.dwellMin, zone.dwellMax);
                yield return new WaitForSeconds(dwell);

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

    IEnumerator MoveTo(Vector3 target)
    {
        Vector3 start   = transform.position;
        float   elapsed = 0f;
        float   dur     = Mathf.Max(0.01f, moveDuration);

        SetFacing(target - start);
        SetAnimatorSpeed(1f);
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

    /// <summary>딱딱 소리가 난 방향으로 돌아본다. 발각 판정 전 단계다(F-6 각주).</summary>
    public void LookToward(Vector3 worldPosition)
    {
        Vector3 dir = worldPosition - transform.position;
        if (dir.sqrMagnitude < 0.0001f) return;

        var vision = GetComponentInChildren<SeraVision>();
        vision?.SetFacing(dir.normalized);
        SetFacing(dir);          // 스프라이트도 같은 쪽을 본다
    }

    void OnDisable()
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
    }
}
