using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어를 뒤따르는 동료 NPC 동행 시스템.
/// 플레이어가 이동한 경로를 기록하여, 일정 거리 뒤에서 동일한 경로로 따라옵니다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class CompanionFollow : MonoBehaviour
{
    [Header("추적 설정")]
    [Tooltip("플레이어와 유지할 최소 거리 (유닛)")]
    public float followDistance = 1.5f;
    [Tooltip("동료 이동 속도 — 플레이어 walkSpeed(4f)와 맞추세요")]
    public float moveSpeed = 4f;

    // ── 내부 상태 ──
    private Transform      _player;
    private Rigidbody2D    _rb;
    private Animator       _anim;

    // 플레이어 경로 기록 큐
    private readonly Queue<Vector2> _pathHistory = new Queue<Vector2>();
    private Vector2 _lastRecordedPos;

    // 이 거리(유닛)만큼 플레이어가 이동할 때마다 경로점 1개 추가
    private const float RECORD_STEP = 0.15f;

    // ════════════════════════════════════════
    //  Unity
    // ════════════════════════════════════════
    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale   = 0f;
        _rb.freezeRotation = true;
        _anim = GetComponent<Animator>(); // 없어도 동작
    }

    void Start()
    {
        if (PlayerStats.Instance != null)
        {
            _player          = PlayerStats.Instance.transform;
            _lastRecordedPos = _player.position;
        }
        else
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                _player          = p.transform;
                _lastRecordedPos = p.transform.position;
            }
            else
            {
                Debug.LogWarning("[CompanionFollow] 'Player' 태그를 가진 오브젝트를 찾을 수 없습니다.");
            }
        }
    }

    // ════════════════════════════════════════
    //  경로 기록 (Update — 매 프레임)
    // ════════════════════════════════════════
    void Update()
    {
        if (_player == null || Time.timeScale == 0f) return;

        // 플레이어가 RECORD_STEP 이상 이동했을 때만 큐에 추가 (과밀 방지)
        if (Vector2.Distance((Vector2)_player.position, _lastRecordedPos) >= RECORD_STEP)
        {
            _pathHistory.Enqueue(_player.position);
            _lastRecordedPos = _player.position;
        }
    }

    // ════════════════════════════════════════
    //  이동 처리 (FixedUpdate — 물리 주기)
    // ════════════════════════════════════════
    void FixedUpdate()
    {
        // 전투 중(timeScale = 0) 또는 플레이어 없음 → 정지
        if (_player == null || Time.timeScale == 0f)
        {
            _rb.linearVelocity = Vector2.zero;
            _anim?.SetBool("isRun", false);
            return;
        }

        float distToPlayer = Vector2.Distance(transform.position, _player.position);

        // 플레이어가 충분히 가까우면 정지 + 경로 기록 초기화
        if (distToPlayer <= followDistance)
        {
            _rb.linearVelocity = Vector2.zero;
            _anim?.SetBool("isRun", false);
            _pathHistory.Clear();
            return;
        }

        // 이미 도달한 경로점 제거 (큐 앞부분 정리)
        while (_pathHistory.Count > 0 &&
               Vector2.Distance(transform.position, _pathHistory.Peek()) <= RECORD_STEP)
        {
            _pathHistory.Dequeue();
        }

        // 목표 지점: 큐에 경로가 있으면 가장 오래된 지점, 없으면 플레이어 직선
        Vector2 goal = _pathHistory.Count > 0
            ? _pathHistory.Peek()
            : (Vector2)_player.position;

        Vector2 dir = (goal - (Vector2)transform.position).normalized;
        _rb.linearVelocity = dir * moveSpeed;
        _anim?.SetBool("isRun", true);

        // 스프라이트 방향 전환 (SimplePlayerController 방식: localScale.x 반전)
        if (Mathf.Abs(dir.x) > 0.01f)
        {
            Vector3 s = transform.localScale;
            s.x = Mathf.Abs(s.x) * (dir.x < 0f ? -1f : 1f);
            transform.localScale = s;
        }
    }

    // ════════════════════════════════════════
    //  외부 API
    // ════════════════════════════════════════
    /// <summary>씬 전환 등으로 동료를 순간이동시킬 때 경로 기록을 초기화합니다.</summary>
    public void TeleportTo(Vector2 position)
    {
        transform.position = position;
        _pathHistory.Clear();
        if (_player != null) _lastRecordedPos = _player.position;
    }
}
