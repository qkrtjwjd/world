using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 집 구간 탈출 압박 — 결계가 조인다 (C-14-2 / F-6 / 근거 A-13-3).
///
/// S#11 에서 루가 "제가 아빠 데리러 갈게요" 라고 말한 직후 발동해 90초를 센다.
/// 현관문을 통과하면 해제, 시간이 다하면 현관문이 영구 폐쇄되고 배드 엔딩 ① 로 간다.
/// (F-6 「타이머·조임 정지 — 현관문 통과」 · 마당과 정문 앞은 압박 밖이다)
///
/// 배치 불필요 — ScreenEdgeEffectController 와 같은 지연 자동 생성 방식이다.
/// 씬에 직접 배치하면 그 인스턴스의 인스펙터 값이 쓰인다(수치 조정용).
///
/// 조임은 <b>연속 보간이 아니라 이산 이벤트</b>다 (C-14-2-1 · E-34-3).
/// 20 / 45 / 65 / 80초에 네 번 발동하고, 단계와 단계 사이에서는 어떤 값도 변하지 않는다.
/// 간격이 25 → 20 → 15 → 10 으로 좁혀지는 것이 조여오는 속도를 숫자 없이 전달하는 유일한 수단이다.
///
/// ⚠ 금지 사항
///   · UI 타이머를 띄우지 않는다. 90초는 내부 값이다 (CLAUDE.md §7 · F-6)
///   · 실패에 인형화 페널티를 붙이지 않는다. 되감기와 이중 처벌이 된다 (CLAUDE.md §2 · C-14-2)
///   · S#06~S#10 에는 걸지 않는다. 시계가 돌면 아무도 유의 라디오를 안 듣는다 (C-14-2)
///   · <b>네 연출을 90초에 걸쳐 동시에 선형 보간하지 않는다.</b> 계속 어두워지면 플레이어는
///     화면 효과로 읽고, 멈춰 있다가 한 번에 조이면 사건으로 읽는다 (C-14-2-1)
///   · <b>1차에 화면을 건드리지 않는다.</b> 첫 신호를 소리에 맡겨야 이후 세 단계가
///     같은 종류의 변화로 뭉뚱그려지지 않는다 (C-14-2-1)
/// </summary>
public class HouseEscapePressureController : MonoBehaviour
{
    // ── 싱글톤 ──────────────────────────────────────────────────────────────
    public static HouseEscapePressureController Instance
    {
        get
        {
            if (_instance == null) CreateInstance();
            return _instance;
        }
    }
    static HouseEscapePressureController _instance;

    // ── 수치 (F-6 초안값. "데모를 실제로 돌려본 뒤 조정한다") ─────────────────
    [Header("제한 시간")]
    [Tooltip("F-6 초안값 90초. 다락방에서 현관까지 직선 이동 약 25초 기준 여유 3배.")]
    public float timeLimit = 90f;
    [Tooltip("시간이 다한 뒤 배드 엔딩으로 넘어가기 전까지 조임을 유지하는 시간(초).")]
    public float failLingerDuration = 2f;

    [Header("조임 단계 (C-14-2-1 · F-6)")]
    [Tooltip("F-6 「집 조임 단계 — 4단계 · 20 / 45 / 65 / 80초」. 이 시점에만 값이 바뀌고 " +
             "사이에는 정지한다. 간격 25 → 20 → 15 → 10 이 조여오는 속도를 전달한다.")]
    public float[] stageSeconds = { 20f, 45f, 65f, 80f };

    [Tooltip("F-6 「단계 전환은 0.4초 이내에 끝낸다」. 전환이 끝나면 다음 단계까지 어떤 값도 변하지 않는다.")]
    public float stageTransitionDuration = 0.4f;

    [Header("연출 — 화면")]
    [Tooltip("지속형 비네팅 색. 알파는 아래 단계별 값으로 대체된다.")]
    public Color vignetteColor = new Color(0.02f, 0.02f, 0.04f);
    [Tooltip("단계별 가장자리 진하기 — 2차 / 3차 / 4차. 1차는 화면을 건드리지 않으므로 목록에 없다. " +
             "⚠ 정본에 수치가 없어 정한 값이다 — F-6 이 규정한 것은 폭(18/30/44%)뿐이고 진하기는 없다. " +
             "⭐ 그래도 폭만으로는 C-14-2-1 문단 1041 의 「4차 — 진행 방향 외에는 시야가 거의 남지 " +
             "않는다」가 성립하지 않는다. 2026-09-05 배치모드 실측에서 0.45 고정이면 가장자리가 " +
             "밝기 126 → 98, 22% 밖에 안 내려갔고 2·3·4차가 전부 같은 값이었다. " +
             "⚠ 프로젝트가 Linear 색공간이라 알파를 그대로 밝기 감소로 읽으면 안 된다. 배경 126 기준 " +
             "0.55 → 87 · 0.75 → 65 · 0.95 → 26 으로 떨어진다.")]
    public Vector3 vignetteAlphas = new Vector3(0.55f, 0.75f, 0.95f);

