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

    [Header("스탯 바 (일시정지·인벤토리에서만 표시)")]
    public GameObject hpBar;
    public GameObject mentalBar;

    private bool _isPaused = false;

    void Start()
    {
        CloseAll();
    }

    // 열려있는 서브 패널이 있는지
    private bool IsSubPanelOpen =>
        IsActive(inventoryPanel) ||
        IsActive(journalPanel)   ||
        IsActive(savePanel)      ||
        IsActive(loadPanel)      ||
        SettingsPanelUI.IsOpen;

    static bool IsActive(GameObject go) => go != null && go.activeSelf;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
        {
            if (!YarnDialogue.IsRunning)
                if (!IsActive(pauseMenuPanel))
                    ToggleInventory();
            return;
        }

        if (!Input.GetKeyDown(KeyCode.Escape) && !Input.GetKeyDown(KeyCode.Backspace))
            return;

        // 설정 패널이 열려 있으면 먼저 닫기
        if (SettingsPanelUI.IsOpen)
        {
            SettingsPanelUI.Hide();
            return;
        }

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

        // 대화 중이 아닐 때 플레이어 이동 잠금 해제 (도메인 리로드 대비)
        if (!YarnDialogue.IsRunning)
        {
            var ctrl = Object.FindAnyObjectByType<ClearSky.SimplePlayerController>();
            ctrl?.Unlock();
        }
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

    public void OpenInventory()
    {
        // 일시정지 메뉴를 거치지 않고 직접 열 때 timeScale 정상화
        if (!_isPaused) Time.timeScale = 1f;
        SetOnly(inventoryPanel);
    }

    /// <summary>가방 버튼용: 일시정지 없이 인벤토리를 토글합니다.</summary>
    public void ToggleInventory()
    {
        if (IsActive(inventoryPanel))
            ResumeGame();
        else
            OpenInventory();
    }

    public void OpenJournal()    => SetOnly(journalPanel);
    public void OpenSave()       => SetOnly(savePanel);
    public void OpenLoad()       => SetOnly(loadPanel);
    public void OpenSettings()   { SetOnly(null); SettingsPanelUI.Show(); }

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

        bool showStats = target == pauseMenuPanel || target == inventoryPanel;
        SetActive(hpBar,     showStats);
        SetActive(mentalBar, showStats);
    }

    void CloseAll()
    {
        SetActive(pauseMenuPanel, false);
        SetActive(inventoryPanel, false);
        SetActive(journalPanel,   false);
        SetActive(savePanel,      false);
        SetActive(loadPanel,      false);
        SettingsPanelUI.Hide();
        SetActive(hpBar,     false);
        SetActive(mentalBar, false);
        ItemDetailUI.Instance?.Hide();
    }

    static void SetActive(GameObject go, bool active)
    {
        if (go != null) go.SetActive(active);
    }
}