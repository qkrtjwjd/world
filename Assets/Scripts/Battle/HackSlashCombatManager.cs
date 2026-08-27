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

    // ── 숲 2차 전투 마무리 구간 (F-2-6 · 정본 S#19C) ─────────────────────
    [Header("마무리 구간 (F-2-6)")]
    [Tooltip("켜면 적 HP 가 임계 이하로 떨어졌을 때 마무리 구간에 들어간다. 숲 전투 전용")]
    public bool  useFinisherWindow = false;
    [Tooltip("마무리 구간 진입 HP 비율. 정본 기준 5%")]
    [Range(0.01f, 0.5f)] public float finisherHealthRatio = 0.05f;
    [Tooltip("이탈 시간(초). 이 시간 동안 입력이 없으면 적이 도주하고 불살이 성립한다")]
    public float finisherEscapeSeconds = 2f;
    [Tooltip("약점 표시가 뜨는 누적 피해 비율. 데미지 배율은 두지 않는다")]
    [Range(0.05f, 0.95f)] public float weakpointDamageRatio = 0.4f;
    [Tooltip("마무리 일격 키")]
    public KeyCode finisherKey = KeyCode.E;
    [Tooltip("E키 프롬프트 텍스트. 없으면 resultText 를 쓴다")]
    public TMP_Text finisherPromptText;

    // ─────────────────────────────────────────────
    //  내부 상태
    // ─────────────────────────────────────────────
    private bool _isCombatActive = false;
    private bool _isModeTransitioning = false;
    private bool _enemyKilled = false; // NotifyEnemyDead에서 _activeEnemy가 null이 된 뒤에도 처치 사실을 기억

    // 마무리 구간 상태 — 전투 단위
    private bool  _weakpointShown;
    private bool  _finisherOpen;
    private float _finisherOpenedAt;
    private bool  _enemySpared;      // 이탈 시간 경과로 적이 도주 → 불살 성립

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
        else { SingletonGuard.DestroyDuplicate(this); return; }
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

        // 4. 마무리 구간 상태 초기화 — 전투 단위다
        _weakpointShown = false;
        _finisherOpen   = false;
        _enemySpared    = false;
        ShowFinisherPrompt("");

        // EnemyHealth 는 기본적으로 HP 10% 미만에서 스스로 도주한다.
        // 마무리 구간은 5% 에서 열리므로 그대로 두면 창이 열리기 전에 적이 달아난다.
        // 숲 전투에서는 자동 도주를 끄고 이탈 시간(2초)이 도주를 정하게 한다(F-2-6).
        if (useFinisherWindow && _activeEnemyHealth != null)
            _activeEnemyHealth.fleeHealthRatio = 0f;

        // 5. 무적 시간 + 폴링 루프 시작
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

            if (useFinisherWindow) TickFinisherWindow();
        }
    }

    // ─────────────────────────────────────────────
    //  마무리 구간 (F-2-6 · 정본 S#19C)
    //
    //  판정은 셋이다.
    //    E키 입력      → 몰살 (전용 연출)
    //    일반 공격 적중 → 몰살 (연출 없이 사망) — 폴링이 HP 0 을 잡아 EndCombat 으로 간다
    //    이탈 시간 경과 → 적 도주, 불살 성립
    //
    //  ⚠ 이 구간에서 적을 무적으로 만들지 않는다. 무적으로 두면 손을 놓고 있는 것이
    //     곧 불살이 되어 액션 쪽 불살이 가장 쉬운 행동이 된다(F-2-6 ※ · C-6-3).
    //  ⚠ 의도하지 않은 몰살이 발생할 수 있다. 막지 않는다. 확인 창을 넣지 않는다.
    // ─────────────────────────────────────────────
    void TickFinisherWindow()
    {
        if (_activeEnemyHealth == null || _activeEnemyHealth.maxHealth <= 0f) return;

        float ratio = _activeEnemyHealth.currentHealth / _activeEnemyHealth.maxHealth;

        // 약점 표시 — 누적 피해가 임계를 넘으면 1회. 데미지 배율은 두지 않는다.
        if (!_weakpointShown && ratio <= 1f - weakpointDamageRatio)
        {
            _weakpointShown = true;
            BattleTutorialDirector.Instance?.OnWeakpointRevealed();
        }

        if (!_finisherOpen)
        {
            if (ratio > finisherHealthRatio) return;

            _finisherOpen     = true;
            _finisherOpenedAt = Time.time;
            if (_activeEnemyAI != null) _activeEnemyAI.SetChase(false);  // 물러나는 동작만 한다
            ShowFinisherPrompt($"[{finisherKey}] 숨통을 끊는다");
            BattleTutorialDirector.Instance?.OnFinisherWindowOpened();
            return;
        }

        if (Input.GetKeyDown(finisherKey))
        {
            ShowFinisherPrompt("");
            _enemyKilled = true;
            _activeEnemyHealth.TakeRealityDamage(_activeEnemyHealth.currentHealth);
            return;
        }

        // 이탈 시간 경과 → 적이 도주한다. 불살 성립.
        if (Time.time - _finisherOpenedAt >= finisherEscapeSeconds)
        {
            ShowFinisherPrompt("");
            _enemySpared = true;
            EndCombat(true);
        }
    }

    void ShowFinisherPrompt(string message)
    {
        var target = finisherPromptText != null ? finisherPromptText : resultText;
        if (target == null) return;
        target.text = message;
        target.gameObject.SetActive(!string.IsNullOrEmpty(message));
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
        // 숲 전투는 보상이 정본 고정값이라 인형화 굴림과 전리품 테이블을 쓰지 않는다 (F-2-6).
        // 몰살/불살 판정과 지급은 BattleTutorialDirector 가 맡는다.
        if (useFinisherWindow)
        {
            BattleTutorialDirector.Instance?.HandleOutcome(
                !playerWon      ? BattleOutcome.Lost
                : _enemySpared  ? BattleOutcome.Spared
                                : BattleOutcome.Killed);
        }

        // 사망 통보 시점에 _activeEnemy가 이미 null이 되므로 _enemyKilled 플래그로도 판정
        // 불살(적 도주)은 처치가 아니므로 처치 등록·경험치·전리품을 지급하지 않는다.
        int gainedXp = 0;
        if (playerWon && !_enemySpared && (_enemyKilled || _activeEnemy != null))
        {
            GameState.RegisterDefeatedEnemy(EncounterManager.currentEnemyID);

            // 숲 전투는 인형화가 데모 고정값(+2)이라 여기서 굴리지 않는다 (F-2-6 ※).
            if (!useFinisherWindow)
                PlayerStats.Instance?.AddPuppetizationOnKill();

            // 경험치 — 전리품과 동일하게 DB 타입 ID 로 조회
            gainedXp = EncounterManager.Instance?.enemyDatabase
                           ?.GetXpReward(EncounterManager.currentEnemyTypeID) ?? 0;
            int levelUps = PlayerGrowth.AddExp(gainedXp);
            BattleSystem.ApplyLevelUpToPlayerStats(levelUps);

            // 숲 전투는 몰살/불살에 따라 떨어지는 것이 정본에 못박혀 있어 테이블을 굴리지 않는다.
            var loot = useFinisherWindow
                       ? null
                       : EncounterManager.Instance?.enemyDatabase
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

    /// <summary>게이지 소진 시 호출됩니다. (호출처였던 DarkRealityController 는 2026-08-27 폐기)</summary>
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
