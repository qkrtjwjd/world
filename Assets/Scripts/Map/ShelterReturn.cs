using UnityEngine;
using UnityEngine.SceneManagement;

public class ShelterReturn : MonoBehaviour
{
    private bool _isPlayerNear = false;

    void Update()
    {
        // 설정 메뉴에서 리바인딩한 상호작용 키 사용
        KeyCode interactKey = SettingsManager.Instance?.keyInteract ?? KeyCode.E;
        if (_isPlayerNear && Input.GetKeyDown(interactKey))
            ReturnToOriginalWorld();
    }

    public void ReturnToOriginalWorld()
    {
        string target = !string.IsNullOrEmpty(GameState.battleReturn.returnSceneName)
            ? GameState.battleReturn.returnSceneName
            : PlayerPrefs.GetString("LastScene", SceneNames.Map);

        if (TransitionManager.Instance != null)
            TransitionManager.Instance.DoSceneTransition(target);
        else
            SceneManager.LoadScene(target);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        _isPlayerNear = true;
        KeyCode k = SettingsManager.Instance?.keyInteract ?? KeyCode.E;
        InteractionTextUI.Instance?.Show($"{k}키를 눌러 나가기");
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        _isPlayerNear = false;
        InteractionTextUI.Instance?.Hide();
    }
}