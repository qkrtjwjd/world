using UnityEngine;

/// <summary>
/// 특정 트리거 콜라이더에 플레이어가 닿으면 목적지로 순간이동합니다.
/// </summary>
public class Teleport : MonoBehaviour
{
    [Header("설정")]
    public Transform    targetDestination;
    [Tooltip("반응할 콜라이더를 직접 지정하세요. (방 전체 트리거와 구분하기 위해 필수)")]
    public Collider2D   specificTrigger;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (specificTrigger == null)
        {
            Debug.LogError($"[Teleport] '{gameObject.name}' 의 Specific Trigger 가 비어있습니다!");
            return;
        }

        if (!other.IsTouching(specificTrigger)) return;
        if (!other.CompareTag("Player"))        return;

        // 쿨타임 중이면 무시
        if (InteractionManager.Instance != null && InteractionManager.Instance.IsCoolingDown)
            return;

        // 이동
        other.transform.position = targetDestination.position;
        Physics2D.SyncTransforms();

        // 카메라 바운드 갱신
        RoomTransfer room = targetDestination.GetComponent<RoomTransfer>();
        if (room != null)
        {
            room.EnterRoom();
            CameraFollow.Instance?.SetBound(room.roomBound, snap: true);
        }

        InteractionManager.Instance?.SetCooldown(1.0f);
    }
}