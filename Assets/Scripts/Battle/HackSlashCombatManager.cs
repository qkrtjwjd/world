using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 핵앤슬래시 전투 세션을 총괄합니다.
/// - 적 스폰 / AI 활성화
/// - 전투 종료 판정 (적 처치 or 플레이어 사망)
/// - 결과 UI 표시 후 쿨타임 세팅
/// </summary>
public class HackSlashCombatManager : MonoBehaviour
{
    public static HackSlashCombatManager Instance { get; private set; }

    /// <summary>핵앤슬래시 전투가 진행 중인지 여부. 전투 중 여부 판단에 사용.</summary>
    public static bool IsActive => Instance != null && Instance._isCombatActive;

    // ─────────────────────────────────────────────
    //  인스펙터 연결
    // ─────────────────────────────────────────────
    [Header("전투 결과 UI")]
    [Tooltip("'승리!' / '패배...' 를 띄울 텍스트 (없으면 생략)")]
    public TMP_Text resultText;
    [Tooltip("결과 텍스트가 자동으로 사라질 시간(초)")]
    public float resultDisplayTime = 2.5f;

    [Header("글리치 구간 전환")]
    [Tooltip("핵앤슬래시 중 마시멜로를 섭취하여 턴제로 전환하는 버튼")]
    public Button marshmallowButton;

    [Header("플레이어 무적 시간")]
    [Tooltip("전투 시작 직후 플레이어가 피해를 받지 않는 시간(초)")]
    public float startInvincibleTime = 1.0f;

    [Header("스폰 설정")]
    [Tooltip("기존 적 오브젝트가 없을 때 플레이어 기준 적 스폰 오프셋")]
    [SerializeField] private Vector3 _spawnOffset = new Vector3(2f, 0f, 0f);

    [Header("게이지 복원 설정")]
    [Tooltip("전투 시작 시 게이지를 현실 100%로 고정하고, 이 시간(초) 동안 아무 공격이 없으면 이전 값으로 복원합니다.")]
    public float combatRealityIdleTimeout = 60f;

    // ─────────────────────────────────────────────
    //  내부 상태
    // ─────────────────────────────────────────────
    private bool _isCombatActive = false;
    private bool _isModeTransitioning = false;
    private bool _enemyKilled = false; // NotifyEnemyDead에서 _activeEnemy가 null이 된 뒤에도 처치 사실을 기억

    /// <summary>마지막으로 공격이 발생한 Time.time. GaugeManager의 복원 타이머가 감시합니다.</summary>
    public float LastCombatActivityTime { get; private set; } = -999f;

    /// <summary>EnemyAI / RealityCombatController 에서 공격 발생 시 호출합니다.</summary>
    public void NotifyCombatActivity() => LastCombatActivityTime = Time.time;

    private GameObject _activeEnemy;
    private EnemyAI    _activeEnemyAI;     // BeginCombat 시 캐시
    private EnemyHealth _activeEnemyHealth; // BeginCombat 시 캐시
    private GameObject _enemyPrefabRef;

    // 플레이어 컨트롤러 캐시
    private RealityCombatController _combatCtrl;

    // WaitForSeconds 캐시 (GC 절약)
    private WaitForSeconds _waitPoll;
    private WaitForSeconds _waitInvincible;
    private WaitForSecondsRealtime _waitSwitchDelay;
    private WaitForSeconds _waitResultDisplay;

    // ─────────────────────────────────────────────
    //  Unity
    // ─────────────────────────────────────────────
    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        // WaitForSeconds 캐시
        _waitPoll            = new WaitForSeconds(0.2f);
        _waitInvincible      = new WaitForSeconds(startInvincibleTime);
        _waitSwitchDelay     = new WaitForSecondsRealtime(1.5f);
        _waitResultDisplay   = new WaitForSeconds(resultDisplayTime);

        // 플레이어에 RealityCombatController 가 없으면 자동 추가
        GameObject player = PlayerStats.Instance != null
            ? PlayerStats.Instance.gameObject
            : GameObject.FindGameObjectWithTag("Player");

        if (player != null)
        {
            _combatCtrl = player.GetComponent<RealityCombatController>();
            if (_combatCtrl == null)
                _combatCtrl = player.AddComponent<RealityCombatController>();
            _combatCtrl.enabled = false;
        }

