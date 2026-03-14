using UnityEngine;

public class EnemySymbol : MonoBehaviour
{
    [Header("전투 설정")]
    [Tooltip("이 적과 부딪혔을 때 전투 씬에서 나타날 실제 적 프리팹")]
    public GameObject battleEnemyPrefab;

    private void Awake()
    {
        // ★ 처치 목록(defeatedEnemyIDs)에 내 이름이 있으면 즉시 삭제 ★
        if (GameState.defeatedEnemyIDs.Contains(gameObject.name))
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // ★ 전투에서 방금 돌아온 상태라면 충돌 무시 ★
        if (GameState.isComingFromBattle) return;

        // 플레이어와 부딪혔는지 확인 (태그가 Player여야 함)
        if (collision.CompareTag("Player"))
        {
            if (EncounterManager.Instance != null)
            {
                // 부딪힌 이 오브젝트 정보를 매니저에 전달
                EncounterManager.Instance.StartEncounter(gameObject);
                
                // 전투에 쓰일 프리팹 정보를 임시 저장 (BattleSystem에서 참조용)
                BattleData.nextEnemyPrefab = battleEnemyPrefab;
            }
        }
    }
}