    [Tooltip("F-6 「집 어두워짐 3단계 — 가장자리 화면비 18% / 30% / 44%」. 2·3·4차에 대응하며 " +
             "1차는 화면을 건드리지 않는다. 화면 가장자리에서 안쪽으로 파고드는 폭이다.")]
    public Vector3 edgeRatios = new Vector3(0.18f, 0.30f, 0.44f);

    [Header("연출 — 공간 (C-14-2-2)")]
    [Tooltip("F-6 「집 복도 축소 — 좌우 벽·천장 스프라이트 안쪽 오프셋 3차 6px · 4차 12px」. " +
             "3차와 4차에만 발생한다.\n" +
             "⚠ 단위는 비율이 아니라 도트 픽셀이다. 1px = 0.5625/32 = 0.017578 월드유닛 " +
             "(도트 PPU 32 · 씬 배치 0.5625배)이므로 12px 는 0.2109 유닛이다.\n" +
             "⚠ 콜라이더와 카메라는 바꾸지 않는다. 실제로 좁히면 통행 불가 구간이 생기고 " +
             "그것은 제한 시간이 아니라 벽이 된다 (C-14-2-2).\n" +
             "⭐ 문틀(4차 8px · 3차 4px)과 비율이 0.5 로 같아진다 — 이 값을 6/12 가 아닌 것으로 " +
             "바꾸면 문틀 3차가 다시 4px 에서 어긋난다.")]
    public float corridorShrinkStage3 = 6f;
    public float corridorShrinkStage4 = 12f;

    [Header("연출 — 저음")]
    [Tooltip("AudioManager 에 등록된 드론 루프 이름. 비우면 아래 절차 생성 저음을 대신 쓴다.")]
    public string droneSoundName = "";

    [Tooltip("F-6 「기본 대역 80~120Hz」 — 절차 생성 저음이 차지하는 대역의 아래끝(Hz).\n" +
             "⚠ 이전의 42Hz 는 노트북 스피커의 재생 하한 아래라 아무 소리도 들리지 않았다. " +
             "이 대역은 그것을 정본이 직접 올려 잡은 값이다.")]
    public float droneBandLowHz = 80f;
    [Tooltip("F-6 「기본 대역 80~120Hz」 — 대역의 위끝(Hz).")]
    public float droneBandHighHz = 120f;
    [Tooltip("대역 안에 흩을 성분의 개수. F-6 「순음을 쓰지 않는다」 — 1 로 두면 순음이 되므로 금지다.")]
    [Range(3, 24)] public int dronePartials = 9;
    [Tooltip("4차 시점의 드론 볼륨 상한 (Ambient 볼륨에 곱해진다). F-6 「1차 진입 · 단계마다 +6dB」 이므로 " +
             "1차는 이 값의 1/8 에서 시작해 단계마다 두 배가 된다.")]
    [Range(0f, 1f)] public float droneMaxVolume = 0.5f;

    [Tooltip("「단계 상승을 저음 볼륨만으로 만들지 않는다. 로우패스 컷오프를 함께 열어 배음이 늘어나게 한다」 " +
             "(F-6 문단 792). 볼륨만 올리면 스피커 한계에서 변화가 멈춘다.\n" +
             "⚠ 정본에 수치가 없어 정한 값이다. 1~4차 순서다.\n" +
             "⭐ 단계마다 배음이 한 층씩 열리도록 맞춰 놓았다. 80~120Hz 대역에 4배음까지 쌓으면 " +
             "내용물이 80~120 · 160~240 · 240~360 · 320~480Hz 에 놓이므로 컷오프도 그 사이를 짚는다.\n" +
             "⚠ droneHarmonics 를 줄이면 위쪽 단계가 빈 대역을 열게 되어 아무 변화도 나지 않는다. " +
             "실측으로 480Hz 위에는 성분이 없다 — 700Hz 같은 값을 넣으면 4차가 3차와 같아진다.")]
    public float[] droneCutoffHz = { 150f, 250f, 370f, 500f };

