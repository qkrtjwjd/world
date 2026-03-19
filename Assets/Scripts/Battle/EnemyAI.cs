using System.Collections;
using UnityEngine;

/// <summary>
/// 핵앤슬래시 전투용 적 AI.
/// - 플레이어를 추격합니다.
/// - 근접 거리에 들어오면 근접 공격을 가합니다.
/// - EnemyHealth 가 함께 있어야 합니다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAI : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  인스펙터
    // ─────────────────────────────────────────────
    [Header("이동")]
    [Tooltip("추격 속도 (단위/초)")]
    public float moveSpeed = 2.5f;

    [Header("근접 공격")]
    [Tooltip("공격이 시작되는 거리")]
    public float attackRange = 1.0f;
    [Tooltip("한 번 공격에 가하는 피해")]
    public float attackDamage = 10f;
    [Tooltip("공격 쿨타임 (초)")]
    public float attackCooldown = 1.5f;
    [Tooltip("공격 판정 유지 시간 (초) — 히트박스가 켜지는 시간")]
    public float attackDuration = 0.2f;

    [Header("피격 넉백")]
    [Tooltip("피격 시 밀려나는 힘")]
    public float knockbackForce = 4f;
    [Tooltip("넉백이 유지되는 시간 (초)")]
    public float knockbackDuration = 0.15f;

    [Header("스프라이트")]
    [Tooltip("연결된 SpriteRenderer (방향 전환 등)")]
    public SpriteRenderer spriteRenderer;

    [Header("도주")]
    [Tooltip("도주 속도 (단위/초)")]
    public float fleeSpeed = 4f;
    [Tooltip("도주 시작 후 이 시간(초)이 지나면 씬에서 사라짐")]
    public float fleeDuration = 3f;

    // ─────────────────────────────────────────────
    //  내부 상태
    // ─────────────────────────────────────────────
    private Transform    _player;
    private Rigidbody2D  _rb;
    private Rigidbody2D  _playerRb;
    private bool         _isChasing    = true;
    private bool         _isKnockedBack = false;
    private float        _attackTimer  = 0f;
    private bool         _isFleeing    = false;

    // ─────────────────────────────────────────────
    //  Unity
    // ─────────────────────────────────────────────
    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;  // 탑다운 2D
        _rb.constraints  = RigidbodyConstraints2D.FreezeRotation;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    void OnEnable()
    {
        // 활성화될 때 플레이어 참조 갱신
        FindPlayer();
    }

    void Update()
    {
        if (_isFleeing || !_isChasing || _player == null) return;

        _attackTimer = Mathf.Max(0f, _attackTimer - Time.deltaTime);

        float dist = Vector2.Distance(transform.position, _player.position);

        // 공격 범위 내 && 쿨타임 끝 → 공격
        if (dist <= attackRange && _attackTimer <= 0f)
        {
            StartCoroutine(PerformAttack());
        }
    }

    void FixedUpdate()
    {
        if (_isFleeing)
        {
            // 도주: 플레이어 반대 방향으로 질주
            if (_player != null)
            {
                Vector2 dir = ((Vector2)transform.position - (Vector2)_player.position).normalized;
                _rb.linearVelocity = dir * fleeSpeed;

                if (spriteRenderer != null)
                    spriteRenderer.flipX = dir.x > 0f; // 도망치는 방향이므로 반전 기준 반대
            }
            return;
        }

        if (!_isChasing || _isKnockedBack || _player == null)
        {
            if (!_isChasing || _isKnockedBack)
                _rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, Vector2.zero, 0.3f);
            return;
        }

        float dist = Vector2.Distance(transform.position, _player.position);

        if (dist > attackRange * 0.8f)
        {
            Vector2 dir = ((Vector2)_player.position - (Vector2)transform.position).normalized;
            _rb.linearVelocity = dir * moveSpeed;

            // 방향에 따라 스프라이트 반전
            if (spriteRenderer != null)
                spriteRenderer.flipX = dir.x < 0f;
        }
        else
        {
            _rb.linearVelocity = Vector2.zero;
        }
    }

    // ─────────────────────────────────────────────
    //  공격
    // ─────────────────────────────────────────────
    IEnumerator PerformAttack()
    {
        _attackTimer = attackCooldown;

        // 짧은 동작 대기
        yield return new WaitForSeconds(attackDuration);

        // 아직 사거리 안이면 실제 피해
        if (_player != null)
        {
            float dist = Vector2.Distance(transform.position, _player.position);
            if (dist <= attackRange * 1.1f)
            {
                PlayerStats.Instance?.TakeDamage(attackDamage);

                // 플레이어에게 넉백
                Rigidbody2D playerRb = _playerRb;
                if (playerRb != null)
                {
                    Vector2 dir = ((Vector2)_player.position - (Vector2)transform.position).normalized;
                    playerRb.AddForce(dir * knockbackForce, ForceMode2D.Impulse);
                }
            }
        }
    }

    // ─────────────────────────────────────────────
    //  피격 시 넉백 (RealityCombatController 에서 호출)
    // ─────────────────────────────────────────────
    public void ApplyKnockback(Vector2 direction)
    {
        StartCoroutine(KnockbackRoutine(direction));
    }

    IEnumerator KnockbackRoutine(Vector2 direction)
    {
        _isKnockedBack = true;
        _rb.linearVelocity = direction.normalized * knockbackForce;
        yield return new WaitForSeconds(knockbackDuration);
        _isKnockedBack = false;
        _rb.linearVelocity = Vector2.zero;
    }

    // ─────────────────────────────────────────────
    //  외부 제어
    // ─────────────────────────────────────────────
    public void SetChase(bool active)
    {
        _isChasing = active;
        if (!active && !_isFleeing) _rb.linearVelocity = Vector2.zero;
    }

    /// <summary>체력 10% 미만 시 EnemyHealth 에서 호출. 도주를 시작합니다.</summary>
    public void StartFlee()
    {
        if (_isFleeing) return;
        _isFleeing = true;
        _isChasing = false;
        Debug.Log($"[EnemyAI] {gameObject.name} 도주 시작!");
        StartCoroutine(FleeRoutine());
    }

    IEnumerator FleeRoutine()
    {
        yield return new WaitForSeconds(fleeDuration);

        // EnemyHealth 에 도주 완료 위임 (귀환 감시 + 매니저 통보)
        EnemyHealth eh = GetComponent<EnemyHealth>();
        if (eh != null) eh.OnFledComplete();
        else
        {
            HackSlashCombatManager.Instance?.NotifyEnemyFled(gameObject);
            Destroy(gameObject);
        }
    }

    /// <summary>EnemyHealth 에서 귀환 시 호출. 도주 상태를 초기화합니다.</summary>
    public void ResetFlee()
    {
        _isFleeing = false;
        _isChasing = true;
        _rb.linearVelocity = Vector2.zero;
    }

    // ─────────────────────────────────────────────
    //  유틸
    // ─────────────────────────────────────────────
    void FindPlayer()
    {
        if (PlayerStats.Instance != null)
        {
            _player = PlayerStats.Instance.transform;
        }
        else
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) _player = p.transform;
        }

        if (_player != null)
            _playerRb = _player.GetComponent<Rigidbody2D>();
    }

    // 에디터에서 공격 범위 시각화
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
