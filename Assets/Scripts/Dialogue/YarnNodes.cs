public static class YarnNodes
{
    // House.yarn
    public const string House_Start          = "House_Start";
    public const string House_Kitchen        = "House_Kitchen";
    public const string House_Kitchen_Noise  = "House_Kitchen_Noise";
    public const string House_Kitchen_After  = "House_Kitchen_After";
    public const string House_Attic          = "House_Attic";
    public const string House_Attic_Inside   = "House_Attic_Inside";
    public const string House_MomRoom        = "House_MomRoom";

    // Opening_Monologue.yarn
    public const string Opening_Monologue = "Opening_Monologue";

    // Village_Demo.yarn
    // 2026-08-14 — Village_Entrance / Village_DaggerDistortion / Village_Exit_Sol /
    //   Village_Exit_KuruMeet 상수 삭제. D-2 정본에 대응 항목이 없어 노드 자체를 지웠다.
    public const string Village_Sol_Square       = "Village_Sol_Square";
    public const string Village_FlowerShop       = "Village_FlowerShop";
    public const string Village_Bakery           = "Village_Bakery";
    public const string BakeryNPC_Loop_Normal    = "BakeryNPC_Loop_Normal";
    public const string BakeryNPC_Loop_Dagger    = "BakeryNPC_Loop_Dagger";

    // Forest_Demo.yarn
    // D-3 정본 (S#16~S#17). 전투 노드는 TutorialBattleManager 가 조건별로 호출한다.
    public const string Forest_Entrance            = "Forest_Entrance";
    public const string Forest_Kuru_Greet          = "Forest_Kuru_Greet";
    public const string Forest_Kuru_Radio          = "Forest_Kuru_Radio";
    public const string Forest_Kuru_SugarCube      = "Forest_Kuru_SugarCube";
    public const string Forest_Wolf_Encounter      = "Forest_Wolf_Encounter";
    public const string Forest_Wolf_Tutorial       = "Forest_Wolf_Tutorial";
    public const string Forest_Wolf_Guard_Praise   = "Forest_Wolf_Guard_Praise";
    public const string Forest_Wolf_React_Attack   = "Forest_Wolf_React_Attack";
    public const string Forest_Wolf_React_Guard    = "Forest_Wolf_React_Guard";
    public const string Forest_Wolf_React_Pet      = "Forest_Wolf_React_Pet";
    public const string Forest_Wolf_Pet2           = "Forest_Wolf_Pet2";
    public const string Forest_Wolf_Pet3           = "Forest_Wolf_Pet3";
    public const string Forest_Wolf_Hurt           = "Forest_Wolf_Hurt";
    public const string Forest_Wolf_KillEnd        = "Forest_Wolf_KillEnd";
    public const string Forest_Wolf_PurifyEnd      = "Forest_Wolf_PurifyEnd";

    // S#19 2차 전투(액션). 2026-08-16 D 개정분.
    public const string Forest_Wolf2_Encounter     = "Forest_Wolf2_Encounter";
    public const string Forest_Wolf2_Reveal        = "Forest_Wolf2_Reveal";
    public const string Forest_Wolf2_Weakpoint     = "Forest_Wolf2_Weakpoint";
    public const string Forest_Wolf2_Finisher      = "Forest_Wolf2_Finisher";
    public const string Forest_Wolf2_KillEnd       = "Forest_Wolf2_KillEnd";
    public const string Forest_Wolf2_SpareEnd      = "Forest_Wolf2_SpareEnd";

    // 구 원고(명세서 v7) 잔존분 — 정본에 S#18 이후 원고가 없어 남겨둔 것. 나오면 교체한다.
    public const string Forest_GoldenThorns        = "Forest_GoldenThorns";
    public const string Forest_Camp_Night          = "Forest_Camp_Night";

    // Radio_Yu.yarn
    public const string Radio_Fountain       = "Radio_Fountain";
    public const string Radio_FlowerShop     = "Radio_FlowerShop";
    public const string Radio_BreadDough     = "Radio_BreadDough";
    public const string Radio_DistortedVillage = "Radio_DistortedVillage";
    public const string Radio_Tracker        = "Radio_Tracker";
    public const string Radio_Miru           = "Radio_Miru";
    public const string Radio_Amo            = "Radio_Amo";
    public const string Radio_ToothFlower    = "Radio_ToothFlower";
    public const string Radio_Stream         = "Radio_Stream";
    public const string Radio_FallenFruit    = "Radio_FallenFruit";
    public const string Radio_AllianceMark   = "Radio_AllianceMark";
    public const string Radio_Wolf           = "Radio_Wolf";
    public const string Radio_Pond           = "Radio_Pond";
    public const string Radio_Tripped        = "Radio_Tripped";
    public const string Radio_Campfire       = "Radio_Campfire";
    public const string Radio_Shelter        = "Radio_Shelter";
    public const string Radio_BattleSpare    = "Radio_BattleSpare";
    public const string Radio_BattleWin      = "Radio_BattleWin";

    // 솔 거래 (SolTradeUI / SolTradeRules)
    // ⚠ 아래 노드들은 아직 .yarn 파일에 작성되지 않았다.
    //   YarnDialogue.PlayIfExists 로 호출하므로 노드가 없으면 경고만 나고 거래창은 정상 동작한다.
    //   거절 사유 텍스트는 전부 이 노드들 안에 있고 C# 에는 이름만 둔다.
    public const string Sol_Trade_Success                = "Sol_Trade_Success";
    public const string Sol_Trade_Reject_Village         = "Sol_Trade_Reject_Village";
    public const string Sol_Trade_Reject_GradeMismatch   = "Sol_Trade_Reject_GradeMismatch";
    public const string Sol_Trade_Reject_Contaminated    = "Sol_Trade_Reject_Contaminated";
    public const string Sol_Trade_Reject_Empty           = "Sol_Trade_Reject_Empty";
    public const string Sol_Trade_Reject_PlayerWithdraws = "Sol_Trade_Reject_PlayerWithdraws";
}
