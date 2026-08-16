using System;
using UnityEngine;

/// <summary>
/// 배틀 시스템 전역 이벤트 허브.
/// 매니저 간 직접 참조를 줄이고 UI/효과 시스템이 느슨하게 구독할 수 있게 합니다.
///
/// 도메인 리로드(Edit Mode → Play 전환) 시 정적 핸들러가 누적되는 것을 막기 위해
/// <see cref="ResetHandlers"/>가 SubsystemRegistration 단계에서 자동 호출됩니다.
/// </summary>
public static class BattleEvents
{
    /// <summary>유닛이 피격된 직후. (피격된 유닛, 데미지 결과). 회피 시에도 발행 (result.isMiss = true).</summary>
    public static event Action<Unit, DamageResult> OnUnitDamaged;

    /// <summary>유닛이 사망한 직후. (사망 유닛).</summary>
    public static event Action<Unit> OnUnitDied;

    /// <summary>유닛이 회복된 직후. (회복된 유닛, 회복량). HP바 갱신용.</summary>
    public static event Action<Unit, int> OnUnitHealed;

    /// <summary>전투 모드 전환 완료 시. (전환 후 모드).</summary>
    public static event Action<BattleMode> OnModeChanged;

    /// <summary>플레이어/적 턴 시작 시. (행동 유닛).</summary>
    public static event Action<Unit> OnTurnStarted;

    /// <summary>전투 종료 시 (승리/패배 무관).</summary>
    public static event Action OnBattleEnded;

    /// <summary>스킬 사용 직후. (시전자, 스킬, 결과). UI 상태 갱신용.</summary>
    public static event Action<Unit, SkillData, DamageResult> OnSkillUsed;

    /// <summary>유닛 MP 변동 시. (유닛, 현재MP, 최대MP). MP바/스킬 슬롯 활성화 갱신용.</summary>
    public static event Action<Unit, int, int> OnUnitMPChanged;

    /// <summary>전투 중 아이템 사용 시. (아이템, 사용자). ItemUseTracker/ItemEffectHandler/BuffManager 가 구독.</summary>
    public static event Action<ItemData, Unit> OnItemUsed;

    /// <summary>
    /// 플레이어가 턴제 행동을 고른 직후. (행동 종류, 쓰다듬기 누적 횟수).
    /// 행동이 실행되기 전에 발행되므로 튜토리얼 대사를 앞에 끼워 넣을 수 있다.
    /// </summary>
    public static event Action<BattleActionKind, int> OnPlayerAction;

    /// <summary>전투가 어떻게 끝났는지. <see cref="OnBattleEnded"/> 직전에 발행한다.</summary>
    public static event Action<BattleOutcome> OnBattleFinished;

    // ─── 발행 헬퍼 ─────────────────────────────────────────────
    public static void RaiseUnitDamaged(Unit unit, DamageResult result) => OnUnitDamaged?.Invoke(unit, result);
    public static void RaiseUnitDied(Unit unit)                          => OnUnitDied?.Invoke(unit);
    public static void RaiseUnitHealed(Unit unit, int amount)            => OnUnitHealed?.Invoke(unit, amount);
    public static void RaiseModeChanged(BattleMode mode)                 => OnModeChanged?.Invoke(mode);
    public static void RaiseTurnStarted(Unit unit)                       => OnTurnStarted?.Invoke(unit);
    public static void RaiseBattleEnded()                                => OnBattleEnded?.Invoke();
    public static void RaiseSkillUsed(Unit caster, SkillData skill, DamageResult result)
                                                                         => OnSkillUsed?.Invoke(caster, skill, result);
    public static void RaiseUnitMPChanged(Unit unit)                     => OnUnitMPChanged?.Invoke(unit, unit != null ? unit.currentMP : 0,
                                                                                                              unit != null ? unit.maxMP     : 0);
    public static void RaiseItemUsed(ItemData item, Unit user)           => OnItemUsed?.Invoke(item, user);
    public static void RaisePlayerAction(BattleActionKind kind, int sootheCount)
                                                                         => OnPlayerAction?.Invoke(kind, sootheCount);
    public static void RaiseBattleFinished(BattleOutcome outcome)        => OnBattleFinished?.Invoke(outcome);

    /// <summary>
    /// 도메인 리로드 시 정적 이벤트가 이전 씬의 핸들러를 끌고 가는 문제 방지.
    /// Unity 2019.3+ 의 SubsystemRegistration 시점에 호출됩니다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetHandlers()
    {
        OnUnitDamaged    = null;
        OnUnitDied       = null;
        OnUnitHealed     = null;
        OnModeChanged    = null;
        OnTurnStarted    = null;
        OnBattleEnded    = null;
        OnSkillUsed      = null;
        OnUnitMPChanged  = null;
        OnItemUsed       = null;
        OnPlayerAction   = null;
        OnBattleFinished = null;
    }
}

/// <summary>턴제 행동 종류. 튜토리얼 대사 분기(정본 ▶ 행동별 반응)의 키다.</summary>
public enum BattleActionKind
{
    Attack,
    Defend,
    /// <summary>[쓰다듬기]. 데미지·판정 없음. 누적 3회에 정화가 성립한다 (F-2-6).</summary>
    Soothe,
}

/// <summary>
/// 전투 종료 방식. 턴제·액션 공통이며 보상이 여기서 갈린다 (F-2-6).
/// 불살 → 붉은 결정 · 인형화 -2 · 심리 +5 / 몰살 → 검은 구슬 · 인형화 +2.
/// </summary>
public enum BattleOutcome
{
    /// <summary>몰살. 적을 쓰러뜨렸다.</summary>
    Killed,
    /// <summary>불살. 정화가 성립했거나 적이 물러났다.</summary>
    Spared,
    /// <summary>도주. 보상 없음.</summary>
    Escaped,
    /// <summary>패배.</summary>
    Lost,
}
