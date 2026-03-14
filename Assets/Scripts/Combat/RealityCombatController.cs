using UnityEngine;
using UnityEngine.InputSystem; // Input System 사용 가정

public class RealityCombatController : MonoBehaviour
{
    [Header("■ 전투 설정")]
    [Tooltip("공격 사거리 (레이캐스트 길이)")]
    public float attackRange = 5f;
    [Tooltip("기본 공격력 (약점 타격 시 이 데미지의 100% 적용)")]
    public float attackDamage = 50f;
    [Tooltip("공격 쿨타임 (초)")]
    public float attackCooldown = 0.5f;
    [Tooltip("적을 감지할 레이어")]
    public LayerMask enemyLayer;

    [Header("■ 리스크 설정")]
    [Tooltip("적 처치 시 증가할 트라우마(멘탈 감소량)")]
    public float traumaOnKill = 5f;
    [Tooltip("공격 적중 시 증가할 트라우마(멘탈 감소량)")]
    public float traumaOnHit = 1f;
    [Tooltip("전투 행동 시 감소할 인형화 수치")]
    public float puppetReductionOnCombat = 2f;

    [Header("■ 시각 효과")]
    [Tooltip("일반 타격 이펙트")]
    public GameObject hitEffect;
    [Tooltip("약점(치명타) 타격 이펙트")]
    public GameObject bloodEffect;

    private float lastAttackTime;
    private PlayerStats stats;
    private Camera mainCamera;

    void Start()
    {
        stats = GetComponent<PlayerStats>();
        if (stats == null) stats = PlayerStats.Instance;
        mainCamera = Camera.main;
    }

    void Update()
    {
        // 쿨타임 체크
        if (Time.time < lastAttackTime + attackCooldown) return;

        // 마우스 체크 (마우스가 없거나 연결 안 된 경우 방지)
        if (Mouse.current == null) return;

        // 마우스 왼쪽 클릭 (New Input System or Legacy)
        if (Mouse.current.leftButton.wasPressedThisFrame) 
        {
            PerformAttack();
        }
    }

    void PerformAttack()
    {
        lastAttackTime = Time.time;
        
        // 1. 마우스 방향 계산
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector2 worldPoint = mainCamera.ScreenToWorldPoint(mousePos);
        Vector2 origin = transform.position;
        Vector2 direction = (worldPoint - origin).normalized;

        // 2. 레이캐스트 발사
        RaycastHit2D hit = Physics2D.Raycast(origin, direction, attackRange, enemyLayer);

        // 디버그 라인
        Debug.DrawRay(origin, direction * attackRange, Color.red, 0.5f);

        if (hit.collider != null)
        {
            HandleHit(hit);
        }
    }

    void HandleHit(RaycastHit2D hit)
    {
        float finalDamage = attackDamage;
        bool isWeakPoint = false;

        // 최적화: TryGetComponent 사용 (가비지 할당 감소)
        if (hit.collider.TryGetComponent<WeakPoint>(out WeakPoint weakPoint))
        {
            // 약점 명중!
            isWeakPoint = true;
            if (weakPoint.isCore)
            {
                Debug.Log("약점(핵) 명중! 치명타!");
            }
            else
            {
                // 약점 스크립트는 있지만 핵은 아님 (그냥 부위) -> 그래도 약점 배율 적용 가능
                finalDamage *= weakPoint.damageMultiplier; 
            }
        }
        else
        {
            // 약점 아님 -> 90% 데미지 감소
            finalDamage *= 0.1f;
            Debug.Log($"빗나감... 데미지 90% 감소 ({finalDamage})");
        }

        // 적 체력 스크립트 찾기 (부모나 자신에게서)
        EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
        if (enemy != null)
        {
            enemy.TakeRealityDamage(finalDamage);
        }
        else
        {
             // 혹시 EnemyHealth가 없으면 기존 방식(SendMessage)으로 폴백
             hit.collider.SendMessageUpwards("TakeRealityDamage", finalDamage, SendMessageOptions.DontRequireReceiver);
        }

        // ■ 트라우마 증가 및 인형화 감소
        if (stats != null)
        {
            stats.AddTrauma(traumaOnHit);
            stats.ReducePuppetization(puppetReductionOnCombat);
        }

        // 효과 재생
        if (isWeakPoint && bloodEffect != null)
        {
            Instantiate(bloodEffect, hit.point, Quaternion.identity);
        }
        else if (hitEffect != null)
        {
            Instantiate(hitEffect, hit.point, Quaternion.identity);
        }
    }
}
