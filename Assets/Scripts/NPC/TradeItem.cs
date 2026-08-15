using UnityEngine;

/// <summary>
/// 거래 등급. 인접 등급끼리만 교환되며 교환비는 1:5(양방향 등가)다.
/// ※ ItemData.grade(ItemGrade)와는 별개 개념이다. 서로 변환하지 않는다.
/// </summary>
public enum Grade { Low, Mid, High, Top }

/// <summary>
/// 솔이 취급하는 거래 품목 한 종.
/// 메뉴: Assets ▸ Create ▸ NPC ▸ Trade Item
///
/// [주의]
/// - displayName 은 반드시 인스펙터에서 채운다. 코드 어디에도 품목 이름을 하드코딩하지 않는다.
/// - 숲 신규 물품은 명칭 미정이므로 id 를 "forest_currency_tmp" 로 두고 displayName 은 비워둔다.
/// - source 를 연결하면 InventoryManager 와 연동되어 실제로 아이템이 오간다.
///   비워두면 표시 전용 품목이 되어 거래가 성립해도 인벤토리는 변하지 않는다.
/// </summary>
[CreateAssetMenu(fileName = "New Trade Item", menuName = "NPC/Trade Item")]
public class TradeItem : ScriptableObject
{
    [Header("식별")]
    [Tooltip("임시 키 허용. 예: forest_currency_tmp")]
    public string id;

    [Header("표시 (마을 모드에서는 감춰진다)")]
    [Tooltip("품목 이름. 명칭이 정해지지 않았다면 비워둔다.")]
    public string displayName;

    [TextArea(2, 5)]
    public string description;

    [Header("거래")]
    public Grade  grade;
    public Sprite icon;

    [Header("인벤토리 연동 (선택)")]
    [Tooltip("이 품목에 대응하는 ItemData. 연결하면 거래 성립 시 실제로 아이템이 증감한다.")]
    public ItemData source;

    [Header("거절 오버라이드 (선택)")]
    [Tooltip("체크하면 등급 규칙보다 아래 사유가 우선한다. 루가 이 품목을 내밀었을 때 적용된다.")]
    public bool hasRejectOverride;

    [Tooltip("마을(VillageBrowse)에서 이 품목을 내밀었을 때의 거절 사유")]
    public RejectReason villageReject;

    [Tooltip("숲(ForestTrade)에서 이 품목을 내밀었을 때의 거절 사유")]
    public RejectReason forestReject;

    [Header("거절 Yarn 노드 오버라이드 (선택 — 비우면 사유별 기본 노드)")]
    [Tooltip("예: 각설탕은 마을에서 루의 독백 노드로 보낸다.")]
    public string rejectNodeVillage;
    public string rejectNodeForest;
}
