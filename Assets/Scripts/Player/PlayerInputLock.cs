using UnityEngine;

/// <summary>
/// 플레이어 입력 잠금을 중첩 방식으로 관리합니다.
/// lockCount가 0보다 크면 잠금 상태이며, 모든 Lock() 호출에 대응하는
/// Unlock()이 호출되어야 실제로 해제됩니다.
/// </summary>
public class PlayerInputLock : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  싱글톤
    // ─────────────────────────────────────────────
    public static PlayerInputLock Instance
    {
        get
        {
            if (!_instance)
            {
                var go = new GameObject("PlayerInputLock [Auto]");
                _instance = go.AddComponent<PlayerInputLock>();
            }
            return _instance;
        }
    }
    private static PlayerInputLock _instance;

    // ─────────────────────────────────────────────
    //  상태
    // ─────────────────────────────────────────────
    private int _lockCount = 0;
    private ClearSky.SimplePlayerController _ctrl;

    /// <summary>현재 잠금 상태 여부.</summary>
    public bool IsLocked => _lockCount > 0;

    // ─────────────────────────────────────────────
    //  라이프사이클
    // ─────────────────────────────────────────────
    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    // ─────────────────────────────────────────────
    //  공개 API
    // ─────────────────────────────────────────────
    /// <summary>입력을 잠급니다. 중첩 호출 지원.</summary>
    public void Lock()
    {
        if (_lockCount == 0)
        {
            var ctrl = GetCtrl();
            if (ctrl == null)
            {
                Debug.LogWarning("[PlayerInputLock] SimplePlayerController를 찾을 수 없습니다. 잠금을 건너뜁니다.");
            }
            else
            {
                ctrl.Lock();
            }
        }
        _lockCount++;
        Debug.Log($"[PlayerInputLock] Lock() — lockCount: {_lockCount}");
    }

    /// <summary>입력 잠금을 해제합니다. lockCount가 0이 되어야 실제로 해제됩니다.</summary>
    public void Unlock()
    {
        if (_lockCount <= 0)
        {
            Debug.LogWarning("[PlayerInputLock] Unlock() 호출되었으나 이미 잠금 해제 상태입니다.");
            return;
        }

        _lockCount--;
        Debug.Log($"[PlayerInputLock] Unlock() — lockCount: {_lockCount}");

        if (_lockCount == 0)
        {
            var ctrl = GetCtrl();
            if (ctrl == null)
            {
                Debug.LogWarning("[PlayerInputLock] SimplePlayerController를 찾을 수 없습니다. 해제를 건너뜁니다.");
            }
            else
            {
                ctrl.Unlock();
            }
        }
    }

    // ─────────────────────────────────────────────
    //  내부 헬퍼
    // ─────────────────────────────────────────────
    private ClearSky.SimplePlayerController GetCtrl()
    {
        if (!_ctrl)
            _ctrl = FindAnyObjectByType<ClearSky.SimplePlayerController>();
        return _ctrl;
    }
}
