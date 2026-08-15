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
    /// <summary>씬 오브젝트 이름. 처치 기록(EnemySymbol 리스폰 차단)용 — 개체 단위 식별.</summary>
    public static string currentEnemyID;
    /// <summary>EnemyDatabase 의 적 타입 ID. 전리품/경험치 조회용 — 종류 단위 식별.</summary>
    public static string currentEnemyTypeID;

    [Header("배틀 UI 프리팹")]
    [Tooltip("Canvas + BattleSystem 이 포함된 프리팹. 턴제 전투 시 인스턴스화됩니다.")]
    public GameObject battleUIPrefab;

    [Tooltip("글리치 구간(31~69) 전투 진입 시 표시할 모드 선택 UI 프리팹")]
    public GameObject pendingModeUIPrefab;

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

    void Update()
    {
        GameState.battleReturn.Tick(Time.deltaTime);
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
        // 전리품/경험치는 씬 오브젝트 이름이 아니라 DB 타입 ID 로 조회해야 함
        currentEnemyTypeID = (symbol != null && !string.IsNullOrEmpty(symbol.enemyID))
            ? symbol.enemyID
            : enemyObject.name;
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
        currentEnemyTypeID  = enemyName;
        AutoStartBattle();
    }

    // ─────────────────────────────────────────────
    //  자동 모드 선택
    // ─────────────────────────────────────────────

    void AutoStartBattle()
    {
        // 설정: 전투 전 자동 저장 (기본값 ON)
        if (SettingsManager.Instance?.autoSaveEnabled ?? true)
            SaveManager.Instance?.SavePreBattle();

        BattleModeController.GetOrCreate().ResetBattleSession();
        float gauge = GaugeManager.Instance != null ? GaugeManager.Instance.fantasyRealityGauge : 0f;

        if (gauge >= GaugeBoundaryMonitor.RealityBoundary)
        {
            StartHackSlash();
        }
        else if (gauge > GaugeBoundaryMonitor.FantasyBoundary)
        {
            // 글리치 구간 — 설정에 따라 자동 결정 또는 PendingModeUI 표시
            bool combatAuto = SettingsManager.Instance?.combatModeAuto ?? false;
            if (combatAuto)
            {
                // 게이지 50 기준 자동 결정
                if (gauge >= 50f)
                {
                    GaugeManager.Instance?.ForceTempReality();
                    StartHackSlash();
                }
                else
                {
                    GaugeManager.Instance?.ForceTempFantasy();
                    StartTurnBased();
                }
            }
            else if (pendingModeUIPrefab != null)
            {
                // 기존: PendingModeUI로 모드 선택
                GameState.pendingModeSelection = true;
                Instantiate(pendingModeUIPrefab);
                StartTurnBased();
            }
            else
            {
                StartTurnBased();
            }
        }
        else
        {
            StartTurnBased();
        }
    }

    void StartHackSlash()
    {
        BattleTransitionManager.Instance?.SyncMode(BattleMode.Reality);
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

        BattleTransitionManager.Instance?.SyncMode(BattleMode.Fantasy);

        PlayerInputLock.Instance?.Lock();

        foreach (var rb in FindObjectsByType<Rigidbody2D>(FindObjectsInactive.Exclude))
            rb.linearVelocity = Vector2.zero;

        Time.timeScale = 0f;
        Instantiate(battleUIPrefab);
    }

    public void OnPendingModeSelected(BattleMode mode)
    {
        GameState.pendingModeSelection = false;

        if (mode == BattleMode.Reality)
        {
            // 대기 중이던 BattleSystem을 파괴하고 액션 전투 시작
            if (BattleSystem.Instance != null)
            {
                Time.timeScale = 1f;
                PlayerInputLock.Instance?.Unlock();
                Destroy(BattleSystem.Instance.gameObject);
            }
            StartHackSlash();
        }
        else
        {
            // 대기 중인 BattleSystem 시작 (없으면 신규 생성)
            if (BattleSystem.Instance != null)
                BattleSystem.Instance.StartBattleAfterModeSelection();
            else
                StartTurnBased();
        }
    }

    // ─────────────────────────────────────────────
    //  튜토리얼 전용 — 게이지 무관 강제 시작
    // ─────────────────────────────────────────────

    /// <summary>게이지 값과 무관하게 턴제 전투를 강제 시작합니다. 튜토리얼 전용.</summary>
    public void ForceStartTurnBased(GameObject enemyPrefab, string enemyId = "tutorial_battle_1")
    {
        if (SettingsManager.Instance?.autoSaveEnabled ?? true)
            SaveManager.Instance?.SavePreBattle();

        BattleModeController.GetOrCreate().ResetBattleSession();
        _currentEnemyObject = null;
        enemyPrefabToSpawn  = enemyPrefab;
        currentEnemyID      = enemyId;
        StartTurnBased();
    }

    /// <summary>게이지 값과 무관하게 핵앤슬래시 전투를 강제 시작합니다. 튜토리얼 전용.</summary>
    public void ForceStartHackSlash(GameObject existingEnemy, GameObject enemyPrefab, string enemyId = "tutorial_battle_2")
    {
        if (SettingsManager.Instance?.autoSaveEnabled ?? true)
            SaveManager.Instance?.SavePreBattle();

        BattleModeController.GetOrCreate().ResetBattleSession();
        _currentEnemyObject = existingEnemy;
        enemyPrefabToSpawn  = enemyPrefab;
        currentEnemyID      = enemyId;
        StartHackSlash();
    }
}
