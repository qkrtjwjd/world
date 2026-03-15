using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseSystem : MonoBehaviour
{
    [Header("패널 연결")]
    public GameObject pauseMenuPanel;
    public GameObject inventoryPanel;
    public GameObject journalPanel;
    public GameObject savePanel;
    public GameObject loadPanel;

    private bool _isPaused = false;

    // 열려있는 서브 패널이 있는지
    private bool IsSubPanelOpen =>
        IsActive(inventoryPanel) ||
        IsActive(journalPanel)   ||
        IsActive(savePanel)      ||
        IsActive(loadPanel);

    static bool IsActive(GameObject go) => go != null && go.activeSelf;

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape) && !Input.GetKeyDown(KeyCode.Backspace))
            return;

        if (IsSubPanelOpen)
        {
            // 서브 패널이 열려있을 때
            // - 일시정지 상태에서 열었으면 → 메인 메뉴로 뒤로가기
            // - 게임 중에 열었으면 → 완전히 닫기
            if (_isPaused) OpenMainMenu();
            else           ResumeGame();
        }
        else if (_isPaused)
        {
            ResumeGame();
        }
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            PauseGame();
        }
    }

    // ─────────────────────────────────────────────
    //  기본 동작
    // ─────────────────────────────────────────────
    public void ResumeGame()
    {
        CloseAll();
        Time.timeScale = 1f;
        _isPaused      = false;
    }

    public void PauseGame()
    {
        SetOnly(pauseMenuPanel);
        Time.timeScale = 0f;
        _isPaused      = true;
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneNames.Title);
    }

    // ─────────────────────────────────────────────
    //  패널 전환 (인스펙터 버튼 / 코드 양쪽에서 호출 가능)
    // ─────────────────────────────────────────────
    public void OpenMainMenu()   => SetOnly(pauseMenuPanel);
    public void OpenInventory()  => SetOnly(inventoryPanel);
    public void OpenJournal()    => SetOnly(journalPanel);
    public void OpenSave()       => SetOnly(savePanel);
    public void OpenLoad()       => SetOnly(loadPanel);

    // ─────────────────────────────────────────────
    //  헬퍼
    // ─────────────────────────────────────────────

    /// <summary>target 만 켜고 나머지는 모두 끕니다.</summary>
    void SetOnly(GameObject target)
    {
        SetActive(pauseMenuPanel, pauseMenuPanel == target);
        SetActive(inventoryPanel, inventoryPanel == target);
        SetActive(journalPanel,   journalPanel   == target);
        SetActive(savePanel,      savePanel      == target);
        SetActive(loadPanel,      loadPanel      == target);
    }

    void CloseAll()
    {
        SetActive(pauseMenuPanel, false);
        SetActive(inventoryPanel, false);
        SetActive(journalPanel,   false);
        SetActive(savePanel,      false);
        SetActive(loadPanel,      false);
    }

    static void SetActive(GameObject go, bool active)
    {
        if (go != null) go.SetActive(active);
    }
}