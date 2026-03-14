using UnityEngine;

public class Teleport : MonoBehaviour
{
    [Header("설정")]
    public Transform targetDestination; // 플레이어가 도착할 위치
    [Tooltip("이 스크립트가 반응할 특정 콜라이더를 지정하세요. (비워두면 아무거나 반응함)")]
    public Collider2D specificTrigger;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // [중요] 사용자가 Inspector에서 'Specific Trigger'를 할당하지 않았다면 에러 띄우기
        if (specificTrigger == null)
        {
            Debug.LogError($"[Teleport 오류] '{gameObject.name}' 오브젝트의 Teleport 스크립트에 'Specific Trigger'가 비어있습니다! \n문 역할을 하는 작은 BoxCollider2D를 드래그해서 넣어주세요.");
            return;
        }

        // 할당된 '그 트리거'에 닿은 게 아니라면 무조건 무시 (방 전체 트리거 등등 무시)
        if (!other.IsTouching(specificTrigger)) return;

        if (other.CompareTag("Player"))
        {
            // ... (나머지 로직 동일)
            if (InteractionManager.Instance != null && InteractionManager.Instance.IsCoolingDown)
            {
                // 쿨타임 중이면 로그 없이 조용히 리턴 (스팸 방지)
                return;
            }

            // 1. 플레이어 이동
            other.transform.position = targetDestination.position;
            // Debug.Log($"[Teleport] 플레이어 이동 완료 -> {targetDestination.name}");

            // 2. 카메라 즉시 이동 (스냅)
            RoomTransfer roomTransfer = targetDestination.GetComponent<RoomTransfer>();
            if (roomTransfer != null)
            {
                // ★ 방 입장 처리 (덮개 제거 등)
                roomTransfer.EnterRoom();

                if (CameraFollow.Instance != null)
                {
                    // Debug.Log("[Teleport] RoomTransfer 발견! 카메라 즉시 이동(Snap) 실행");
                    CameraFollow.Instance.SetBound(roomTransfer.roomBound, true);
                }
            }
            else
            {
                // 돌아오는 게 이상하다면 여기가 문제일 수 있음
                Debug.LogWarning($"[Teleport 경고] 도착지 '{targetDestination.name}'에 'RoomTransfer' 컴포넌트가 없습니다! 카메라가 이동하지 않을 수 있습니다.");
            }

            // 3. 쿨타임 설정
            if (InteractionManager.Instance != null)
            {
                InteractionManager.Instance.SetCooldown(1.0f);
            }
        }
    }
}