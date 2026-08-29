public static class YarnNodes
{
    // 2026-08-30 — 죽은 상수 8개 삭제. 다시 만들지 말 것.
    //   House_Start · House_Kitchen · House_Kitchen_Noise · House_Kitchen_After ·
    //   House_Attic · House_Attic_Inside · House_MomRoom · Opening_Monologue
    //   전부 .yarn 에 노드가 없고 코드에서 부르는 곳도 0회였다. 구버전 House.yarn 시절의
    //   잔재이며 정본 D 에 대응 항목이 없다. Opening_Monologue 노드는 2026-08-08 에
    //   사용자 지시로 삭제됐다(Home 씬에서 오프닝 독백이 중복 재생되던 노드).

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
    // 2026-08-30 — 구 라디오 반응 시스템을 걷어내며 Radio_* 상수 18개를 삭제했다.
    //   다시 만들지 말 것. E-39-2 가 반응 대상 16종 목록을, E-52 가 [라디오] 선택지
    //   방식을 폐기했다. 유의 반응은 이제 대비 오브젝트 노드 안에서 $라디오소지 로
    //   조건 분기한다(F-8-4) — 별도 노드도 매니저도 두지 않는다.
    //   구 대사 18건 전문은 무채색_낙원_D이관대기자료.docx 3절에 보존돼 있다.

    // 솔 거래 (SolTradeUI / SolTradeRules)
    // ⚠ 아래 노드들은 아직 .yarn 파일에 작성되지 않았다.
    //   YarnDialogue.PlayIfExists 로 호출하므로 노드가 없으면 경고만 나고 거래창은 정상 동작한다.
    //   거절 사유 텍스트는 전부 이 노드들 안에 있고 C# 에는 이름만 둔다.
    // ⚠ Sol_Trade_Success 는 2026-08-30 삭제했다. 다시 만들지 말 것.
    //   정본에 거래 성립 대사가 없다 — D-3 S#20 은 A 조우 · B 각설탕 거절 · C 검은 구슬 ·
    //   D 상시 대화 넷뿐이고 수취 순간에 대사를 두지 않았다. F-7-1 의 조작 흐름도 성립 시
    //   대사 호출을 규정하지 않는다(「성립 판정은 가치 비교로 처리한다」뿐). 거절은 사유별로
    //   전부 규정해 둔 문서가 성립만 비워 둔 것이므로 무음이 의도다.
    //   정본에 성립 대사가 생기면 노드를 만들고 SolTradeRules.Resolve 에서 다시 부를 것.
    public const string Sol_Trade_Reject_Village         = "Sol_Trade_Reject_Village";
    public const string Sol_Trade_Reject_GradeMismatch   = "Sol_Trade_Reject_GradeMismatch";
    public const string Sol_Trade_Reject_Contaminated    = "Sol_Trade_Reject_Contaminated";
    public const string Sol_Trade_Reject_Empty           = "Sol_Trade_Reject_Empty";
    public const string Sol_Trade_Reject_PlayerWithdraws = "Sol_Trade_Reject_PlayerWithdraws";
}
