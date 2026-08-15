using UnityEngine;

/// <summary>
/// 스킬 실행 로직. MP 소모, 데미지/회복 적용, 이벤트 발행을 담당.
/// 호출자: 턴제 BattleSystem 또는 액션 모드의 스킬 컨트롤러.
/// </summary>
public static class SkillExecutor
{
    /// <summary>스킬 사용 가능 여부 (MP 충분, 쿨다운 종료).</summary>
    public static bool CanUse(Unit caster, SkillData skill, int currentCooldownTurns = 0)
    {
        if (caster == null || skill == null) return false;
        if (caster.currentMP < skill.mpCost)  return false;
        if (currentCooldownTurns > 0)         return false;
        return true;
    }

    /// <summary>
    /// 단일 대상 스킬 실행. MP를 소모하고 결과를 반환합니다.
    /// </summary>
    /// <returns>대상에게 적용된 데미지 결과. 회복형이면 amount는 회복량 (음수 아님).</returns>
    public static DamageResult ExecuteSingle(Unit caster, Unit target, SkillData skill)
    {
        if (caster == null || skill == null) return DamageResult.Hit(0);

        // MP 소모
        caster.currentMP = Mathf.Max(0, caster.currentMP - skill.mpCost);

        // 버프 부여 (BuffManager는 플레이어 단일 대상 — 스킬 시전자는 항상 플레이어)
        if (skill.buffs != null && skill.buffs.Count > 0)
            BuffManager.Instance?.AddBuffs(skill.buffs);

        // 회복 스킬
        if (skill.healAmount > 0)
        {
            Unit healTarget = target != null ? target : caster;
            healTarget.Heal(skill.healAmount);
            return DamageResult.Hit(skill.healAmount);
        }

        // 데미지 스킬 (버프/공감 전용 스킬은 damageMultiplier 0으로 데미지 생략)
        if (target == null || skill.damageMultiplier <= 0f) return DamageResult.Hit(0);

        float atkMul    = BuffManager.Instance != null ? BuffManager.Instance.AttackMultiplier : 1f;
        int   critBonus = BuffManager.Instance != null ? Mathf.RoundToInt(BuffManager.Instance.CritBonus) : 0;
        DamageResult result = DamageCalculator.Calculate(
            caster, target, skill.damageMultiplier,
            attackMultiplier: atkMul, critBonus: critBonus);
        target.TakeDamage(result);
        return result;
    }
}
