using System.Collections;
using UnityEngine;

/// <summary>
/// 솔(상인) NPC. 광장/마을 출구 두 위치에 배치.
/// 플레이어 접근 시 자동 인사 대화 1회 시작.
/// </summary>
public class SolNPC : MonoBehaviour
{
    [Header("Yarn 노드")]
    [SerializeField] private string autoGreetNode   = "Village_Sol_Square";
    [SerializeField] private string breadRejectNode = "Sol_BreadDoughReject";

    [Header("근접 감지")]
    [SerializeField] private float     autoTriggerRadius = 2f;
    [SerializeField] private LayerMask playerLayer;

    private bool _hasGreeted;

    void Update()
    {
        if (_hasGreeted || YarnDialogue.IsRunning) return;

        Collider2D hit = Physics2D.OverlapCircle(transform.position, autoTriggerRadius, playerLayer);
        if (hit == null) return;

        _hasGreeted = true;
        GameState.hasMerchantMetAtSquare = true;
        StartCoroutine(YarnDialogue.PlayAndWait(autoGreetNode, true));
    }

    /// <summary>빵 반죽 거래 시도 시 호출 (InteractionTrigger.onInteract에 연결)</summary>
    public void OnBreadDoughTradeAttempt()
    {
        if (YarnDialogue.IsRunning) return;
        StartCoroutine(YarnDialogue.PlayAndWait(breadRejectNode, true));
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, autoTriggerRadius);
    }
}
