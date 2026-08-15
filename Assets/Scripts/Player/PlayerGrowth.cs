using UnityEngine;

/// <summary>
/// 플레이어 경험치/레벨 성장을 관리하는 정적 클래스 (GameState 패턴).
/// 프롤로그 규모에 맞춰 레벨 캡 5, 스탯 곡선은 선형.
/// </summary>
public static class PlayerGrowth
{
    public const int MaxLevel = 5;

    public static int Level { get; private set; } = 1;
    public static int Exp   { get; private set; } = 0;

    /// <summary>레벨업 시 호출 (인자: 새 레벨). UI 토스트 등에서 구독.</summary>
    public static event System.Action<int> OnLevelUp;

    // ──────────────────────────────────────────
    //  성장 곡선
    // ──────────────────────────────────────────
    /// <summary>lv → lv+1 에 필요한 경험치.</summary>
    public static int ExpToNext(int lv)   => 20 + 10 * (lv - 1);

    public static float MaxHP(int lv)     => 100f + 10f * (lv - 1);
    public static int   Attack(int lv)    => 10 + 2 * (lv - 1);
    public static int   MaxMP(int lv)     => 30 + 5 * (lv - 1);
    /// <summary>핵앤슬래시(현실) 모드 공격력.</summary>
    public static int   ActionAttack(int lv) => 50 + 4 * (lv - 1);

    // 현재 레벨 기준 편의 프로퍼티
    public static float CurrentMaxHP        => MaxHP(Level);
    public static int   CurrentAttack       => Attack(Level);
    public static int   CurrentMaxMP        => MaxMP(Level);
    public static int   CurrentActionAttack => ActionAttack(Level);

    // ──────────────────────────────────────────
    //  경험치 지급
    // ──────────────────────────────────────────
    /// <summary>경험치를 더하고, 발생한 레벨업 횟수를 반환합니다.</summary>
    public static int AddExp(int amount)
    {
        if (amount <= 0 || Level >= MaxLevel) return 0;

        Exp += amount;
        int levelUps = 0;
        while (Level < MaxLevel && Exp >= ExpToNext(Level))
        {
            Exp -= ExpToNext(Level);
            Level++;
            levelUps++;
            OnLevelUp?.Invoke(Level);
        }
        if (Level >= MaxLevel) Exp = 0; // 캡 도달 후 잉여 경험치 버림
        return levelUps;
    }

    // ──────────────────────────────────────────
    //  세이브 복원
    // ──────────────────────────────────────────
    /// <summary>세이브 로드 시 호출. 이벤트를 발생시키지 않고 값만 복원.</summary>
    public static void Load(int level, int exp)
    {
        Level = Mathf.Clamp(level, 1, MaxLevel);
        Exp   = Mathf.Max(0, exp);
    }

    // ──────────────────────────────────────────
    //  플레이 시작 시 정적 변수 초기화 (GameState 패턴)
    // ──────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ResetOnPlay()
    {
        Level     = 1;
        Exp       = 0;
        OnLevelUp = null;
    }
}
