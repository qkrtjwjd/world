using UnityEngine;

public class RoomTransfer : MonoBehaviour
{
    [Header("카메라 바운드")]
    public BoxCollider2D roomBound;

    [Header("방 덮개 (방 밖에서 보이지 않게 가리는 스프라이트)")]
    public GameObject roomCover;

    [Header("입장 감지")]
    [Tooltip("방 트리거 경계 안쪽으로 이 거리만큼 들어왔을 때 방 입장으로 판정 (문 근처 오작동 방지)")]
    public float entryThreshold = 0.2f;

    [Header("카메라 줌")]
    [Tooltip("이 방에서 사용할 orthographicSize (0이면 기본값 유지)")]
    public float targetOrthoSize = 0f;
    [Tooltip("줌 전환 시간(초)")]
    public float zoomDuration = 0.4f;

    public static RoomTransfer CurrentRoom { get; private set; }

    public static event System.Action<BoxCollider2D> OnRoomEntered;
    public static event System.Action OnRoomExited;

    // ─────────────────────────────────────────────
    //  초기화 — 커버는 반드시 ON 으로 시작
    // ─────────────────────────────────────────────
    void Start()
    {
        // 야간 시퀀스 중 = 방 안에서 시작 → 커버 OFF
        // 이후 = 방 밖에서 시작 → 커버 ON
        SetCover(GameState.isNightSequenceWatched);

        // 씬 시작 시 플레이어가 이미 방 안에 있으면 OnTriggerEnter2D가 발생하지 않으므로
        // Physics2D.OverlapCollider로 직접 확인 후 입장 처리
        var col = GetComponent<Collider2D>();
        if (col == null) return;

        var player = GameObject.FindWithTag("Player");
        if (player == null) return;

        if (col.bounds.Contains(player.transform.position))
        {
            EnterRoom();
            CameraFollow.Instance?.SetBound(roomBound, snap: true);
        }
    }

    // ─────────────────────────────────────────────
    //  트리거 (방을 나갈 때 카메라 바운드 해제)
    // ─────────────────────────────────────────────

    // OnTriggerEnter2D 대신 Stay 사용:
    // Enter는 콜라이더 가장자리가 닿는 순간 발동되므로 문 앞 접근만으로도 오작동함.
    // Stay에서 플레이어 위치가 실제로 방 안쪽(entryThreshold 이상 진입)에 들어왔을 때만 처리.
    private void OnTriggerStay2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || other.isTrigger) return;
        if (CurrentRoom == this) return; // 이미 입장 처리됨

        Collider2D myCol = GetComponent<Collider2D>();
        if (myCol == null) return;

        Bounds inner = myCol.bounds;
        inner.Expand(-entryThreshold * 2f); // 사방으로 threshold 만큼 축소
        if (inner.size.x <= 0f || inner.size.y <= 0f) return; // 방이 threshold보다 작으면 bounds 역전 방지
        if (!inner.Contains(other.transform.position)) return;

        EnterRoom();
        CameraFollow.Instance?.SetBound(roomBound, snap: false);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || other.isTrigger) return;
        if (CurrentRoom == this)
        {
            CurrentRoom = null;
            ExitRoom();
            CameraFollow.Instance?.SetBound(null, snap: false);
        }
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
        var triggerCol = GetComponent<BoxCollider2D>();
        OnRoomEntered?.Invoke(triggerCol != null ? triggerCol : roomBound);
        SetCover(false); // 덮개 열기

        if (targetOrthoSize > 0f)
            CameraFollow.Instance?.ZoomTo(targetOrthoSize, zoomDuration);
    }

    public void ExitRoom()
    {
        SetCover(true); // 덮개 닫기
        OnRoomExited?.Invoke();
    }

    // ─────────────────────────────────────────────
    //  헬퍼
    // ─────────────────────────────────────────────
    void SetCover(bool active)
    {
        if (roomCover != null) roomCover.SetActive(active);
    }

}