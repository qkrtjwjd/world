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
/// ⚠ 금지 사항
///   · UI 타이머를 띄우지 않는다. 90초는 내부 값이다 (CLAUDE.md §7 · F-6)
///   · 실패에 인형화 페널티를 붙이지 않는다. 되감기와 이중 처벌이 된다 (CLAUDE.md §2 · C-14-2)
///   · S#06~S#10 에는 걸지 않는다. 시계가 돌면 아무도 유의 라디오를 안 듣는다 (C-14-2)
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

    [Header("연출 — 화면")]
    [Tooltip("지속형 비네팅 색. 알파는 아래 상한값으로 대체된다.")]
    public Color vignetteColor = new Color(0.02f, 0.02f, 0.04f);
    [Tooltip("경과 100% 시점의 알파 상한. ScreenEdgeEffectController 의 Image 는 현재 " +
             "'가장자리'가 아니라 화면 전체 단색이므로 높이면 화면이 통째로 어두워진다.")]
    [Range(0f, 1f)] public float vignetteMaxAlpha = 0.45f;

    [Header("연출 — 저음")]
    [Tooltip("AudioManager 에 등록된 드론 루프 이름. 비우면 아래 절차용 사인파를 대신 쓴다.")]
    public string droneSoundName = "";
    [Tooltip("절차 생성 드론의 기본 주파수(Hz). 밖에서 안으로 밀려드는 저음.")]
    public float droneHz = 42f;
    [Tooltip("경과 100% 시점의 드론 볼륨 상한 (Ambient 볼륨에 곱해진다).")]
    [Range(0f, 1f)] public float droneMaxVolume = 0.5f;

    /// <summary>
    /// 압박 강도(0~1) 변화 알림. 복도 축소·문틀 좁아짐처럼 씬 오브젝트를 움직이는 연출이
    /// <see cref="EscapePressureShrinker"/> 로 여기에 붙는다.
    /// </summary>
    public static event Action<float> OnLevelChanged;

    // ── 내부 상태 ────────────────────────────────────────────────────────────
    bool        _active;
    float       _elapsed;
    float       _lastLevel = -1f;
    AudioSource _drone;

    /// <summary>압박이 진행 중인지 여부.</summary>
    public static bool IsActive => _instance != null && _instance._active;

    /// <summary>경과 비율 0~1. UI 에 표시하지 말 것 — 디버그·연출 계산 전용이다.</summary>
    public static float Level =>
        _instance == null || !_instance._active ? 0f
        : Mathf.Clamp01(_instance._elapsed / Mathf.Max(0.01f, _instance.timeLimit));

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
        c._lastLevel = -1f;
        c.StartDrone();
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
        _lastLevel = -1f;
        OnLevelChanged?.Invoke(0f);
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

        float level = Mathf.Clamp01(_elapsed / Mathf.Max(0.01f, timeLimit));
        ApplyLevel(level);

        if (_elapsed >= timeLimit) StartCoroutine(FailRoutine());
    }

    void ApplyLevel(float level)
    {
        // 화면 — ⚠ 접근성 설정으로 꺼질 수 있는 채널이다. 저음이 정보를 중복해서 전달한다.
        ScreenEdgeEffectController.SetSustainedLevel(vignetteColor, level * vignetteMaxAlpha);

        // 저음 — 밖에서 안으로 밀려드는 소리. 설정 볼륨을 매 프레임 반영하되
        // AudioManager 의 카테고리 풀에는 등록하지 않는다(ApplyVolume 이 개별 볼륨을 리셋한다).
        if (_drone != null)
        {
            float ambient = SettingsManager.Instance != null ? SettingsManager.Instance.ambientVolume : 1f;
            _drone.volume = ambient * droneMaxVolume * level * level;   // 후반에 급히 차오르게
        }

        // 공간 — 구독자가 있을 때만. 0.01 단위로만 알린다.
        if (Mathf.Abs(level - _lastLevel) >= 0.01f)
        {
            _lastLevel = level;
            OnLevelChanged?.Invoke(level);
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

        // 조임이 끝까지 간 상태를 한 박자 유지한다.
        ScreenEdgeEffectController.SetSustainedLevel(vignetteColor, vignetteMaxAlpha);
        if (_drone != null)
        {
            float ambient = SettingsManager.Instance != null ? SettingsManager.Instance.ambientVolume : 1f;
            _drone.volume = ambient * droneMaxVolume;
        }
        yield return new WaitForSeconds(failLingerDuration);

        StopDrone();
        ScreenEdgeEffectController.ClearSustained();
        OnLevelChanged?.Invoke(0f);
        _lastLevel = -1f;
        YarnDialogue.UnlockPlayer(ctrl);

        // 인형화 페널티 없음 (CLAUDE.md §2).
        EndingManager.TriggerBadEnding(BadEndingType.HouseSealed);
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
