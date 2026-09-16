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
    // D-3 정본 (S#16~S#21). 전투 노드는 TutorialBattleManager 가 조건별로 호출한다.
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

    // S#20 솔 재조우 · 거래. 2026-08-27 D 개정분. 인형화 판정은 진입 시 1회다(F-4-8) —
    //   _Low / _High 앞의 디스패처 노드(Forest_Sol_Reunion 등)는 정본 대사가 0줄인 수기 허브라
    //   node_map.json 에 등재하지 않는다. 여기에는 상수를 둔다 — .yarn 에 실재하는 노드다.
    public const string Forest_Sol_Reunion              = "Forest_Sol_Reunion";
    public const string Forest_Sol_Reunion_Low          = "Forest_Sol_Reunion_Low";
    public const string Forest_Sol_Reunion_High         = "Forest_Sol_Reunion_High";
    public const string Forest_Sol_Reject_Sugar         = "Forest_Sol_Reject_Sugar";
    public const string Forest_Sol_Reject_Sugar_Low     = "Forest_Sol_Reject_Sugar_Low";
    public const string Forest_Sol_Reject_Sugar_High    = "Forest_Sol_Reject_Sugar_High";
    public const string Forest_Sol_TalkMenu             = "Forest_Sol_TalkMenu";
    public const string Forest_Sol_Talk_Faster          = "Forest_Sol_Talk_Faster";
    public const string Forest_Sol_Talk_Faster_Low      = "Forest_Sol_Talk_Faster_Low";
    public const string Forest_Sol_Talk_Faster_High     = "Forest_Sol_Talk_Faster_High";
    public const string Forest_Sol_Talk_Stock           = "Forest_Sol_Talk_Stock";
    public const string Forest_Sol_Talk_Stock_Intro     = "Forest_Sol_Talk_Stock_Intro";
    public const string Forest_Sol_Talk_Stock_Low       = "Forest_Sol_Talk_Stock_Low";
    public const string Forest_Sol_Talk_Stock_High      = "Forest_Sol_Talk_Stock_High";
    public const string Forest_Sol_Talk_Stock_Common    = "Forest_Sol_Talk_Stock_Common";
    public const string Forest_Sol_Talk_Outside         = "Forest_Sol_Talk_Outside";
    public const string Forest_Sol_Talk_Outside_Intro   = "Forest_Sol_Talk_Outside_Intro";
    public const string Forest_Sol_Talk_Outside_Low     = "Forest_Sol_Talk_Outside_Low";
    public const string Forest_Sol_Talk_Outside_High    = "Forest_Sol_Talk_Outside_High";

    // S#21 데모 종료. 2026-09-16 D 개정분. 아직 배선 전이며 호출하는 곳이 없다.
    //   ⚠ S#21B 는 폐기됐다(E-62-2). 번호를 재배열하지 않아 자리가 비어 있다 — 만들지 말 것.
    public const string Forest_Barrier_Arrival     = "Forest_Barrier_Arrival";
    public const string Forest_Barrier_Reinforced  = "Forest_Barrier_Reinforced";
    public const string Forest_Sera_Voice          = "Forest_Sera_Voice";
    public const string Forest_Demo_End            = "Forest_Demo_End";

    // 2026-09-16 — Forest_GoldenThorns · Forest_Camp_Night 삭제. 다시 만들지 말 것.
    //   구 원고(명세서 v7)로 데모 종료 자리를 임시로 메우던 노드다. D 에 S#21 원고가
    //   들어와 위 4개로 교체했고 .yarn 의 노드도 함께 지웠다. 삭제 근거는
    //   Forest_Demo.yarn 의 S#21 블록 머리 주석에 있다.
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
