using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 핵앤슬래시 플레이어 공격 컨트롤러.
/// 마우스 클릭 방향으로 레이캐스트를 발사해 적을 공격합니다.
/// HackSlashCombatManager 에 의해 활성화/비활성화됩니다.
/// </summary>
public class RealityCombatController : MonoBehaviour
{
    [Header("■ 전투 설정")]
    [Tooltip("공격 사거리 (레이캐스트 길이)")]
    public float attackRange = 5f;
    [Tooltip("기본 공격력 (약점 타격 시 100% 적용)")]
    public float attackDamage = 50f;
    [Tooltip("비약점 타격 시 데미지 배율 (기본 10%)")]
    public float nonWeakPointMultiplier = 0.1f;
    [Tooltip("공격 쿨타임 (초)")]
    public float attackCooldown = 0.3f;
    [Tooltip("적을 감지할 레이어")]
    public LayerMask enemyLayer;

    [Header("■ 리스크")]
    [Tooltip("공격 적중 시 증가할 트라우마(멘탈 감소량)")]
    public float traumaOnHit = 1f;
    [Tooltip("적 처치 시 증가할 트라우마")]
    public float traumaOnKill = 5f;
    [Tooltip("전투 행동 시 감소할 인형화 수치")]
    public float puppetReductionOnCombat = 2f;

    [Header("■ 시각 효과")]
    [Tooltip("일반 타격 이펙트")]
    public GameObject hitEffect;
    [Tooltip("약점 타격 이펙트")]
    public GameObject critEffect;

    [Header("■ 넉백")]
    [Tooltip("공격 시 적에게 가하는 넉백 힘")]
    public float knockbackForce = 3f;

    // ─────────────────────────────────────────────
    //  내부 상태
    // ─────────────────────────────────────────────
    private float  _lastAttackTime = -999f;
    private Camera _mainCamera;

    // ─────────────────────────────────────────────
    //  Unity
    // ─────────────────────────────────────────────
    void Start()
    {
        _mainCamera = Camera.main;

        // enemyLayer 가 0(Nothing)이면 Enemy 레이어 자동 설정
        if (enemyLayer.value == 0)
            enemyLayer = LayerMask.GetMask("Enemy");
        // Enemy 레이어가 없으면 Default
        if (enemyLayer.value == 0)
            enemyLayer = ~0;
    }

    void Update()
    {
        if (Time.time < _lastAttackTime + attackCooldown) return;
        if (Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
            PerformAttack();
    }

    // ─────────────────────────────────────────────
    //  공격
    // ─────────────────────────────────────────────
    void PerformAttack()
    {
        _lastAttackTime = Time.time;

        if (_mainCamera == null) _mainCamera = Camera.main;

        Vector2 mouseWorld = _mainCamera.ScreenToWorldPoint(
            Mouse.current.position.ReadValue());
        Vector2 origin     = transform.position;
        Vector2 direction  = (mouseWorld - origin).normalized;

        Debug.DrawRay(origin, direction * attackRange, Color.red, 0.3f);

        RaycastHit2D hit = Physics2D.Raycast(origin, direction, attackRange, enemyLayer);
        if (hit.collider != null)
            HandleHit(hit, direction);
    }

    void HandleHit(RaycastHit2D hit, Vector2 attackDir)
    {
        bool  isWeak      = false;
        float finalDamage = attackDamage;

        // 약점 체크
        WeakPoint wp = hit.collider.GetComponent<WeakPoint>();
        if (wp == null) wp = hit.collider.GetComponentInParent<WeakPoint>();

        if (wp != null)
        {
            isWeak      = true;
            finalDamage *= wp.damageMultiplier;
        }
        else
        {
            finalDamage *= nonWeakPointMultiplier;
        }

        // ── EnemyHealth (현실 전투용) ──
        EnemyHealth eh = hit.collider.GetComponentInParent<EnemyHealth>();
        if (eh == null) eh = hit.collider.GetComponent<EnemyHealth>();
        if (eh != null)
        {
            eh.TakeRealityDamage(finalDamage);
            // 사망 통보
            if (eh.currentHealth <= 0)
                HackSlashCombatManager.Instance?.NotifyEnemyDead(eh.gameObject);
        }

        // ── Unit (턴제 배틀 유닛이 씬에 있는 경우 폴백) ──
        else
        {
            Unit unit = hit.collider.GetComponentInParent<Unit>();
            if (unit != null)
                unit.TakeDamage(Mathf.RoundToInt(finalDamage));
        }

        // ── 넉백 ──
        EnemyAI ai = hit.collider.GetComponentInParent<EnemyAI>();
        if (ai == null) ai = hit.collider.GetComponent<EnemyAI>();
        if (ai != null)
            ai.ApplyKnockback(attackDir);

        // ── 스탯 영향 ──
        PlayerStats.Instance?.AddTrauma(traumaOnHit);
        PlayerStats.Instance?.ReducePuppetization(puppetReductionOnCombat);

        // ── 이펙트 ──
        GameObject fx = isWeak ? critEffect : hitEffect;
        if (fx != null) Instantiate(fx, hit.point, Quaternion.identity);
    }
}