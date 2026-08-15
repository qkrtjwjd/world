using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerStats : MonoBehaviour, IPlayerStatsService
{
    public static PlayerStats Instance;

    // ── IPlayerStatsService 구현 (명시적 — 기존 public 필드와 명명 충돌 회피) ──
    float IPlayerStatsService.MaxHealth     { get => maxHealth;     set => maxHealth     = value; }
    float IPlayerStatsService.CurrentHealth { get => currentHealth; set => currentHealth = value; }

    [Header("기본 스탯")]
    public float maxHealth        = 100f;
    public float currentHealth;
    public float allyMaxHP        = 100f;
    public float allyCurrentHP    = 100f;
    public float maxMental        = 100f;
    public float currentMental;

    // 인형화 수치는 CorruptionManager 가 단일 소스 — 여기서는 읽기 전용 위임
    public float maxPuppetization =>
        CorruptionManager.Instance != null ? CorruptionManager.Instance.maxCorruption : 100f;
    public float currentPuppetization =>
        CorruptionManager.Instance != null ? CorruptionManager.Instance.currentCorruption : 20f;

    // 이전 값 캐시 (UI 갱신 최소화)
    private float _lastHealth = -1f;
    private float _lastMental = -1f;

    private bool _gameStateDirty = false;
    private PlayerStatusUI _statusUI;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BattleServices.Register((IPlayerStatsService)this);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        // 성장 시스템 최대 HP 적용 (인스펙터 값은 레벨 1 기준 폴백)
        if (PlayerGrowth.Level > 1)
            maxHealth = PlayerGrowth.CurrentMaxHP;

        // GameState 에 저장된 값이 있으면 불러오기, 없으면 최대치로 초기화
        if (GameState.player.IsInitialized)
        {
            currentHealth = GameState.player.health;
            currentMental = GameState.player.mental;
        }
        else
        {
            currentHealth = maxHealth;
            currentMental = maxMental;
        }

        _statusUI = PlayerStatusUI.Instance;
        UpdateUI(true);
    }

    void Update()
    {
        // 멘탈 붕괴 체크
        if (currentMental <= 0 && GameState.mentalBreakdownTimer <= 0)
            TriggerMentalBreakdown();

        // 멘탈 붕괴 타이머 감소
        if (GameState.mentalBreakdownTimer > 0)
        {
            GameState.mentalBreakdownTimer -= Time.deltaTime;
            _gameStateDirty = true;
            if (GameState.mentalBreakdownTimer <= 0)
            {
                GameState.mentalBreakdownTimer = 0;
            }
        }

        // GameState 에 변경된 경우에만 동기화
        if (_gameStateDirty)
        {
            GameState.player = new GameState.PlayerState
            {
                health        = currentHealth,
                mental        = currentMental,
                puppetization = currentPuppetization,
            };
            _gameStateDirty = false;
        }

        UpdateUI(false);
    }

    void TriggerMentalBreakdown()
    {
        GameState.mentalBreakdownTimer = 60f;

        string current = SceneManager.GetActiveScene().name;
        if (SceneNames.IsRealityScene(current))
        {
            GameState.lastPosition                     = transform.position;
            GameState.hasPositionSaved                 = true;
            GameState.battleReturn.isComingFromBattle  = false;
            SceneManager.LoadScene(SceneNames.GetFantasyScene(current));
        }
    }

    // ── UI 갱신 ──
    public void UpdateUI(bool force = false)
    {
        if (_statusUI == null) return;

        if (force || Mathf.Abs(currentHealth - _lastHealth) > 0.01f)
        {
            _statusUI.UpdateHP(currentHealth, maxHealth);
            _lastHealth = currentHealth;
        }
        if (force || Mathf.Abs(currentMental - _lastMental) > 0.01f)
        {
            _statusUI.UpdateMental(currentMental, maxMental);
            _lastMental = currentMental;
        }
    }

    // ── 스탯 변경 메서드 ──
    public void TakeDamage(float amount)
    {
        // 면역 / 취약 / 보호막 버프 반영 (턴제는 BattleSystem.EnemyTurn 에서 처리 — 이 경로 미경유)
        if (BuffManager.Instance != null)
        {
            amount = BuffManager.Instance.ModifyIncomingDamage(amount);
            if (amount <= 0f) return;
        }

        float prevRatio = currentHealth / maxHealth;
        currentHealth = Mathf.Max(0f, currentHealth - amount);
        _gameStateDirty = true;

        // HP가 30% 임계값을 처음 넘어갈 때만 1회 트라우마 적용
        if (prevRatio > 0.3f && currentHealth / maxHealth <= 0.3f)
            AddTrauma(5f);

        StaticUIManager.Instance?.UpdateHealthBars();
    }

    public void RecoverHealth(float amount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        _gameStateDirty = true;
    }

    public void AddTrauma(float amount)
    {
        currentMental = Mathf.Max(0f, currentMental - amount);
        _gameStateDirty = true;
        ReducePuppetization(amount * 0.5f);
    }

    public void RecoverMental(float amount)
    {
        currentMental = Mathf.Min(maxMental, currentMental + amount);
        _gameStateDirty = true;
    }

    public void AddPuppetization(float amount)
    {
        CorruptionManager.Instance?.AddCorruption(amount);
        _gameStateDirty = true;
    }

    public void ReducePuppetization(float amount)
    {
        CorruptionManager.Instance?.AddCorruption(-amount);
        _gameStateDirty = true;
    }

    /// <summary>
    /// 적 처치 시 현재 인형화 구간에 따라 인형화를 증가시킵니다.
    /// </summary>
    /// <param name="multiplier">최종 배율 (공감 승리 등 감면 시 1 미만 전달)</param>
    public void AddPuppetizationOnKill(float multiplier = 1f)
    {
        float pct = maxPuppetization > 0f ? currentPuppetization / maxPuppetization * 100f : 0f;

        float baseAmount;
        float stageMultiplier;

        switch (CorruptionManager.GetStage(pct))
        {
            case CorruptionStage.Autonomy:
                baseAmount = Random.Range(1f, 2f);     stageMultiplier = 1.0f; break;
            case CorruptionStage.Crack:
                baseAmount = Random.Range(1.5f, 2.5f); stageMultiplier = 1.2f; break;
            case CorruptionStage.Backfire:
                baseAmount = Random.Range(2f, 3f);     stageMultiplier = 1.5f; break;
            default: // Loss, Doll
                baseAmount = Random.Range(2.5f, 4f);   stageMultiplier = 2.0f; break;
        }

        AddPuppetization(baseAmount * stageMultiplier * multiplier);
    }
}
