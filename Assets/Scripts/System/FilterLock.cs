using UnityEngine;

/// <summary>
/// 탐험 중 필터 고정 (C-17-2 · F-6 · E-71-4).
///   마시멜로(마시멜로를 띄운 핫코코 포함)를 먹으면 환상에 <see cref="FantasySeconds"/>초,
///   단검을 뽑으면 현실에 <see cref="RealitySeconds"/>초 고정된다.
///
/// 고정 중에는 반대쪽 도구가 반응하지 않으며 아이템도 소모되지 않는다. <b>잠금 표시를 띄우지 않는다</b> —
/// 키가 그냥 듣지 않을 뿐이다(C-17-2). 아이템 칸의 섭취 제한 잠금(C-17-3 · <see cref="MarshmallowRules"/>)과는 다른 처리다.
///
/// 전투 중에는 고정을 걸지 않는다 — 전투 중 전환은 양방향이어야 한다(C-6-2). 전투가 도는 동안은
/// 고정이 없는 것으로 보고 시계도 멈춘다.
/// </summary>
public static class FilterLock
{
    public const float FantasySeconds = 60f;
    public const float RealitySeconds = 30f;

    static float _fantasyLeft;
    static float _realityLeft;

    static bool InBattle => BattleSystem.IsActive || HackSlashCombatManager.IsActive;

    /// <summary>마시멜로로 환상에 고정돼 있는가. 이 동안 단검(F키)은 듣지 않는다.</summary>
    public static bool FantasyLocked => !InBattle && _fantasyLeft > 0f;

    /// <summary>단검으로 현실에 고정돼 있는가. 이 동안 마시멜로는 먹히지 않고 소모되지도 않는다.</summary>
    public static bool RealityLocked => !InBattle && _realityLeft > 0f;

    public static void LockFantasy()
    {
        if (InBattle) return;
        _realityLeft = 0f;
        _fantasyLeft = FantasySeconds;
    }

    public static void LockReality()
    {
        if (InBattle) return;
        _fantasyLeft = 0f;
        _realityLeft = RealitySeconds;
    }

    /// <summary>씬 재시작 · 되감기 · 새 게임에서 고정을 푼다.</summary>
    public static void Clear()
    {
        _fantasyLeft = 0f;
        _realityLeft = 0f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Clear();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateTicker()
    {
        var go = new GameObject("FilterLock [Auto]");
        Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideInHierarchy;
        go.AddComponent<Ticker>();
    }

    sealed class Ticker : MonoBehaviour
    {
        void Update()
        {
            // 일시정지(timeScale 0)에서는 deltaTime 이 0 이라 시계도 멈춘다.
            if (InBattle) return;
            float dt = Time.deltaTime;
            if (_fantasyLeft > 0f) _fantasyLeft = Mathf.Max(0f, _fantasyLeft - dt);
            if (_realityLeft > 0f) _realityLeft = Mathf.Max(0f, _realityLeft - dt);
        }
    }
}
