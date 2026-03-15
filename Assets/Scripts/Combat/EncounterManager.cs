using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 적과 부딪혔을 때 "핵앤슬래시" vs "턴제 전투" 선택지를 표시합니다.
/// 어느 씬이든 두 선택지 모두 제공합니다.
/// </summary>
public class EncounterManager : MonoBehaviour
{
    public static EncounterManager Instance;

    // ── 전투 씬 전달용 공개 변수 ──
    public static string currentEnemyID;

    [Header("UI 연결")]
    public GameObject encounterPanel;
    public Text       titleText;
    public Button     hackSlashBtn;
    public Button     turnBasedBtn;
    public Text       hackSlashBtnText;
    public Text       turnBasedBtnText;

    // 현재 인카운터 대상
    public  GameObject enemyPrefabToSpawn;   // BattleSystem 에서 읽음
    private GameObject _currentEnemyObject;  // 씬에 존재하는 심볼/오브젝트

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        if (encounterPanel != null) encounterPanel.SetActive(false);
    }

    // ─────────────────────────────────────────────
    //  외부 진입점
    // ─────────────────────────────────────────────

    /// <summary>씬 위에 있는 심볼 오브젝트와 부딪혔을 때 호출.</summary>
    public void StartEncounter(GameObject enemyObject)
    {
        _currentEnemyObject = enemyObject;
        currentEnemyID      = enemyObject.name;
        enemyPrefabToSpawn  = null;

        // EnemySymbol 이 battleEnemyPrefab 을 갖고 있다면 읽어오기
        var symbol = enemyObject.GetComponent<EnemySymbol>();
        if (symbol != null) enemyPrefabToSpawn = symbol.battleEnemyPrefab;

        ShowPanel($"적과 마주쳤다!\n({enemyObject.name})");
    }

    /// <summary>랜덤 인카운터 (프리팹 직접 지정).</summary>
    public void StartRandomEncounter(GameObject prefab, string enemyName)
    {
        _currentEnemyObject = null;
        enemyPrefabToSpawn  = prefab;
        currentEnemyID      = enemyName;
        ShowPanel($"정체불명의 기척이 느껴진다...\n({enemyName})");
    }

    // ─────────────────────────────────────────────
    //  패널 표시
    // ─────────────────────────────────────────────
    void ShowPanel(string title)
    {
        Time.timeScale = 0f;

        if (titleText        != null) titleText.text        = title;
        if (hackSlashBtnText != null) hackSlashBtnText.text = "⚔ 핵앤슬래시 전투";
        if (turnBasedBtnText != null) turnBasedBtnText.text = "🎲 턴제 전투";

        if (hackSlashBtn != null) hackSlashBtn.interactable = true;
        if (turnBasedBtn != null) turnBasedBtn.interactable = true;

        if (encounterPanel != null) encounterPanel.SetActive(true);
    }

    // ─────────────────────────────────────────────
    //  버튼 콜백 (인스펙터에서 연결)
    // ─────────────────────────────────────────────

    /// <summary>핵앤슬래시 선택.</summary>
    public void OnChooseHackSlash()
    {
        ClosePanel();

        // HackSlashCombatManager 에게 전투 시작 위임
        if (HackSlashCombatManager.Instance != null)
        {
            HackSlashCombatManager.Instance.BeginCombat(_currentEnemyObject, enemyPrefabToSpawn);
        }
        else
        {
            Debug.LogWarning("[EncounterManager] HackSlashCombatManager 가 없습니다.");
        }
    }

    /// <summary>턴제 선택 → BattleScene 이동.</summary>
    public void OnChooseTurnBased()
    {
        ClosePanel();
        SavePlayerPosition();
        SceneManager.LoadScene(SceneNames.Battle);
    }

    // ─────────────────────────────────────────────
    //  유틸
    // ─────────────────────────────────────────────
    void ClosePanel()
    {
        Time.timeScale = 1f;
        if (encounterPanel != null) encounterPanel.SetActive(false);
    }

    void SavePlayerPosition()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) return;
        GameState.lastPosition       = p.transform.position;
        GameState.hasPositionSaved   = true;
        GameState.isComingFromBattle = true;
        GameState.returnSceneName    = SceneManager.GetActiveScene().name;
    }
}