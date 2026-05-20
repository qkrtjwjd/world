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
    [Tooltip("기본 공격력 (DamageCalculator의 attack 스탯)")]
    public float attackDamage = 50f;
    [Tooltip("비약점 타격 시 데미지 배율 (1.0 = 동일, 0.6 = 60%)")]
    public float nonWeakPointMultiplier = 0.6f;
    [Tooltip("공격 쿨타임 (초)")]
    public float attackCooldown = 0.3f;
    [Tooltip("적을 감지할 레이어")]
    public LayerMask enemyLayer;

    [Header("■ 플레이어 스탯 (DamageCalculator용)")]
    [Tooltip("플레이어 레벨. 적 레벨과의 차이로 데미지 보정.")]
    public int playerLevel    = 1;
    [Tooltip("명중률 0~100. (acc - eva) 가 명중 확률.")]
    [Range(0, 100)] public int playerAccuracy  = 95;
    [Tooltip("크리티컬 확률 0~100.")]
    [Range(0, 100)] public int playerCritRate  = 10;
    [Tooltip("크리티컬 배율.")]
    public float playerCritMultiplier = 1.5f;

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
        HackSlashCombatManager.Instance?.NotifyCombatActivity();

        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera == null) { Debug.LogError("[RealityCombatController] Main Camera를 찾을 수 없습니다."); return; }

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
        // 약점 판정
        if (!hit.collider.TryGetComponent(out WeakPoint wp))
            hit.collider.transform.parent?.TryGetComponent(out wp);

        bool  isWeak     = wp != null;
        float weakPointMul = isWeak ? wp.damageMultiplier : nonWeakPointMultiplier;

        // ── EnemyHealth (현실 전투용) ──
        if (!hit.collider.TryGetComponent(out EnemyHealth eh))
            hit.collider.transform.parent?.TryGetComponent(out eh);
        if (eh != null)
        {
            DamageResult result = DamageCalculator.CalculateRaw(
                playerLevel, Mathf.RoundToInt(attackDamage), playerAccuracy, playerCritRate, playerCritMultiplier,
                eh.level,    eh.defense,                     eh.evasion,
                weakPointMultiplier: weakPointMul);

            if (!result.isMiss)
                eh.TakeRealityDamage(result.amount);

            // 사망 통보
            if (eh.currentHealth <= 0)
                HackSlashCombatManager.Instance?.NotifyEnemyDead(eh.gameObject);
        }

        // ── Unit (턴제 배틀 유닛이 씬에 있는 경우 폴백) ──
        else
        {
            Unit unit = hit.collider.GetComponentInParent<Unit>();
            if (unit != null)
            {
                DamageResult result = DamageCalculator.CalculateRaw(
                    playerLevel, Mathf.RoundToInt(attackDamage), playerAccuracy, playerCritRate, playerCritMultiplier,
                    unit.level,  unit.defense,                   unit.evasion,
                    weakPointMultiplier: weakPointMul);

                unit.TakeDamage(result);
            }
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
        if (fx != null)
        {
            if (EffectPool.Instance != null) EffectPool.Instance.Play(fx, hit.point);
            else                             Instantiate(fx, hit.point, Quaternion.identity);
        }
    }
}