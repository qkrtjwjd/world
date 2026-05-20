using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 적과 부딪혔을 때 GaugeManager 게이지 구간에 따라 전투 모드를 자동 결정합니다.
/// - 0~69 (환상·글리치 구간) → 턴제 자동 시작
/// - 70~100 (현실 구간)      → 액션 모드 자동 시작
/// 경계값은 GaugeBoundaryMonitor 인스턴스가 있으면 그 값을, 없으면 기본값(70)을 사용합니다.
/// </summary>
public class EncounterManager : MonoBehaviour
{
    public static EncounterManager Instance { get; private set; }

    // ── 전투 씬 전달용 공개 변수 ──
    public static string currentEnemyID;

    [Header("배틀 UI 프리팹")]
    [Tooltip("Canvas + BattleSystem 이 포함된 프리팹. 턴제 전투 시 인스턴스화됩니다.")]
    public GameObject battleUIPrefab;

    [Tooltip("적 ID → 전투 프리팹 매핑 테이블. Project 창에서 생성: Create → Battle → Enemy Database")]
    public EnemyDatabase enemyDatabase;

    // 현재 인카운터 대상
    public  GameObject enemyPrefabToSpawn;
    private GameObject _currentEnemyObject;
    public  GameObject CurrentEnemyObject => _currentEnemyObject;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(this);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (GameState.pendingSwitchToHackSlash)
        {
            GameState.pendingSwitchToHackSlash = false;
            enemyPrefabToSpawn = GameState.pendingEnemyPrefab;
            GameState.pendingEnemyPrefab = null;
            StartCoroutine(DelayedHackSlashStart());
        }
    }

    /// <summary>배틀 UI 종료 시 EnemyEncounterTrigger 리셋. BattleSystem에서 호출.</summary>
    public void OnBattleEnded()
    {
        if (_currentEnemyObject == null) return;
        var trigger = _currentEnemyObject.GetComponent<EnemyEncounterTrigger>();
        if (trigger != null) trigger.ResetEncounter();
    }

    IEnumerator DelayedHackSlashStart()
    {
        yield return null;
        StartHackSlash();
    }

    // ─────────────────────────────────────────────
    //  외부 진입점
    // ─────────────────────────────────────────────

    /// <summary>씬 위에 있는 심볼 오브젝트와 부딪혔을 때 호출.</summary>
    public void StartEncounter(GameObject enemyObject)
    {
        if (BattleSystem.IsActive || HackSlashCombatManager.IsActive) return;
        _currentEnemyObject = enemyObject;
        currentEnemyID      = enemyObject.name;

        var symbol = enemyObject.GetComponent<EnemySymbol>();
        enemyPrefabToSpawn = (symbol != null && enemyDatabase != null)
            ? enemyDatabase.GetPrefab(symbol.enemyID)
            : null;

        if (enemyPrefabToSpawn == null && symbol != null && !string.IsNullOrEmpty(symbol.enemyID))
            Debug.LogWarning($"[EncounterManager] EnemyDatabase에 '{symbol.enemyID}' ID가 없습니다. enemyDatabase 연결 또는 ID를 확인하세요.");

        AutoStartBattle();
    }

    /// <summary>랜덤 인카운터 (프리팹 직접 지정).</summary>
    public void StartRandomEncounter(GameObject prefab, string enemyName)
    {
        if (BattleSystem.IsActive || HackSlashCombatManager.IsActive) return;
        _currentEnemyObject = null;
        enemyPrefabToSpawn  = prefab;
        currentEnemyID      = enemyName;
        AutoStartBattle();
    }

    // ─────────────────────────────────────────────
    //  자동 모드 선택
    // ─────────────────────────────────────────────

    void AutoStartBattle()
    {
        SaveManager.Instance?.SavePreBattle();
        BattleModeController.GetOrCreate().ResetBattleSession();
        float gauge = GaugeManager.Instance != null ? GaugeManager.Instance.fantasyRealityGauge : 0f;

        // 글리치 구간(fantasyBoundary < gauge < realityBoundary)은 턴제로 처리
        if (gauge >= GaugeBoundaryMonitor.RealityBoundary) StartHackSlash();
        else                                               StartTurnBased();
    }

    void StartHackSlash()
    {
        if (HackSlashCombatManager.Instance != null)
            HackSlashCombatManager.Instance.BeginCombat(_currentEnemyObject, enemyPrefabToSpawn);
        else
            Debug.LogWarning("[EncounterManager] HackSlashCombatManager 가 없습니다.");
    }

    void StartTurnBased()
    {
        if (battleUIPrefab == null)
        {
            Debug.LogError("[EncounterManager] battleUIPrefab이 연결되지 않았습니다. Inspector에서 연결해주세요.");
            return;
        }

        GameState.battleReturn.returnSceneName         = SceneManager.GetActiveScene().name;
        if (!BattleModeController.GetOrCreate().RequestTurnBasedStart(showAppearPanel: true))
            return;

        PlayerInputLock.Instance?.Lock();

        foreach (var rb in FindObjectsByType<Rigidbody2D>(FindObjectsInactive.Exclude))
            rb.linearVelocity = Vector2.zero;

        Time.timeScale = 0f;
        Instantiate(battleUIPrefab);
    }

    // 하위 호환
    public void OnChooseHackSlash() => StartHackSlash();
    public void OnChooseTurnBased() => StartTurnBased();
    public void OnPendingModeSelected(BattleMode mode) { } // 미사용 — 하위 호환 유지

    // ─────────────────────────────────────────────
    //  튜토리얼 전용 — 게이지 무관 강제 시작
    // ─────────────────────────────────────────────

    /// <summary>게이지 값과 무관하게 턴제 전투를 강제 시작합니다. 튜토리얼 전용.</summary>
    public void ForceStartTurnBased(GameObject enemyPrefab, string enemyId = "tutorial_battle_1")
    {
        BattleModeController.GetOrCreate().ResetBattleSession();
        _currentEnemyObject = null;
        enemyPrefabToSpawn  = enemyPrefab;
        currentEnemyID      = enemyId;
        StartTurnBased();
    }

    /// <summary>게이지 값과 무관하게 핵앤슬래시 전투를 강제 시작합니다. 튜토리얼 전용.</summary>
    public void ForceStartHackSlash(GameObject existingEnemy, GameObject enemyPrefab, string enemyId = "tutorial_battle_2")
    {
        BattleModeController.GetOrCreate().ResetBattleSession();
        _currentEnemyObject = existingEnemy;
        enemyPrefabToSpawn  = enemyPrefab;
        currentEnemyID      = enemyId;
        StartHackSlash();
    }
}
