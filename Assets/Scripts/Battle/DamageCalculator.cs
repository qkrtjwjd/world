using UnityEngine;

/// <summary>
/// 배틀 데미지 계산을 담당하는 정적 유틸.
/// 명중률, 레벨 보정, 방어, 스킬/약점 배수, 크리티컬, 변동성을 일괄 처리합니다.
/// 턴제(BattleSystem)와 액션(RealityCombatController) 양쪽에서 동일하게 사용됩니다.
/// </summary>
public static class DamageCalculator
{
    /// <summary>명중률 하한 (회피 100% 방지). 0~100 범위.</summary>
    public const int MinHitChance = 10;

    /// <summary>명중률 상한.</summary>
    public const int MaxHitChance = 100;

    /// <summary>레벨 차이 1당 데미지 배율 (양수면 공격자가 높을수록 유리).</summary>
    public const float LevelFactorPerLevel = 0.05f;

    /// <summary>최종 데미지 변동폭 (1.0 ± Variance).</summary>
    public const float Variance = 0.1f;

    /// <summary>방어 시 데미지 감산 비율 (Unit.defendDamageReduction과 별개로 적용 안 함 — Unit.TakeDamage에서 처리).</summary>

    /// <summary>
    /// 데미지 계산. 회피 시 <see cref="DamageResult.Miss"/> 반환.
    /// </summary>
    /// <param name="attacker">공격 유닛.</param>
    /// <param name="defender">방어 유닛.</param>
    /// <param name="skillMultiplier">스킬 데미지 배수 (기본 1.0).</param>
    /// <param name="weakPointMultiplier">약점/비약점 배수 (기본 1.0).</param>
    /// <param name="attackMultiplier">공격력 배율 — 버프(AttackUp/Down) 반영용 (기본 1.0).</param>
    /// <param name="critBonus">크리티컬 확률 가산치(%) — 버프(CritChanceUp/Down) 반영용 (기본 0).</param>
    /// <param name="defenseMultiplier">방어력 배율 — 버프(DefenseUp/Down) 반영용 (기본 1.0).</param>
    public static DamageResult Calculate(
        Unit  attacker,
        Unit  defender,
        float skillMultiplier      = 1f,
        float weakPointMultiplier  = 1f,
        float attackMultiplier     = 1f,
        int   critBonus            = 0,
        float defenseMultiplier    = 1f)
    {
        if (attacker == null || defender == null)
            return DamageResult.Miss;

        return CalculateRaw(
            attacker.level, attacker.attack, attacker.accuracy, attacker.critRate, attacker.critMultiplier,
            defender.level, defender.defense, defender.evasion,
            skillMultiplier, weakPointMultiplier,
            attackMultiplier, critBonus, defenseMultiplier);
    }

    /// <summary>
    /// Unit이 없는 컨텍스트(액션 모드 플레이어, EnemyHealth 등)용 raw-stats 오버로드.
    /// </summary>
    public static DamageResult CalculateRaw(
        int   attackerLevel,
        int   attackerAttack,
        int   attackerAccuracy,
        int   attackerCritRate,
        float attackerCritMultiplier,
        int   defenderLevel,
        int   defenderDefense,
        int   defenderEvasion,
        float skillMultiplier     = 1f,
        float weakPointMultiplier = 1f,
        float attackMultiplier    = 1f,
        int   critBonus           = 0,
        float defenseMultiplier   = 1f)
    {
        // 1) 명중 판정
        int hitChance = Mathf.Clamp(attackerAccuracy - defenderEvasion, MinHitChance, MaxHitChance);
        if (Random.Range(0, 100) >= hitChance)
            return DamageResult.Miss;

        // 2) 기본 데미지 (감산 + 레벨 보정 + 버프 배율)
        float effAttack   = attackerAttack * Mathf.Max(0f, attackMultiplier);
        float effDefense  = defenderDefense * Mathf.Max(0f, defenseMultiplier);
        float levelFactor = 1f + (attackerLevel - defenderLevel) * LevelFactorPerLevel;
        float baseDmg     = (effAttack * 2f - effDefense) * levelFactor;
        baseDmg           = Mathf.Max(1f, baseDmg);

        // 3) 스킬 / 약점 배수
        baseDmg *= Mathf.Max(0f, skillMultiplier);
        baseDmg *= Mathf.Max(0f, weakPointMultiplier);

        // 4) 크리티컬 (critBonus: 버프 가산치)
        bool isCrit = false;
        int  effCritRate = Mathf.Clamp(attackerCritRate + critBonus, 0, 100);
        if (effCritRate > 0 && Random.Range(0, 100) < effCritRate)
        {
            baseDmg *= attackerCritMultiplier;
            isCrit   = true;
        }

        // 5) 변동성 ±10%
        baseDmg *= Random.Range(1f - Variance, 1f + Variance);

        int finalAmount = Mathf.Max(1, Mathf.RoundToInt(baseDmg));
        return DamageResult.Hit(finalAmount, isCrit);
    }
}
