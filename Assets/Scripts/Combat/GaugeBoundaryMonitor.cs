using System.Collections;
using UnityEngine;

/// <summary>
/// GaugeManager 게이지가 구간 경계(Fantasy/Glitch/Reality)를 넘을 때
/// 전투 중이면 강제 모드 전환 및 예고 연출을 실행합니다.
///
/// 구간: 0~30 Fantasy / 31~69 Glitch / 70~100 Reality
/// </summary>
public class GaugeBoundaryMonitor : MonoBehaviour
{
    public static GaugeBoundaryMonitor Instance { get; private set; }

    [Header("구간 경계 임계값")]
    public float fantasyGlitchBoundary = 30f;  // 이하: Fantasy / 초과: Glitch
    public float glitchRealityBoundary = 70f;  // 미만: Glitch / 이상: Reality
    public float warningDistance       = 2.8125f;   // 경계 ± 이내에서 경고 연출 시작

    [Header("경계 돌파 글리치 연출")]
    public float        glitchDuration = 0.5f;
    public GlitchPreset glitchPreset   = GlitchManager.PresetStrong;

    [Header("BGM 노이즈 (경계 접근 시 볼륨 상승)")]
    public AudioSource noiseSource;
    public float       maxNoiseVolume  = 0.4f;
    public float       noiseBlendSpeed = 4f;

    public static float FantasyBoundary => Instance != null ? Instance.fantasyGlitchBoundary : 30f;
    public static float RealityBoundary => Instance != null ? Instance.glitchRealityBoundary : 70f;

    private enum GaugeZone { Fantasy, Glitch, Reality }
    private GaugeZone _currentZone;
    private bool      _isTransitioning;
    private bool      _realityThresholdGlitchFired;

    private WaitForSeconds _glitchWait;
    private float          _cachedGlitchDuration;

    // ──────────────────────────────────────────
    //  생명주기
    // ──────────────────────────────────────────
    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        _cachedGlitchDuration = glitchDuration;
        _glitchWait           = new WaitForSeconds(glitchDuration);

