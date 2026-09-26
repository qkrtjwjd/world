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

    // ⛔ 2026-09-27: 방별 줌을 폐기했다(E-64 · F-3-9 · CLAUDE.md §11 — 정사영 크기 불변).
    //   필드는 씬 직렬화 값이 남아 있어 지우지 않고 두지만 **읽지 않는다.** 다시 쓰지 말 것.
    //   화면보다 작은 방은 카메라 바운드만으로 방 전경에 고정된다.
    [Header("카메라 줌 — 폐기됨 (읽지 않는다)")]
    [Tooltip("⛔ 폐기. 정사영 크기는 바꾸지 않는다(F-3-9). 값이 있어도 무시된다.")]
    public float targetOrthoSize = 0f;
    [Tooltip("⛔ 폐기.")]
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
        // ⚠ 2026-09-27: RoomTransfer 는 콜라이더 없는 RoomSpawnPoint 에 붙어 있어 여기서 늘 빠져나갔다 —
        //   방 안에서 시작하는 판정이 한 번도 돌지 않았다(실측). 방 경계가 있으면 그것으로 판정한다.
        if (col == null && roomBound == null) return;

        var player = GameObject.FindWithTag("Player");
        if (player == null) return;

        // 2026-09-27: 판정을 방의 카메라 경계(roomBound)로 한다. 이 컴포넌트의 콜라이더는 문 앞의 작은 트리거라,
        //   침대 위에서 시작하는 S#01 의 루를 「방 밖」으로 보고 경계를 걸지 않았다 — 그러면 카메라가 루를 따라가
        //   방 아래쪽이 잘렸다. 개정 D S#01 「페이드인 후 고정. 루의 방 전경을 한 화면에」(보호 구역 §3).
        Bounds area = roomBound != null ? roomBound.bounds : col.bounds;
        Vector3 pp = player.transform.position;
        if (area.Contains(new Vector3(pp.x, pp.y, area.center.z)))
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