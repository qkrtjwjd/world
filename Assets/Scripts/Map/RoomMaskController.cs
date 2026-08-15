using UnityEngine;

[DefaultExecutionOrder(1001)]
public class RoomMaskController : MonoBehaviour
{
    [Header("오버레이 색상")]
    [SerializeField] Color overlayColor = Color.black;
    [SerializeField] string overlaySortingLayer = "Default";
    [SerializeField] int overlaySortingOrder = 50;

    [Header("카메라")]
    [Tooltip("비워두면 CameraFollow → Camera.main 순으로 자동 감지")]
    [SerializeField] Camera targetCamera;

    public static RoomMaskController Instance { get; private set; }

    Transform _top, _bottom, _left, _right;
    SpriteRenderer _topSR, _bottomSR, _leftSR, _rightSR;
    BoxCollider2D _currentRoomCol;
    bool _debugLogged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        ResolveCamera();
        CreatePanels();
    }

    void Start()
    {
        ResolveCamera();
        if (RoomTransfer.CurrentRoom != null)
        {
            ApplyRoom(RoomTransfer.CurrentRoom);
            return;
        }
        var player = GameObject.FindWithTag("Player");
        if (player == null) return;
        var playerPos = player.transform.position;
        foreach (var room in FindObjectsByType<RoomTransfer>())
        {
            BoxCollider2D rb = room.roomBound;
            if (rb == null) continue;
            if (rb.bounds.Contains(playerPos)) { ApplyRoom(room); break; }
        }
    }

    void OnEnable()
    {
        RoomTransfer.OnRoomEntered += HandleRoomEntered;
        RoomTransfer.OnRoomExited  += HandleRoomExited;
    }

    void OnDisable()
    {
        RoomTransfer.OnRoomEntered -= HandleRoomEntered;
        RoomTransfer.OnRoomExited  -= HandleRoomExited;
    }

    void LateUpdate()
    {
        if (targetCamera == null)
        {
            ResolveCamera();
            if (targetCamera == null) { SetPanelsActive(false); return; }
        }

        if (_currentRoomCol == null) { SetPanelsActive(false); return; }

        float halfH = targetCamera.orthographicSize;
        float halfW = halfH * targetCamera.aspect;
        Vector3 cam  = targetCamera.transform.position;
        Bounds  room = _currentRoomCol.bounds;
        float   z    = transform.position.z;

        // 카메라 중심이 방 bounds 밖 → 텔레포트 후 _currentRoomCol 미갱신, 마스킹 해제
        if (cam.x < room.min.x || cam.x > room.max.x || cam.y < room.min.y || cam.y > room.max.y)
        {
            _currentRoomCol = null;
            _debugLogged    = false;
            SetPanelsActive(false);
            return;
        }

        float topOver   = Mathf.Max(0f, (cam.y + halfH) - room.max.y);
        float botOver   = Mathf.Max(0f, room.min.y - (cam.y - halfH));
        float leftOver  = Mathf.Max(0f, room.min.x - (cam.x - halfW));
        float rightOver = Mathf.Max(0f, (cam.x + halfW) - room.max.x);

        if (!_debugLogged)
        {
            _debugLogged = true;
            Debug.Log($"[RoomMask] col={_currentRoomCol.name} bounds={room} | cam={cam} orthoH={halfH} aspect={targetCamera.aspect:F2} | over T{topOver:F2} B{botOver:F2} L{leftOver:F2} R{rightOver:F2}");
        }

        float pW = halfW * 4f;
        float pH = halfH * 4f;

        SetPanel(_top,    topOver   > 0.001f, cam.x,                         room.max.y + topOver   * 0.5f, z, pW,             topOver   + 0.1f);
        SetPanel(_bottom, botOver   > 0.001f, cam.x,                         room.min.y - botOver   * 0.5f, z, pW,             botOver   + 0.1f);
        SetPanel(_left,   leftOver  > 0.001f, room.min.x - leftOver  * 0.5f, cam.y,                         z, leftOver  + 0.1f, pH);
        SetPanel(_right,  rightOver > 0.001f, room.max.x + rightOver * 0.5f, cam.y,                         z, rightOver + 0.1f, pH);
    }

    void HandleRoomEntered(BoxCollider2D _)
    {
        if (RoomTransfer.CurrentRoom != null)
            ApplyRoom(RoomTransfer.CurrentRoom);
    }

    void HandleRoomExited()
    {
        _currentRoomCol = null;
        _debugLogged    = false;
        SetPanelsActive(false);
    }

    void ApplyRoom(RoomTransfer room)
    {
        if (room.roomBound != null) { _currentRoomCol = room.roomBound; return; }
        // roomBound 없음 → 이 방에서는 마스킹 안 함
    }

    void ResolveCamera()
    {
        if (targetCamera != null) return;

        if (CameraFollow.Instance != null)
            targetCamera = CameraFollow.Instance.GetComponent<Camera>();
        if (targetCamera != null) return;

        targetCamera = Camera.main;
        if (targetCamera != null) return;

        var cf = FindAnyObjectByType<CameraFollow>();
        if (cf != null) targetCamera = cf.GetComponent<Camera>();
        if (targetCamera != null) return;

        Debug.LogWarning("[RoomMaskController] 카메라를 찾지 못했습니다. Inspector에서 Target Camera를 직접 연결하세요.");
    }

    void CreatePanels()
    {
        _top    = MakePanel("_RoomMask_Top",    out _topSR);
        _bottom = MakePanel("_RoomMask_Bottom", out _bottomSR);
        _left   = MakePanel("_RoomMask_Left",   out _leftSR);
        _right  = MakePanel("_RoomMask_Right",  out _rightSR);
        SetPanelsActive(false);
    }

    Transform MakePanel(string panelName, out SpriteRenderer sr)
    {
        var go  = new GameObject(panelName);
        go.transform.SetParent(null);
        sr  = go.AddComponent<SpriteRenderer>();
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        sr.sprite           = Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
        sr.color            = overlayColor;
        sr.sortingLayerName = overlaySortingLayer;
        sr.sortingOrder     = overlaySortingOrder;
        return go.transform;
    }

    void SetPanel(Transform t, bool active, float x, float y, float z, float w, float h)
    {
        if (t == null) return;
        t.gameObject.SetActive(active);
        if (!active) return;
        t.position   = new Vector3(x, y, z);
        t.localScale = new Vector3(w, h, 1f);
    }

    void SetPanelsActive(bool active)
    {
        if (_top)    _top.gameObject.SetActive(active);
        if (_bottom) _bottom.gameObject.SetActive(active);
        if (_left)   _left.gameObject.SetActive(active);
        if (_right)  _right.gameObject.SetActive(active);
    }
}
