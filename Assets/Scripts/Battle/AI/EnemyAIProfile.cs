using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 AI 행동 프로파일. 가중치 기반 추첨으로 다음 행동을 결정합니다.
/// EnemyAI에 할당하면 기본 근접 공격 대신 이 프로파일의 행동들이 사용됩니다.
/// </summary>
[CreateAssetMenu(fileName = "EnemyAIProfile", menuName = "Battle/Enemy AI Profile")]
public class EnemyAIProfile : ScriptableObject
{
    [Tooltip("이 프로파일에서 선택 가능한 행동 목록.")]
    public List<EnemyAction> actions = new List<EnemyAction>();

    [Tooltip("어떤 행동도 실행 불가능할 때 추가 대기 시간(초).")]
    public float idleFallbackDelay = 0.5f;

    /// <summary>
    /// 실행 가능한 행동 중 가중치 추첨으로 하나 선택. 후보가 없으면 null.
    /// </summary>
    /// <param name="cooldownState">action → 마지막 실행 시각. 쿨다운 필터링에 사용.</param>
    public EnemyAction PickAction(EnemyAI ai, Transform target, Dictionary<EnemyAction, float> cooldownState)
    {
        if (actions == null || actions.Count == 0) return null;

        float now = Time.time;
        int   totalWeight = 0;

        // 1) 후보 필터링 + 가중치 합계
        for (int i = 0; i < actions.Count; i++)
        {
            var a = actions[i];
            if (a == null) continue;
            if (cooldownState != null && cooldownState.TryGetValue(a, out float lastUsed))
                if (now - lastUsed < a.cooldown) continue;
            if (!a.CanExecute(ai, target)) continue;
            totalWeight += Mathf.Max(0, a.weight);
        }
        if (totalWeight <= 0) return null;

        // 2) 가중치 추첨
        int roll = Random.Range(0, totalWeight);
        int acc  = 0;
        for (int i = 0; i < actions.Count; i++)
        {
            var a = actions[i];
            if (a == null) continue;
            if (cooldownState != null && cooldownState.TryGetValue(a, out float lastUsed))
                if (now - lastUsed < a.cooldown) continue;
            if (!a.CanExecute(ai, target)) continue;

            acc += Mathf.Max(0, a.weight);
            if (roll < acc) return a;
        }
        return null;
    }
}
