using UnityEngine;

public class EnemySymbol : MonoBehaviour
{
    [Header("전투 설정")]
    [Tooltip("EnemyDatabase에 등록된 적 ID. 이 값으로 전투 프리팹을 조회합니다.")]
    public string enemyID;

    private void Awake()
    {
        if (GameState.defeatedEnemyIDs.Contains(gameObject.name))
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (GameState.battleReturn.IsBlocked) return;
        if (!collision.CompareTag("Player")) return;

        if (EncounterManager.Instance != null)
            EncounterManager.Instance.StartEncounter(gameObject);
    }
}