        if (resultText != null) resultText.gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────────
    //  전투 시작
    // ─────────────────────────────────────────────
    public void BeginCombat(GameObject existingEnemy, GameObject enemyPrefab)
    {
        if (_isCombatActive) return;
        _isCombatActive = true;
        _isModeTransitioning = false;
        _enemyKilled = false;
        _enemyPrefabRef = enemyPrefab;
        LastCombatActivityTime = -999f;

        // 1. 적 준비
        if (existingEnemy != null)
        {
            _activeEnemy = existingEnemy;
        }
        else if (enemyPrefab != null)
        {
            GameObject player = PlayerStats.Instance != null
                ? PlayerStats.Instance.gameObject
                : GameObject.FindGameObjectWithTag("Player");
            Vector3 spawnPos = player != null
                ? player.transform.position + _spawnOffset
                : Vector3.zero;
            _activeEnemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            Debug.LogWarning("[HackSlashCombatManager] 적 오브젝트/프리팹이 없습니다.");
        }

        if (_activeEnemy != null)
        {
            ActivateEnemyAI(_activeEnemy);
            // AI·Health 컴포넌트 캐시 (CombatLoop에서 반복 GetComponent 방지)
            _activeEnemyAI     = _activeEnemy.GetComponent<EnemyAI>();
            _activeEnemyHealth = _activeEnemy.GetComponent<EnemyHealth>();
        }

        // 2. 플레이어 입력 잠금 강제 해제 + 공격 컨트롤러 활성화
        PlayerInputLock.Instance?.ForceUnlock();
        if (_combatCtrl != null) _combatCtrl.enabled = true;

        // 3. 핵앤슬래시 전투 중 무공격 대기 시간 후 게이지 이전 값으로 복원
        GaugeManager.Instance?.ForceCombatReality(combatRealityIdleTimeout);

        // 3. 마시멜로 버튼 활성화
        if (marshmallowButton != null)
        {
            marshmallowButton.onClick.RemoveAllListeners();
            marshmallowButton.onClick.AddListener(OnMarshmallowButton);
            marshmallowButton.gameObject.SetActive(true);
        }

        // 4. 무적 시간 + 폴링 루프 시작
        StartCoroutine(CombatLoop());
    }

    // ─────────────────────────────────────────────
    //  전투 루프 (매 0.2 초마다 종료 조건 체크)
    // ─────────────────────────────────────────────
    IEnumerator CombatLoop()
    {
        if (_activeEnemyAI != null) _activeEnemyAI.SetChase(false);
        yield return _waitInvincible;
        if (_activeEnemyAI != null) _activeEnemyAI.SetChase(true);

        while (_isCombatActive)
        {
            yield return _waitPoll;

            if (PlayerStats.Instance != null && PlayerStats.Instance.currentHealth <= 0)
            { EndCombat(false); yield break; }

            if (_activeEnemy == null)
            { EndCombat(true); yield break; }

            if (_activeEnemyHealth != null && _activeEnemyHealth.currentHealth <= 0)
            { EndCombat(true); yield break; }
        }
    }

    // ─────────────────────────────────────────────
    //  모드 전환 (현실 핵앤슬래시 → 턴제)
    // ─────────────────────────────────────────────
    IEnumerator SwitchToTurnBased(string message)
    {
        BattleModeController.Instance?.SetSwitched();
        TeardownCombat(registerKill: false, destroyEnemy: false);

        if (_activeEnemyAI != null) _activeEnemyAI.SetChase(false);

        ShowMessage(message);

        // 시각 연출 (GaugeBoundaryMonitor에서 이미 호출된 경우 isTransitioning 가드로 무시됨)
        BattleTransitionManager.Instance?.TransitionToFantasy();

        yield return _waitSwitchDelay;

        GameState.battleReturn.returnSceneName =
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        if (EncounterManager.Instance != null)
            EncounterManager.Instance.enemyPrefabToSpawn = _enemyPrefabRef;

        _activeEnemy       = null;
        _activeEnemyAI     = null;
        _activeEnemyHealth = null;

        if (EncounterManager.Instance != null && EncounterManager.Instance.battleUIPrefab != null)
        {
            if (!BattleModeController.GetOrCreate().RequestTurnBasedStart(showAppearPanel: false))
                yield break;
            Time.timeScale = 0f;
            UnityEngine.Object.Instantiate(EncounterManager.Instance.battleUIPrefab);
        }
        else
        {
            Debug.LogError("[HackSlashCombatManager] battleUIPrefab이 없습니다. EncounterManager에 연결해주세요.");
        }
    }

    // ─────────────────────────────────────────────
    //  전투 종료
    // ─────────────────────────────────────────────
    void EndCombat(bool playerWon)
    {
        // 사망 통보 시점에 _activeEnemy가 이미 null이 되므로 _enemyKilled 플래그로도 판정
        int gainedXp = 0;
        if (playerWon && (_enemyKilled || _activeEnemy != null))
        {
            GameState.RegisterDefeatedEnemy(EncounterManager.currentEnemyID);
            PlayerStats.Instance?.AddPuppetizationOnKill();

            // 경험치 — 전리품과 동일하게 DB 타입 ID 로 조회
            gainedXp = EncounterManager.Instance?.enemyDatabase
                           ?.GetXpReward(EncounterManager.currentEnemyTypeID) ?? 0;
            int levelUps = PlayerGrowth.AddExp(gainedXp);
            BattleSystem.ApplyLevelUpToPlayerStats(levelUps);

            var loot = EncounterManager.Instance?.enemyDatabase
                           ?.GetLootTable(EncounterManager.currentEnemyTypeID);
            if (loot != null)
            {
                var drops = loot.RollDrops();
                if (drops.Count > 0) InventoryManager.Instance?.AddItems(drops);
            }
        }

        TeardownCombat(registerKill: false, destroyEnemy: playerWon);
        ShowResult(playerWon, gainedXp);
    }

