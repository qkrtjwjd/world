using System.Collections;
using UnityEngine;

/// <summary>
/// 적 AI가 수행 가능한 행동의 추상 베이스.
/// ScriptableObject로 만들어 에디터에서 자산으로 관리합니다.
/// </summary>
public abstract class EnemyAction : ScriptableObject
{
    [Tooltip("로그/디버그용 이름.")]
    public string actionName = "Action";

    [Tooltip("이 행동의 쿨다운(초). 같은 EnemyAI에서 마지막 실행으로부터 이 시간이 지나야 다시 선택 가능.")]
    public float cooldown = 1f;

    [Tooltip("프로파일 가중치 추첨 비중. 높을수록 자주 선택됨.")]
    [Range(0, 100)] public int weight = 50;

    /// <summary>이 행동을 실행할 수 있는지 검사. (사거리, HP, 자원 등)</summary>
    public abstract bool CanExecute(EnemyAI ai, Transform target);

    /// <summary>실제 행동 실행. EnemyAI의 코루틴으로 돌아갑니다.</summary>
    public abstract IEnumerator Execute(EnemyAI ai, Transform target);
}
