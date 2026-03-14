using UnityEngine;

public class RoomTransfer : MonoBehaviour
{
    [Header("Configuration")]
    public BoxCollider2D roomBound; // Drag the BoxCollider2D defining the room area here

    [Header("옵션")]
    [Tooltip("이 방을 가리는 검은색 덮개(Sprite)를 연결하세요. 방에 들어가면 꺼지고, 나가면 켜집니다.")]
    public GameObject roomCover;

    [Tooltip("체크하면 이 방에 있을 때 '현실 붕괴(Reality Gauge)'가 차오릅니다.")]
    public bool enableRealityGauge = false;

    [Header("씬 설정 따르기")]
    [Tooltip("체크하면 위 설정(Enable Reality Gauge)을 무시하고, RealitySystem의 씬 전체 설정을 따릅니다.")]
    public bool useSceneDefault = true;

    // 현재 플레이어가 있는 방을 기억하는 변수 (모든 방이 공유함)
    public static RoomTransfer currentActiveRoom;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the object entering is the Player
        // Ensure "Player" tag is set on your player object
        if (other.CompareTag("Player") && !other.isTrigger) 
        {
            EnterRoom(); // 방 입장 처리

            // Find the CameraFollow script (Optimized with Singleton)
            if (CameraFollow.Instance != null)
            {
                // Update the camera's clamping bounds to this room
                CameraFollow.Instance.SetBound(roomBound);
            }
            else
            {
                Debug.LogWarning("CameraFollow Instance not found.");
            }
        }
    }

    // 외부(Teleport.cs)에서 호출할 수 있게 public으로 변경
    public void EnterRoom()
    {
        // 1. 이전에 있던 방이 있다면 닫아주기 (덮개 덮기)
        if (currentActiveRoom != null && currentActiveRoom != this)
        {
            currentActiveRoom.ExitRoom();
        }

        // 2. 현재 방을 '활성 방'으로 등록
        currentActiveRoom = this;

        // 3. 이 방의 덮개 열기 (방 보여주기)
        if (roomCover != null)
        {
            roomCover.SetActive(false);
        }

        // 4. RealitySystem 제어 (게이지 켜기/끄기)
        if (RealitySystem.Instance != null)
        {
            // 씬 기본값을 따를지, 이 방만의 설정을 쓸지 결정
            bool finalState = useSceneDefault ? RealitySystem.Instance.sceneDefaultActive : enableRealityGauge;
            
            RealitySystem.Instance.isSystemActive = finalState;
        }
    }

    // 방에서 나갈 때(다른 방으로 갈 때) 호출됨
    public void ExitRoom()
    {
        // 덮개 다시 덮기 (방 가리기)
        if (roomCover != null)
        {
            roomCover.SetActive(true);
        }
    }
}
