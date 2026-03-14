using UnityEngine;

public class EnemyRemover : MonoBehaviour
{
    void Start()
    {
        // 게임 시작하자마자 장부를 확인
        if (GameState.isZombieDefeated == true)
        {
            // "어? 나 죽었다고 적혀있네?" -> 자폭
            Destroy(gameObject);
        }
    }
}