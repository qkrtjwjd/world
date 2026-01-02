using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public BattleSystem battleSystem; // 심판을 알고 있어야 함
    public int maxHP = 100;
    public int currentHP;

    Animator anim;

    void Start()
    {
        anim = GetComponent<Animator>();
        currentHP = maxHP;
    }

    void Update()
    {
        // 내 턴이 아니면 키보드 입력 무시!
        if (battleSystem.state != BattleState.PLAYERTURN) return;

        // Z키로 공격 -> 턴 종료
        if (Input.GetKeyDown(KeyCode.Z))
        {
            StartCoroutine(AttackRoutine());
        }

        // X키로 방어 (이번 턴 넘기기) -> 방어는 나중에 구현해도 됨
    }

    // 공격 동작과 턴 넘기기를 자연스럽게 연결
    System.Collections.IEnumerator AttackRoutine()
    {
        // 1. 공격 애니메이션
        anim.SetTrigger("doAttack");

        // 2. 칼이 닿을 때까지 잠깐 대기 (0.5초 정도)
        yield return new WaitForSeconds(0.5f);

        // 3. 심판에게 "나 때렸어! 턴 넘겨줘"라고 보고
        battleSystem.PlayerAttackSuccess();
    }

    public void TakeDamage(int damage)
    {
        currentHP -= damage;
        anim.SetTrigger("doHit"); // 맞는 모션

        Debug.Log("플레이어 체력: " + currentHP);

        if (currentHP <= 0)
        {
            anim.SetTrigger("doDie");
            battleSystem.state = BattleState.LOST; // 졌다고 심판에게 알림
        }
    }
}