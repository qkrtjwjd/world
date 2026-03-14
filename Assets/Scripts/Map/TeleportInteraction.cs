using UnityEngine;

[RequireComponent(typeof(InteractionTrigger))]
public class TeleportInteraction : MonoBehaviour
{
    [Header("설정")]
    [Tooltip("플레이어가 이동할 목적지 위치 (빈 오브젝트를 방 안쪽에 만들어서 여기에 넣으세요)")]
    public Transform targetLocation;

    void Start()
    {
        // [수정] 자동 연결 코드 삭제 (인스펙터 중복 연결 방지)
        // 꼭 유니티 인스펙터창의 InteractionTrigger > On Interact 목록에 
        // 이 오브젝트의 TeleportInteraction.Teleport 함수를 드래그해서 넣어주세요!
    }

    // 실제로 이동시키는 함수
    public void Teleport()
    {
        if (targetLocation == null)
        {
            Debug.LogError("[TeleportInteraction] 목적지(Target Location)가 비어있습니다! 인스펙터에서 설정해주세요.");
            return;
        }

        // 플레이어 찾기 (PlayerStats 싱글톤 활용)
        Transform playerTransform = null;

        if (PlayerStats.Instance != null)
        {
            playerTransform = PlayerStats.Instance.transform;
        }
        else
        {
            // 만약 PlayerStats가 없다면 태그로 찾기
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        // 이동 실행
        if (playerTransform != null)
        {
            // 1. 위치 이동
            playerTransform.position = targetLocation.position;
            
            // 2. 물리 좌표 즉시 동기화 (충돌/끼임 방지)
            Physics2D.SyncTransforms();

            Debug.Log($"플레이어를 '{targetLocation.name}' 위치로 순간이동했습니다.");

            // 3. 만약 목적지나 그 부모에게 RoomTransfer가 있다면 즉시 카메라 갱신 시도
            // (SpawnPoint 자체에 없어도 부모 방 오브젝트에 있으면 찾아냄)
            RoomTransfer roomTransfer = targetLocation.GetComponentInParent<RoomTransfer>();
            if (roomTransfer != null)
            {
                // ★ 방 입장 처리 (덮개 제거 등)
                roomTransfer.EnterRoom();

                if (CameraFollow.Instance != null)
                {
                    // true를 넣어서 즉시 이동시킴 (Snap)
                    CameraFollow.Instance.SetBound(roomTransfer.roomBound, true);
                }
            }
            else
            {
                Debug.LogWarning($"[TeleportInteraction] 목적지 '{targetLocation.name}' 또는 그 부모에게서 'RoomTransfer'를 찾을 수 없습니다. 덮개가 열리지 않을 수 있습니다.");
            }

            // ★ 순간이동 후 1초 동안 상호작용 금지 (무한 루프 방지)
            if (InteractionManager.Instance != null)
            {
                InteractionManager.Instance.SetCooldown(1.0f);
            }
        }
        else
        {
            Debug.LogError("[TeleportInteraction] 플레이어를 찾을 수 없습니다! (Player 태그 확인)");
        }
    }
}
