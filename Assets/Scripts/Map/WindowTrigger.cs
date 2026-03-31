using UnityEngine;

/// <summary>
/// 플레이어가 창문 오브젝트 트리거존에 진입하면 NightSequenceManager에 알린다.
/// S#2 에서 S#3 으로 전환하는 트리거.
/// </summary>
public class WindowTrigger : MonoBehaviour
{
    public NightSequenceManager sequenceManager;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            sequenceManager?.OnWindowReached();
    }
}
