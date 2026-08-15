using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 마을 발각 규칙 — 순찰 라운드 기준 (C-14-3-4 / E-34-1).
///
/// | 순찰 1회차   | 결계의 신호가 아직 닿지 않았다. 마주쳐도 알아보지 못하고 "…루?" 하고 넘어간다 |
/// | 1회차 종료   | 걸음을 멈추고 손끝이 결계 쪽으로 당겨진다. 2회차 진입 예고                    |
/// | 2회차 이후   | 한 번 걸리면 잡힌다. 다정하게 집으로 데려가고 방문이 잠긴다 → 감금 엔딩       |
///
/// ⚠ 1회차 종료 예고 연출은 선택이 아니라 필수다. 없으면 1회차를 완벽하게 피한 플레이어가
///   3회차에 처음 걸렸을 때 경고 없이 감금된다 — 잘한 사람이 더 크게 처벌받는 구조가 된다(E-34-1).
///
/// 세라가 루의 이탈을 알게 되는 근거는 결계의 시차다(A-13-3). 세라는 집으로 돌아가지 않으므로
/// 루가 없다는 것을 눈으로 확인하지 않는다.
/// </summary>
public class VillagePatrolController : MonoBehaviour
{
    public static VillagePatrolController Instance { get; private set; }

    [Header("Yarn 노드")]
    [Tooltip("1차 발각 — D-2 S#15 15-A 의 세라 \"…루?\". 아직 Village_Demo.yarn 에 미변환이라 " +
             "PlayIfExists 로 호출한다. 노드가 없으면 경고만 남고 순찰은 정상 진행된다.")]
    public string yarnNode_firstSighting = "Village_Sera_FirstSighting";
    [Tooltip("감금 엔딩 직전 대사. 원고 미작성이라 비워둬도 된다.")]
    public string yarnNode_captured = "Village_Sera_Captured";

    [Header("1회차 종료 예고")]
    [Tooltip("예고 연출 시간(초). 세라가 멈추고 손끝이 결계 쪽으로 당겨지는 구간.")]
    public float warningDuration = 2.5f;
    [Tooltip("예고 시점에 재생할 SFX (AudioManager 등록 이름. 비우면 무음).")]
    public string sfxWarningName = "";

    bool _handlingSighting;
    bool _captured;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnEnable()
    {
        SeraVision.OnPlayerSpotted  += HandleSpotted;
        SeraPatrol.OnRoundCompleted += HandleRoundCompleted;
    }

    void OnDisable()
    {
        SeraVision.OnPlayerSpotted  -= HandleSpotted;
        SeraPatrol.OnRoundCompleted -= HandleRoundCompleted;
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        // 감금 엔딩 후 돌아올 자리 = 마을에 처음 들어온 지점 (C-14-3-4 · 데모 범위 §5).
        // 마을에는 세이브 포인트를 두지 않으므로(C-14-3-5) 이 지점이 유일한 복귀 지점이다.
        if (SceneManager.GetActiveScene().name == SceneNames.Map)
            SaveManager.Instance?.SaveRewindPoint();
    }

    // ── 발각 ─────────────────────────────────────────────────────────────────
    void HandleSpotted()
    {
        if (_captured || _handlingSighting) return;

        int round = SeraPatrol.Instance != null ? SeraPatrol.Instance.RoundNumber : 1;

        if (round <= 1) StartCoroutine(FirstSightingRoutine());
        else            StartCoroutine(CaptureRoutine());
    }

    /// <summary>
    /// 1회차 — 세라가 루를 정확히 알아보지 못한다. "…루?" 하고 이내 고개를 돌려버린다.
    /// 그 자리를 벗어나면 넘어간다. 이 장면 하나가 은폐 규칙의 튜토리얼이자 경고다(D-2 15-A 각주).
    /// </summary>
    IEnumerator FirstSightingRoutine()
    {
        _handlingSighting = true;

        yield return YarnDialogue.PlayIfExists(yarnNode_firstSighting);

        // 그 자리를 벗어날 때까지 같은 대사를 반복하지 않는다.
        var vision = SeraPatrol.Instance != null
            ? SeraPatrol.Instance.GetComponentInChildren<SeraVision>() : null;
        if (vision != null)
            yield return new WaitUntil(() => !vision.PlayerInSight);

        _handlingSighting = false;
    }

    /// <summary>
    /// 2회차 이후 — 한 번 걸리면 잡힌다. 세라는 화내지 않는다.
    /// 다정하게 집으로 데려가고 방문이 잠긴다 (C-14-3-4).
    /// </summary>
    IEnumerator CaptureRoutine()
    {
        _captured = true;
        _handlingSighting = true;

        yield return YarnDialogue.PlayIfExists(yarnNode_captured);

        // 인형화 페널티 없음 (CLAUDE.md §2 · C-14-3-4).
        EndingManager.TriggerBadEnding(BadEndingType.Captured);
    }

    // ── 1회차 종료 예고 ──────────────────────────────────────────────────────
    void HandleRoundCompleted(int finishedRound)
    {
        // 1회차가 끝나는 순간에만. 이후 회차는 이미 위험 구간이므로 예고하지 않는다.
        if (finishedRound != 1) return;
        StartCoroutine(WarningRoutine());
    }

    IEnumerator WarningRoutine()
    {
        Dbg.Log("[마을순찰] 1회차 종료 — 2회차 진입 예고");

        if (!string.IsNullOrEmpty(sfxWarningName))
            AudioManager.Instance?.Play(sfxWarningName);

        // 화면 전체가 아니라 아주 짧게 한 번 — 결계가 세라에게 닿았다는 신호다.
        // 집 구간의 지속형 압박과 구분되어야 한다(C-14-3: 마을과 반드시 다르게).
        ScreenEdgeEffectController.ShowEdge(new Color(0.05f, 0.05f, 0.08f, 0.5f), warningDuration);

        yield return new WaitForSeconds(warningDuration);
    }
}
