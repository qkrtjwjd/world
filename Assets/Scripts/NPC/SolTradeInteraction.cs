using System.Collections;
using UnityEngine;

/// <summary>
/// 솔 거래 진입 컴포넌트. E키 상호작용 시 인사 대사(선택)를 재생한 뒤 거래창을 엽니다.
///
/// ※ SolNPC 의 자동 인사 트리거와는 별개입니다. SolNPC 는 접근 시 1회 인사만 하고,
///    이 컴포넌트는 E키를 눌렀을 때의 거래 진입을 담당합니다. 같은 오브젝트에 함께 붙일 수 있습니다.
///
/// [사용법]
/// 1. 솔 오브젝트에 InteractionTrigger 추가
/// 2. 이 컴포넌트 추가
/// 3. stock : SolStock ScriptableObject 연결 (Assets ▸ Create ▸ NPC ▸ Sol Stock)
/// 4. mode  : 마을이면 VillageBrowse, 숲이면 ForestTrade
/// 5. yarnNode_greeting : 처음 대화 시 재생할 노드 (선택, 없으면 바로 거래창)
/// </summary>
[RequireComponent(typeof(InteractionTrigger))]
public class SolTradeInteraction : MonoBehaviour
{
    [Header("좌판 데이터")]
    public SolStock stock;

    [Header("거래 모드")]
    [Tooltip("VillageBrowse: 이름·설명이 감춰지고 어떤 거래도 성립하지 않는다.\nForestTrade: 이름·설명이 열리고 성립한다.")]
    public TradeMode mode = TradeMode.VillageBrowse;

    [Header("인사 대사 (선택)")]
    [Tooltip("E키로 처음 접촉했을 때 재생할 Yarn 노드 이름. 비워두면 바로 거래창이 열립니다.\n예: Village_Sol_Square / Shelter_Exit_Sol")]
    public string yarnNode_greeting;

    [Tooltip("인사 대사 중 플레이어 이동 잠금 여부")]
    public bool lockPlayerDuringGreeting = false;

    private bool _isOpening = false;

    void Awake()
    {
        GetComponent<InteractionTrigger>().onInteract.AddListener(OnInteract);
    }

    void OnDestroy()
    {
        var trigger = GetComponent<InteractionTrigger>();
        if (trigger != null)
            trigger.onInteract.RemoveListener(OnInteract);
    }

    void OnInteract()
    {
        if (_isOpening || SolTradeUI.IsOpen) return;
        if (stock == null)
        {
            Debug.LogWarning($"[SolTradeInteraction] '{gameObject.name}': stock 이 비어 있습니다.");
            return;
        }
        if (YarnDialogue.IsRunning) return;

        StartCoroutine(OpenTrade());
    }

    IEnumerator OpenTrade()
    {
        _isOpening = true;

        if (!string.IsNullOrEmpty(yarnNode_greeting))
            yield return YarnDialogue.PlayIfExists(yarnNode_greeting, lockPlayerDuringGreeting);

        if (SolTradeUI.Instance != null)
            SolTradeUI.Instance.Open(stock, mode);
        else
            Debug.LogWarning("[SolTradeInteraction] SolTradeUI 인스턴스를 찾을 수 없습니다. Canvas 에 SolTradeUI 를 배치해주세요.");

        _isOpening = false;
    }
}
