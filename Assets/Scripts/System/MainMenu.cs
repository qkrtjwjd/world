using UnityEngine;
using UnityEngine.SceneManagement; // 씬 이동하려면 필수!

public class MainMenu : MonoBehaviour
{
    // 게임 시작 버튼을 누르면 실행될 함수
    public void OnClickStart()
    {
        Debug.Log("게임 시작!");
        SceneManager.LoadScene("Home"); 
    }

    // 로드 버튼을 누르면 실행될 함수
    public void OnClickLoad()
    {
        Debug.Log("게임 로드!");
        // SaveManager의 싱글톤 변수 이름이 소문자 instance임
        if (SaveManager.instance != null)
        {
            // 기본적으로 0번 슬롯(가장 최근/첫 번째)을 로드하도록 설정
            SaveManager.instance.LoadGame(0);
        }
        else
        {
            Debug.LogWarning("SaveManager 인스턴스를 찾을 수 없습니다.");
        }
    }

    // 종료 버튼을 누르면 실행될 함수
    public void OnClickExit()
    {
        Debug.Log("게임 종료!");
        Application.Quit();
    }
}