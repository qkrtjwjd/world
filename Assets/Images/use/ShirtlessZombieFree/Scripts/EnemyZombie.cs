using UnityEngine;

public class EnemyZombie : MonoBehaviour
{
    public int maxHP = 100;
    public int currentHP;
    
    Animator anim;

    void Start()
    {
        anim = GetComponent<Animator>();
        currentHP = maxHP;
    }

    // 심판(BattleSystem)이 호출하는 공격 함수
    public void AttackPlayer()
    {
        anim.SetTrigger("doAttack");
    }

    // 플레이어가 때렸을 때 호출 (HP 깎기 -> 죽었는지 심판에게 보고)
    public bool TakeDamage(int damage)
    {
        currentHP -= damage;
        
        if (currentHP <= 0)
        {
            Die();
            return true; // "나 죽었어"라고 리턴
        }
        else
        {
            anim.SetTrigger("doHit");
            return false; // "아직 안 죽었어"
        }
    }

    void Die()
    {
        anim.SetTrigger("doDie");
        Destroy(gameObject, 2.0f); // 2초 뒤 삭제
    }
}