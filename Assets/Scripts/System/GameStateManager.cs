using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CorruptionManager(인형화)와 GaugeManager(환상/현실 게이지)를 구독하여
/// 시나리오 시스템에서 사용할 통합 이벤트 허브 역할을 한다.
/// dollification / psycheGauge 값 변경을 단일 지점에서 감지할 수 있다.
/// </summary>
public class GameStateManager : PersistentSingleton<GameStateManager>
{
    // ── 이벤트 ────────────────────────────────────────────────────────────
    public event System.Action<float>        OnDollificationChanged;
    public event System.Action<float>        OnPsycheGaugeChanged;

    // ── 플래그 딕셔너리 ───────────────────────────────────────────────────
    public Dictionary<string, bool> flags = new Dictionary<string, bool>();

    // ── 프로퍼티 (기존 매니저에서 읽기) ──────────────────────────────────
    public float dollification =>
        CorruptionManager.Instance != null ? CorruptionManager.Instance.currentCorruption : 0f;

    public float psycheGauge =>
        GaugeManager.Instance != null ? GaugeManager.Instance.fantasyRealityGauge : 50f;

    // ── 라이프사이클 ──────────────────────────────────────────────────────
    protected override void OnAwake()
    {
        SubscribeToManagers();
    }

    void Start()
    {
        // Start 시점에 다시 구독 시도 (매니저 초기화 순서 대응)
        SubscribeToManagers();
    }

    protected override void OnDestroy()
    {
        if (CorruptionManager.Instance != null)
            CorruptionManager.Instance.OnCorruptionChanged -= HandleCorruptionChanged;
        if (GaugeManager.Instance != null)
            GaugeManager.Instance.OnGaugeChanged -= HandleGaugeChanged;
        base.OnDestroy();
    }

    void SubscribeToManagers()
    {
        if (CorruptionManager.Instance != null)
        {
            CorruptionManager.Instance.OnCorruptionChanged -= HandleCorruptionChanged;
            CorruptionManager.Instance.OnCorruptionChanged += HandleCorruptionChanged;
        }
        if (GaugeManager.Instance != null)
        {
            GaugeManager.Instance.OnGaugeChanged -= HandleGaugeChanged;
            GaugeManager.Instance.OnGaugeChanged += HandleGaugeChanged;
        }
    }

    // ── 이벤트 핸들러 ────────────────────────────────────────────────────
    void HandleCorruptionChanged(float delta)
    {
        // CorruptionManager는 delta를 발행, 절댓값으로 변환하여 전달
        OnDollificationChanged?.Invoke(dollification);
    }

    void HandleGaugeChanged(float newValue)
    {
        // GaugeManager는 절댓값을 발행
        OnPsycheGaugeChanged?.Invoke(newValue);
    }

    // ── 쓰기 API ─────────────────────────────────────────────────────────
    /// <summary>인형화 수치를 amount만큼 증가/감소시킨다. 음수면 감소.</summary>
    public void AddDollification(float amount)
    {
        if (CorruptionManager.Instance != null)
            CorruptionManager.Instance.AddCorruption(amount);
    }

    /// <summary>환상/현실 게이지를 amount만큼 변경한다. 양수=현실, 음수=환상.</summary>
    public void ChangePsycheGauge(float amount)
    {
        if (GaugeManager.Instance != null)
            GaugeManager.Instance.ChangeGauge(amount);
    }

    /// <summary>환상/현실 게이지를 절댓값으로 설정한다.</summary>
    public void SetPsycheGauge(float value)
    {
        if (GaugeManager.Instance != null)
            GaugeManager.Instance.SetGaugeValue(value);
    }

    // ── 플래그 API ────────────────────────────────────────────────────────
    public void SetFlag(string key, bool value)
    {
        flags[key] = value;
    }

    public bool GetFlag(string key, bool defaultValue = false)
    {
        return flags.TryGetValue(key, out bool val) ? val : defaultValue;
    }

    public bool HasFlag(string key) => flags.ContainsKey(key);

    /// <summary>세이브 로드 전용. 이벤트를 발생시키지 않고 flags를 직접 복원합니다.</summary>
    public void LoadFlags(Dictionary<string, bool> loaded)
    {
        flags = new Dictionary<string, bool>(loaded);
    }
}
