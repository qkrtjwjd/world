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
    public float maxHunger        = 100f;
    public float currentHunger;
    public float maxPuppetization = 100f;
    public float currentPuppetization;

    [Header("자연 감소")]
    public float hungerDecreaseRate = 1.0f;

    // 이전 값 캐시 (UI 갱신 최소화)
    private float _lastHealth = -1f;
    private float _lastMental = -1f;
    private float _lastHunger = -1f;

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
            currentHunger        = GameState.player.hunger;
            currentPuppetization = GameState.player.puppetization;
        }
        else
        {
            currentHealth        = maxHealth;
            currentMental        = maxMental;
            currentHunger        = maxHunger;
            currentPuppetization = 0f;
        }

        UpdateUI(true);
    }

    void Update()
    {
        // 배고픔 자연 감소
        currentHunger = Mathf.Max(0f, currentHunger - hungerDecreaseRate * Time.deltaTime);

        // 멘탈 붕괴 체크
        if (currentMental <= 0 && GameState.mentalBreakdownTimer <= 0)
            TriggerMentalBreakdown();

        // 멘탈 붕괴 타이머 감소
        if (GameState.mentalBreakdownTimer > 0)
        {
            GameState.mentalBreakdownTimer -= Time.deltaTime;
            if (GameState.mentalBreakdownTimer <= 0)
            {
                GameState.mentalBreakdownTimer = 0;
                GlitchManager.Instance?.SetGlitchLoop(false);
                GlitchManager.Instance?.PlayGlitch(0.3f, 0.3f);
            }
        }

        // GameState 에 실시간 동기화 (struct 이므로 한번에 할당)
        GameState.player = new GameState.PlayerState
        {
            health        = currentHealth,
            mental        = currentMental,
            hunger        = currentHunger,
            puppetization = currentPuppetization,
        };

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
        if (PlayerStatusUI.Instance == null) return;

        if (force || Mathf.Abs(currentHealth - _lastHealth) > 0.01f)
        {
            PlayerStatusUI.Instance.UpdateHP(currentHealth, maxHealth);
            _lastHealth = currentHealth;
        }
        if (force || Mathf.Abs(currentMental - _lastMental) > 0.01f)
        {
            PlayerStatusUI.Instance.UpdateMental(currentMental, maxMental);
            _lastMental = currentMental;
        }
        if (force || Mathf.Abs(currentHunger - _lastHunger) > 0.01f)
        {
            PlayerStatusUI.Instance.UpdateHunger(currentHunger, maxHunger);
            _lastHunger = currentHunger;
        }
    }

    // ── 스탯 변경 메서드 ──
    public void TakeDamage(float amount)
    {
        currentHealth = Mathf.Max(0f, currentHealth - amount);
        PlayerStatusUI.Instance?.UpdateHP(currentHealth, maxHealth);
    }

    public void EatFood(float amount)
    {
        currentHunger = Mathf.Min(maxHunger, currentHunger + amount);
        AddPuppetization(5f);
        RecoverMental(5f);
        PlayerStatusUI.Instance?.UpdateHunger(currentHunger, maxHunger);
    }

    public void AddTrauma(float amount)
    {
        currentMental = Mathf.Max(0f, currentMental - amount);
        ReducePuppetization(amount * 0.5f);
        PlayerStatusUI.Instance?.UpdateMental(currentMental, maxMental);
    }

    public void RecoverMental(float amount)
    {
        currentMental = Mathf.Min(maxMental, currentMental + amount);
        PlayerStatusUI.Instance?.UpdateMental(currentMental, maxMental);
    }

    public void AddPuppetization(float amount)
    {
        currentPuppetization = Mathf.Min(maxPuppetization, currentPuppetization + amount);
    }

    public void ReducePuppetization(float amount)
    {
        currentPuppetization = Mathf.Max(0f, currentPuppetization - amount);
    }
}