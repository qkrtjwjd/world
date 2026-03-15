using UnityEngine;
using UnityEngine.SceneManagement;

public class ShelterReturn : MonoBehaviour
{
    [Header("설정")]
    public KeyCode interactKey = KeyCode.E;

    private bool _isPlayerNear = false;

    void Update()
    {
        if (_isPlayerNear && Input.GetKeyDown(interactKey))
            ReturnToOriginalWorld();
    }

    public void ReturnToOriginalWorld()
    {
        string target = !string.IsNullOrEmpty(GameState.returnSceneName)
            ? GameState.returnSceneName
            : PlayerPrefs.GetString("LastScene", SceneNames.Map);

        SceneManager.LoadScene(target);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        _isPlayerNear = true;
        InteractionTextUI.Instance?.Show("E키를 눌러 나가기");
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        _isPlayerNear = false;
        InteractionTextUI.Instance?.Hide();
    }
}