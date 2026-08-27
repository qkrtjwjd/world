using UnityEngine;

/// <summary>
/// DontDestroyOnLoad 싱글톤 공통 패턴.
/// Awake/OnDestroy 보일러플레이트를 제거하고 OnAwake()만 재정의하세요.
/// </summary>
public abstract class PersistentSingleton<T> : MonoBehaviour where T : MonoBehaviour
{
    public static T Instance { get; protected set; }

    protected virtual void Awake()
    {
        if (Instance == null)
        {
            Instance = this as T;
            DontDestroyOnLoad(gameObject);
            OnAwake();
        }
        else
        {
            SingletonGuard.DestroyDuplicate(this);
        }
    }

    protected virtual void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    protected virtual void OnAwake() { }
}
