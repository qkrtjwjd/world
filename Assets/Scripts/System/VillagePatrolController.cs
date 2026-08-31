using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    [Tooltip("1차 발각 — D-2 S#15 15-A 의 세라 \"…루?\". Village_Demo.yarn 에 반영돼 있다.")]
    public string yarnNode_firstSighting = "Village_Sera_Spotted";
    [Tooltip("감금 엔딩 직전 대사. 원고 미작성이라 비워둬도 된다.")]
    public string yarnNode_captured = "Village_Sera_Captured";

    [Header("BE#02-a — 발각 컷 (정본 문단 622~638)")]
    [Tooltip("손을 잡는 순간의 클로즈업. 세라의 손과 루의 도자기 손가락이 한 화면에 들어오는 컷. 아트가 아직 없으므로 비워 두면 조용히 건너뛴다.")]
    public Image handTakenCloseup;
    [Tooltip("클로즈업을 띄워 두는 시간(초).")]
    public float closeupHoldSeconds = 1.4f;
    [Tooltip("뒤에서 다가오는 발소리(AudioManager 등록 이름. 비우면 무음).")]
    public string sfxApproachStepsName = "";
    [Tooltip("손을 잡는 아주 작은 소리(AudioManager 등록 이름. 비우면 무음).")]
    public string sfxHandTakenName = "";
    [Tooltip("세라가 뒤에서 다가오는 데 걸리는 시간(초). 루는 굳어서 움직이지 못한다.")]
    public float approachSeconds = 1.6f;
    [Tooltip("대사가 끝나고 집으로 넘기기 전 여백(초). 0 으로 두지 말 것 — 아래 주석 참조.")]
    public float postCaptureSeconds = 0.6f;

    [Header("1회차 종료 예고")]
    [Tooltip("예고 연출 시간(초). 세라가 멈추고 손끝이 결계 쪽으로 당겨지는 구간.")]
    public float warningDuration = 2.5f;
    [Tooltip("예고 시점에 재생할 SFX (AudioManager 등록 이름. 비우면 무음).")]
    public string sfxWarningName = "";

    bool _handlingSighting;
    bool _captured;

    void Awake()
    {
        // ⚠ Destroy(gameObject) 를 쓰면 안 된다. 이 컴포넌트는 세라 GameObject 에 붙어 있어
        //    SeraVision·SeraPatrol·Animator 가 통째로 날아간다. 에러는 0건이라 콘솔로도 안 잡힌다.
        if (Instance != null && Instance != this) { SingletonGuard.DestroyDuplicate(this); return; }
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
        if (SceneManager.GetActiveScene().name != SceneNames.Map) return;

        SaveManager.Instance?.SaveRewindPoint();
        ResetVillageState();
    }

    /// <summary>
    /// 마을 진입 시점의 상태로 되돌립니다 (C-14-3-6 · 수치 F-6).
    ///
    /// BE#02 이후의 복귀는 <b>되감기이며 저장 데이터를 불러오는 것이 아니다.</b>
    /// 아이템·인형화는 마을 진입 스냅샷(전용 되감기 키)이 되돌리고, 저장 슬롯은 읽지도 덮지도 않는다.
    /// 이 메서드는 그 스냅샷에 담기지 않는 <b>런타임 상태</b>만 맡는다.
    ///
    ///   · 순찰 라운드 카운터 → 1회차
    ///   · 세라 → 광장에서 점검 상태로 재시작
    ///   · 엄폐물 → 전량 복원, 소실 단계 0
    ///   · 필터 → 환상 복귀, 단검 파지 해제
    ///
    /// 첫 진입에서는 전부 이미 그 상태이므로 아무 일도 일어나지 않는다.
    /// 되감기로 다시 들어왔을 때만 실제로 되돌린다.
    ///
    /// ⚠ 인형화 페널티는 붙이지 않는다. 발각 자체로는 어떤 값도 올리지 않는다 (C-14-3-6).
    /// </summary>
    void ResetVillageState()
    {
        SeraPatrol.Instance?.ResetPatrol();
        VillageCoverController.Instance?.ResetAll();

        // 단검을 파지한 채 잡혔더라도 환상으로 돌려놓는다. 상태는 IsRealityView 하나만 본다.
        DaggerFilterController.Instance?.SwitchToFantasyForced();
        FilterManager.Instance?.SetFilter(FilterType.Fantasy);

        _captured         = false;
        _handlingSighting = false;

        Dbg.Log("[마을순찰] 진입 상태 초기화 완료 (C-14-3-6)");
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

        // 정본 문단 625 — 마을 BGM 이 뚝 끊긴다. 페이드아웃하지 않는다.
        AudioManager.Instance?.StopAllBGM();

        // 정본 문단 624 — 단검을 파지한 상태에서 발각되었더라도 발동과 동시에 환상으로 되돌린다.
        DaggerFilterController.Instance?.SwitchToFantasyForced();
        FilterManager.Instance?.SetFilter(FilterType.Fantasy);

        // 정본 문단 620 — 발각 판정이 성립한 순간부터 엔딩이 끝날 때까지 조작권을 돌려주지 않는다.
        // 정본 문단 627 — 발각 알림을 띄우지 않는다. 그래서 HUD 를 내리기만 한다.
        var ctrl = YarnDialogue.LockPlayer();
        ObjectiveManager.Instance?.HideHUD();

        // 루의 뒤쪽에서 세라가 천천히 걸어온다. 루는 굳어서 움직이지 못한다(문단 629).
        PlaySfxIfNamed(sfxApproachStepsName);
        yield return new WaitForSeconds(approachSeconds);

        // [CAM] 손을 잡는 순간 손 클로즈업 — 세라의 손과 루의 도자기 손가락이 한 화면에(문단 628).
        PlaySfxIfNamed(sfxHandTakenName);
        yield return FlashCloseup(handTakenCloseup);

        yield return YarnDialogue.PlayIfExists(yarnNode_captured, false);

        // ⚠ 대사가 끝난 '직후' 에 씬을 넘기면 안 된다.
        //    DialogueRunner 는 줄이 끝나면 IsDialogueRunning 을 내리지만, 대사창을 지우는
        //    페이드는 아직 돌고 있다(Yarn 의 LinePresenter → Effects.FadeAlphaAsync).
        //    그 상태로 씬을 넘기면 CanvasGroup 이 파괴돼 MissingReferenceException 이 난다
        //    (2026-08-23 실측). 한 박자 두면 페이드가 끝난다.
        //    연출로도 이쪽이 맞다 — 손을 잡히고 나서 컷이 넘어가는 사이의 정적이다.
        yield return new WaitForSeconds(postCaptureSeconds);

        // 인형화 페널티 없음 (CLAUDE.md §2 · C-14-3-4).
        // 정본 문단 636 — 마을에서 집까지의 이동은 컷 하나로 넘긴다. 걸어가는 과정을 보여주지 않는다.
        // 집에 도착한 뒤의 BE#02-b · c 는 Home 씬의 BadEndingDirector 가 이어 재생한다.
        BadEndingDirector.QueueCapturedHousePart();
        YarnDialogue.UnlockPlayer(ctrl);

        if (TransitionManager.Instance != null)
            TransitionManager.Instance.DoSceneTransition(SceneNames.Home);
        else
            SceneManager.LoadScene(SceneNames.Home);
    }

    /// <summary>클로즈업 Image 를 잠깐 띄웠다 끈다. 비어 있으면 조용히 건너뛴다.</summary>
    IEnumerator FlashCloseup(Image image)
    {
        if (image == null) yield break;
        image.gameObject.SetActive(true);
        yield return new WaitForSeconds(closeupHoldSeconds);
        image.gameObject.SetActive(false);
    }

    void PlaySfxIfNamed(string soundName)
    {
        if (string.IsNullOrEmpty(soundName)) return;
        AudioManager.Instance?.Play(soundName);
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
