using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerStats : MonoBehaviour
{
    public static PlayerStats Instance;

    [Header("기본 스탯")]
    public float maxHealth        = 100f;
    public float currentHealth;
    public float allyMaxHP        = 100f;
    public float allyCurrentHP    = 100f;
    public float maxMental        = 100f;
    public float currentMental;
    public float maxPuppetization = 100f;
    public float currentPuppetization;

    // 이전 값 캐시 (UI 갱신 최소화)
    private float _lastHealth = -1f;
    private float _lastMental = -1f;

    private bool _gameStateDirty = false;
    private PlayerStatusUI _statusUI;

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        // GameState 에 저장된 값이 있으면 불러오기, 없으면 최대치로 초기화
        if (GameState.player.IsInitialized)
        {
            currentHealth        = GameState.player.health;
            currentMental        = GameState.player.mental;
            currentPuppetization = GameState.player.puppetization;
        }
        else
        {
            currentHealth        = maxHealth;
            currentMental        = maxMental;
            currentPuppetization = 20f;
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
                GlitchManager.Instance?.SetGlitchLoop(false);
                GlitchManager.Instance?.PlayGlitch(0.3f, 0.3f);
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
        GlitchManager.Instance?.PlayGlitch(1.0f, 0.8f);
        GlitchManager.Instance?.SetGlitchLoop(true, 0.2f);

        string current = SceneManager.GetActiveScene().name;
        if (SceneNames.IsRealityScene(current))
        {
            GameState.lastPosition     = transform.position;
            GameState.hasPositionSaved = true;
            GameState.isComingFromBattle = false;
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
        currentHealth = Mathf.Max(0f, currentHealth - amount);
        _gameStateDirty = true;

        // HP 30% 이하 구간이면 추가 트라우마
        if (currentHealth / maxHealth <= 0.3f)
            AddTrauma(5f);

        _statusUI?.UpdateHP(currentHealth, maxHealth);
        StaticUIManager.Instance?.UpdateHealthBars();
    }

    public void RecoverHealth(float amount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        _gameStateDirty = true;
        _statusUI?.UpdateHP(currentHealth, maxHealth);
    }

    public void AddTrauma(float amount)
    {
        currentMental = Mathf.Max(0f, currentMental - amount);
        _gameStateDirty = true;
        ReducePuppetization(amount * 0.5f);
        _statusUI?.UpdateMental(currentMental, maxMental);
    }

    public void RecoverMental(float amount)
    {
        currentMental = Mathf.Min(maxMental, currentMental + amount);
        _gameStateDirty = true;
        _statusUI?.UpdateMental(currentMental, maxMental);
    }

    public void AddPuppetization(float amount)
    {
        currentPuppetization = Mathf.Min(maxPuppetization, currentPuppetization + amount);
        _gameStateDirty = true;
    }

    public void ReducePuppetization(float amount)
    {
        currentPuppetization = Mathf.Max(0f, currentPuppetization - amount);
        _gameStateDirty = true;
    }

    /// <summary>
    /// 적 처치 시 현재 인형화 구간에 따라 인형화를 증가시킵니다.
    /// </summary>
    public void AddPuppetizationOnKill()
    {
        float pct = maxPuppetization > 0f ? currentPuppetization / maxPuppetization * 100f : 0f;

        float baseAmount;
        float multiplier;

        if (pct <= 30f)
        {
            baseAmount = Random.Range(1f, 2f);
            multiplier = 1.0f;
        }
        else if (pct <= 60f)
        {
            baseAmount = Random.Range(1.5f, 2.5f);
            multiplier = 1.2f;
        }
        else if (pct <= 80f)
        {
            baseAmount = Random.Range(2f, 3f);
            multiplier = 1.5f;
        }
        else
        {
            baseAmount = Random.Range(2.5f, 4f);
            multiplier = 2.0f;
        }

        AddPuppetization(baseAmount * multiplier);
    }
}