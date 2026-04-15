using UnityEngine;

/// <summary>
/// 인형화(Puppetization) 수치를 관리하는 싱글톤.
/// 내부적으로 CorruptionManager에 위임합니다.
///
/// [사용법]
/// PuppetizationManager.Instance.Add(2.5f);
/// </summary>
public class PuppetizationManager : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  싱글톤
    // ─────────────────────────────────────────────
    public static PuppetizationManager Instance { get; private set; }

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
    // TODO: 인형화 수치 처리 로직 확장 (UI 갱신, 연출 트리거 등)
    public void Add(float amount)
    {
        if (CorruptionManager.instance == null)
        {
            Debug.LogWarning("[PuppetizationManager] CorruptionManager.instance 가 null 입니다.");
            return;
        }
        CorruptionManager.instance.AddCorruption(amount);
    }
}
