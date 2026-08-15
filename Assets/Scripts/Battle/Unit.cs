using System;
using System.Collections.Generic;
using UnityEngine;

public class Unit : MonoBehaviour
{
    public string unitName;
    public int    unitLevel;
    public int    maxHP;
    public int    currentHP;

    [Header("MP")]
    [Tooltip("최대 MP. 스킬 사용에 소모.")]
    public int maxMP     = 0;
    [Tooltip("현재 MP.")]
    public int currentMP = 0;

    [Header("스킬")]
    [Tooltip("이 유닛이 장착한 스킬 리스트. SkillQuickSlotUI가 이 리스트를 표시합니다.")]
    public List<SkillData> equippedSkills = new List<SkillData>();

    /// <summary>스킬별 남은 쿨다운(턴). 직렬화하지 않음 — 전투 시작 시 깨끗한 상태.</summary>
    [NonSerialized] public Dictionary<SkillData, int> skillCooldowns = new Dictionary<SkillData, int>();
    [NonSerialized] private List<SkillData> _cooldownKeys = new List<SkillData>();

    [Header("전투 스탯 (DamageCalculator용)")]
    [Tooltip("유닛 레벨. 공격자/방어자 차이로 ±5%/Lv 배율 적용. unitLevel(표시용 레거시)과 별개 — DamageCalculator는 이 필드를 사용합니다.")]
    public int   level           = 1;
    [Tooltip("공격력. (atk*2 - def) 의 atk.")]
    public int   attack          = 10;
    [Tooltip("방어력. (atk*2 - def) 의 def.")]
    public int   defense         = 5;
    [Tooltip("명중률 0~100. (acc - eva) 가 명중 확률.")]
    [Range(0, 100)] public int accuracy = 95;
    [Tooltip("회피율 0~100.")]
    [Range(0, 100)] public int evasion  = 5;
    [Tooltip("크리티컬 확률 0~100.")]
    [Range(0, 100)] public int critRate = 5;
    [Tooltip("크리티컬 배율.")]
    public float critMultiplier  = 1.5f;

    [Header("방어")]
    [Tooltip("방어 행동 시 데미지 배율 (0.5 = 50% 감소)")]
    [Range(0f, 1f)]
    public float defendDamageReduction = 0.5f;

    public bool isDefending = false;

    /// <summary>마지막 피격 결과. UI/이펙트가 즉시 조회 가능.</summary>
    public DamageResult LastDamageResult { get; private set; }

    public void ResetState()
    {
        isDefending = false;
    }

    /// <returns>사망 여부</returns>
    public bool TakeDamage(int dmg)
    {
        if (isDefending)
            dmg = Mathf.RoundToInt(dmg * (1f - defendDamageReduction));

        currentHP = Mathf.Max(0, currentHP - dmg);
        LastDamageResult = DamageResult.Hit(dmg);
        BattleEvents.RaiseUnitDamaged(this, LastDamageResult);
        bool died = currentHP <= 0;
        if (died) BattleEvents.RaiseUnitDied(this);
        return died;
    }

    /// <summary>DamageResult 기반 피격. MISS는 데미지 0으로 처리하되 이벤트는 발행.</summary>
    /// <returns>사망 여부</returns>
    public bool TakeDamage(DamageResult result)
    {
        if (result.isMiss)
        {
            LastDamageResult = result;
            BattleEvents.RaiseUnitDamaged(this, result);
            return false;
        }

        int dmg = result.amount;
        if (isDefending)
            dmg = Mathf.RoundToInt(dmg * (1f - defendDamageReduction));

        currentHP = Mathf.Max(0, currentHP - dmg);
        LastDamageResult = DamageResult.Hit(dmg, result.isCrit);
        BattleEvents.RaiseUnitDamaged(this, LastDamageResult);

        bool died = currentHP <= 0;
        if (died) BattleEvents.RaiseUnitDied(this);
        return died;
    }

    public void Heal(int amount)
    {
        int before = currentHP;
        currentHP = Mathf.Min(maxHP, currentHP + amount);
        BattleEvents.RaiseUnitHealed(this, currentHP - before);
    }

    // ─── 스킬 쿨다운 헬퍼 ───────────────────────────────────────────

    /// <summary>해당 스킬의 남은 쿨다운(턴). 0이면 사용 가능.</summary>
    public int GetCooldown(SkillData skill)
    {
        if (skill == null) return 0;
        return skillCooldowns.TryGetValue(skill, out int v) ? v : 0;
    }

    /// <summary>스킬 사용 직후 호출. SkillData.cooldownTurns 만큼 쿨다운 시작.</summary>
    public void StartCooldown(SkillData skill)
    {
        if (skill == null) return;
        skillCooldowns[skill] = skill.cooldownTurns;
    }

    /// <summary>턴 시작 시 호출. 모든 쿨다운을 1씩 감소.</summary>
    public void TickCooldowns()
    {
        if (skillCooldowns.Count == 0) return;
        _cooldownKeys.Clear();
        _cooldownKeys.AddRange(skillCooldowns.Keys);
        foreach (var k in _cooldownKeys)
            if (skillCooldowns[k] > 0) skillCooldowns[k]--;
    }
}