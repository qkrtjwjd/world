using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 아이템 사용 횟수를 추적합니다.
///
/// [비전투 상황]
///   - 같은 아이템은 1회만 사용 가능합니다.
///   - 이미 사용한 아이템을 다시 사용하려 하면 차단 메시지를 표시합니다.
///
/// [전투 중 (BattleSystem)]
///   - 횟수 제한 없이 사용 가능합니다.
///   - 같은 아이템을 repeatThreshold 이상 사용하면 아이템 데이터의
///     repeatItemEffect(수치)와 repeatUseEffect(시각 효과)가 발동됩니다.
///
/// [초기화 시점]
///   - 전투 종료: BattleSystem.EndBattleCoroutine()에서 ResetAll() 호출
///   - 쉼터 또는 타이틀 씬 진입: OnSceneLoaded에서 자동 초기화
/// </summary>
public class ItemUseTracker : MonoBehaviour
{
    public static ItemUseTracker Instance { get; private set; }

    [Header("전투 중 반복 사용 효과")]
    [Tooltip("전투 중 같은 아이템을 이 횟수 이상 사용하면 repeatItemEffect와 repeatUseEffect가 발동됩니다.")]
    [SerializeField] private int repeatThreshold = 3;

    // 아이템 이름 → 사용 횟수
    private readonly Dictionary<string, int> _useCounts = new Dictionary<string, int>();

    // ─────────────────────────────────────────────
    //  Unity 생명주기
    // ─────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
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

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 쉼터 복귀 또는 타이틀(로그아웃) 시 사용 횟수 전체 초기화
        if (scene.name == SceneNames.Shelter || scene.name == SceneNames.Title)
            ResetAll();
    }

    // ─────────────────────────────────────────────
    //  비전투 전용
    // ─────────────────────────────────────────────

    /// <summary>
    /// 비전투 상황에서 아이템 사용을 시도합니다.
    /// 이미 사용한 아이템이면 차단 메시지를 표시하고 false를 반환합니다.
    /// 처음 사용하는 아이템이면 횟수를 기록하고 true를 반환합니다.
    /// </summary>
    public bool TryUseOutsideBattle(ItemData item)
    {
        if (item == null) return true;

        string key = item.itemName;
        int count  = _useCounts.ContainsKey(key) ? _useCounts[key] : 0;

        if (count >= 1)
        {
            ItemNotificationUI.Instance?.Show($"'{item.DisplayName}'은(는) 이미 사용했습니다.");
            return false;
        }

        // 첫 사용 → 기록
        _useCounts[key] = 1;
        return true;
    }

    // ─────────────────────────────────────────────
    //  전투 중 전용
    // ─────────────────────────────────────────────

    /// <summary>
    /// 전투 중 아이템 사용을 기록합니다.
    /// repeatThreshold 이상 사용되면 TriggerRepeatEffect()를 호출합니다.
    /// </summary>
    public void RecordBattleUse(ItemData item)
    {
        if (item == null) return;

        string key = item.itemName;
        if (!_useCounts.ContainsKey(key)) _useCounts[key] = 0;
        _useCounts[key]++;

        if (_useCounts[key] >= repeatThreshold)
            TriggerRepeatEffect(item);
    }

    // ─────────────────────────────────────────────
    //  공통
    // ─────────────────────────────────────────────

    /// <summary>모든 아이템 사용 횟수를 초기화합니다.</summary>
    public void ResetAll()
    {
        _useCounts.Clear();
    }

    /// <summary>특정 아이템의 현재 사용 횟수를 반환합니다.</summary>
    public int GetUseCount(ItemData item)
    {
        if (item == null) return 0;
        return _useCounts.TryGetValue(item.itemName, out int c) ? c : 0;
    }

    // ─────────────────────────────────────────────
    //  반복 사용 효과
    // ─────────────────────────────────────────────

    private void TriggerRepeatEffect(ItemData item)
    {
        // 1. 반복 사용 대사 표시
        string dialogue = GetLocalizedDialogue(
            item.repeatUseDialogue_ko,
            item.repeatUseDialogue_en,
            item.repeatUseDialogue_jp);
        if (!string.IsNullOrEmpty(dialogue))
            ItemNotificationUI.Instance?.ShowDialogue(dialogue);

        // 2. repeatItemEffect 수치 및 버프 적용
        ApplyItemEffect(item.repeatItemEffect);

        // 3. repeatUseEffect 시각 효과 발동
        ItemEffectHandler.Instance?.HandleEffect(item.repeatUseEffect);

        Debug.Log($"[ItemUseTracker] '{item.DisplayName}' 반복 사용 효과 발동! ({_useCounts[item.itemName]}회)");
    }

    private void ApplyItemEffect(ItemEffect effect)
    {
        if (PlayerStats.Instance != null)
        {
            if (effect.healthChange > 0)       PlayerStats.Instance.RecoverHealth(effect.healthChange);
            else if (effect.healthChange < 0)  PlayerStats.Instance.TakeDamage(-effect.healthChange);

            if (effect.mentalChange > 0)       PlayerStats.Instance.RecoverMental(effect.mentalChange);
            else if (effect.mentalChange < 0)  PlayerStats.Instance.AddTrauma(-effect.mentalChange);

            if (effect.puppetizationChange > 0)       PlayerStats.Instance.AddPuppetization(effect.puppetizationChange);
            else if (effect.puppetizationChange < 0)  PlayerStats.Instance.ReducePuppetization(-effect.puppetizationChange);
        }

        BuffManager.Instance?.AddBuffs(effect.buffs);
    }

    private string GetLocalizedDialogue(string ko, string en, string jp)
    {
        if (LocalizationManager.Instance == null) return ko;
        switch (LocalizationManager.Instance.currentLanguage)
        {
            case LocalizationManager.Language.EN: return string.IsNullOrEmpty(en) ? ko : en;
            case LocalizationManager.Language.JP: return string.IsNullOrEmpty(jp) ? ko : jp;
            default: return ko;
        }
    }
}
