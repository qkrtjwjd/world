using UnityEngine;

public class EnemyEncounterTrigger : MonoBehaviour
{
    private bool _hasEncountered = false;

    private void OnCollisionEnter2D(Collision2D collision)  => CheckEncounter(collision.gameObject);
    private void OnTriggerEnter2D(Collider2D other)         => CheckEncounter(other.gameObject);

    void CheckEncounter(GameObject obj)
    {
        if (GameState.battleReturn.IsBlocked) return;
        if (_hasEncountered || !obj.CompareTag("Player")) return;
        if (EncounterManager.Instance == null) return;

        _hasEncountered = true;

        Rigidbody2D rb = obj.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            Vector2 dir = (obj.transform.position - transform.position).normalized;
            rb.AddForce(dir * 5f, ForceMode2D.Impulse);
        }

        EncounterManager.Instance.StartEncounter(gameObject);
    }

    public void ResetEncounter() => _hasEncountered = false;
}