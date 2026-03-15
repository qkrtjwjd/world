using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public void OnClickStart()
    {
        SceneManager.LoadScene(SceneNames.Home);
    }

    public void OnClickLoad()
    {
        if (SaveManager.instance != null)
            SaveManager.instance.LoadGame(0);
        else
            Debug.LogWarning("SaveManager 인스턴스를 찾을 수 없습니다.");
    }

    public void OnClickExit()
    {
        Application.Quit();
    }
}