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
/// </summary>
public class BuffManager : MonoBehaviour
{
    public static BuffManager Instance { get; private set; }

    private const float TICK_INTERVAL = 1f; // DoT/HoT 틱 간격 (초)

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

    private readonly List<ActiveBuff> _activeBuffs = new List<ActiveBuff>();
    private float _tickTimer = 0f;

    // ─────────────────────────────────────────────
    //  Unity 생명주기
    // ─────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        _tickTimer += dt;
        bool doTick = _tickTimer >= TICK_INTERVAL;
        if (doTick) _tickTimer -= TICK_INTERVAL;

        for (int i = _activeBuffs.Count - 1; i >= 0; i--)
        {
            ActiveBuff buff = _activeBuffs[i];
            buff.remainingDuration -= dt;

            if (doTick)
            {
                if (buff.type == BuffType.DamageOverTime)
                    PlayerStats.Instance?.TakeDamage(buff.value);
                else if (buff.type == BuffType.HealOverTime)
                    PlayerStats.Instance?.RecoverHealth(buff.value);
            }

            if (buff.remainingDuration <= 0f)
                _activeBuffs.RemoveAt(i);
        }
    }

    // ─────────────────────────────────────────────
    //  버프 추가
    // ─────────────────────────────────────────────

    /// <summary>버프 하나를 추가합니다. 동일 타입이 이미 있으면 duration과 value를 갱신합니다.</summary>
    public void AddBuff(BuffInfo info)
    {
        if (info.type == BuffType.None || info.duration <= 0f) return;

        foreach (ActiveBuff existing in _activeBuffs)
        {
            if (existing.type == info.type)
            {
                existing.remainingDuration = Mathf.Max(existing.remainingDuration, info.duration);
                existing.value             = Mathf.Max(existing.value, info.value);
                return;
            }
        }
        _activeBuffs.Add(new ActiveBuff(info));
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
    public bool HasBuff(BuffType type)
    {
        foreach (ActiveBuff b in _activeBuffs)
            if (b.type == type) return true;
        return false;
    }

    /// <summary>해당 타입의 버프 수치를 반환합니다. 없으면 0.</summary>
    public float GetBuffValue(BuffType type)
    {
        foreach (ActiveBuff b in _activeBuffs)
            if (b.type == type) return b.value;
        return 0f;
    }

    /// <summary>해당 타입의 버프 남은 지속시간(초)을 반환합니다. 없으면 0.</summary>
    public float GetRemainingDuration(BuffType type)
    {
        foreach (ActiveBuff b in _activeBuffs)
            if (b.type == type) return b.remainingDuration;
        return 0f;
    }

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

    // ─────────────────────────────────────────────
    //  초기화
    // ─────────────────────────────────────────────

    /// <summary>모든 활성 버프를 제거합니다.</summary>
    public void ClearAll()
    {
        _activeBuffs.Clear();
        _tickTimer = 0f;
    }
}
