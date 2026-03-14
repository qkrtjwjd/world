using UnityEngine;
using UnityEngine.SceneManagement;

public class ShelterReturn : MonoBehaviour
{
    [Header("설정")]
    [Tooltip("상호작용 키 (기본 E)")]
    public KeyCode interactKey = KeyCode.E;

    private bool _isPlayerNear = false;

    void Update()
    {
        // 플레이어가 근처에 있고 키를 누르면 실행
        if (_isPlayerNear && Input.GetKeyDown(interactKey))
        {
            ReturnToOriginalWorld();
        }
    }

    public void ReturnToOriginalWorld()
    {
        // 1. 저장된 씬 이름 불러오기 (GameState 우선, 없으면 PlayerPrefs)
        string targetScene = GameState.returnSceneName;
        if (string.IsNullOrEmpty(targetScene))
        {
            targetScene = PlayerPrefs.GetString("LastScene", "MapScene"); // 기본값 MapScene
        }

        Debug.Log($"[ShelterReturn] {targetScene} 씬으로 돌아갑니다.");

        // 2. 돌아가는 씬 로드
        // (주의: 씬이 로드된 직후에 플레이어 위치를 옮기는 건 SceneManager의 sceneLoaded 이벤트나
        // 해당 씬의 Start()에서 GameState.lastPosition을 체크해서 처리해야 함.
        // 이미 RealitySystem.cs나 PlayerStats.cs 등에서 그 처리가 되어 있으므로 씬만 넘기면 됨!)
        SceneManager.LoadScene(targetScene);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerNear = true;
            // UI 띄우기 (InteractionTextUI 사용)
            if (InteractionTextUI.Instance != null)
                InteractionTextUI.Instance.Show("E키를 눌러 나가기");
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerNear = false;
            // UI 숨기기
            if (InteractionTextUI.Instance != null)
                InteractionTextUI.Instance.Hide();
        }
    }
}
