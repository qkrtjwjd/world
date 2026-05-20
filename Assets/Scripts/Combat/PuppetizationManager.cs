using UnityEngine;

/// <summary>
/// 인형화(Puppetization) 수치를 관리하는 싱글톤.
/// 내부적으로 CorruptionManager에 위임합니다.
///
/// [사용법]
/// PuppetizationManager.Instance.Add(2.5f);
/// PuppetizationManager.OnValueAdded += amount => RefreshUI(amount);
/// </summary>
public class PuppetizationManager : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  싱글톤
    // ─────────────────────────────────────────────
    public static PuppetizationManager Instance { get; private set; }

    // ─────────────────────────────────────────────
    //  이벤트 — UI·연출 시스템이 구독
    // ─────────────────────────────────────────────

    /// <summary>Add() 호출 시 발행됩니다. 인자는 전달된 delta 값입니다.</summary>
    public static event System.Action<float> OnValueAdded;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    // ─────────────────────────────────────────────
    //  공개 API
    // ─────────────────────────────────────────────

    /// <summary>인형화 수치를 amount 만큼 증가시킵니다.</summary>
    public void Add(float amount)
    {
        if (CorruptionManager.Instance == null)
        {
            Debug.LogWarning("[PuppetizationManager] CorruptionManager.Instance 가 null 입니다.");
            return;
        }
        CorruptionManager.Instance.AddCorruption(amount);
        OnValueAdded?.Invoke(amount);
    }

    /// <summary>현재 인형화 수치를 반환합니다. CorruptionManager가 없으면 0을 반환합니다.</summary>
    public float GetValue() => CorruptionManager.Instance?.currentCorruption ?? 0f;
}
