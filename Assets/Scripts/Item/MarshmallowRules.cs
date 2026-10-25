using UnityEngine;

/// <summary>
/// 탐험 중 마시멜로 섭취 (C-5 · C-17 · F-2-6 · F-6 · E-71).
///
/// 마시멜로와 마시멜로를 띄운 핫코코(<see cref="ItemData.countsAsMarshmallow"/>)는 아이템 창에서 먹을 때
/// 이 규칙을 탄다 — 인형화 +2(데모 고정값) · 환상 100 · 환상 고정 60초(<see cref="FilterLock"/>).
///
/// 섭취 제한 (C-17-3) — 하나 먹으면 아이템 칸에서 잠금 상태가 되고 조건을 넘기면 다시 먹을 수 있다.
///   집   1회. S#04E 아침 마시멜로는 세지 않는다(컷신이라 이 경로를 타지 않는다).
///   마을 2회. 두 번째는 순찰 1라운드가 지난 뒤 열린다.
///   숲   전투 1회당 1회. 먹으면 다음 전투가 시작될 때까지 잠긴다.
/// 균열 굴복의 자동 사용은 이 제한을 받지 않는다 — 그 경로는 여기를 거치지 않는다.
///
/// 고정 중 반대쪽 도구 — 단검으로 현실에 고정된 30초 동안은 먹히지 않고 소모되지도 않는다.
/// 잠금 표시를 띄우지 않는다(C-17-2). 아이템 칸의 잠금(위 섭취 제한)과 다른 처리다.
/// </summary>
public static class MarshmallowRules
{
    /// <summary>먹을 때 인형화 변동. 데모 고정값(C-2-2 · F-2-6).</summary>
    public const float CorruptionPerEat = 2f;

    public enum Zone { House, Village, Forest }

    // 진행 플래그(GameStateManager.flags) — 저장 슬롯에 함께 들어간다.
    const string FlagHouse    = "mm_eaten_house";
    const string FlagVillage1 = "mm_eaten_village_1";
    const string FlagVillage2 = "mm_eaten_village_2";

    // 숲은 「이번 전투 구간」에 먹었는지만 본다. 전투가 시작될 때마다 구간 번호가 오른다.
    static int _battleSerial;
    static int _forestEatenSerial = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _battleSerial = 0;
        _forestEatenSerial = -1;
        _wasInBattle = false;
    }

    public static Zone CurrentZone
    {
        get
        {
            if (!GameState.isFrontDoorPassed) return Zone.House;
            if (ForestEntranceDirector.IsLuInForest || GameState.isKuruJoined) return Zone.Forest;
            return Zone.Village;
        }
    }

    /// <summary>아이템 칸에서 잠금으로 보일 것인가(C-17-3). 먹을 수 있는지와 같다.</summary>
    public static bool IsLockedForUse(ItemData item)
    {
        if (item == null || !item.countsAsMarshmallow) return false;
        return !HasAllowance();
    }

    static bool HasAllowance()
    {
        switch (CurrentZone)
        {
            case Zone.House:
                return !Flag(FlagHouse);
            case Zone.Village:
                if (!Flag(FlagVillage1)) return true;
                if (Flag(FlagVillage2)) return false;
                var patrol = SeraPatrol.Instance;
                return patrol != null && patrol.RoundNumber >= 2;   // 1라운드가 끝나면 2회차가 시작된다
            default:
                return _forestEatenSerial != _battleSerial;
        }
    }

    static void ConsumeAllowance()
    {
        switch (CurrentZone)
        {
            case Zone.House:   SetFlag(FlagHouse, true); break;
            case Zone.Village: SetFlag(Flag(FlagVillage1) ? FlagVillage2 : FlagVillage1, true); break;
            default:           _forestEatenSerial = _battleSerial; break;
        }
    }

    /// <summary>
    /// 탐험 중 아이템 창에서 먹는다. 먹었으면 true — 그때만 부르는 쪽이 아이템을 1개 지운다.
    /// 단검 고정 중이거나 섭취 제한에 걸리면 아무 일도 일어나지 않는다.
    /// </summary>
    public static bool TryEat(ItemData item)
    {
        if (item == null || !item.countsAsMarshmallow) return false;

        // 단검 현실 고정 중 — 반응하지 않는다. 표시도 없다(C-17-2).
        if (FilterLock.RealityLocked) return false;

        // 섭취 제한 — 칸이 이미 잠금으로 보인다. 불가음만 낸다(아이템 칸 잠금 · C-17-3).
        if (!HasAllowance())
        {
            UISfx.Buzzer();
            return false;
        }

        ConsumeAllowance();

        CorruptionManager.Instance?.AddCorruption(CorruptionPerEat);
        GaugeManager.Instance?.ForceFantasyMax();
        FilterLock.LockFantasy();
        Dbg.Log($"[마시멜로] {item.itemName} 섭취 — 인형화 +{CorruptionPerEat} · 환상 100 · 환상 고정 {FilterLock.FantasySeconds}초 ({CurrentZone})");
        return true;
    }

    /// <summary>숲에서 이번 전투에 이미 마시멜로를 썼는가(C-17-3 「전투 1회당 1회」). 집 · 마을 전투는 데모에 없다.</summary>
    public static bool UsedThisBattle => CurrentZone == Zone.Forest && _forestEatenSerial == _battleSerial;

    /// <summary>전투 중 아이템으로 마시멜로를 썼다 — 그 전투 구간의 1회를 쓴 것으로 친다(숲).</summary>
    public static void NoteBattleUse(ItemData item)
    {
        if (item == null || !item.countsAsMarshmallow) return;
        if (CurrentZone == Zone.Forest) _forestEatenSerial = _battleSerial;
    }

    static bool Flag(string key) => GameStateManager.Instance != null && GameStateManager.Instance.GetFlag(key);
    static void SetFlag(string key, bool v) => GameStateManager.Instance?.SetFlag(key, v);

    // ── 전투 구간 번호 ─────────────────────────────────
    static bool _wasInBattle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateWatcher()
    {
        var go = new GameObject("MarshmallowRules [Auto]");
        Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideInHierarchy;
        go.AddComponent<Watcher>();
        BattleEvents.OnItemUsed += (item, user) => NoteBattleUse(item);
    }

    sealed class Watcher : MonoBehaviour
    {
        void Update()
        {
            bool inBattle = BattleSystem.IsActive || HackSlashCombatManager.IsActive;
            if (inBattle && !_wasInBattle) _battleSerial++;   // 다음 전투 — 숲의 1회가 다시 열린다
            _wasInBattle = inBattle;
        }
    }
}
