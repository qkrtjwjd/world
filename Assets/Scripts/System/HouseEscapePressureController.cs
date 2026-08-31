using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 집 구간 탈출 압박 — 결계가 조인다 (C-14-2 / F-6 / 근거 A-13-3).
///
/// S#11 에서 루가 "제가 아빠 데리러 갈게요" 라고 말한 직후 발동해 90초를 센다.
/// 정문을 통과하면 해제, 시간이 다하면 현관문이 영구 폐쇄되고 배드 엔딩 ① 로 간다.
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
    [Tooltip("지속형 비네팅 색. 알파는 아래 상한값으로 대체된다.")]
    public Color vignetteColor = new Color(0.02f, 0.02f, 0.04f);
    [Tooltip("4차(최대) 시점의 알파 상한. ScreenEdgeEffectController 의 Image 는 현재 " +
             "'가장자리'가 아니라 화면 전체 단색이므로 높이면 화면이 통째로 어두워진다.")]
    [Range(0f, 1f)] public float vignetteMaxAlpha = 0.45f;

    [Tooltip("F-6 「집 어두워짐 3단계 — 가장자리 화면비 18% / 30% / 44%」. 2·3·4차에 대응하며 " +
             "1차는 화면을 건드리지 않는다.\n" +
             "⚠ 현재 오버레이가 가장자리 그라디언트가 아니라 전면 단색이라 '화면비'를 그대로 그릴 수 없다. " +
             "세 값의 비율만 알파에 옮기고 44% 를 vignetteMaxAlpha 에 맞춘다. " +
             "가장자리 스프라이트가 나오면 이 값을 폭으로 직접 쓴다.")]
    public Vector3 edgeRatios = new Vector3(0.18f, 0.30f, 0.44f);

    [Header("연출 — 공간 (C-14-2-2)")]
    [Tooltip("F-6 「집 복도 축소 — 3차 −8% · 4차 −14%」. 3차와 4차에만 발생한다.\n" +
             "⚠ 콜라이더와 이동 가능 범위는 바꾸지 않는다. 실제로 좁히면 통행 불가 구간이 생기고 " +
             "그것은 제한 시간이 아니라 벽이 된다 (C-14-2-2).")]
    public float corridorShrinkStage3 = 0.08f;
    public float corridorShrinkStage4 = 0.14f;

    [Header("연출 — 저음")]
    [Tooltip("AudioManager 에 등록된 드론 루프 이름. 비우면 아래 절차용 사인파를 대신 쓴다.")]
    public string droneSoundName = "";
    [Tooltip("절차 생성 드론의 기본 주파수(Hz). 밖에서 안으로 밀려드는 저음.")]
    public float droneHz = 42f;
    [Tooltip("4차 시점의 드론 볼륨 상한 (Ambient 볼륨에 곱해진다). F-6 「1차 진입 · 단계마다 +6dB」 이므로 " +
             "1차는 이 값의 1/8 에서 시작해 단계마다 두 배가 된다.")]
    [Range(0f, 1f)] public float droneMaxVolume = 0.5f;

    /// <summary>
    /// 공간 압박 강도(0~1) 변화 알림. 복도 축소·문틀 좁아짐처럼 씬 오브젝트를 움직이는 연출이
    /// <see cref="EscapePressureShrinker"/> 로 여기에 붙는다.
    ///
    /// 1 은 <see cref="corridorShrinkStage4"/>(−14%) 에 해당한다. 즉 구독자의 눌린 상태는
    /// <b>4차 기준</b>으로 만들어 두면 되고, 3차는 그 비율만큼만 적용된다.
    ///
    /// ⚠ 단계가 바뀔 때만 발행된다. 단계 사이에는 호출되지 않는다 (C-14-2-1).
    /// </summary>
    public static event Action<float> OnLevelChanged;

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
    float _shrink,     _fromShrink;
    float _droneLevel, _fromDroneLevel;

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

    /// <summary>단계별 비네팅 알파. 1차는 화면을 건드리지 않으므로 0 이다 (C-14-2-1).</summary>
    float TargetEdgeAlpha(int stage)
    {
        if (stage <= 1) return 0f;
        float max = Mathf.Max(0.0001f, edgeRatios.z);
        float ratio = stage == 2 ? edgeRatios.x : stage == 3 ? edgeRatios.y : edgeRatios.z;
        return vignetteMaxAlpha * Mathf.Clamp01(ratio / max);
    }

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
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
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
    }

    /// <summary>정문을 통과했습니다. 압박을 해제합니다(C-14-2 성공 경로).</summary>
    public static void NotifyEscaped()
    {
        if (_instance == null || !_instance._active) return;
        Dbg.Log("[탈출압박] 정문 통과 — 해제");
        _instance.Stop();
    }

    /// <summary>압박을 즉시 중단하고 모든 연출을 걷어냅니다.</summary>
    public void Stop()
    {
        if (!_active) return;
        _active = false;
        _elapsed = 0f;
        StopDrone();
        ScreenEdgeEffectController.ClearSustained();
        ResetStageState();
        OnLevelChanged?.Invoke(0f);
    }

    /// <summary>단계와 연출값을 발동 전 상태로 되돌립니다.</summary>
    void ResetStageState()
    {
        _stage      = 0;
        _transition = 1f;
        _edgeAlpha  = _fromEdgeAlpha  = 0f;
        _shrink     = _fromShrink     = 0f;
        _droneLevel = _fromDroneLevel = 0f;
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
        _fromShrink     = _shrink;
        _fromDroneLevel = _droneLevel;
        _transition     = stageTransitionDuration > 0f ? 0f : 1f;

        Dbg.Log($"[탈출압박] {stage}차 조임 — 경과 {_elapsed:F1}초");
    }

    void AdvanceTransition()
    {
        if (_transition >= 1f) return;   // 단계 사이 — 아무것도 변하지 않는다

        _transition = Mathf.Clamp01(_transition + Time.deltaTime / Mathf.Max(0.0001f, stageTransitionDuration));

        _edgeAlpha  = Mathf.Lerp(_fromEdgeAlpha,  TargetEdgeAlpha(_stage),  _transition);
        _shrink     = Mathf.Lerp(_fromShrink,     TargetShrink(_stage),     _transition);
        _droneLevel = Mathf.Lerp(_fromDroneLevel, TargetDroneLevel(_stage), _transition);

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
        ScreenEdgeEffectController.SetSustainedLevel(vignetteColor, _edgeAlpha);

        // 저음 — 밖에서 안으로 밀려드는 소리. 설정 볼륨을 매 프레임 반영하되
        // AudioManager 의 카테고리 풀에는 등록하지 않는다(ApplyVolume 이 개별 볼륨을 리셋한다).
        if (_drone != null)
        {
            float ambient = SettingsManager.Instance != null ? SettingsManager.Instance.ambientVolume : 1f;
            _drone.volume = ambient * _droneLevel;
        }
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
        _shrink     = TargetShrink(4);
        _droneLevel = TargetDroneLevel(4);
        OnLevelChanged?.Invoke(_shrink);
        ApplyStageValues();

        yield return new WaitForSeconds(failLingerDuration);

        StopDrone();
        OnLevelChanged?.Invoke(0f);
        ResetStageState();
        YarnDialogue.UnlockPlayer(ctrl);

        // ⚠ ClearSustained 를 여기서 부르지 않는다. 정본 문단 460 이
        //   「가장자리 어두워짐 · 복도 축소 · 문틀 좁아짐이 끝까지 갔다가 암전으로 닫힌다」로 못박았다.
        //   조임이 걷힌 뒤 암전이 오면 그 연결이 끊긴다. 지우는 것은 BE#01-a 의 암전 안에서
        //   BadEndingDirector 가 한다.

        // 인형화 페널티 없음 (CLAUDE.md §2).
        // 정본 BE#01-a~d 컷씬을 재생한 뒤 디렉터가 TriggerBadEnding 까지 처리한다.
        yield return BadEndingDirector.PlayHouseSealed();
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

    AudioClip BuildDroneClip()
    {
        const int rate = 44100;

        // 루프 이음매에서 딱 소리가 나지 않도록 버퍼 길이를 정수 주기로 맞춘다.
        // 섞는 성분 중 가장 낮은 것이 droneHz/2 이므로 그쪽 주기를 기준으로 삼는다.
        float subHz  = Mathf.Max(1f, droneHz * 0.5f);
        int   cycles = Mathf.Max(1, Mathf.RoundToInt(subHz * 2f));   // 약 2초
        int   samples = Mathf.RoundToInt(rate * cycles / subHz);
        var   data    = new float[samples];

        // 기본 주파수 + 한 옥타브 아래를 살짝 섞어 '밀려드는' 느낌을 만든다.
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / rate;
            data[i] = Mathf.Sin(2f * Mathf.PI * droneHz * t) * 0.7f
                    + Mathf.Sin(2f * Mathf.PI * droneHz * 0.5f * t) * 0.3f;
        }

        var clip = AudioClip.Create("EscapePressureDrone", samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
