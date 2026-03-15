using UnityEngine;

/// <summary>
/// 핵앤슬래시 전투에서 적의 체력을 관리합니다.
/// 사망 시 HackSlashCombatManager 에 통보합니다.
/// </summary>
public class EnemyHealth : MonoBehaviour
{
    [Header("체력")]
    public float maxHealth     = 100f;
    public float currentHealth = 100f;

    [Header("사망 이펙트")]
    public GameObject deathEffect;

    void Start()
    {
        currentHealth = maxHealth;
    }

    /// <summary>RealityCombatController 에서 호출됩니다.</summary>
    public void TakeRealityDamage(float damage)
    {
        currentHealth -= damage;
        currentHealth  = Mathf.Max(0f, currentHealth);

        Debug.Log($"[EnemyHealth] {gameObject.name} 피격: -{damage:F1}  남은 HP: {currentHealth:F1}");

        if (currentHealth <= 0f)
            Die();
    }

    void Die()
    {
        // HackSlashCombatManager 에 사망 통보
        HackSlashCombatManager.Instance?.NotifyEnemyDead(gameObject);

        if (deathEffect != null)
            Instantiate(deathEffect, transform.position, Quaternion.identity);

        // 짧은 딜레이 후 오브젝트 삭제 (이펙트가 보일 시간 확보)
        Destroy(gameObject, 0.1f);
    }
}