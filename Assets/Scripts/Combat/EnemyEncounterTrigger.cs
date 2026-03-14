using UnityEngine;

public class EnemyEncounterTrigger : MonoBehaviour
{
    private bool hasEncountered = false;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        CheckEncounter(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        CheckEncounter(other.gameObject);
    }

    void CheckEncounter(GameObject obj)
    {
        // ★ 전투에서 방금 돌아온 상태라면 충돌 무시 ★
        if (GameState.isComingFromBattle) return;

        // 이미 조우했거나 플레이어가 아니면 패스
        if (hasEncountered || !obj.CompareTag("Player")) return;

        // EncounterManager가 존재하는지 확인
        if (EncounterManager.Instance != null)
        {
            hasEncountered = true; // 중복 호출 방지
            
            // 약간 밀쳐내기 (선택하는 동안 겹쳐있으면 버그 날 수 있으므로)
            Rigidbody2D rb = obj.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                Vector2 dir = (obj.transform.position - transform.position).normalized;
                rb.AddForce(dir * 5f, ForceMode2D.Impulse);
            }

            EncounterManager.Instance.StartEncounter(this.gameObject);
        }
    }
    
    // 전투가 끝나거나 취소됐을 때 다시 조우 가능하게 하려면 외부에서 false로
    public void ResetEncounter()
    {
        hasEncountered = false;
    }
}
