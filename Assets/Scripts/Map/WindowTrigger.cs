using UnityEngine;

/// <summary>
/// 플레이어가 창문 오브젝트 트리거존에 진입한 것을 알린다.
///
/// 두 곳에서 쓴다.
///   1. S#01 루의 방 창문 — NightSequenceManager 에 알려 S#02 로 넘어간다.
///   2. S#04F 부엌 창문   — KitchenTriggerCutscene 이 HasReached 를 폴링해 마당의 각설탕을 보여준다.
///
/// 2번은 sequenceManager 를 비워 둔 채로 쓴다.
/// </summary>
public class WindowTrigger : MonoBehaviour
{
    [Tooltip("S#01 전용. S#04F 부엌 창문에서는 비워 둡니다.")]
    public NightSequenceManager sequenceManager;

    /// <summary>Arm() 이후 플레이어가 이 트리거에 들어왔는지 여부.</summary>
    public bool HasReached { get; private set; }

    /// <summary>대기를 시작하기 직전에 호출해 이전 진입 기록을 지웁니다.</summary>
    public void Arm() => HasReached = false;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        HasReached = true;
        sequenceManager?.OnWindowReached();
    }
}
