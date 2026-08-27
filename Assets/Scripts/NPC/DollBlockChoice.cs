using System.Collections;
using UnityEngine;

/// <summary>
/// 인형 NPC가 루를 막는 상황. 선택지 3개를 제공하며
/// 세 번째 선택(그림자 밟기) 시 NPC 비틀거림 애니메이션 + 루 혼잣말 출력.
/// </summary>
public class DollBlockChoice : MonoBehaviour
{
    [Header("인형 NPC Animator")]
    [SerializeField] private Animator dollAnimator;
    [SerializeField] private string   stumbleTrigger = "Stumble";

    [Header("Yarn 노드")]
    [SerializeField] private string choiceNode = "DollBlock_Choice";

    [Header("근접 감지")]
    [SerializeField] private float     triggerRadius = 0.675f;
    [SerializeField] private LayerMask playerLayer;

    private bool _hasFired;

    void Update()
    {
        if (_hasFired || YarnDialogue.IsRunning) return;

        Collider2D hit = Physics2D.OverlapCircle(transform.position, triggerRadius, playerLayer);
        if (hit == null) return;

        _hasFired = true;
        StartCoroutine(YarnDialogue.PlayAndWait(choiceNode, true));
    }

    /// <summary>Yarn <<dollStumble>> 커맨드로 호출됩니다.</summary>
    [Yarn.Unity.YarnCommand("dollStumble")]
    public void DollStumble()
    {
        dollAnimator?.SetTrigger(stumbleTrigger);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, triggerRadius);
    }
}
