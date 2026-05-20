using UnityEngine;

/// <summary>
/// 배틀 시스템이 사용하는 서비스들의 등록·조회 단일 진입점.
/// 가벼운 Service Locator 패턴.
///
/// 사용:
///   - 구체 구현(<see cref="PlayerStats"/>, <see cref="InventoryManager"/>)이 Awake에서
///     <see cref="Register"/> 를 호출해 자기 자신을 등록.
///   - 배틀 시스템 등 소비자는 <see cref="PlayerStats"/> / <see cref="Inventory"/> 프로퍼티로 조회.
///   - 등록 전 또는 매니저가 없는 환경에서는 null 반환 — 호출자는 ?. 패턴으로 안전 호출.
/// </summary>
public static class BattleServices
{
    public static IPlayerStatsService PlayerStats { get; private set; }
    public static IInventoryService   Inventory   { get; private set; }

    public static void Register(IPlayerStatsService p) => PlayerStats = p;
    public static void Register(IInventoryService   i) => Inventory   = i;

    /// <summary>
    /// 도메인 리로드(Edit Mode → Play 전환) 시 정적 참조가 이전 씬의 매니저를
    /// 그대로 들고 있는 문제를 방지.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        PlayerStats = null;
        Inventory   = null;
    }
}
