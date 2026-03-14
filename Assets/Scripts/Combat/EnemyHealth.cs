using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Header("적 스탯")]
    public float maxHealth = 100f;
    public float currentHealth;
    
    [Header("사망 효과")]
    public GameObject deathEffect;

    void Start()
    {
        currentHealth = maxHealth;
    }

    // 현실 모드 전용 데미지 함수
    public void TakeRealityDamage(float damage)
    {
        currentHealth -= damage;
        Debug.Log($"적 피격! 남은 체력: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Debug.Log("적 사망!");
        if (deathEffect != null)
        {
            Instantiate(deathEffect, transform.position, Quaternion.identity);
        }
        Destroy(gameObject);
    }
}
