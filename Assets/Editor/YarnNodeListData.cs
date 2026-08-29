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
        // ── 2026-08-14 실제 .yarn 기준으로 전수 재작성 ──
        // 옛 House.yarn / Opening_Monologue.yarn 시절 이름 8개는 파일 자체가 없어져 삭제:
        //   House_Start, House_Kitchen, House_Kitchen_Noise, House_Kitchen_After,
        //   House_Attic, House_Attic_Inside, House_MomRoom, Opening_Monologue
        //
        // ── 2026-08-30 삭제 ──
        //   Radio_* 18개 — 구 라디오 반응 시스템 폐기(E-39-2 · E-52). Radio_Yu.yarn 도 지웠다.
        //   House_lu_star_r — 구 라디오 반응 대사. House_lu_sofa — 배선 없는 고아 노드.
        //   Village_Sol_Radio — 정본 D-2 15-F 삭제(E 3.0).
        //   Sol_Trade_Success — 정본에 거래 성립 대사가 없다(D-3 S#20 · F-7-1).

        // Intro.yarn
        "Intro",
        // House_Opening.yarn (D-1 정본 S#01~S#04H)
        "House_Owl_Wake",
        "House_Sera_Lock_Enter", "House_Sera_Lock_Window", "House_Sera_Lock_Tuck",
        "House_Unheard_Owl", "House_Unheard_Close",
        "House_Kitchen_ThreePlates",
        "House_Doorbell_Ring", "House_Doorbell_Tap", "House_Doorbell_Sugar",
        "House_Eavesdrop_Tea", "House_Eavesdrop_Silence", "House_Eavesdrop_Name", "House_Eavesdrop_Light",
        "House_Note", "House_Note_Table",
        "House_Marshmallow_Eat",
        "House_Yard_Sugar", "House_Yard_Refuse", "House_Yard_Refuse2",
        "House_Window_Plea",
        // House_Attic.yarn (D-1 정본 S#06~S#13)
        "House_Doorknob_Refused", "House_Search_Drawer", "House_Search_SeraDoor",
        "House_Kitchen_Drawer", "House_Attic_Box", "House_Coat_Key",
        "House_Radio_Yu_First", "House_Dagger_Flash", "House_FrontDoor_Depart",
        // 집 안 상호작용 (개별 .yarn)
        "House_attic_door",
        "House_kitchen_marshmallow", "House_kitchen_refrigerator", "House_kitchen_refrigerator1",
        "House_lu_marshmallow", "House_lu_star", "House_lu_star1",
        "House_lu_toybox", "House_lu_toybox1",
        "House_sera_bed", "House_sera_closet",
        // Village_Demo.yarn
        "Village_Sol_Square", "Village_FlowerShop", "Village_Bakery",
        "BakeryNPC_Loop_Normal", "BakeryNPC_Loop_Dagger", "Sol_BreadDoughReject",
        // D-2 정본 신규 노드 (Scenario/node_map.json 의 S#14·S#15) — 아직 .yarn 미작성
        "Village_Sol_Talk_Purity", "Village_Sol_Talk_Since", "Village_Sol_Talk_Money",
        "Village_Sera_Spotted", "Village_Square_Children",
        "Village_Sol_Trade_Sugar",
        // Forest_Demo — D-3 정본 (S#16~S#17)
        "Forest_Entrance", "Forest_Kuru_Greet", "Forest_Kuru_Radio", "Forest_Kuru_SugarCube",
        "Forest_Wolf_Encounter", "Forest_Wolf_Tutorial", "Forest_Wolf_Guard_Praise",
        "Forest_Wolf_React_Attack", "Forest_Wolf_React_Guard", "Forest_Wolf_React_Pet",
        "Forest_Wolf_Pet2", "Forest_Wolf_Pet3", "Forest_Wolf_Hurt",
        "Forest_Wolf_KillEnd", "Forest_Wolf_PurifyEnd",
        // 구 원고 잔존분 — S#18 이후 원고가 나오면 교체 대상
        "Forest_GoldenThorns", "Forest_Camp_Night",
        // 솔 거래 노드 (아직 .yarn 미작성)
        "Sol_Trade_Reject_Village", "Sol_Trade_Reject_GradeMismatch",
        "Sol_Trade_Reject_Contaminated", "Sol_Trade_Reject_Empty", "Sol_Trade_Reject_PlayerWithdraws"
    };
}
