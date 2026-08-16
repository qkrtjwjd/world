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
        SettingsPanelUI.IsOpen   ||
        DialogueLogUI.IsOpen     ||
        JournalUI.IsOpen;

    static bool IsActive(GameObject go) => go != null && go.activeSelf;

    void Update()
    {
        // 키 리바인딩 중(또는 직후 프레임)에는 취소용 ESC·새로 바인딩된 키를 여기서 처리하지 않음
        if (SettingsPanelUI.IsRebinding) return;

        // 솔 거래창이 열려 있으면 취소 키를 거래창이 직접 처리한다 (일시정지 메뉴가 겹쳐 열리지 않게)
        if (SolTradeUI.IsOpen) return;

        // ESC 메뉴(F-8-1)가 열려 있으면 키 입력은 MainMenuUI 가 직접 처리한다
        if (MainMenuUI.IsOpen) return;

        // 리바인딩 가능 키
        KeyCode inventoryKey = SettingsManager.Instance?.keyInventory ?? KeyCode.I;
        KeyCode pauseKey     = SettingsManager.Instance?.keyPause     ?? KeyCode.Escape;

        if (Input.GetKeyDown(inventoryKey))
        {
            if (!YarnDialogue.IsRunning)
                if (!IsActive(pauseMenuPanel) && !DialogueLogUI.IsOpen)
                    ToggleInventory();
            return;
        }

        bool pausePressed = Input.GetKeyDown(pauseKey) || Input.GetKeyDown(KeyCode.Backspace);
        if (!pausePressed) return;

        // 설정 패널이 열려 있으면 먼저 닫기
        if (SettingsPanelUI.IsOpen)
        {
            SettingsPanelUI.Hide();
            return;
        }

        // 대화 로그가 열려 있으면 먼저 닫기 (일시정지 메뉴가 같은 프레임에 열리지 않게)
        if (DialogueLogUI.IsOpen)
        {
            DialogueLogUI.Hide();
            return;
        }

        // 저널(자동 생성 UI)이 열려 있으면 먼저 닫기 — 일시정지 중이었으면 메인 메뉴로 복귀
        if (JournalUI.IsOpen)
        {
            JournalUI.Hide();
            if (_isPaused) OpenMainMenu();
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
        else if (Input.GetKeyDown(pauseKey))
        {
            // F-8-1: ESC 진입점은 MainMenuUI 로 옮겼다.
            // 진입 가드(전투·컷씬·대화·90초 압박)는 MainMenuUI.CanOpen() 이 판정한다.
            // 아래 PauseGame()/OpenMainMenu() 등 기존 API 와 씬 버튼 배선은 그대로 살아 있다.
            MainMenuUI.Show();
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

    public void OpenJournal()
    {
        // 씬에 저널 패널이 배선돼 있으면 그 패널, 없으면 코드 생성 JournalUI 폴백
        if (journalPanel != null) SetOnly(journalPanel);
        else                      { SetOnly(null); JournalUI.Show(); }
    }
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
        JournalUI.Hide();
        SetActive(hpBar,     false);
        SetActive(mentalBar, false);
        ItemDetailUI.Instance?.Hide();
    }

    static void SetActive(GameObject go, bool active)
    {
        if (go != null) go.SetActive(active);
    }
}