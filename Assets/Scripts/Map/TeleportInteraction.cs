using UnityEngine;

[RequireComponent(typeof(InteractionTrigger))]
public class TeleportInteraction : MonoBehaviour
{
    [Header("설정")]
    [Tooltip("플레이어가 이동할 목적지 위치")]
    public Transform targetLocation;

    public void Teleport()
    {
        if (targetLocation == null)
        {
            Debug.LogError("[TeleportInteraction] Target Location 이 비어있습니다!");
            return;
        }

        Transform player = GetPlayerTransform();
        if (player == null)
        {
            Debug.LogError("[TeleportInteraction] 플레이어를 찾을 수 없습니다. (Player 태그 확인)");
            return;
        }

        // 이동 + 물리 동기화
        player.position = targetLocation.position;
        Physics2D.SyncTransforms();

        // 카메라 + 방 덮개 갱신
        RoomTransfer room = targetLocation.GetComponentInParent<RoomTransfer>();
        if (room != null)
        {
            room.EnterRoom();
            CameraFollow.Instance?.SetBound(room.roomBound, snap: true);
        }

        InteractionManager.Instance?.SetCooldown(1.0f);
    }

    // ─────────────────────────────────────────────
    static Transform GetPlayerTransform()
    {
        if (PlayerStats.Instance != null) return PlayerStats.Instance.transform;
        var p = GameObject.FindGameObjectWithTag("Player");
        return p != null ? p.transform : null;
    }
}