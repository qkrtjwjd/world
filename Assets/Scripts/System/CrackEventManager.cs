using UnityEngine;

public class CrackEventManager : MonoBehaviour
{
    public static CrackEventManager Instance { get; private set; }

    public event System.Action OnCrackEvent;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public static void TriggerCrackEvent()
    {
        if (Instance == null)
        {
            Debug.LogWarning("[CrackEventManager] Instance가 null입니다. 씬에 CrackEventManager 프리팹을 배치해 주세요.");
            return;
        }
        Instance.FireCrackEvent();
    }

    void FireCrackEvent()
    {
        OnCrackEvent?.Invoke();
        Dbg.Log("[CrackEvent] 균열 이벤트 발동");
    }
}
