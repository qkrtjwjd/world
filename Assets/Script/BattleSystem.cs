using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

// 게임 상태를 정의 (내 차례, 적 차례, 이김, 짐)
public enum BattleState { START, PLAYERTURN, ENEMYTURN, WON, LOST }

public class BattleSystem : MonoBehaviour
{
    public BattleState state;

    public PlayerController player;
    public EnemyZombie enemy;

    void Start()
    {
        state = BattleState.START;
        StartCoroutine(SetupBattle());
    }

    // 1. 전투 시작 세팅
    IEnumerator SetupBattle()
    {
        Debug.Log("전투 시작! 1초 뒤 플레이어 턴입니다.");
        yield return new WaitForSeconds(1f);

        state = BattleState.PLAYERTURN;
        PlayerTurn();
    }

    // 2. 플레이어 턴 알림
    void PlayerTurn()
    {
        Debug.Log("나의 턴! (Z: 공격, X: 방어)");
        // 여기서 아무것도 안 해도 됨. PlayerController가 입력을 기다릴 것임.
    }

    // 3. 플레이어 공격 완료 -> 적 턴으로 넘기기
    public void PlayerAttackSuccess()
    {
        if (state != BattleState.PLAYERTURN) return;

        // 적에게 데미지 입힘 (예: 10)
        bool isDead = enemy.TakeDamage(20);

        if (isDead)
        {
            state = BattleState.WON;
            EndBattle();
        }
        else
        {
            state = BattleState.ENEMYTURN;
            StartCoroutine(EnemyTurn());
        }
    }

    // 4. 적 턴 진행
    IEnumerator EnemyTurn()
    {
        Debug.Log("좀비의 턴! 공격 준비 중...");
        yield return new WaitForSeconds(2f); // 좀비가 바로 때리면 정신없으니 2초 딜레이

        // 좀비가 플레이어 공격
        enemy.AttackPlayer();
        
        // 플레이어가 맞았는지 확인 (여기선 단순화해서 플레이어가 바로 맞는다고 가정)
        player.TakeDamage(10); 

        yield return new WaitForSeconds(1f); // 때리고 잠시 대기

        // 플레이어가 죽었는지 체크는 PlayerController에서 하겠지만, 흐름상 다시 턴을 넘김
        if (state != BattleState.LOST)
        {
            state = BattleState.PLAYERTURN;
            PlayerTurn();
        }
    }

    // 5. 전투 종료
    void EndBattle()
    {
        if (state == BattleState.WON)
        {
            Debug.Log("승리! 2D 맵으로 돌아갑니다.");
            
            // 좀비 죽음 기록 (세미콜론 ; 포함)
            GameState.isZombieDefeated = true; 

            // 2초 뒤 맵으로 이동
            Invoke("ReturnToMap", 2f); 
        }
        else if (state == BattleState.LOST)
        {
            Debug.Log("패배했습니다... 부상당한 채로 도망갑니다."); 
            
            // 패배해도 맵으로 이동
            Invoke("ReturnToMap", 2f); 
        }
    }

    // (참고) ReturnToMap 함수는 그대로 두거나 없으면 아래 내용 추가
    void ReturnToMap()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("MapScene");
    }
}