using System.Collections;
using UnityEngine;

/// <summary>
/// 사거리 안 + 공격 쿨다운 종료 시 근접 공격을 수행. EnemyAI 내장 PerformAttack 로직을 호출.
/// </summary>
[CreateAssetMenu(fileName = "MeleeAttackAction", menuName = "Battle/AI Actions/Melee Attack")]
public class MeleeAttackAction : EnemyAction
{
    [Tooltip("이 사거리 이내에서만 발동 (단위: 월드).")]
    public float maxRange = 0.84375f;

    public override bool CanExecute(EnemyAI ai, Transform target)
    {
        if (ai == null || target == null) return false;
        return Vector2.Distance(ai.transform.position, target.position) <= maxRange;
    }

    public override IEnumerator Execute(EnemyAI ai, Transform target)
    {
        ai.PerformProfileMeleeAttack();
        yield return null;
    }
}
