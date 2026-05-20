using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Yarn 대화 노드 이름 목록.
/// 인스펙터에서 편집 후 Yarn 대사 빌더 창의 드롭다운에 반영됩니다.
/// 생성: 우클릭 → Create → Yarn/Node List
/// </summary>
[CreateAssetMenu(fileName = "YarnNodeListData", menuName = "Yarn/Node List")]
public class YarnNodeListData : ScriptableObject
{
    [Tooltip("드롭다운에 표시할 노드 이름 목록. 각 항목은 Yarn 파일의 title: 값과 일치해야 합니다.")]
    public List<string> nodeNames = new List<string>
    {
        "House_Start", "House_Kitchen", "House_Kitchen_Noise", "House_Kitchen_After",
        "House_Attic",  "House_Attic_Inside", "House_MomRoom",
        "Village_Start","Village_Bakery","Village_Bakery_Buy",
        "Village_Mannequin","Village_Flower","Village_Kuru","Village_Kuru_Together","Village_Kuru_Forest",
        "Forest_Start","Forest_TurnBack","Forest_Inside",
        "Forest_Observe","Forest_Deep","Forest_Discovery","Forest_End",
        "Merchant_Sol_Greeting",
        "Merchant_Sol_Deal1_Propose","Merchant_Sol_Deal1_Accept","Merchant_Sol_Deal1_Reject"
    };
}
