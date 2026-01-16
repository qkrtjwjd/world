using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseSystem : MonoBehaviour
{
    [Header("메인 일시정지 패널")]
    public GameObject pauseMenuPanel;

    [Header("서브 메뉴 패널들 (새로 추가!)")]
    public GameObject inventoryPanel; // 아이템창
    public GameObject journalPanel;   // 기록창
    public GameObject savePanel;      // 저장창

    private bool isPaused = false;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // 서브 메뉴가 열려있다면 -> 서브 메뉴만 닫고 메인 메뉴로 돌아옴
            if (inventoryPanel.activeSelf || journalPanel.activeSelf || savePanel.activeSelf)
            {
                OpenMainMenu(); 
            }
            // 그게 아니라면 -> 게임을 멈추거나 재개
            else if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    // --- 기본 기능 ---
    public void ResumeGame()
    {
        CloseAllPanels(); // 모든 창 닫기
        Time.timeScale = 1f;
        isPaused = false;
    }

    public void PauseGame()
    {
        pauseMenuPanel.SetActive(true); // 메인 메뉴 열기
        Time.timeScale = 0f;
        isPaused = true;
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("TitleScene");
    }

    // --- ★ 창 전환 기능 (여기 추가됨) ---

    // 1. 메인 메뉴만 보여주는 함수 (뒤로가기 버튼용)
    public void OpenMainMenu()
    {
        // 서브 창들은 다 끄고
        inventoryPanel.SetActive(false);
        journalPanel.SetActive(false);
        savePanel.SetActive(false);

        // 메인 메뉴만 켜기
        pauseMenuPanel.SetActive(true);
    }

    // 2. 가방 열기
    public void OpenInventory()
    {
        pauseMenuPanel.SetActive(false); // 메인 끄고
        inventoryPanel.SetActive(true);  // 가방 켜기
    }

    // 3. 일지 열기
    public void OpenJournal()
    {
        pauseMenuPanel.SetActive(false);
        journalPanel.SetActive(true);
    }

    // 4. 저장 창 열기
    public void OpenSave()
    {
        pauseMenuPanel.SetActive(false);
        savePanel.SetActive(true);
    }

    // 도우미 함수: 싹 다 끄기
    void CloseAllPanels()
    {
        pauseMenuPanel.SetActive(false);
        if(inventoryPanel != null) inventoryPanel.SetActive(false);
        if(journalPanel != null) journalPanel.SetActive(false);
        if(savePanel != null) savePanel.SetActive(false);
    }
}