    [Tooltip("대역 위쪽 배음을 몇 배음까지 쌓을지. 1 이면 배음이 없어 로우패스가 무효가 된다. " +
             "위의 컷오프 사다리가 이 값(4)을 전제로 짜여 있다.")]
    [Range(1, 6)] public int droneHarmonics = 4;

    [Tooltip("F-6 「4차에서 최대. 그 외 BGM은 낮춘다」 — 4차 시점의 BGM 배율. " +
             "드론이 커지는 만큼 BGM 이 물러나야 저음이 들린다. " +
             "⚠ 정본에 수치가 없어 정한 값이다(4차에서 약 -9dB). 단계 사이는 드론 세기에 비례한다.")]
    [Range(0f, 1f)] public float bgmDuckAtStage4 = 0.35f;

    /// <summary>
    /// 공간 압박 강도(0~1) 변화 알림. 복도 축소·문틀 좁아짐처럼 씬 오브젝트를 움직이는 연출이
    /// <see cref="EscapePressureShrinker"/> 로 여기에 붙는다.
    ///
    /// 1 은 <see cref="corridorShrinkStage4"/>(12px) 에 해당한다. 즉 구독자의 눌린 상태는
    /// <b>4차 기준</b>으로 만들어 두면 되고, 3차는 그 비율만큼만 적용된다.
    ///
    /// ⚠ 단계가 바뀔 때만 발행된다. 단계 사이에는 호출되지 않는다 (C-14-2-1).
    /// </summary>
    public static event Action<float> OnLevelChanged;

    /// <summary>
    /// 압박이 발동한 순간 발행된다. 다락방 문 잠금처럼 <b>단계와 무관하게 즉시</b> 일어나는
    /// 처리가 여기에 붙는다 (C-14-2-3 — 발동 즉시, 대사를 붙이지 않는다).
    /// </summary>
    public static event Action OnPressureBegan;

    /// <summary>압박이 해제된 순간 발행된다(현관문 통과·실패 처리 후).</summary>
    public static event Action OnPressureEnded;

    // ── 내부 상태 ────────────────────────────────────────────────────────────
    bool        _active;
    float       _elapsed;
    AudioSource _drone;

    /// <summary>현재 조임 단계. 0 = 발동만 하고 아직 아무 변화 없음, 1~4 = F-6 의 1~4차.</summary>
    int   _stage;

    /// <summary>단계 전환 진행도 0~1. 1 이면 전환이 끝났고 다음 단계까지 값이 고정된다.</summary>
    float _transition = 1f;

    // 현재 적용 중인 연출값과 전환 시작값. 전환 구간에서만 둘 사이를 오간다.
    float _edgeAlpha,  _fromEdgeAlpha;
    float _edgeRatio,  _fromEdgeRatio;
    float _shrink,     _fromShrink;
    float _droneLevel, _fromDroneLevel;
    float _cutoff,     _fromCutoff;
    AudioLowPassFilter _droneLowPass;

    /// <summary>압박이 진행 중인지 여부.</summary>
    public static bool IsActive => _instance != null && _instance._active;

    /// <summary>현재 조임 단계 0~4. UI 에 표시하지 말 것 — 디버그·검증 전용이다.</summary>
    public static int Stage => _instance == null || !_instance._active ? 0 : _instance._stage;

    /// <summary>
    /// 현재 공간 압박 강도 0~1(= 4차 기준). UI 에 표시하지 말 것.
    /// <b>경과 비율이 아니다</b> — 단계값이므로 20~45초 구간에서는 계속 같은 값이 나온다.
    /// </summary>
    public static float Level => _instance == null || !_instance._active ? 0f : _instance._shrink;

    // ── 단계별 목표값 (F-6) ──────────────────────────────────────────────────

    /// <summary>
    /// 단계별 비네팅 진하기. 1차는 화면을 건드리지 않으므로 0 이다 (C-14-2-1).
    ///
    /// 폭(<see cref="TargetEdgeRatio"/>)과 진하기가 <b>함께</b> 자란다. F-6 이 규정한 것은 폭뿐이지만,
    /// 폭만 키우면 4차에서도 가장자리가 22% 밖에 어두워지지 않아 C-14-2-1 문단 1041 의
    /// 「진행 방향 외에는 시야가 거의 남지 않는다」에 닿지 못한다(2026-09-05 배치모드 실측).
    ///
    /// ⚠ 화면 전체를 고르게 덮는 것이 아니다. 어디까지나 가장자리에서 안쪽으로 파고드는 그라디언트이며,
    ///   가운데는 끝까지 열려 있어야 「사방에서 안쪽」(C-14-2 문단 1018)이 성립한다.
    /// </summary>
    float TargetEdgeAlpha(int stage) =>
        stage <= 1 ? 0f : stage == 2 ? vignetteAlphas.x : stage == 3 ? vignetteAlphas.y : vignetteAlphas.z;

