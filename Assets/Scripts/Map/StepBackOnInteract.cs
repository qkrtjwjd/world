using System.Collections;
using UnityEngine;

/// <summary>
/// 특정 오브젝트와 상호작용 시 플레이어를 오브젝트 반대 방향으로 한 걸음 뒤로 이동시킵니다.
///
/// [사용법]
/// 1. 오브젝트에 InteractionTrigger 추가
/// 2. 이 컴포넌트 추가
/// 3. stepBackDistance / stepBackDuration 인스펙터에서 조정
/// </summary>
[RequireComponent(typeof(InteractionTrigger))]
public class StepBackOnInteract : MonoBehaviour
{
    [Header("후퇴 설정")]
    [Tooltip("플레이어가 뒤로 물러날 거리 (유닛)")]
    public float stepBackDistance = 0.8f;

    [Tooltip("후퇴 소요 시간 (초). 작을수록 빠르게 이동합니다.")]
    public float stepBackDuration = 0.2f;

    void Awake()
    {
        GetComponent<InteractionTrigger>().onInteract.AddListener(TriggerStepBack);
    }

    void OnDestroy()
    {
        var trigger = GetComponent<InteractionTrigger>();
        if (trigger != null)
            trigger.onInteract.RemoveListener(TriggerStepBack);
    }

    void TriggerStepBack()
    {
        GameObject player = null;

        if (PlayerStats.Instance != null)
            player = PlayerStats.Instance.gameObject;
        else
            player = GameObject.FindWithTag("Player");

        if (player == null) return;

        StartCoroutine(StepBack(player));
    }

    IEnumerator StepBack(GameObject player)
    {
        // 오브젝트에서 플레이어 방향으로 후퇴
        Vector2 dir = ((Vector2)player.transform.position - (Vector2)transform.position).normalized;
        if (dir == Vector2.zero) dir = Vector2.down; // 같은 위치일 경우 아래로 기본값

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        Vector2 startPos = player.transform.position;
        Vector2 targetPos = startPos + dir * stepBackDistance;

        float elapsed = 0f;

        while (elapsed < stepBackDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / stepBackDuration);
            // EaseOut 곡선으로 자연스러운 감속
            t = 1f - (1f - t) * (1f - t);

            Vector2 newPos = Vector2.Lerp(startPos, targetPos, t);
            if (rb != null)
                rb.MovePosition(newPos);
            else
                player.transform.position = newPos;

            yield return null;
        }

        // 정확한 최종 위치 보정
        if (rb != null)
            rb.MovePosition(targetPos);
        else
            player.transform.position = targetPos;
    }
}