    /// <summary>공통 전투 종료 처리: 플래그 초기화, 컨트롤러·AI 비활성화, 쿨타임 설정.</summary>
    void TeardownCombat(bool registerKill, bool destroyEnemy)
    {
        _isCombatActive = false;
        if (marshmallowButton != null)
            marshmallowButton.gameObject.SetActive(false);

        if (_combatCtrl != null) _combatCtrl.enabled = false;

        if (_activeEnemy != null)
        {
            if (_activeEnemyAI != null) _activeEnemyAI.SetChase(false);
            if (destroyEnemy) Destroy(_activeEnemy);
        }

        _activeEnemy       = null;
        _activeEnemyAI     = null;
        _activeEnemyHealth = null;

        GameState.battleReturn.SetReturning(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, 3f);
    }

    // ─────────────────────────────────────────────
    //  결과 표시
    // ─────────────────────────────────────────────
    void ShowResult(bool playerWon, int gainedXp = 0)
    {
        string msg = playerWon ? "⚔ 전투 승리!" : "💀 전투 패배...";
        if (playerWon && gainedXp > 0) msg += $" 경험치 +{gainedXp}";
        ShowMessage(msg);
        if (!playerWon)
            StartCoroutine(ShowGameOverAfterDelay(resultDisplayTime));
    }

    IEnumerator ShowGameOverAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        GameOverUI.Instance?.Show();
    }

    void ShowMessage(string message)
    {
        if (resultText == null) return;
        resultText.text = message;
        resultText.gameObject.SetActive(true);
        StartCoroutine(HideResultAfter());
    }

    IEnumerator HideResultAfter()
    {
        yield return _waitResultDisplay;
        if (resultText != null) resultText.gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────────
    //  유틸
    // ─────────────────────────────────────────────
    static void ActivateEnemyAI(GameObject enemy)
    {
        EnemyAI ai = enemy.GetComponent<EnemyAI>();
        if (ai == null)
        {
            Debug.LogWarning($"[HackSlashCombatManager] {enemy.name}에 EnemyAI가 없어 자동 추가합니다.");
            ai = enemy.AddComponent<EnemyAI>();
        }
        ai.enabled = true;

        EnemyHealth eh = enemy.GetComponent<EnemyHealth>();
        if (eh == null)
        {
            Debug.LogWarning($"[HackSlashCombatManager] {enemy.name}에 EnemyHealth가 없어 자동 추가합니다.");
            eh = enemy.AddComponent<EnemyHealth>();
            eh.maxHealth = 100f;
        }
        eh.enabled = true;
    }

    // ─────────────────────────────────────────────
    //  외부 알림 (EnemyHealth / EnemyAI 에서 호출)
    // ─────────────────────────────────────────────
    public void NotifyEnemyDead(GameObject enemy)
    {
        if (_isCombatActive && enemy == _activeEnemy)
        {
            _enemyKilled = true;
            _activeEnemy = null; // CombatLoop가 다음 체크에서 EndCombat 호출
        }
    }

    /// <summary>마시멜로 섭취 버튼 클릭 시 호출됩니다.</summary>
    void OnMarshmallowButton()
    {
        if (!_isCombatActive || _isModeTransitioning) return;
        var ctrl = BattleModeController.Instance;
        if (ctrl != null && ctrl.HasSwitchedMode)
        {
            Debug.LogWarning("[HackSlashCombatManager] 이미 모드 전환이 발생했습니다. 마시멜로 전환이 차단됩니다.");
            return;
        }
        _isModeTransitioning = true;
        StartCoroutine(SwitchToTurnBased("마시멜로를 먹었다..."));
    }

    /// <summary>GaugeBoundaryMonitor가 Glitch→Fantasy 전환 시 호출합니다. 마시멜로 외 전환 차단.</summary>
    public void ForceSwitchToTurnBased()
    {
        // 핵앤슬래시 중에는 마시멜로를 통해서만 턴제로 전환 가능
        Debug.LogWarning("[HackSlashCombatManager] 핵앤슬래시 중 강제 턴제 전환이 차단됩니다. 마시멜로를 사용하세요.");
    }

    /// <summary>선택 UI 대기 중 적 AI를 활성/비활성화합니다.</summary>
    public void SetEnemyActive(bool active)
    {
        if (_activeEnemyAI != null) _activeEnemyAI.SetChase(active);
        if (_combatCtrl    != null) _combatCtrl.enabled = active;
    }

    /// <summary>DarkRealityController 게이지 소진 시 호출됩니다.</summary>
    public void ForceEndCombatByGauge()
    {
        if (!_isCombatActive) return;
        if (_activeEnemy != null) Destroy(_activeEnemy);
        TeardownCombat(registerKill: false, destroyEnemy: false);
        ShowMessage("현실이 사라진다...");
    }

    /// <summary>적이 도주에 성공했을 때 EnemyAI 에서 호출됩니다.</summary>
    public void NotifyEnemyFled(GameObject enemy)
    {
        if (!_isCombatActive || enemy != _activeEnemy) return;
        TeardownCombat(registerKill: false, destroyEnemy: false);
        ShowMessage("적이 도망쳤다...");
    }
}
