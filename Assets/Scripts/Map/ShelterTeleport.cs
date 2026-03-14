using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq; // 배열 검색용 기능

public class ShelterTeleport : MonoBehaviour
{
    [Header("설정")]
    public KeyCode teleportKey = KeyCode.H; // 이동할 키 (H키)
    public string shelterSceneName = "Shelter"; // 쉼터 씬 이름 (정확히!)

    [Header("이동 가능한 맵 이름들")]
    // 여기에 쉼터로 갈 수 있게 허락할 씬 이름들을 적으세요
    public string[] allowedScenes; 

    void Update()
    {
        // 1. 키를 눌렀는지 확인
        if (Input.GetKeyDown(teleportKey))
        {
            TryTeleportToShelter();
        }
    }

    void TryTeleportToShelter()
    {
        // 현재 내가 있는 씬 이름 알아내기
        string currentScene = SceneManager.GetActiveScene().name;

        // 2. 지금 있는 곳이 허락된 맵인지 확인
        // (allowedScenes 목록에 currentScene이 포함되어 있는지 검사)
        if (allowedScenes.Contains(currentScene))
        {
            Debug.Log("쉼터로 이동합니다!");
            
            // ★ 중요: 쉼터로 가기 전에 현재 위치를 저장해두면 좋겠죠?
            // (나중에 '돌아가기' 기능을 만들 거라면 여기에 위치 저장 코드 추가 필요)

            // ★ [추가됨] 출발하기 전에 'LastScene'이라는 이름으로 현재 맵 이름을 저장해둠!
            PlayerPrefs.SetString("LastScene", currentScene);

            // ★ [중요] 정확한 위치 좌표도 저장해야 돌아올 때 그 자리에 설 수 있음
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                GameState.lastPosition = player.transform.position;
                GameState.hasPositionSaved = true;
                GameState.returnSceneName = currentScene; // GameState에도 저장 (더 안전함)
            }
            
            SceneManager.LoadScene(shelterSceneName);
        }
        else
        {
            Debug.Log("여기서는 쉼터로 갈 수 없습니다.");
        }
    }
}