        if (GaugeManager.Instance == null) return;
        float initial = GaugeManager.Instance.fantasyRealityGauge;
        _currentZone = GetZone(initial);
        _realityThresholdGlitchFired = initial >= glitchRealityBoundary;
        GaugeManager.Instance.OnGaugeChanged += OnGaugeChanged;
    }

    void OnDisable()
    {
        StopAllCoroutines();
        _isTransitioning = false;
    }

    void OnDestroy()
    {
        if (GaugeManager.Instance != null)
            GaugeManager.Instance.OnGaugeChanged -= OnGaugeChanged;
    }

    // ──────────────────────────────────────────
    //  게이지 변화 처리
    // ──────────────────────────────────────────
    void OnGaugeChanged(float gauge)
    {
        if (gauge < glitchRealityBoundary)
        {
            _realityThresholdGlitchFired = false;
        }
        else if (!_realityThresholdGlitchFired)
        {
            _realityThresholdGlitchFired = true;
            if (!IsInBattle())
                GlitchManager.Instance?.PlayGlitch(glitchDuration, glitchPreset);
        }

        UpdateWarningEffects(gauge);

        if (_isTransitioning) return;

        GaugeZone newZone = GetZone(gauge);
        if (newZone == _currentZone) return;

        GaugeZone prevZone = _currentZone;
        _currentZone = newZone;

        if (IsInBattle())
            StartCoroutine(HandleZoneCrossing(prevZone, newZone));
    }

    // ──────────────────────────────────────────
    //  경고 연출 (경계 5 이내 접근)
    // ──────────────────────────────────────────
    void UpdateWarningEffects(float gauge)
    {
        float dist = Mathf.Min(
            Mathf.Abs(gauge - fantasyGlitchBoundary),
            Mathf.Abs(gauge - glitchRealityBoundary)
        );
        bool isWarning = dist < warningDistance;

        // 슬라이더 흔들림 (GaugeManager → GaugeSliderUI에도 이벤트 전파됨)
        GaugeManager.Instance?.SetWarningShake(isWarning);

        // BGM 노이즈 볼륨
        if (noiseSource != null)
        {
            float targetVol = isWarning ? (1f - dist / warningDistance) * maxNoiseVolume : 0f;
            noiseSource.volume = Mathf.MoveTowards(noiseSource.volume, targetVol,
                                                   Time.deltaTime * noiseBlendSpeed);
            if (targetVol > 0f && !noiseSource.isPlaying) noiseSource.Play();
            else if (targetVol <= 0f && noiseSource.isPlaying) noiseSource.Stop();
        }
    }

    // ──────────────────────────────────────────
    //  강제 전환 흐름
    // ──────────────────────────────────────────
    IEnumerator HandleZoneCrossing(GaugeZone from, GaugeZone to)
    {
        _isTransitioning = true;

        // 경계 돌파 글리치 0.5초
        GlitchManager.Instance?.PlayGlitch(glitchDuration, glitchPreset);
        if (!Mathf.Approximately(_cachedGlitchDuration, glitchDuration))
        {
            _cachedGlitchDuration = glitchDuration;
            _glitchWait           = new WaitForSeconds(glitchDuration);
        }
        yield return _glitchWait;

        // 전환 규칙
        if ((from == GaugeZone.Fantasy && to == GaugeZone.Glitch) ||
            (from == GaugeZone.Reality && to == GaugeZone.Glitch))
        {
            // 선택 UI 없이 현재 모드 유지 — 인게임 단검/마시멜로 버튼으로 전환
            _isTransitioning = false;
            yield break;
        }

        if (from == GaugeZone.Glitch && to == GaugeZone.Fantasy)
            ForceToTurnBased();
        else if (from == GaugeZone.Glitch && to == GaugeZone.Reality)
            ForceToAction();

        _isTransitioning = false;
    }

    // ──────────────────────────────────────────
    //  모드 전환 실행
    // ──────────────────────────────────────────
    void ForceToTurnBased()
    {
        if (HackSlashCombatManager.IsActive)
        {
            // 핵앤슬래시 중에는 마시멜로를 통해서만 턴제 전환 가능 —
            // ForceSwitchToTurnBased가 차단되므로 환상 전환 연출을 먼저 틀면
            // 연출만 나오고 모드는 유지되는 모순이 생김. 힌트로 유도만 한다.
            HintManager.ShowHint("battle_marshmallow_hint",
                                 "환상이 돌아오려 한다... 마시멜로를 먹으면 넘어갈 수 있다.", 4f);
        }
        else if (BattleSystem.IsActive)
        {
            // 이미 턴제 → 비주얼만
            BattleTransitionManager.Instance?.TransitionToFantasy();
        }
    }

    void ForceToAction()
    {
        if (BattleSystem.IsActive)
        {
            // GlitchAndSwitchToHackSlash 내부에 연출 포함 — 별도 TransitionToReality 불필요
            BattleSystem.Instance?.ForceSwitchToHackSlash();
        }
        else if (HackSlashCombatManager.IsActive)
        {
            // 이미 액션 모드 → 비주얼만
            BattleTransitionManager.Instance?.TransitionToReality();
        }
    }

    // ──────────────────────────────────────────
    //  헬퍼
    // ──────────────────────────────────────────

    /// <summary>
    /// 현재 구간을 강제로 지정합니다. 게이지 강제 설정 직전에 호출하면 경계 돌파 이벤트를 방지합니다.
    /// </summary>
    public void SilentSetZone(float gauge)
    {
        _currentZone = GetZone(gauge);
    }

    GaugeZone GetZone(float gauge)
    {
        if (gauge <= fantasyGlitchBoundary) return GaugeZone.Fantasy;
        if (gauge <  glitchRealityBoundary) return GaugeZone.Glitch;
        return GaugeZone.Reality;
    }

    static bool IsInBattle()
        => BattleSystem.IsActive || HackSlashCombatManager.IsActive;
}
