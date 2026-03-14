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
    public GameObject loadPanel;      // 로드창 (추가됨)

    private bool isPaused = false;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
        {
            // 1. 서브 메뉴가 열려있는지 확인 (null 체크 안전하게 추가)
            bool isSubPanelOpen = (inventoryPanel != null && inventoryPanel.activeSelf) ||
                                  (journalPanel != null && journalPanel.activeSelf) ||
                                  (savePanel != null && savePanel.activeSelf) ||
                                  (loadPanel != null && loadPanel.activeSelf);

            if (isSubPanelOpen)
            {
                // [수정됨] 만약 일시정지 상태였다면(메뉴에서 들어옴) -> 메인 메뉴로 뒤로가기
                if (isPaused)
                {
                    OpenMainMenu(); 
                }
                // 만약 게임 중이었다면(상호작용으로 열림) -> 그냥 닫기
                else
                {
                    ResumeGame();
                }
            }
            // 2. 이미 일시정지 상태라면 -> 게임 재개 (메뉴 닫기)
            else if (isPaused)
            {
                ResumeGame();
            }
            // 3. 게임 중이라면 -> 일시정지 (메뉴 열기)
            else
            {
                // 백스페이스로는 일시정지 메뉴를 열지 않도록 (원하시면 추가 가능)
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    PauseGame();
                }
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
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true); // 메인 메뉴 열기
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
        // 서브 창들은 다 끄고 (null 체크)
        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        if (journalPanel != null) journalPanel.SetActive(false);
        if (savePanel != null) savePanel.SetActive(false);
        if (loadPanel != null) loadPanel.SetActive(false);

        // 메인 메뉴만 켜기
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
    }

    // 2. 가방 열기
    public void OpenInventory()
    {
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false); // 메인 끄고
        if (inventoryPanel != null) inventoryPanel.SetActive(true);  // 가방 켜기
    }

    // 3. 일지 열기
    public void OpenJournal()
    {
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (journalPanel != null) journalPanel.SetActive(true);
    }

    // 4. 저장 창 열기
    public void OpenSave()
    {
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (savePanel != null) savePanel.SetActive(true);
    }

    // 5. 로드 창 열기 (추가됨)
    public void OpenLoad()
    {
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (loadPanel != null) loadPanel.SetActive(true);
    }

    // 도우미 함수: 싹 다 끄기
    void CloseAllPanels()
    {
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        if (journalPanel != null) journalPanel.SetActive(false);
        if (savePanel != null) savePanel.SetActive(false);
        if (loadPanel != null) loadPanel.SetActive(false);
    }
}