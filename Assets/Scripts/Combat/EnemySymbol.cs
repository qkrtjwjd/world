using UnityEngine;

public class EnemySymbol : MonoBehaviour
{
    [Header("전투 설정")]
    [Tooltip("전투 씬에서 나타날 실제 적 프리팹")]
    public GameObject battleEnemyPrefab;

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
        {
            EncounterManager.Instance.StartEncounter(gameObject);
            BattleData.nextEnemyPrefab = battleEnemyPrefab;
        }
    }
}