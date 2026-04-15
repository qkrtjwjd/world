using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상인 한 명의 거래 목록을 담는 ScriptableObject.
/// 메뉴: Assets > Create > NPC > Merchant Data
/// </summary>
[CreateAssetMenu(fileName = "New Merchant", menuName = "NPC/Merchant Data")]
public class MerchantData : ScriptableObject
{
    [Header("상인 정보")]
    public string merchantName;
    public Sprite portrait;

    [Header("거래 목록")]
    public List<BarterDeal> deals = new List<BarterDeal>();
}

/// <summary>
/// 물물교환 거래 한 건을 정의합니다.
/// </summary>
[System.Serializable]
public class BarterDeal
{
    [Tooltip("인스펙터 식별용 이름")]
    public string dealName;

    [Header("플레이어가 줄 것")]
    public List<BarterItemEntry> playerOffer = new List<BarterItemEntry>();

    [Header("상인이 줄 것")]
    public List<BarterItemEntry> merchantOffer = new List<BarterItemEntry>();

    [Header("수락 여부")]
    [Tooltip("false 로 설정하면 상인이 거절합니다 (대사는 rejectDialogue 사용)")]
    public bool merchantAccepts = true;

    [Header("대사")]
    [Tooltip("플레이어가 거래를 제안할 때 재생되는 대사")]
    public DialogueData proposeDialogue;

    [Tooltip("상인이 거래를 수락할 때 재생되는 대사")]
    public DialogueData acceptDialogue;

    [Tooltip("상인이 거래를 거절할 때 재생되는 대사")]
    public DialogueData rejectDialogue;

    [Header("옵션")]
    [Tooltip("체크 시: 거래 완료 후 목록에서 제거됩니다.")]
    public bool oneTimeOnly = true;

    // 런타임 상태 (저장 안 됨)
    [System.NonSerialized] public bool isCompleted = false;
}

/// <summary>
/// 거래에서 주고받는 아이템 한 항목.
/// </summary>
[System.Serializable]
public struct BarterItemEntry
{
    public ItemData item;
    [Min(1)] public int quantity;
}
