using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BattleEvents 구독 → BattleCommentaryUI 호출 브리지.
/// 전투 씬 오브젝트에 붙이고, ui / data 슬롯을 인스펙터에서 연결하세요.
///
/// 외부에서 직접 호출하려면 정적 이벤트를 사용하세요:
///   BattleCommentaryTrigger.OnCommentaryNeeded?.Invoke("쿠루", "skill_use");
///
/// 지원 태그: turn_start / skill_use / low_hp / ally_down / victory / boss_appear
/// </summary>
public class BattleCommentaryTrigger : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  정적 API — 외부 시스템에서 직접 호출
    // ─────────────────────────────────────────────

    /// <summary>
    /// (characterName, tag) 전달 시 등록된 트리거가 대사를 표시합니다.
    /// 직접 구독이 필요한 경우 += 로 핸들러를 등록하세요.
    /// </summary>
    public static event System.Action<string, string> OnCommentaryNeeded;

    /// <summary>
    /// 외부에서 대사를 요청할 때 사용합니다.
    /// 예: BattleCommentaryTrigger.RaiseCommentaryNeeded("쿠루", "skill_use");
    /// </summary>
    public static void RaiseCommentaryNeeded(string characterName, string tag)
        => OnCommentaryNeeded?.Invoke(characterName, tag);

    // ─────────────────────────────────────────────
    //  인스펙터 연결
    // ─────────────────────────────────────────────
    [Header("연결")]
    [Tooltip("BattleCommentaryUI 컴포넌트가 붙은 오브젝트.")]
    [SerializeField] private BattleCommentaryUI ui;
    [Tooltip("캐릭터 대사 데이터 ScriptableObject.")]
    [SerializeField] private BattleCommentaryData data;

    [Header("자동 발화 캐릭터")]
    [Tooltip("BattleEvents에서 자동으로 대사를 발화할 기본 캐릭터 이름 (data에 등록된 이름과 일치해야 합니다).")]
    public string defaultCharacterName = "쿠루";

    [Header("HP 임계값")]
    [Tooltip("플레이어 HP가 이 비율(0~1) 이하일 때 low_hp 태그 대사를 발화합니다.")]
    [Range(0f, 1f)]
    public float lowHpThreshold = 0.3f;

    private bool _lowHpTriggeredThisBattle;
    private readonly Dictionary<Unit, bool> _isEnemyCache = new Dictionary<Unit, bool>();

    // ─────────────────────────────────────────────
    //  라이프사이클
    // ─────────────────────────────────────────────
    void OnEnable()
    {
        _isEnemyCache.Clear();
        BattleEvents.OnTurnStarted  += HandleTurnStarted;
        BattleEvents.OnSkillUsed    += HandleSkillUsed;
        BattleEvents.OnUnitDamaged  += HandleUnitDamaged;
        BattleEvents.OnUnitDied     += HandleUnitDied;
        BattleEvents.OnBattleEnded  += HandleBattleEnded;
        OnCommentaryNeeded          += HandleExternalRequest;

        _lowHpTriggeredThisBattle = false;
    }

    void OnDisable()
    {
        BattleEvents.OnTurnStarted  -= HandleTurnStarted;
        BattleEvents.OnSkillUsed    -= HandleSkillUsed;
        BattleEvents.OnUnitDamaged  -= HandleUnitDamaged;
        BattleEvents.OnUnitDied     -= HandleUnitDied;
        BattleEvents.OnBattleEnded  -= HandleBattleEnded;
        OnCommentaryNeeded          -= HandleExternalRequest;
    }

    bool IsEnemyUnit(Unit unit)
    {
        if (_isEnemyCache.TryGetValue(unit, out bool cached)) return cached;
        bool result = unit.GetComponent<EnemyAI>() != null
                   || unit.GetComponent<EnemyHealth>() != null;
        _isEnemyCache[unit] = result;
        return result;
    }

    // ─────────────────────────────────────────────
    //  공개 API
    // ─────────────────────────────────────────────

    /// <summary>characterName 캐릭터의 tag 대사를 즉시 표시합니다.</summary>
    public void TriggerComment(string characterName, string tag)
    {
        if (ui == null || data == null) return;

        var character = data.GetCharacter(characterName);
        if (character == null) return;

        string dialogue = character.GetRandomComment(tag);
        if (string.IsNullOrEmpty(dialogue)) return;

        ui.ShowComment(character.faceSprite, character.characterName, dialogue);
    }

    // ─────────────────────────────────────────────
    //  이벤트 핸들러
    // ─────────────────────────────────────────────

    void HandleTurnStarted(Unit unit)
    {
        if (unit == null) return;
        if (!IsEnemyUnit(unit))
            TriggerComment(defaultCharacterName, "turn_start");
    }

    void HandleSkillUsed(Unit caster, SkillData skill, DamageResult result)
    {
        TriggerComment(defaultCharacterName, "skill_use");
    }

    void HandleUnitDamaged(Unit unit, DamageResult result)
    {
        if (_lowHpTriggeredThisBattle) return;
        if (unit == null) return;
        if (IsEnemyUnit(unit)) return;

        float maxHP = unit.maxHP > 0 ? unit.maxHP : 1;
        if ((float)unit.currentHP / maxHP <= lowHpThreshold)
        {
            _lowHpTriggeredThisBattle = true;
            TriggerComment(defaultCharacterName, "low_hp");
        }
    }

    void HandleUnitDied(Unit unit)
    {
        if (unit == null) return;
        TriggerComment(defaultCharacterName, IsEnemyUnit(unit) ? "victory" : "ally_down");
    }

    void HandleBattleEnded()
    {
        _lowHpTriggeredThisBattle = false;
    }

    void HandleExternalRequest(string characterName, string tag)
    {
        TriggerComment(characterName, tag);
    }
}
