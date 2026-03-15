using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;

public class ShelterTeleport : MonoBehaviour
{
    [Header("설정")]
    public KeyCode   teleportKey      = KeyCode.H;
    public string    shelterSceneName = SceneNames.Shelter;
    public string[]  allowedScenes;

    void Update()
    {
        if (Input.GetKeyDown(teleportKey))
            TryTeleportToShelter();
    }

    void TryTeleportToShelter()
    {
        string current = SceneManager.GetActiveScene().name;
        if (!allowedScenes.Contains(current)) return;

        PlayerPrefs.SetString("LastScene", current);

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            GameState.lastPosition     = player.transform.position;
            GameState.hasPositionSaved = true;
            GameState.returnSceneName  = current;
        }

        SceneManager.LoadScene(shelterSceneName);
    }
}