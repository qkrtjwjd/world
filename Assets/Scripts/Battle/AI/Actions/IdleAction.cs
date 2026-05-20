using System.Collections;
using UnityEngine;

/// <summary>
/// 일정 시간 정지하여 행동 패턴에 텀을 주는 행동.
/// </summary>
[CreateAssetMenu(fileName = "IdleAction", menuName = "Battle/AI Actions/Idle")]
public class IdleAction : EnemyAction
{
    [Tooltip("정지 시간 (초).")]
    public float idleDuration = 0.8f;

    public override bool CanExecute(EnemyAI ai, Transform target) => true;

    public override IEnumerator Execute(EnemyAI ai, Transform target)
    {
        yield return new WaitForSeconds(idleDuration);
    }
}
