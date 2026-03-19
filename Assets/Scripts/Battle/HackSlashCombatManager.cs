using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 핵앤슬래시 전투 세션을 총괄합니다.
/// - 적 스폰 / AI 활성화
/// - 전투 종료 판정 (적 처치 or 플레이어 사망)
/// - 결과 UI 표시 후 쿨타임 세팅
/// </summary>
public class HackSlashCombatManager : MonoBehaviour
{
    public static HackSlashCombatManager Instance { get; private set; }

    // ─────────────────────────────────────────────
    //  인스펙터 연결
    // ─────────────────────────────────────────────
    [Header("전투 결과 UI")]
    [Tooltip("'승리!' / '패배...' 를 띄울 텍스트 (없으면 생략)")]
    public Text resultText;
    [Tooltip("결과 텍스트가 자동으로 사라질 시간(초)")]
    public float resultDisplayTime = 2.5f;

    [Header("플레이어 무적 시간")]
    [Tooltip("전투 시작 직후 플레이어가 피해를 받지 않는 시간(초)")]
    public float startInvincibleTime = 1.0f;

    [Header("모드 전환 설정")]
    [Tooltip("현실 게이지가 이 값 이하로 떨어지면 턴제로 전환 (DarkRealitySystem.maxTime 기준)")]
    public float realityDropThreshold = 15f;

    // ─────────────────────────────────────────────
    //  내부 상태
    // ─────────────────────────────────────────────
    private bool _isCombatActive = false;
    private bool _isModeTransitioning = false;

    private GameObject _activeEnemy;
    private EnemyAI    _activeEnemyAI;     // BeginCombat 시 캐시
    private EnemyHealth _activeEnemyHealth; // BeginCombat 시 캐시
    private GameObject _enemyPrefabRef;

    // 플레이어 컨트롤러 캐시
    private RealityCombatController _combatCtrl;

    // WaitForSeconds 캐시 (GC 절약)
    private WaitForSeconds _waitPoll;
    private WaitForSeconds _waitInvincible;
    private WaitForSeconds _waitMonitorDelay;
    private WaitForSeconds _waitMonitorInterval;
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
        _waitMonitorDelay    = new WaitForSeconds(startInvincibleTime + 0.5f);
        _waitMonitorInterval = new WaitForSeconds(0.3f);
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
        _enemyPrefabRef = enemyPrefab;

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
                ? player.transform.position + new Vector3(2f, 0f, 0f)
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

        // 2. 플레이어 공격 활성화
        if (_combatCtrl != null) _combatCtrl.enabled = true;

        // 3. 무적 시간 + 폴링 루프 시작
        StartCoroutine(CombatLoop());

        // 4. 현실씬에서만 모드 전환 감시 시작
        if (SceneNames.IsRealityScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name))
            StartCoroutine(MonitorModeSwitch());
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
    IEnumerator MonitorModeSwitch()
    {
        yield return _waitMonitorDelay;

        bool daggerWasEquipped = DaggerSystem.IsEquipped;

        while (_isCombatActive && !_isModeTransitioning)
        {
            bool realityDropped = DarkRealitySystem.Instance != null
                && DarkRealitySystem.Instance.CurrentReality <= realityDropThreshold;
            bool daggerPutAway = daggerWasEquipped && !DaggerSystem.IsEquipped;

            if (realityDropped || daggerPutAway)
            {
                _isModeTransitioning = true;
                StartCoroutine(SwitchToTurnBased(realityDropped
                    ? "현실이 흐릿해진다..."
                    : "단검을 집어넣었다..."));
                yield break;
            }

            daggerWasEquipped = DaggerSystem.IsEquipped;
            yield return _waitMonitorInterval;
        }
    }

    IEnumerator SwitchToTurnBased(string message)
    {
        TeardownCombat(registerKill: false, destroyEnemy: false);

        if (_activeEnemyAI != null) _activeEnemyAI.SetChase(false);

        ShowMessage(message);
        yield return _waitSwitchDelay;

        GameState.returnSceneName =
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        if (EncounterManager.Instance != null)
            EncounterManager.Instance.enemyPrefabToSpawn = _enemyPrefabRef;

        _activeEnemy       = null;
        _activeEnemyAI     = null;
        _activeEnemyHealth = null;

        Time.timeScale = 0f;
        if (EncounterManager.Instance != null && EncounterManager.Instance.battleUIPrefab != null)
            UnityEngine.Object.Instantiate(EncounterManager.Instance.battleUIPrefab);
        else
            Debug.LogError("[HackSlashCombatManager] battleUIPrefab이 없습니다. EncounterManager에 연결해주세요.");
    }

    // ─────────────────────────────────────────────
    //  전투 종료
    // ─────────────────────────────────────────────
    void EndCombat(bool playerWon)
    {
        if (playerWon && _activeEnemy != null)
        {
            GameState.RegisterDefeatedEnemy(EncounterManager.currentEnemyID);
            Destroy(_activeEnemy);
        }

        TeardownCombat(registerKill: false, destroyEnemy: false);
        ShowResult(playerWon);
    }

    /// <summary>공통 전투 종료 처리: 플래그 초기화, 컨트롤러·AI 비활성화, 쿨타임 설정.</summary>
    void TeardownCombat(bool registerKill, bool destroyEnemy)
    {
        _isCombatActive = false;

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
    void ShowResult(bool playerWon)
        => ShowMessage(playerWon ? "⚔ 전투 승리!" : "💀 전투 패배...");

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
    }

    // ─────────────────────────────────────────────
    //  외부 알림 (EnemyHealth / EnemyAI 에서 호출)
    // ─────────────────────────────────────────────
    public void NotifyEnemyDead(GameObject enemy)
    {
        if (_isCombatActive && enemy == _activeEnemy)
            _activeEnemy = null; // CombatLoop가 다음 체크에서 EndCombat 호출
    }

    /// <summary>적이 도주에 성공했을 때 EnemyAI 에서 호출됩니다.</summary>
    public void NotifyEnemyFled(GameObject enemy)
    {
        if (!_isCombatActive || enemy != _activeEnemy) return;
        TeardownCombat(registerKill: false, destroyEnemy: false);
        ShowMessage("적이 도망쳤다...");
    }
}
