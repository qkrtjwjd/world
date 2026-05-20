using System.Collections;
using System.Collections.Generic;
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

    [Header("AI 프로파일 (선택)")]
    [Tooltip("할당하면 가중치 기반 행동 선택을 사용합니다. null이면 기본 근접 공격만 수행.")]
    public EnemyAIProfile aiProfile;

    [Header("AI 튜닝")]
    [Tooltip("이 값(공격범위 ×) 보다 멀어지면 추격 재개. 0.8 = 공격범위의 80%.")]
    [Range(0.1f, 1f)] public float attackRangeMovementThreshold = 0.8f;
    [Tooltip("실제 데미지 판정 시 사거리 보정치. 1.1 = 공격범위의 110% 까지 인정.")]
    [Range(1f, 2f)]   public float attackValidationMultiplier   = 1.1f;
    [Tooltip("정지/넉백 시 속도 감쇄 비율 (0~1).")]
    [Range(0f, 1f)]   public float velocityDampingFactor        = 0.3f;
    [Tooltip("플레이어 재탐색 간격 (초).")]
    public float findPlayerInterval = 0.5f;

    // ─────────────────────────────────────────────
    //  내부 상태
    // ─────────────────────────────────────────────
    private enum AIState { Chase, Idle, Knockback, Flee }

    private Transform    _player;
    private Rigidbody2D  _rb;
    private Rigidbody2D  _playerRb;
    private EnemyHealth  _enemyHealth;
    private AIState      _state              = AIState.Chase;
    private AIState      _stateBeforeKnockback = AIState.Chase;
    private float        _attackTimer        = 0f;
    private float        _findPlayerTimer    = 0f;

    // 프로파일 모드 상태
    private bool                                _isProfileActionRunning = false;
    private readonly Dictionary<EnemyAction, float> _profileCooldowns = new Dictionary<EnemyAction, float>();

    // WaitForSeconds 캐시 (매 호출마다 new 방지)
    private WaitForSeconds _waitAttackDuration;
    private WaitForSeconds _waitKnockbackDuration;
    private WaitForSeconds _waitFleeDuration;

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

        _enemyHealth = GetComponent<EnemyHealth>();

        _waitAttackDuration   = new WaitForSeconds(attackDuration);
        _waitKnockbackDuration = new WaitForSeconds(knockbackDuration);
        _waitFleeDuration     = new WaitForSeconds(fleeDuration);
    }

    void OnEnable()
    {
        // 참조가 이미 유효하면 재탐색 생략
        if (_player != null) return;
        FindPlayer();
    }

    void Update()
    {
        // 플레이어가 적보다 늦게 활성화된 경우 재탐색 (0.5초 쿨타임으로 스팸 방지)
        if (_player == null)
        {
            _findPlayerTimer -= Time.deltaTime;
            if (_findPlayerTimer <= 0f)
            {
                FindPlayer();
                _findPlayerTimer = findPlayerInterval;
            }
            return;
        }

        if (_state != AIState.Chase) return;

        _attackTimer = Mathf.Max(0f, _attackTimer - Time.deltaTime);

        // 프로파일 모드: 가중치 추첨 행동 사용
        if (aiProfile != null)
        {
            if (!_isProfileActionRunning && _attackTimer <= 0f)
            {
                EnemyAction action = aiProfile.PickAction(this, _player, _profileCooldowns);
                if (action != null)
                {
                    _profileCooldowns[action] = Time.time;
                    StartCoroutine(RunProfileAction(action));
                }
                else
                {
                    // 후보 없음 → 짧은 대기로 폭주 방지
                    _attackTimer = aiProfile.idleFallbackDelay;
                }
            }
            return;
        }

        // 기본 모드: 사거리 안 + 쿨다운 종료 시 근접 공격
        float dist = Vector2.Distance(transform.position, _player.position);
        if (dist <= attackRange && _attackTimer <= 0f)
        {
            StartCoroutine(PerformAttack());
        }
    }

    IEnumerator RunProfileAction(EnemyAction action)
    {
        _isProfileActionRunning = true;
        try
        {
            yield return action.Execute(this, _player);
        }
        finally
        {
            _isProfileActionRunning = false;
        }
    }

    /// <summary>프로파일의 MeleeAttackAction이 호출하는 진입점. 기존 PerformAttack과 같은 로직.</summary>
    public void PerformProfileMeleeAttack()
    {
        if (_attackTimer > 0f) return;
        StartCoroutine(PerformAttack());
    }

    void FixedUpdate()
    {
        if (_state == AIState.Flee)
        {
            // 도주: 플레이어 반대 방향으로 질주
            if (_player != null)
            {
                Vector2 dir = ((Vector2)transform.position - (Vector2)_player.position).normalized;
                _rb.linearVelocity = dir * fleeSpeed;

                if (spriteRenderer != null)
                    spriteRenderer.flipX = dir.x > 0f;
            }
            return;
        }

        if (_state == AIState.Idle || _state == AIState.Knockback || _player == null)
        {
            if (_state == AIState.Idle || _state == AIState.Knockback)
                _rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, Vector2.zero, velocityDampingFactor);
            return;
        }

        float dist = Vector2.Distance(transform.position, _player.position);

        if (dist > attackRange * attackRangeMovementThreshold)
        {
            Vector2 dir = ((Vector2)_player.position - (Vector2)transform.position).normalized;
            _rb.linearVelocity = dir * moveSpeed;

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
        HackSlashCombatManager.Instance?.NotifyCombatActivity();

        yield return _waitAttackDuration;

        // SetChase(false) 또는 도주 중이면 데미지 취소 (모드 전환 시 잔여 공격 방지)
        if (_state == AIState.Idle || _state == AIState.Flee) yield break;

        // 아직 사거리 안이면 실제 피해
        if (_player != null)
        {
            float dist = Vector2.Distance(transform.position, _player.position);
            if (dist <= attackRange * attackValidationMultiplier)
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
        _stateBeforeKnockback = _state;
        _state = AIState.Knockback;
        _rb.linearVelocity = direction.normalized * knockbackForce;
        yield return _waitKnockbackDuration;
        _state = _stateBeforeKnockback;
        _rb.linearVelocity = Vector2.zero;
    }

    // ─────────────────────────────────────────────
    //  외부 제어
    // ─────────────────────────────────────────────
    public void SetChase(bool active)
    {
        if (!active && _state == AIState.Chase)
        {
            _state = AIState.Idle;
            _rb.linearVelocity = Vector2.zero;
        }
        else if (active && _state == AIState.Idle)
        {
            _state = AIState.Chase;
        }
    }

    /// <summary>체력 10% 미만 시 EnemyHealth 에서 호출. 도주를 시작합니다.</summary>
    public void StartFlee()
    {
        if (_state == AIState.Flee) return;
        _state = AIState.Flee;
        StartCoroutine(FleeRoutine());
    }

    IEnumerator FleeRoutine()
    {
        yield return _waitFleeDuration;

        // EnemyHealth 에 도주 완료 위임 (귀환 감시 + 매니저 통보)
        if (_enemyHealth != null) _enemyHealth.OnFledComplete();
        else
        {
            HackSlashCombatManager.Instance?.NotifyEnemyFled(gameObject);
            Destroy(gameObject);
        }
    }

    /// <summary>EnemyHealth 에서 귀환 시 호출. 도주 상태를 초기화합니다.</summary>
    public void ResetFlee()
    {
        _state = AIState.Chase;
        _rb.linearVelocity = Vector2.zero;
    }

    // ─────────────────────────────────────────────
    //  유틸
    // ─────────────────────────────────────────────
    void FindPlayer()
    {
        if (PlayerStats.Instance != null)
            _player = PlayerStats.Instance.transform;
        else
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) _player = p.transform;
        }

        if (_player != null)
        {
            _playerRb = _player.GetComponent<Rigidbody2D>();
            if (_playerRb == null)
                _playerRb = _player.GetComponentInChildren<Rigidbody2D>();
            if (_playerRb == null)
                _playerRb = _player.GetComponentInParent<Rigidbody2D>();
            if (_playerRb == null)
                Debug.LogWarning("[EnemyAI] 플레이어에서 Rigidbody2D를 찾을 수 없습니다. 넉백이 작동하지 않습니다.");
        }
    }

    // 에디터에서 공격 범위 시각화
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
