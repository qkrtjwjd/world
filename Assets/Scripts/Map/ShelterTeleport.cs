using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class ShelterTeleport : MonoBehaviour
{
    [Header("설정")]
    public KeyCode   teleportKey      = KeyCode.H;
    public string    shelterSceneName = SceneNames.Shelter;
    public string[]  allowedScenes;

    private Transform _playerTransform;
    private HashSet<string> _allowedScenesSet;

    void Start()
    {
        if (PlayerStats.Instance != null)
            _playerTransform = PlayerStats.Instance.transform;
        else
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) _playerTransform = p.transform;
        }

        _allowedScenesSet = new HashSet<string>(allowedScenes ?? System.Array.Empty<string>());
    }

    void Update()
    {
        if (Input.GetKeyDown(teleportKey))
            TryTeleportToShelter();
    }

    void TryTeleportToShelter()
    {
        string current = SceneManager.GetActiveScene().name;
        if (!_allowedScenesSet.Contains(current)) return;

        PlayerPrefs.SetString("LastScene", current);

        if (_playerTransform != null)
        {
            GameState.lastPosition     = _playerTransform.position;
            GameState.hasPositionSaved = true;
            GameState.battleReturn.returnSceneName  = current;
        }

        if (TransitionManager.Instance != null)
            TransitionManager.Instance.DoSceneTransition(shelterSceneName);
        else
            SceneManager.LoadScene(shelterSceneName);
    }
}
