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

        // 회복 스킬
        if (skill.healAmount > 0)
        {
            Unit healTarget = target != null ? target : caster;
            healTarget.Heal(skill.healAmount);
            return DamageResult.Hit(skill.healAmount);
        }

        // 데미지 스킬
        if (target == null) return DamageResult.Hit(0);
        DamageResult result = DamageCalculator.Calculate(caster, target, skill.damageMultiplier);
        target.TakeDamage(result);
        return result;
    }
}
