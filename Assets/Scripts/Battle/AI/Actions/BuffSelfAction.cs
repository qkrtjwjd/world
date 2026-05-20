using System.Collections;
using UnityEngine;

/// <summary>
/// 일정 시간동안 공격력을 증가시키는 자버프. 효과 종료 시 원복.
/// </summary>
[CreateAssetMenu(fileName = "BuffSelfAction", menuName = "Battle/AI Actions/Buff Self")]
public class BuffSelfAction : EnemyAction
{
    [Tooltip("공격력 증가 배율 (1.5 = +50%).")]
    public float attackMultiplier = 1.5f;

    [Tooltip("버프 지속 시간(초).")]
    public float duration = 4f;

    [Tooltip("HP가 이 비율 이하일 때만 발동 (0~1, 1 = 항상).")]
    [Range(0f, 1f)] public float hpRatioThreshold = 0.5f;

    public override bool CanExecute(EnemyAI ai, Transform target)
    {
        if (ai == null) return false;
        var eh = ai.GetComponent<EnemyHealth>();
        if (eh == null || eh.maxHealth <= 0f) return true;
        return eh.currentHealth / eh.maxHealth <= hpRatioThreshold;
    }

    public override IEnumerator Execute(EnemyAI ai, Transform target)
    {
        if (ai == null) yield break;

        float originalDamage = ai.attackDamage;
        ai.attackDamage = originalDamage * attackMultiplier;

        yield return new WaitForSeconds(duration);

        if (ai != null) ai.attackDamage = originalDamage;
    }
}
