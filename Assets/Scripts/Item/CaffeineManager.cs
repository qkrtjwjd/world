using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 카페인 스택을 관리합니다.
///
/// 아이템의 ItemEffect.specialEffectCode = CaffeineStack 이면
/// 사용할 때마다 스택이 1 쌓이고, 3잔(EFFECT_THRESHOLD) 달성 시
/// 카페인 중독 효과(addictionEffect)를 발동합니다.
/// 각성 효과(SpeedUp 등)는 아이템 에셋의 realityEffect에 설정해 매 사용 시 적용됩니다.
///
/// [초기화 시점]
///   - 마지막 사용 후 10분(STACK_RESET_TIME) 경과 시 자동 초기화
///   - 쉼터(Shelter) 씬 또는 타이틀(TitleScene) 진입 시 즉시 초기화
/// </summary>
public class CaffeineManager : MonoBehaviour
{
    public static CaffeineManager Instance { get; private set; }

    private const int   EFFECT_THRESHOLD = 3;
    private const float STACK_RESET_TIME = 600f; // 10분

    [Header("3잔 달성 — 카페인 중독")]
    [Tooltip("3잔 달성 시 발동할 중독 페널티.\n예) healthChange -5\n디버프: DamageOverTime 2/120s, Confusion 1/30s\n\n각성 효과(SpeedUp 등)는 아이템 에셋의 realityEffect에 설정하면 매번 적용됩니다.")]
    public ItemEffect addictionEffect;

    [Header("알림 메시지")]
    [Tooltip("3잔 달성 시 화면에 표시할 메시지")]
    public string thresholdMessage = "카페인 각성 상태에 돌입했다!";

    /// <summary>현재 카페인 스택 수.</summary>
    public int Stack { get; private set; }

    /// <summary>스택 자동 초기화까지 남은 시간(초).</summary>
    public float Timer { get; private set; }

    // ─────────────────────────────────────────────
    //  Unity 생명주기
    // ─────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { SingletonGuard.DestroyDuplicate(this); return; }
        Instance = this;
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Update()
    {
        if (Timer <= 0f) return;
        Timer -= Time.deltaTime;
        if (Timer <= 0f)
        {
            Timer = 0f;
            ResetStack();
        }
    }

    // ─────────────────────────────────────────────
    //  씬 전환 감지
    // ─────────────────────────────────────────────

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == SceneNames.Shelter || scene.name == SceneNames.Title)
            ResetStack();
    }

    // ─────────────────────────────────────────────
    //  공개 메서드
    // ─────────────────────────────────────────────

    /// <summary>카페인 스택을 1 추가합니다. 임계값(3) 도달 시 각성 + 중독 효과를 동시 발동합니다.</summary>
    public void AddStack()
    {
        Stack++;
        Timer = STACK_RESET_TIME;

        if (Stack >= EFFECT_THRESHOLD)
            ApplyThresholdEffects();
    }

    /// <summary>카페인 스택과 타이머를 초기화합니다.</summary>
    public void ResetStack()
    {
        Stack = 0;
        Timer = 0f;
    }

    // ─────────────────────────────────────────────
    //  내부
    // ─────────────────────────────────────────────

    private void ApplyThresholdEffects()
    {
        ApplyEffect(addictionEffect);

        if (!string.IsNullOrEmpty(thresholdMessage))
            ItemNotificationUI.Instance?.Show(thresholdMessage);
    }

    private void ApplyEffect(ItemEffect effect)
    {
        // 수치 효과 → PlayerStats
        if (PlayerStats.Instance != null)
        {
            if (effect.healthChange > 0)       PlayerStats.Instance.RecoverHealth(effect.healthChange);
            else if (effect.healthChange < 0)  PlayerStats.Instance.TakeDamage(-effect.healthChange);

            if (effect.mentalChange > 0)       PlayerStats.Instance.RecoverMental(effect.mentalChange);
            else if (effect.mentalChange < 0)  PlayerStats.Instance.AddTrauma(-effect.mentalChange);

            if (effect.puppetizationChange > 0)      PlayerStats.Instance.AddPuppetization(effect.puppetizationChange);
            else if (effect.puppetizationChange < 0) PlayerStats.Instance.ReducePuppetization(-effect.puppetizationChange);
        }

        // 버프/디버프 → BuffManager
        BuffManager.Instance?.AddBuffs(effect.buffs);
    }
}
