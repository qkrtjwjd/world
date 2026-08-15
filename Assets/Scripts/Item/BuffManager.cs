using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 현재 활성화된 버프/디버프를 추적하고 지속시간을 관리합니다.
///
/// [자동 처리]
///   - DamageOverTime: 1초마다 PlayerStats.TakeDamage() 적용
///   - HealOverTime:   1초마다 PlayerStats.RecoverHealth() 적용
///   - 지속시간 만료 시 자동 제거
///
/// [전투/이동 시스템 연동]
///   - AttackMultiplier, SpeedMultiplier, CritBonus 등 프로퍼티를 쿼리해서 사용하세요.
///   - 예) 공격 데미지 계산: dmg * BuffManager.Instance.AttackMultiplier
///   - 예) 이동속도 계산:    speed * BuffManager.Instance.SpeedMultiplier
///   - 예) 혼란 상태 확인:   BuffManager.Instance.IsConfused
///
/// [미구현]
///   - Taunt(도발): 1 vs 1 전투 구조상 어그로 대상 전환이 무의미 — 의도적으로 소비처 없음.
///   - CooldownReduction/Increase: 턴제 스킬 쿨다운(턴 단위)에는 적용하지 않음 — 액션 모드 공격 쿨타임 전용.
/// </summary>
public class BuffManager : MonoBehaviour
{
    public static BuffManager Instance { get; private set; }

    private const float TICK_INTERVAL = 1f; // DoT/HoT 틱 간격 (초)

    public event System.Action<BuffType> OnBuffAdded;
    public event System.Action<BuffType> OnBuffRemoved;

    private class ActiveBuff
    {
        public BuffType type;
        public float    value;
        public float    remainingDuration;

        public ActiveBuff(BuffInfo info)
        {
            type              = info.type;
            value             = info.value;
            remainingDuration = info.duration;
        }
    }

    private readonly Dictionary<BuffType, ActiveBuff> _activeBuffs = new Dictionary<BuffType, ActiveBuff>();
    private readonly List<BuffType> _toRemove = new List<BuffType>();
    private float _tickTimer = 0f;

    // ─────────────────────────────────────────────
    //  Unity 생명주기
    // ─────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnEnable()  { BattleEvents.OnItemUsed += OnBattleItemUsed; }
    void OnDisable() { BattleEvents.OnItemUsed -= OnBattleItemUsed; }

    /// <summary>BattleEvents.OnItemUsed 구독 핸들러 — 기존 AddBuffs 직접 호출을 대체.</summary>
    void OnBattleItemUsed(ItemData item, Unit user)
    {
        if (item == null) return;
        AddBuffs(item.fantasyEffect.buffs);
    }

    void Update()
    {
        if (_activeBuffs.Count == 0) return;

        float dt = Time.deltaTime;
        _tickTimer += dt;
        bool doTick = _tickTimer >= TICK_INTERVAL;
        if (doTick) _tickTimer -= TICK_INTERVAL;

        _toRemove.Clear();
        foreach (var kv in _activeBuffs)
        {
            ActiveBuff buff = kv.Value;
            buff.remainingDuration -= dt;

            if (doTick)
            {
                if (buff.type == BuffType.DamageOverTime)
                    PlayerStats.Instance?.TakeDamage(buff.value);
                else if (buff.type == BuffType.HealOverTime)
                    PlayerStats.Instance?.RecoverHealth(buff.value);
            }

            if (buff.remainingDuration <= 0f)
                _toRemove.Add(kv.Key);
        }
        foreach (BuffType key in _toRemove)
        {
            _activeBuffs.Remove(key);
            OnBuffRemoved?.Invoke(key);
        }
    }

