using UnityEngine;
using UnityEngine.SceneManagement; // 씬 이동하려면 필수!

public class MainMenu : MonoBehaviour
{
    // 게임 시작 버튼을 누르면 실행될 함수
    public void OnClickStart()
    {
        Debug.Log("게임 시작!");
        // ★ 중요: 이동할 첫 번째 씬 이름을 정확히 적어주세요!
        // 집에서 시작할 거면 "HouseScene", 밖에서 시작할 거면 "MapScene"
        SceneManager.LoadScene("Home"); 
    }

    // 종료 버튼을 누르면 실행될 함수
    public void OnClickExit()
    {
        Debug.Log("게임 종료!");
        Application.Quit(); // 게임 꺼버리기 (에디터에선 안 꺼지고 빌드해야 꺼짐)
    }
}