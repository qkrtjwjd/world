using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 적과 부딪혔을 때 씬 상태에 따라 전투 모드를 자동 결정합니다.
/// - 판타지(MapScene) → 턴제 자동 시작
/// - 현실(DarkReality) → 핵앤슬래시 자동 시작
/// - GameState.pendingSwitchToHackSlash 플래그가 있으면 씬 진입 시 핵앤슬래시 자동 시작
/// </summary>
public class EncounterManager : MonoBehaviour
{
    public static EncounterManager Instance { get; private set; }

    // ── 전투 씬 전달용 공개 변수 ──
    public static string currentEnemyID;

    [Header("배틀 UI 프리팹")]
    [Tooltip("Canvas + BattleSystem 이 포함된 프리팹. 턴제 전투 시 인스턴스화됩니다.")]
    public GameObject battleUIPrefab;

    // 현재 인카운터 대상
    public  GameObject enemyPrefabToSpawn;
    private GameObject _currentEnemyObject;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
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
        _currentEnemyObject = enemyObject;
        currentEnemyID      = enemyObject.name;

        var symbol = enemyObject.GetComponent<EnemySymbol>();
        enemyPrefabToSpawn = symbol != null ? symbol.battleEnemyPrefab : null;

        AutoStartBattle();
    }

    /// <summary>랜덤 인카운터 (프리팹 직접 지정).</summary>
    public void StartRandomEncounter(GameObject prefab, string enemyName)
    {
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
        if (SceneNames.IsRealityScene(SceneManager.GetActiveScene().name))
            StartHackSlash();
        else
            StartTurnBased();
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

        GameState.returnSceneName = SceneManager.GetActiveScene().name;

        // 잔류 속도 제거 후 게임 정지
        foreach (var rb in FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None))
            rb.linearVelocity = Vector2.zero;

        Time.timeScale = 0f;
        Instantiate(battleUIPrefab);
    }

    // 하위 호환 버튼 콜백
    public void OnChooseHackSlash() => StartHackSlash();
    public void OnChooseTurnBased() => StartTurnBased();
}
