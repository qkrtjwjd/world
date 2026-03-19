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
        EncounterManager.Instance.StartEncounter(gameObject);
    }

    public void ResetEncounter() => _hasEncountered = false;
}