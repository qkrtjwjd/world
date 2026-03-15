using UnityEngine;

public class RoomTransfer : MonoBehaviour
{
    [Header("카메라 바운드")]
    public BoxCollider2D roomBound;

    [Header("방 덮개 (방 밖에서 보이지 않게 가리는 스프라이트)")]
    public GameObject roomCover;

    [Header("현실 게이지 설정")]
    [Tooltip("이 방에서 현실 게이지가 차오르는지 여부")]
    public bool enableRealityGauge = false;
    [Tooltip("체크하면 RealitySystem 의 씬 전체 설정을 따릅니다.")]
    public bool useSceneDefault    = true;

    public static RoomTransfer CurrentRoom { get; private set; }

    // ─────────────────────────────────────────────
    //  트리거
    // ─────────────────────────────────────────────
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || other.isTrigger) return;

        EnterRoom();

        if (CameraFollow.Instance != null)
            CameraFollow.Instance.SetBound(roomBound);
    }

    // ─────────────────────────────────────────────
    //  방 입장 / 퇴장
    // ─────────────────────────────────────────────
    public void EnterRoom()
    {
        // 이전 방 퇴장 처리
        if (CurrentRoom != null && CurrentRoom != this)
            CurrentRoom.ExitRoom();

        CurrentRoom = this;
        SetCover(false); // 덮개 열기

        ApplyRealityGauge();
    }

    public void ExitRoom()
    {
        SetCover(true); // 덮개 닫기
    }

    // ─────────────────────────────────────────────
    //  헬퍼
    // ─────────────────────────────────────────────
    void SetCover(bool active)
    {
        if (roomCover != null) roomCover.SetActive(active);
    }

    void ApplyRealityGauge()
    {
        if (RealitySystem.Instance == null) return;

        bool finalState = useSceneDefault
            ? RealitySystem.Instance.sceneDefaultActive
            : enableRealityGauge;

        RealitySystem.Instance.isSystemActive = finalState;
    }
}