    /// <summary>단계별 가장자리 폭. F-6 「가장자리 화면비 18% / 30% / 44%」 — 2·3·4차에 대응한다.</summary>
    float TargetEdgeRatio(int stage) =>
        stage <= 1 ? 0f : stage == 2 ? edgeRatios.x : stage == 3 ? edgeRatios.y : edgeRatios.z;

    /// <summary>단계별 공간 축소량. 3차·4차에만 발생한다 (F-6).</summary>
    float TargetShrink(int stage)
    {
        if (stage <= 2) return 0f;
        float max = Mathf.Max(0.0001f, corridorShrinkStage4);
        return stage == 3 ? Mathf.Clamp01(corridorShrinkStage3 / max) : 1f;
    }

    /// <summary>단계별 드론 볼륨. F-6 「1차 진입 · 단계마다 +6dB」 — +6dB 는 진폭 두 배다.</summary>
    float TargetDroneLevel(int stage)
    {
        if (stage <= 0) return 0f;
        return droneMaxVolume / Mathf.Pow(2f, 4 - Mathf.Clamp(stage, 1, 4));
    }

    /// <summary>
    /// 단계별 로우패스 컷오프(Hz). F-6 문단 792 「로우패스 컷오프를 함께 열어 배음이 늘어나게 한다」.
    /// 0 단계는 1차 값으로 둔다 — 드론 볼륨이 0 이라 들리지 않으므로 시작값이 무엇이든 상관없고,
    /// 1차 진입 때 컷오프가 튀지 않아야 한다.
    /// </summary>
    float TargetDroneCutoff(int stage)
    {
        if (droneCutoffHz == null || droneCutoffHz.Length == 0) return 22000f;
        int i = Mathf.Clamp(stage - 1, 0, droneCutoffHz.Length - 1);
        return Mathf.Max(20f, droneCutoffHz[i]);
    }

    /// <summary>경과 시간이 몇 번째 단계에 해당하는지. F-6 의 임계를 넘은 개수다.</summary>
    int StageAt(float elapsed)
    {
        if (stageSeconds == null) return 0;
        int stage = 0;
        for (int i = 0; i < stageSeconds.Length; i++)
            if (elapsed >= stageSeconds[i]) stage = i + 1;
        return stage;
    }

