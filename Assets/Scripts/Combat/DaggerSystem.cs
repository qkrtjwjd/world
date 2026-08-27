using UnityEngine;

/// <summary>
/// 단검 장착/해제 상태를 관리합니다.
/// - 외부에서 Equip/Unequip 직접 호출
/// - FrontDoorInteraction·BakeryNPC 등이 IsEquipped 로 진행 분기
/// </summary>
public class DaggerSystem : MonoBehaviour
{
    public static DaggerSystem Instance => _instance;
    private static DaggerSystem _instance;

    [Header("현재 상태 (읽기 전용)")]
    [SerializeField] private bool _isDaggerEquipped = false;

    public bool IsDaggerEquipped => _isDaggerEquipped;

    /// <summary>정적 접근용 헬퍼.</summary>
    public static bool IsEquipped => _instance != null && _instance._isDaggerEquipped;

    void Awake()
    {
        if (_instance == null) { _instance = this; DontDestroyOnLoad(gameObject); }
        else if (_instance != this) { SingletonGuard.DestroyDuplicate(this); return; }
    }

    public void Equip()   => _isDaggerEquipped = true;
    public void Unequip() => _isDaggerEquipped = false;
}
