using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 솔이 한 장소에서 펼쳐놓은 좌판.
/// 메뉴: Assets ▸ Create ▸ NPC ▸ Sol Stock
///
/// [데모 기준]
/// - openSlots 에는 하등급(Grade.Low)만 넣는다. 로직은 인접 등급 교환을 지원하지만 데이터로 제한한다.
/// - coveredSlotCount 는 3~4. 천으로 덮인 칸은 마을·숲 어느 모드에서도 열리지 않는다.
/// </summary>
[CreateAssetMenu(fileName = "New Sol Stock", menuName = "NPC/Sol Stock")]
public class SolStock : ScriptableObject
{
    [Header("펼쳐진 칸")]
    public List<TradeItem> openSlots = new List<TradeItem>();

    [Header("천으로 덮인 칸")]
    [Tooltip("표시만 되고 절대 열리지 않는 칸의 개수. 데모: 3~4")]
    [Min(0)] public int coveredSlotCount = 3;

    [Header("대화 Yarn 노드")]
    [Tooltip("Choice 창에서 '대화'를 골랐을 때 재생할 노드. 배치 장소마다 다르다. 비워두면 대화 선택지가 아무 것도 하지 않고 Choice 로 돌아온다.")]
    public string talkNode;
}
