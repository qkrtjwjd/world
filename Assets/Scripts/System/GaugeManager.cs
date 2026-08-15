using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GaugeManager : PersistentSingleton<GaugeManager>
{
    public const float DEFAULT_GAUGE = 30f;

    private const float DollificationDimThreshold  = 81f;
    private const float DollificationGaugeDecrease =  3f;
    private const float SliderDimmedAlpha          = 0.4f;

    [Header("게이지 값")]
    [Range(0f, 100f)] public float fantasyRealityGauge = DEFAULT_GAUGE;

    public float dollificationGauge =>
        CorruptionManager.Instance != null ? CorruptionManager.Instance.currentCorruption : 20f;

    [Header("균열 이벤트")]
    public float crackEventCooldown = 30f;

    [Header("UI 연결 (Inspector)")]
    public Image       fantasyEdgeImage; // ForceFantasyMax 시 가장자리 뽀얀 이펙트용

    [Header("페이드 대상")]
    [Tooltip("슬라이더와 텍스트 레이블을 함께 감싸는 루트 CanvasGroup.\n설정하면 텍스트도 게이지와 함께 페이드됩니다.")]
    public CanvasGroup gaugeRootGroup;

    [Header("효과음")]
    [SerializeField] private AudioClip sfxGlitch;
    [SerializeField] private AudioClip sfxPop;

    public event System.Action<float> OnGaugeChanged;
    public event System.Action<bool>  OnVisibilityChanged;
    public event System.Action<bool>  OnWarningShakeChanged;

    [Header("UI 표시 상태")]
    public bool isGaugeVisible = false;

    [Header("페이드인 시간")]
    public float showFadeDuration = 1.5f;

    [Header("임시 강제 복원")]
    [Tooltip("단검 선택(전투 진입) 후 이전 게이지로 복원하기까지의 대기 시간(초)")]
    public float tempForceDuration = 5f;

    [Header("맵 단검 픽업 시간제한")]
    [Tooltip("맵에서 단검 픽업 후 현실 100% 유지 시간(초). 이후 이전 게이지로 복원됩니다.")]
    public float mapDaggerDuration = 60f;

    private float       _crackCooldownRemaining;
    private Coroutine   _forceReturnCoroutine;
    private Coroutine   _edgeEffectCoroutine;
    private Coroutine   _showCoroutine;
    private Coroutine   _tempForceCoroutine;
    private CanvasGroup _edgeCanvasGroup;
    private float       _savedGaugeBeforeForce;

    // WorldObject 일괄 갱신용 등록 목록
    private static readonly List<WorldObject> _worldObjects = new List<WorldObject>();

    public static void RegisterWorldObject(WorldObject obj)   => _worldObjects.Add(obj);
    public static void UnregisterWorldObject(WorldObject obj) => _worldObjects.Remove(obj);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        _worldObjects.Clear();
    }

    // ──────────────────────────────────────────
    //  생명주기
    // ──────────────────────────────────────────
    protected override void OnAwake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        // Inspector 직렬화 값이 덮어쓰는 것을 막고 항상 DEFAULT_GAUGE로 시작
        fantasyRealityGauge = DEFAULT_GAUGE;

        // 설정: 환상/현실 게이지 가시성 이벤트 구독
        SettingsManager.OnShowFantasyRealityGaugeChanged += OnFantasyGaugeVisibilityChanged;
    }

    void Start()
    {
        var corruption = CorruptionManager.Instance;
        if (corruption != null)
            corruption.OnCorruptionChanged += OnDollificationChanged;

        if (fantasyEdgeImage != null)
        {
            _edgeCanvasGroup = fantasyEdgeImage.GetComponent<CanvasGroup>();
            if (_edgeCanvasGroup == null)
                _edgeCanvasGroup = fantasyEdgeImage.gameObject.AddComponent<CanvasGroup>();
            _edgeCanvasGroup.alpha = 0f;
        }

        // 설정에서 게이지 가시성 반영
        bool gaugeAllowed = SettingsManager.Instance?.showFantasyRealityGauge ?? true;

        string scene = SceneManager.GetActiveScene().name;
        if (IsGameplayScene(scene) && gaugeAllowed)
            ShowGauge();
        else
            SetSliderAlpha(0f);
    }

    protected override void OnDestroy()
    {
        SettingsManager.OnShowFantasyRealityGaugeChanged -= OnFantasyGaugeVisibilityChanged;
        if (Instance == this)
        {
            var corruption = CorruptionManager.Instance;
            if (corruption != null)
                corruption.OnCorruptionChanged -= OnDollificationChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
        base.OnDestroy();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 설정에서 게이지 표시를 꺼둔 경우 씬 이동 후에도 숨김 유지
        bool gaugeAllowed = SettingsManager.Instance?.showFantasyRealityGauge ?? true;
        if (IsGameplayScene(scene.name) && gaugeAllowed)
        {
            if (!isGaugeVisible) ShowGauge();
        }
        else
        {
            HideGauge();
        }
    }

    static bool IsGameplayScene(string name) =>
        name == SceneNames.Map        ||
        name == SceneNames.DarkReality ||
        name == SceneNames.Battle     ||
        name == SceneNames.Shelter;

    void Update()
    {
        if (_crackCooldownRemaining > 0f)
            _crackCooldownRemaining -= Time.deltaTime;
    }

    // ──────────────────────────────────────────
    //  UI 표시/숨김
    // ──────────────────────────────────────────
    public void ShowGauge()
    {
        if (_showCoroutine != null) StopCoroutine(_showCoroutine);
        _showCoroutine = StartCoroutine(FadeInRoutine());
    }

    public void HideGauge()
    {
        if (_showCoroutine != null) StopCoroutine(_showCoroutine);
        isGaugeVisible = false;
        SetSliderAlpha(0f);
        OnVisibilityChanged?.Invoke(false);
    }

    /// <summary>설정 메뉴에서 환상/현실 게이지 표시 토글 시 호출됩니다.</summary>
    void OnFantasyGaugeVisibilityChanged(bool visible)
    {
        string scene = SceneManager.GetActiveScene().name;
        if (visible && IsGameplayScene(scene))
            ShowGauge();
        else
            HideGauge();
    }

    IEnumerator FadeInRoutine()
    {
        float elapsed = 0f;
        while (elapsed < showFadeDuration)
        {
            elapsed += Time.deltaTime;
            SetSliderAlpha(Mathf.Clamp01(elapsed / showFadeDuration));
            yield return null;
        }
        SetSliderAlpha(1f);
        isGaugeVisible = true;
        OnVisibilityChanged?.Invoke(true);
    }

    // ──────────────────────────────────────────
    //  게이지 변경
    // ──────────────────────────────────────────
    public void ChangeGauge(float amount)
    {
        SetGauge(fantasyRealityGauge + amount);
    }

    public void SetGaugeValue(float value)
    {
        SetGauge(value);
    }

    void SetGauge(float value)
    {
        float prev = fantasyRealityGauge;
        fantasyRealityGauge = Mathf.Clamp(value, 0f, 100f);

        if (Mathf.Approximately(prev, fantasyRealityGauge)) return;

        OnGaugeChanged?.Invoke(fantasyRealityGauge);
        NotifyWorldObjects(false);
        CheckCrackEvent();
    }

    // ──────────────────────────────────────────
    //  강제 이동
    // ──────────────────────────────────────────
    public void ForceRealityMax()
    {
        if (_forceReturnCoroutine != null)
            StopCoroutine(_forceReturnCoroutine);
        _forceReturnCoroutine = null;

        fantasyRealityGauge = 100f;
        OnGaugeChanged?.Invoke(fantasyRealityGauge);
        NotifyWorldObjects(true);   // 즉시 교체
        CheckCrackEvent();

        AudioManager.Instance?.Play(sfxGlitch);

        if (dollificationGauge >= 81f)
            _forceReturnCoroutine = StartCoroutine(ForceReturnToValue(100f, 0f, 0.5f));
    }

    public void ForceFantasyMax()
    {
        if (_forceReturnCoroutine != null)
            StopCoroutine(_forceReturnCoroutine);
        _forceReturnCoroutine = null;

        fantasyRealityGauge = 0f;
        OnGaugeChanged?.Invoke(fantasyRealityGauge);
        NotifyWorldObjects(true);   // 즉시 교체

        AudioManager.Instance?.Play(sfxPop);

        if (_edgeEffectCoroutine != null)
            StopCoroutine(_edgeEffectCoroutine);
        _edgeEffectCoroutine = StartCoroutine(EdgeEffectRoutine());
    }

    /// <summary>단검 선택: 현실 100% 강제 후 tempForceDuration 초 뒤 이전 게이지로 복원.</summary>
    public void ForceTempReality()
    {
        if (YarnDialogue.IsRunning) return;
        _savedGaugeBeforeForce = fantasyRealityGauge;
        ForceRealityMax();
        if (_tempForceCoroutine != null) StopCoroutine(_tempForceCoroutine);
        _tempForceCoroutine = StartCoroutine(RestoreGaugeAfterDelay(_savedGaugeBeforeForce, tempForceDuration));
    }

    /// <summary>
    /// 핵앤슬래시 전투 시작 시 호출. 현실 100% 강제 후,
    /// idleDuration 초 동안 아무 공격도 없으면 이전 게이지로 복원합니다.
    /// 공격이 발생하면 idle 타이머를 리셋합니다.
    /// </summary>
    public void ForceCombatReality(float idleDuration)
    {
        _savedGaugeBeforeForce = fantasyRealityGauge;

        // 게이지를 100으로 올릴 때 경계 돌파 이벤트가 발생하지 않도록 구간 미리 동기화
        GaugeBoundaryMonitor.Instance?.SilentSetZone(100f);

        ForceRealityMax();

        // dollification 복귀 코루틴이 100→0으로 되돌리지 않도록 중단
        if (_forceReturnCoroutine != null)
        {
            StopCoroutine(_forceReturnCoroutine);
            _forceReturnCoroutine = null;
        }

        if (_tempForceCoroutine != null) StopCoroutine(_tempForceCoroutine);
        _tempForceCoroutine = StartCoroutine(RestoreGaugeAfterIdleTime(_savedGaugeBeforeForce, idleDuration));
    }

    IEnumerator RestoreGaugeAfterIdleTime(float targetGauge, float idleDuration)
    {
        float idleTimer = 0f;
        float prevActivityTime = HackSlashCombatManager.Instance != null
            ? HackSlashCombatManager.Instance.LastCombatActivityTime : -999f;

        while (idleTimer < idleDuration)
        {
            yield return null;

            // 전투 종료 시 즉시 복원
            if (!HackSlashCombatManager.IsActive)
            {
                SetGauge(targetGauge);
                _tempForceCoroutine = null;
                yield break;
            }

            var _hsm = HackSlashCombatManager.Instance;
            if (_hsm == null) yield break;
            float currentTime = _hsm.LastCombatActivityTime;
            if (!Mathf.Approximately(currentTime, prevActivityTime))
            {
                // 새 공격 발생 → idle 타이머 리셋
                idleTimer = 0f;
                prevActivityTime = currentTime;
            }
            else
            {
                idleTimer += Time.deltaTime;
            }
        }

        _tempForceCoroutine = null;
        SetGauge(targetGauge);
    }

    /// <summary>마시멜로 선택: 환상 100% 강제 후 복원 없이 유지.</summary>
    public void ForceTempFantasy()
    {
        if (YarnDialogue.IsRunning) return;
        if (_tempForceCoroutine != null) { StopCoroutine(_tempForceCoroutine); _tempForceCoroutine = null; }
        ForceFantasyMax();
    }

    /// <summary>맵에서 단검 픽업 시: 현실 100% 강제 후 mapDaggerDuration 초 뒤 이전 게이지로 복원.</summary>
    public void ForceMapDaggerPickup()
    {
        if (YarnDialogue.IsRunning) return;
        _savedGaugeBeforeForce = fantasyRealityGauge;
        ForceRealityMax();
        if (_tempForceCoroutine != null) StopCoroutine(_tempForceCoroutine);
        _tempForceCoroutine = StartCoroutine(RestoreGaugeAfterDelay(_savedGaugeBeforeForce, mapDaggerDuration));
    }

    IEnumerator RestoreGaugeAfterDelay(float targetGauge, float delay)
    {
        yield return new WaitForSeconds(delay);
        _tempForceCoroutine = null;
        SetGauge(targetGauge);
    }

    IEnumerator ForceReturnToValue(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetGauge(Mathf.Lerp(from, to, elapsed / duration));
            yield return null;
        }
        SetGauge(to);
    }

    IEnumerator EdgeEffectRoutine()
    {
        if (_edgeCanvasGroup == null) yield break;

        float half = 0.25f;
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            _edgeCanvasGroup.alpha = t / half;
            yield return null;
        }
        _edgeCanvasGroup.alpha = 1f;
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            _edgeCanvasGroup.alpha = 1f - (t / half);
            yield return null;
        }
        _edgeCanvasGroup.alpha = 0f;
    }

    // ──────────────────────────────────────────
    //  트리거
    // ──────────────────────────────────────────
    /// <summary>triggerId 문자열로 게이지 트리거를 적용합니다. GaugeTriggerRegistry를 참조합니다.</summary>
    public void ApplyTrigger(string triggerId)
    {
        if (!GaugeTriggerRegistry.TryGetAmount(triggerId, out float amount))
        {
            Debug.LogWarning($"[GaugeManager] ApplyTrigger: '{triggerId}' 트리거를 찾지 못했습니다.");
            return;
        }
        ChangeGauge(amount);
    }


    // ──────────────────────────────────────────
    //  인형화 연동
    // ──────────────────────────────────────────
    void OnDollificationChanged(float delta)
    {
        if (delta > 0f)
            ChangeGauge(-DollificationGaugeDecrease);

        if (isGaugeVisible)
            SetSliderAlpha(dollificationGauge >= DollificationDimThreshold ? SliderDimmedAlpha : 1f);
    }

    // ──────────────────────────────────────────
    //  균열 이벤트
    // ──────────────────────────────────────────
    void CheckCrackEvent()
    {
        if (fantasyRealityGauge > 0f && fantasyRealityGauge <= 10f && _crackCooldownRemaining <= 0f)
        {
            _crackCooldownRemaining = crackEventCooldown;
            CrackEventManager.TriggerCrackEvent();
        }
    }

    // ──────────────────────────────────────────
    //  WorldObject 일괄 알림
    // ──────────────────────────────────────────
    void NotifyWorldObjects(bool immediate)
    {
        foreach (var wo in _worldObjects)
        {
            if (wo != null)
                wo.UpdateSprite(fantasyRealityGauge, immediate);
        }
    }

    // ──────────────────────────────────────────
    //  경고 흔들림 (연속)
    // ──────────────────────────────────────────
    public void SetWarningShake(bool active)
    {
        OnWarningShakeChanged?.Invoke(active);
    }

    void SetSliderAlpha(float alpha)
    {
        if (gaugeRootGroup != null)
            gaugeRootGroup.alpha = alpha;
    }
}