    /// <summary>
    /// 턴제 전투용 틱. 전투 중에는 Time.timeScale=0 이라 Update()가 정지하므로,
    /// BattleSystem이 라운드마다(적 턴 시작 시) 호출해 지속시간 차감 + DoT/HoT를 1회 적용합니다.
    /// </summary>
    /// <param name="seconds">1라운드로 환산할 초. 기본 2초 (버프 duration은 초 단위).</param>
    public void TickTurn(float seconds = 2f)
    {
        if (_activeBuffs.Count == 0) return;

        _toRemove.Clear();
        foreach (var kv in _activeBuffs)
        {
            ActiveBuff buff = kv.Value;
            buff.remainingDuration -= seconds;

            if (buff.type == BuffType.DamageOverTime)
                PlayerStats.Instance?.TakeDamage(buff.value);
            else if (buff.type == BuffType.HealOverTime)
                PlayerStats.Instance?.RecoverHealth(buff.value);

            if (buff.remainingDuration <= 0f)
                _toRemove.Add(kv.Key);
        }
        foreach (BuffType key in _toRemove)
        {
            _activeBuffs.Remove(key);
            OnBuffRemoved?.Invoke(key);
        }
    }

    // ─────────────────────────────────────────────
    //  버프 추가
    // ─────────────────────────────────────────────

    /// <summary>디버프 계열 여부. DebuffImmunity 거부 판정에 사용.</summary>
    public static bool IsDebuffType(BuffType type)
    {
        switch (type)
        {
            case BuffType.AttackDown:
            case BuffType.DefenseDown:
            case BuffType.SpeedDown:
            case BuffType.CritChanceDown:
            case BuffType.DamageOverTime:
            case BuffType.ShieldBreak:
            case BuffType.Vulnerable:
            case BuffType.CooldownIncrease:
            case BuffType.Confusion:
            case BuffType.Stun:
                return true;
            default:
                return false;
        }
    }

    /// <summary>버프 하나를 추가합니다. 동일 타입이 이미 있으면 duration과 value를 갱신합니다.</summary>
    public void AddBuff(BuffInfo info)
    {
        if (info.type == BuffType.None || info.duration <= 0f) return;

        // 디버프 면역 중에는 디버프 계열 거부
        if (IsDebuffType(info.type) && HasBuff(BuffType.DebuffImmunity)) return;

        // ShieldBreak 는 즉발 효과 — 활성 Shield 를 즉시 제거
        if (info.type == BuffType.ShieldBreak)
        {
            RemoveBuff(BuffType.Shield);
            return;
        }

        if (_activeBuffs.TryGetValue(info.type, out ActiveBuff existing))
        {
            existing.remainingDuration = Mathf.Max(existing.remainingDuration, info.duration);
            existing.value             = Mathf.Max(existing.value, info.value);
            return;
        }
        _activeBuffs[info.type] = new ActiveBuff(info);
        OnBuffAdded?.Invoke(info.type);
    }

    /// <summary>버프 목록을 한 번에 추가합니다.</summary>
    public void AddBuffs(List<BuffInfo> buffs)
    {
        if (buffs == null) return;
        foreach (BuffInfo b in buffs) AddBuff(b);
    }

    // ─────────────────────────────────────────────
    //  버프 조회
    // ─────────────────────────────────────────────

    /// <summary>해당 타입의 버프가 활성화되어 있는지 확인합니다.</summary>
    public bool HasBuff(BuffType type) => _activeBuffs.ContainsKey(type);

    /// <summary>해당 타입의 버프 수치를 반환합니다. 없으면 0.</summary>
    public float GetBuffValue(BuffType type) =>
        _activeBuffs.TryGetValue(type, out ActiveBuff b) ? b.value : 0f;

    /// <summary>해당 타입의 버프 남은 지속시간(초)을 반환합니다. 없으면 0.</summary>
    public float GetRemainingDuration(BuffType type) =>
        _activeBuffs.TryGetValue(type, out ActiveBuff b) ? b.remainingDuration : 0f;

    // ─────────────────────────────────────────────
    //  편의 프로퍼티 (전투/이동 시스템에서 직접 사용)
    // ─────────────────────────────────────────────

