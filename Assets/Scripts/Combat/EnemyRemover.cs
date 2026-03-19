using UnityEngine;

public class EnemyRemover : MonoBehaviour
{
    void Start()
    {
        if (GameState.defeatedEnemyIDs.Contains(gameObject.name))
            Destroy(gameObject);
    }
}