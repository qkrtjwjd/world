/// <summary>
/// 오버레이 컷 코드 ID — F-8-3 매핑표(F v1.33 문단 959~961)의 코드 쪽 사본이며 코드 안의 단일 출처다.
///
/// <para>
/// 표의 원본은 F-8-3 이다. 규칙으로 만들어 내지 않는다 — 같은 그림이 두 이름을 갖게 된다(F-8-3).
/// 항목을 늘리거나 바꿀 때는 F 를 먼저 고치고 여기를 맞춘다. 반대로 하지 않는다.
/// 원고 ID(한글)는 주석으로만 둔다 — 한글을 코드 경계 안으로 들이지 않는다(F-8-3 · C-16-10).
/// </para>
///
/// <para>
/// 형식은 「구간 접두 + overlay_ + 그림」이다(F-3-9). 규격 384×216 한 종 · 즉시 전환 ·
/// 배경을 어둡게 덮지 않음 · 표시 중 맵 이동 · 상호작용 · 캐릭터 애니메이션 정지.
/// 읽는 물건(쪽지)은 오버레이가 아니다 — <see cref="ReadableOverlay"/> (F-8-9).
/// </para>
/// </summary>
public static class OverlayIds
{
    // ── 집 ──────────────────────────────────────────────────────────────
    public const string HouseSeraDoorEye       = "house_overlay_sera_door_eye";       // 집_루의방_세라문틈눈 · S#02
    public const string HouseSeraTuckHand      = "house_overlay_sera_tuck_hand";      // 집_루의방_이불여미는손 · S#02
    /// <summary>
    /// 집_루의방_도자기손 · S#03 · S#04B. 마디 변형까지 한 ID 다 — 그림 파일만
    /// <see cref="PorcelainTwoJoints"/> · <see cref="PorcelainThreeJoints"/> 접미로 나누고 인형화 단계로 고른다(F-8-3 문단 955).
    /// </summary>
    public const string HouseLuPorcelainHand   = "house_overlay_lu_porcelain_hand";
    public const string HouseKuruDoorFace      = "house_overlay_kuru_door_face";      // 집_현관_쿠루문틈얼굴 · S#04B
    public const string HouseSugarcubePalm     = "house_overlay_sugarcube_palm";      // 집_현관_손바닥각설탕 · S#04B
    public const string HouseYardSugarcube     = "house_overlay_yard_sugarcube";      // 집_마당_각설탕 · S#04F (S#04G 재사용)
    public const string HouseWindowLockOpen    = "house_overlay_window_lock_open";    // 집_부엌_풀린잠금장치 · S#04F
    public const string HouseKitchenDrawer     = "house_overlay_kitchen_drawer";      // 집_부엌_서랍안 · S#08
    public const string HouseAtticBox          = "house_overlay_attic_box";           // 집_다락방_열린상자 · S#09
    public const string HouseFrontDoorKey      = "house_overlay_front_door_key";      // 집_다락방_현관문열쇠 · S#10
    public const string HouseRadioDial         = "house_overlay_radio_dial";          // 집_다락방_라디오다이얼 · S#11
    public const string HouseDaggerGrip        = "house_overlay_dagger_grip";         // 집_다락방_단검쥔손 · S#12

    // ── 마을 ────────────────────────────────────────────────────────────
    public const string TownSeraTakeHand       = "town_overlay_sera_take_hand";       // 마을_전역_세라가잡는손 · BE#02-a
    /// <summary>마을_광장_아이들그림자 · 15-D. ⚠ 넣을지 미정(수동작업 1-2) — ID 만 잡아 둔다. 부르는 곳을 만들지 말 것.</summary>
    public const string TownSquareChildren     = "town_overlay_square_children";

    // ── 숲 ──────────────────────────────────────────────────────────────
    public const string ForestSleeveFingertip  = "forest_overlay_sleeve_fingertip";   // 숲_입구_소매끝손끝 · S#16B (도자기 손 변형)
    public const string ForestSugarcubeFingers = "forest_overlay_sugarcube_fingers";  // 숲_입구_두손가락각설탕 · S#16D
    public const string ForestBarrierDome      = "forest_overlay_barrier_dome";       // 숲_결계_부감 · S#21C (E-65 · 지금은 ForestDomeOverlay 가 그린다)

    // ── 도자기 손 변형 접미 ─────────────────────────────────────────────
    public const string PorcelainTwoJoints   = "_2";   // 두 마디 = 인형화 0~30
    public const string PorcelainThreeJoints = "_3";   // 세 마디 = 인형화 31 이상 (CLAUDE.md §7)

    /// <summary>F-8-3 표 순서 그대로. 표와 대조하거나 그림 파일 누락을 셀 때 쓴다.</summary>
    public static readonly string[] All =
    {
        HouseSeraDoorEye, HouseSeraTuckHand, HouseLuPorcelainHand, HouseKuruDoorFace,
        HouseSugarcubePalm, HouseYardSugarcube, HouseWindowLockOpen, HouseKitchenDrawer,
        HouseAtticBox, HouseFrontDoorKey, HouseRadioDial, HouseDaggerGrip,
        TownSeraTakeHand, TownSquareChildren,
        ForestSleeveFingertip, ForestSugarcubeFingers, ForestBarrierDome,
    };
}
