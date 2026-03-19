using UnityEngine;

/// <summary>
/// 단검 장착/해제 상태를 관리합니다.
/// - F키로 토글 (또는 외부에서 Equip/Unequip 직접 호출)
/// - 판타지 전투 중 장착 → 핵앤슬래시 강제 전환 트리거
/// - 현실 전투 중 해제 → 턴제 강제 전환 트리거
/// </summary>
public class DaggerSystem : MonoBehaviour
{
    public static DaggerSystem Instance { get; private set; }

    [Header("단검 토글 키")]
    public KeyCode toggleKey = KeyCode.F;

    [Header("현재 상태 (읽기 전용)")]
    [SerializeField] private bool _isDaggerEquipped = false;

    public bool IsDaggerEquipped => _isDaggerEquipped;

    /// <summary>정적 접근용 헬퍼.</summary>
    public static bool IsEquipped => Instance != null && Instance._isDaggerEquipped;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        DontDestroyOnLoad(gameObject);
    }

    public void Toggle()
    {
        _isDaggerEquipped = !_isDaggerEquipped;
        Debug.Log($"[DaggerSystem] 단검 {(_isDaggerEquipped ? "장착" : "해제")}");
    }

    public void Equip()   => _isDaggerEquipped = true;
    public void Unequip() => _isDaggerEquipped = false;
}
