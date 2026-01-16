using UnityEngine;

public class Teleport : MonoBehaviour
{
    [Header("이동할 목표 지점")]
    public Transform targetDestination; // 플레이어가 도착할 위치 (빈 오브젝트)

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // 플레이어의 위치를 목표 지점으로 강제 이동!
            other.transform.position = targetDestination.position;
        }
    }
}