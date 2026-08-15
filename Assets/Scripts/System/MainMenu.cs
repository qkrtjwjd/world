using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Header("불러오기 슬롯 패널 (Inspector 연결)")]
    [Tooltip("SaveSlotUI 컴포넌트가 붙은 슬롯들이 들어 있는 패널. 연결하면 불러오기 클릭 시 이 패널을 표시합니다.")]
    public GameObject loadPanel;

    public void OnClickStart()
    {
        // 이름을 정한 뒤에 인트로로 넘어간다. 확정 콜백에서 기존 전환 로직을 그대로 탄다.
        NameEntryUI.Show(StartIntro);
    }

    static void StartIntro()
    {
        if (TransitionManager.Instance != null)
            TransitionManager.Instance.DoSceneTransition(SceneNames.Intro);
        else
            SceneManager.LoadScene(SceneNames.Intro);
    }

    public void OnClickLoad()
    {
        if (loadPanel != null)
        {
            loadPanel.SetActive(true);
        }
        else
        {
            // loadPanel 미연결 시 가장 최근 슬롯 직접 로드
            int slot = FindMostRecentSlot();
            if (SaveManager.Instance != null)
                SaveManager.Instance.LoadGame(slot);
            else
                Debug.LogWarning("[MainMenu] SaveManager 인스턴스를 찾을 수 없습니다.");
        }
    }

    public void OnClickCloseLoadPanel()
    {
        if (loadPanel != null) loadPanel.SetActive(false);
    }

    public void OnClickExit()
    {
        Application.Quit();
    }

    static int FindMostRecentSlot()
    {
        int bestSlot = 0;
        System.DateTime bestDate = System.DateTime.MinValue;
        for (int i = 0; i < 3; i++)
        {
            SaveData data = SaveManager.Instance?.LoadSaveData(i);
            if (data == null) continue;
            if (System.DateTime.TryParse(data.saveDate, out System.DateTime date) && date > bestDate)
            {
                bestDate = date;
                bestSlot = i;
            }
        }
        return bestSlot;
    }
}