    /// <summary>공격력 배율. 기본 1.0. AttackUp/AttackDown에 따라 변동.</summary>
    public float AttackMultiplier =>
        1f + GetBuffValue(BuffType.AttackUp)   / 100f
           - GetBuffValue(BuffType.AttackDown) / 100f;

    /// <summary>이동속도 배율. 기본 1.0. SpeedUp/SpeedDown에 따라 변동.</summary>
    public float SpeedMultiplier =>
        1f + GetBuffValue(BuffType.SpeedUp)   / 100f
           - GetBuffValue(BuffType.SpeedDown) / 100f;

    /// <summary>크리티컬 확률 보너스(%). CritChanceUp - CritChanceDown.</summary>
    public float CritBonus =>
        GetBuffValue(BuffType.CritChanceUp) - GetBuffValue(BuffType.CritChanceDown);

    /// <summary>쿨타임 감소 배율. 기본 1.0.</summary>
    public float CooldownMultiplier =>
        1f - GetBuffValue(BuffType.CooldownReduction) / 100f
           + GetBuffValue(BuffType.CooldownIncrease)  / 100f;

    /// <summary>기절 상태 여부.</summary>
    public bool IsStunned   => HasBuff(BuffType.Stun);

    /// <summary>혼란 상태 여부 (손 떨림 등 조작 방해).</summary>
    public bool IsConfused  => HasBuff(BuffType.Confusion);

    /// <summary>피해 면역 여부.</summary>
    public bool IsImmune    => HasBuff(BuffType.Immunity);

    /// <summary>방어력 배율. 기본 1.0. DefenseUp/DefenseDown에 따라 변동.</summary>
    public float DefenseMultiplier =>
        1f + GetBuffValue(BuffType.DefenseUp)   / 100f
           - GetBuffValue(BuffType.DefenseDown) / 100f;

    // ─────────────────────────────────────────────
    //  받는 피해 보정 (면역 / 보호막 / 취약)
    // ─────────────────────────────────────────────

    /// <summary>
    /// 플레이어가 받는 피해에 면역·보호막·취약을 순서대로 적용한 최종 피해를 반환합니다.
    /// 보호막은 value 를 흡수량 풀로 사용하며, 소진 시 자동 제거됩니다.
    /// 호출부: PlayerStats.TakeDamage(핵앤슬래시·DoT), BattleSystem.EnemyTurn(턴제).
    /// </summary>
    public float ModifyIncomingDamage(float amount)
    {
        if (amount <= 0f) return 0f;

        // 1) 면역
        if (HasBuff(BuffType.Immunity)) return 0f;

        // 2) 취약 (받는 피해 증가)
        if (_activeBuffs.TryGetValue(BuffType.Vulnerable, out ActiveBuff vul))
            amount *= 1f + vul.value / 100f;

        // 3) 보호막 흡수
        if (_activeBuffs.TryGetValue(BuffType.Shield, out ActiveBuff shield))
        {
            float absorbed = Mathf.Min(shield.value, amount);
            shield.value -= absorbed;
            amount       -= absorbed;
            if (shield.value <= 0f) RemoveBuff(BuffType.Shield);
        }

        return amount;
    }

    /// <summary>지정 타입의 버프를 즉시 제거합니다.</summary>
    public void RemoveBuff(BuffType type)
    {
        if (_activeBuffs.Remove(type))
            OnBuffRemoved?.Invoke(type);
    }

    // ─────────────────────────────────────────────
    //  초기화
    // ─────────────────────────────────────────────

    /// <summary>모든 활성 버프를 제거합니다.</summary>
    public void ClearAll()
    {
        foreach (BuffType key in _activeBuffs.Keys)
            OnBuffRemoved?.Invoke(key);
        _activeBuffs.Clear();
        _tickTimer = 0f;
    }
}
