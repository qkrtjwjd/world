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
    public const string Forest_Road_Marshmallow    = "Forest_Road_Marshmallow";
    public const string Forest_Path                = "Forest_Path";
    public const string Forest_Path_Flowerbed      = "Forest_Path_Flowerbed";
    public const string Forest_Path_Stream         = "Forest_Path_Stream";
    public const string Forest_Pond                = "Forest_Pond";
    public const string Forest_WolfBattle_Pre      = "Forest_WolfBattle_Pre";
    public const string Forest_WolfBattle_PostSpare = "Forest_WolfBattle_PostSpare";
    public const string Forest_WolfBattle_PostKill  = "Forest_WolfBattle_PostKill";
    public const string Forest_SolReappear         = "Forest_SolReappear";
    public const string Forest_Bench               = "Forest_Bench";
    public const string Forest_GoldenThorns        = "Forest_GoldenThorns";
    public const string Shelter_Entry              = "Shelter_Entry";
    public const string Shelter_Mirror             = "Shelter_Mirror";
    public const string Shelter_Exit_Sol           = "Shelter_Exit_Sol";
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