    // ── 부트스트랩 ───────────────────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        AtticRadioCutscene.OnResolved -= HandleResolved;
        AtticRadioCutscene.OnResolved += HandleResolved;
    }

    static void HandleResolved() => Begin(anchorRewindPoint: true);

    static void CreateInstance()
    {
        var root = new GameObject("HouseEscapePressureController [Auto]");
        DontDestroyOnLoad(root);
        _instance = root.AddComponent<HouseEscapePressureController>();
    }

    void Awake()
    {
        // 씬에 직접 배치한 경우
        // ⚠ Destroy(gameObject) 를 쓰면 안 된다. 수치 조정용으로 다른 매니저와 같은 GO 에
        //    얹어 두면 그 GO 가 통째로 날아간다.
        if (_instance != null && _instance != this) { SingletonGuard.DestroyDuplicate(this); return; }
        _instance = this;
    }

    void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != SceneNames.Home)
        {
            // 집을 벗어나면 압박은 성립하지 않는다.
            if (_active) Stop();
            return;
        }

        // ⚠ 배드 엔딩 연출이 도는 중이면 재개하지 않는다. BE#02 는 마을에서 발각된 뒤
        //    집으로 넘어와 BE#02-b·c 를 재생하는데, 그때도 이 핸들러가 돈다.
        //    막지 않으면 엔딩을 보는 동안 90초 타이머가 다시 시작된다.
        if (BadEndingDirector.IsPlaying) return;

        // ⚠ 현관문을 이미 통과했으면 재개하지 않는다. 압박 구간은 현관문까지다(F-6 문단 788).
        //    마당 정문 앞에 세이브 포인트가 있으므로(C-13-2), 막지 않으면 그 파일을 불러올 때마다
        //    마당에서 90초가 다시 시작돼 깰 수 없는 파일이 된다(C-13-2 문단 965).
        if (GameState.isFrontDoorPassed) return;

        // 집으로 돌아왔다 — 배드 엔딩 후 되감기 복귀이거나, S#11 이후 지점을 불러온 경우다.
        // 컷씬은 이미 지나갔으므로 OnResolved 가 다시 발행되지 않는다. 여기서 다시 걸지 않으면
        // 실패한 플레이어가 제한 시간 없이 걸어 나가게 된다.
        if (!_active && GameState.isResolved)
            StartCoroutine(ResumeAfterRestore());
    }

    /// <summary>
    /// 저장 복원이 끝난 뒤에 재개합니다. 되감기 지점은 다시 찍지 않습니다 —
    /// 원래의 S#11 직후 스냅샷을 그대로 유지해야 몇 번을 실패해도 같은 자리로 돌아온다.
    /// </summary>
    System.Collections.IEnumerator ResumeAfterRestore()
    {
        // 플레이어 스폰·위치 복원이 끝날 때까지 기다린다.
        float guard = 0f;
        while (GameObject.FindGameObjectWithTag("Player") == null && guard < 5f)
        {
            guard += Time.unscaledDeltaTime;
            yield return null;
        }
        yield return null;

        if (BadEndingDirector.IsPlaying) yield break;
        if (GameState.isFrontDoorPassed) yield break;

        if (!_active && GameState.isResolved)
            Begin(anchorRewindPoint: false);
    }

    // ── 공개 API ─────────────────────────────────────────────────────────────
    /// <summary>압박을 시작합니다.</summary>
    /// <param name="anchorRewindPoint">
    /// true 면 지금 지점을 되감기 지점으로 찍습니다(= S#11 직후, 컷씬 종료 시점).
    /// 되감기 복귀 후 재개할 때는 false 로 불러 원래 스냅샷을 보존합니다.
    /// </param>
    public static void Begin(bool anchorRewindPoint = true)
    {
        var c = Instance;
        if (c._active) return;

        // 배드 엔딩 후 돌아올 자리. 체크포인트는 대화 종료마다 덮어써지므로 전용 키를 쓴다.
        if (anchorRewindPoint) SaveManager.Instance?.SaveRewindPoint();

        c._active  = true;
        c._elapsed = 0f;
        c.ResetStageState();
        c.StartDrone();   // 볼륨 0 으로 시작한다. 소리는 1차(20초)에 들어온다.
        Dbg.Log("[탈출압박] 집 구간 시작 — 제한 " + c.timeLimit + "초");

        // 다락방 문 잠금 등 발동 즉시 처리 (C-14-2-3).
        OnPressureBegan?.Invoke();
    }

    /// <summary>
    /// 현관문을 통과했습니다. 타이머와 전 연출을 즉시 해제합니다
    /// (F-6 「타이머·조임 정지 — 현관문 통과」 · C-14-2-2 문단 1060).
    ///
    /// ⚠ 정문이 아니라 현관문이다. 「출구는 정문이지만 제한 시간은 현관문에서 끝난다」(F-6 문단 788).
    /// ⚠ 단계적으로 풀지 않는다. 서서히 풀면 조임이 실외까지 이어지는 것으로 읽힌다(문단 1064).
    /// </summary>
    public static void NotifyEscaped()
    {
        if (_instance == null || !_instance._active) return;
        Dbg.Log("[탈출압박] 현관문 통과 — 해제");
        _instance.Stop();
    }

    /// <summary>압박을 즉시 중단하고 모든 연출을 걷어냅니다.</summary>
    public void Stop()
    {
        if (!_active) return;
        _active = false;
        _elapsed = 0f;
        StopDrone();
        AudioManager.BgmDuck = 1f;          // 전역 상태다. 켠 쪽이 되돌린다.
        ScreenEdgeEffectController.ClearSustained();
        ResetStageState();
        OnLevelChanged?.Invoke(0f);
        OnPressureEnded?.Invoke();
    }

    /// <summary>단계와 연출값을 발동 전 상태로 되돌립니다.</summary>
    void ResetStageState()
    {
        _stage      = 0;
        _transition = 1f;
        _edgeAlpha  = _fromEdgeAlpha  = 0f;
        _edgeRatio  = _fromEdgeRatio  = 0f;
        _shrink     = _fromShrink     = 0f;
        _droneLevel = _fromDroneLevel = 0f;
        _cutoff     = _fromCutoff     = TargetDroneCutoff(1);
    }

    // ── 진행 ─────────────────────────────────────────────────────────────────
    void Update()
    {
        if (!_active) return;

        // 대화·컷씬 중에는 세지 않는다. S#12 단검 컷씬이 S#11 바로 뒤에 이어지므로
        // 실질적으로 플레이어가 조작권을 되찾은 시점부터 세게 된다.
        bool paused = YarnDialogue.IsRunning
                   || (PlayerInputLock.Instance != null && PlayerInputLock.Instance.IsLocked);

        // Time.deltaTime 이므로 일시정지(timeScale 0)에서도 자동으로 멈춘다.
        if (!paused) _elapsed += Time.deltaTime;

        // ── 단계 판정 ────────────────────────────────────────────────────────
        // 값이 바뀌는 것은 여기 한 곳뿐이다. 임계를 넘지 않는 동안에는
        // _edgeAlpha · _shrink · _droneLevel 이 그대로 유지된다 (C-14-2-1).
        int stage = StageAt(_elapsed);
        if (stage != _stage) EnterStage(stage);

        AdvanceTransition();
        ApplyStageValues();

        if (_elapsed >= timeLimit) StartCoroutine(FailRoutine());
    }

    /// <summary>
    /// 단계를 올리고 전환을 시작합니다. 전환은 <see cref="stageTransitionDuration"/> 안에 끝나며
    /// 그 뒤로는 다음 임계까지 정지합니다.
    /// </summary>
    void EnterStage(int stage)
    {
        _stage          = stage;
        _fromEdgeAlpha  = _edgeAlpha;
        _fromEdgeRatio  = _edgeRatio;
        _fromShrink     = _shrink;
        _fromDroneLevel = _droneLevel;
        _fromCutoff     = _cutoff;
        _transition     = stageTransitionDuration > 0f ? 0f : 1f;

        Dbg.Log($"[탈출압박] {stage}차 조임 — 경과 {_elapsed:F1}초");
    }

    void AdvanceTransition()
    {
        if (_transition >= 1f) return;   // 단계 사이 — 아무것도 변하지 않는다

        _transition = Mathf.Clamp01(_transition + Time.deltaTime / Mathf.Max(0.0001f, stageTransitionDuration));

        _edgeAlpha  = Mathf.Lerp(_fromEdgeAlpha,  TargetEdgeAlpha(_stage),  _transition);
        _edgeRatio  = Mathf.Lerp(_fromEdgeRatio,  TargetEdgeRatio(_stage),  _transition);
        _shrink     = Mathf.Lerp(_fromShrink,     TargetShrink(_stage),     _transition);
        _droneLevel = Mathf.Lerp(_fromDroneLevel, TargetDroneLevel(_stage), _transition);
        _cutoff     = Mathf.Lerp(_fromCutoff,     TargetDroneCutoff(_stage), _transition);

        // 공간 — 전환 중에만 알린다. 단계 사이에는 구독자를 부르지 않는다.
        OnLevelChanged?.Invoke(_shrink);
    }

    /// <summary>
    /// 현재 단계값을 각 채널에 적용합니다. 값 자체는 전환 중에만 바뀌므로 매 프레임 불러도
    /// 이산 발동이 깨지지 않습니다 — 다른 연출이 지운 지속형 오버레이를 다시 세우는 역할을 겸합니다.
    /// </summary>
    void ApplyStageValues()
    {
        // 화면 — ⚠ 접근성 설정으로 꺼질 수 있는 채널이다. 저음이 정보를 중복해서 전달한다.
        ScreenEdgeEffectController.SetSustainedLevel(vignetteColor, _edgeAlpha, _edgeRatio);

        // 저음 — 밖에서 안으로 밀려드는 소리. 설정 볼륨을 매 프레임 반영하되
        // AudioManager 의 카테고리 풀에는 등록하지 않는다(ApplyVolume 이 개별 볼륨을 리셋한다).
        if (_drone != null)
        {
            float ambient = SettingsManager.Instance != null ? SettingsManager.Instance.ambientVolume : 1f;
            _drone.volume = ambient * _droneLevel;
        }

        // 배음을 여는 쪽 (F-6 문단 792). 볼륨과 함께 움직이되 값 자체는 단계 사이에 변하지 않는다.
        if (_droneLowPass != null) _droneLowPass.cutoffFrequency = _cutoff;

        // BGM 을 물린다 (F-6 문단 732 「그 외 BGM은 낮춘다」). 드론 세기에 비례하므로 단계 사이에는 값이 변하지 않는다.
        float ratio = droneMaxVolume > 0.0001f ? Mathf.Clamp01(_droneLevel / droneMaxVolume) : 0f;
        AudioManager.BgmDuck = Mathf.Lerp(1f, bgmDuckAtStage4, ratio);
    }

    System.Collections.IEnumerator FailRoutine()
    {
        Dbg.Log("[탈출압박] 90초 경과 — 현관문 영구 폐쇄");
        _active = false;

        // 문이 닫히는 것을 느낄 시간을 준다. 곧바로 씬을 넘기면 아무 일도 없이 화면만 바뀐다.
        var ctrl = YarnDialogue.LockPlayer();

        // 현관문이 완전히 닫힌다. 열쇠가 통하지 않는다 (C-14-2).
        var door = FindAnyObjectByType<FrontDoorInteraction>();
        if (door != null) door.SealPermanently();

        // 조임이 끝까지 간 상태(4차)를 한 박자 유지한다. 전환 중에 시간이 다했더라도
        // 여기서 4차 목표값으로 맞춰 둔다 — 실패는 항상 끝까지 조인 그림이어야 한다.
        _stage      = 4;
        _transition = 1f;
        _edgeAlpha  = TargetEdgeAlpha(4);
        _edgeRatio  = TargetEdgeRatio(4);
        _shrink     = TargetShrink(4);
        _droneLevel = TargetDroneLevel(4);
        _cutoff     = TargetDroneCutoff(4);
        OnLevelChanged?.Invoke(_shrink);
        ApplyStageValues();

        yield return new WaitForSeconds(failLingerDuration);

        StopDrone();
        AudioManager.BgmDuck = 1f;          // 실패 경로에서도 되돌린다. 배드 엔딩이 BGM 을 쓴다.
        YarnDialogue.UnlockPlayer(ctrl);

        // ⚠ 조임을 여기서 걷지 않는다. D-BE#01-a 문단 463 이 「가장자리 어두워짐 · 복도 축소 ·
        //   문틀 좁아짐이 끝까지 갔다가 암전으로 닫힌다」로 못박았다. 조임이 풀린 뒤에 암전이 오면
        //   그 연결이 끊긴다. 화면(ClearSustained)과 공간(OnLevelChanged) 둘 다 해당한다.
        //
        //   ⛔ 2026-09-05 이전에는 여기서 OnLevelChanged(0f) 와 ResetStageState() 를 불러
        //      복도 축소·문틀 좁아짐만 암전 직전에 원래 크기로 되돌아갔다. 바로 아래 주석이
        //      금지한 것을 같은 함수 안에서 하고 있었다. 되돌리지 말 것.

        // 인형화 페널티 없음 (CLAUDE.md §2).
        // 정본 BE#01-a~d 컷씬을 재생한 뒤 디렉터가 TriggerBadEnding 까지 처리한다.
        yield return BadEndingDirector.PlayHouseSealed();

        // 암전이 끝난 뒤에야 공간을 되돌린다. 되감기로 S#11 직후에 다시 들어오므로
        // 여기서 반드시 풀어야 다음 발동이 기준값부터 시작한다.
        OnLevelChanged?.Invoke(0f);
        ResetStageState();
    }

    // ── 저음 드론 ────────────────────────────────────────────────────────────
    void StartDrone()
    {
        if (_drone == null)
        {
            _drone = gameObject.AddComponent<AudioSource>();
            _drone.loop         = true;
            _drone.playOnAwake  = false;
            _drone.spatialBlend = 0f;
        }

        // F-6 문단 792 — 단계 상승을 볼륨만으로 만들지 않는다. 컷오프를 함께 연다.
        // ⚠ AudioLowPassFilter 는 같은 GameObject 의 AudioSource 에만 걸린다. 드론과 같은 GO 여야 한다.
        if (_droneLowPass == null)
        {
            var existing = gameObject.GetComponent<AudioLowPassFilter>();
            _droneLowPass = existing != null ? existing : gameObject.AddComponent<AudioLowPassFilter>();
            _droneLowPass.lowpassResonanceQ = 1f;
        }
        _droneLowPass.cutoffFrequency = _cutoff > 0f ? _cutoff : TargetDroneCutoff(1);

        AudioClip clip = null;
        if (!string.IsNullOrEmpty(droneSoundName))
            clip = Resources.Load<AudioClip>(droneSoundName);

        // 등록된 클립이 없으면 절차 생성 사인파를 쓴다.
        // 이렇게 하면 아트·사운드 에셋을 기다리지 않고도 '설정으로 끌 수 없는' 채널이 확보된다.
        _drone.clip   = clip != null ? clip : BuildDroneClip();
        _drone.volume = 0f;
        _drone.Play();
    }

    void StopDrone()
    {
        if (_drone != null) _drone.Stop();
    }

    /// <summary>
    /// 절차 생성 저음을 만듭니다 (F-6 「기본 대역 80~120Hz · 순음을 쓰지 않는다」).
    ///
    /// ⚠ <b>사인파 하나로 만들지 말 것.</b> 순음은 '밖에서 밀려드는 소리'가 아니라
    ///   '기기가 내는 신호음'으로 들린다. 대역 안에 성분을 흩고 위상을 어긋나게 해야 한다.
    ///
    /// ⚠ 성분 주파수를 <b>버퍼 기본 주파수의 정수배로 반올림</b>한다. 그래야 모든 성분이
    ///   버퍼 끝에서 같은 위상으로 돌아와 루프 이음매에 딱 소리가 나지 않는다.
    ///   버퍼가 2초면 기본 주파수가 0.5Hz 라 반올림 오차가 최대 0.25Hz 뿐이고 대역을 벗어나지 않는다.
    /// </summary>
    AudioClip BuildDroneClip()
    {
        var data = BuildDroneSamples(DroneRate);
        var clip = AudioClip.Create("EscapePressureDrone", data.Length, 1, DroneRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>절차 생성 저음의 표본율.</summary>
    public const int DroneRate = 44100;

    /// <summary>
    /// 저음 파형을 만듭니다. <see cref="BuildDroneClip"/> 이 이것을 감쌉니다.
    ///
    /// ⚠ <b>클립 생성과 분리해 둔 이유</b> — 배치모드는 오디오 장치가 없어
    ///   <c>AudioSettings.outputSampleRate</c> 가 0 이고 <c>AudioClip.Create</c> 가 빈 클립을 만든다.
    ///   그래서 클립을 거치면 파형을 검증할 방법이 없다. 이쪽은 장치와 무관하게 돈다.
    /// </summary>
    public float[] BuildDroneSamples(int rate)
    {
        const float loopSeconds = 2f;

        int   samples = Mathf.RoundToInt(rate * loopSeconds);
        float f0      = 1f / loopSeconds;                 // 버퍼 기본 주파수
        float lo      = Mathf.Min(droneBandLowHz, droneBandHighHz);
        float hi      = Mathf.Max(droneBandLowHz, droneBandHighHz);
        int   count   = Mathf.Max(3, dronePartials);      // 3 미만은 순음에 가까워진다

        var data = new float[samples];

        // ⚠ UnityEngine.Random 을 쓰지 않는다. 실행할 때마다 소리가 달라지면 검증을 못 한다.
        var rng = new System.Random(20260904);

        int harmonics = Mathf.Clamp(droneHarmonics, 1, 6);

        for (int p = 0; p < count; p++)
        {
            float u  = (float)p / (count - 1);
            float hz = Mathf.Round(Mathf.Lerp(lo, hi, u) / f0) * f0;

            // 낮은 쪽을 두껍게 준다. 위로 갈수록 얇아져야 '저음'으로 읽힌다.
            double amp0 = Mathf.Lerp(1f, 0.35f, u) * (0.7 + 0.3 * rng.NextDouble());

            // 대역 위쪽 배음. 이것이 없으면 120Hz 위에 아무것도 없어서 로우패스를 열어도
            // 드러날 것이 없고, F-6 문단 792 가 무효가 된다.
            // ⚠ hz 가 이미 f0 의 정수배이므로 정수배음도 자동으로 f0 의 정수배다 —
            //   루프 이음매에서 위상이 그대로 돌아온다.
            for (int h = 1; h <= harmonics; h++)
            {
                // ⚠ 1/h 다. 1/h² 로 하면 3·4차 컷오프가 열어도 에너지가 0.97% · 0.16% 뿐이라
                //   「배음이 늘어난다」가 귀에 닿지 않는다(실측). 톱니파와 같은 기울기로 둔다.
                double amp   = amp0 / h;
                double phase = rng.NextDouble() * System.Math.PI * 2.0;
                double w     = 2.0 * System.Math.PI * (hz * h) / rate;

                // 각도가 커지므로 float 로 누적하면 오차가 보인다. double 로 돌린다.
                for (int i = 0; i < samples; i++)
                    data[i] += (float)(System.Math.Sin(w * i + phase) * amp);
            }
        }

        // 성분 수를 바꿔도 체감 크기가 유지되도록 최대 진폭을 맞춘다.
        float peak = 0f;
        for (int i = 0; i < samples; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        if (peak > 0.0001f)
        {
            float k = 0.9f / peak;
            for (int i = 0; i < samples; i++) data[i] *= k;
        }

        return data;
    }
}
