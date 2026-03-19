using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;

public class ShelterTeleport : MonoBehaviour
{
    [Header("설정")]
    public KeyCode   teleportKey      = KeyCode.H;
    public string    shelterSceneName = SceneNames.Shelter;
    public string[]  allowedScenes;

    private Transform _playerTransform;

    void Start()
    {
        if (PlayerStats.Instance != null)
            _playerTransform = PlayerStats.Instance.transform;
        else
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) _playerTransform = p.transform;
        }
    }

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

        if (_playerTransform != null)
        {
            GameState.lastPosition     = _playerTransform.position;
            GameState.hasPositionSaved = true;
            GameState.returnSceneName  = current;
        }

        SceneManager.LoadScene(shelterSceneName);
    }
}