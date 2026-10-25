using UnityEngine;
using UnityEngine.SceneManagement;

public enum ItemCategory { All = 0, Food = 1, Tool = 2, Weapon = 3 }
public enum ItemGrade    { Normal = 0, Rare = 1, Hero = 2, Legend = 3 }

/// <summary>
/// 장착 슬롯 종류 (E-38: 의상 1 + 무기 2).
/// <see cref="ItemCategory"/> 와 별개 축이다 — 카테고리는 아이템창 필터용이고,
/// 이쪽은 장착 가능 여부만 정한다. 기존 에셋은 이 값이 없으므로 기본값 None 이 된다.
/// </summary>
public enum EquipSlotType { None = 0, Clothing = 1, Weapon = 2 }

[CreateAssetMenu(fileName = "New Item", menuName = "Item Data")]
public class ItemData : ScriptableObject
{
    [Tooltip("기본 아이템 이름 (번역 키가 없거나 실패했을 때 표시됨)")]
    public string itemName;

    [Tooltip("다국어 번역을 위한 키 (예: items.apple). 비워두면 위 이름을 그대로 씁니다.")]
    public string localizationKey;

    [Tooltip("먹였을 때 오르는 수치(경험치)입니다.")]
    public int feedValue;

    [Tooltip("체크하면 정체불명의 존재에게 먹일 수 있는 아이템입니다.")]
    public bool canFeed;

    [Tooltip("현실(F키로 볼 때)의 아이콘 겸 기본 폴백 아이콘입니다.")]
    public Sprite itemIcon;

    [Header("■ 환상 아이콘")]
    [Tooltip("환상(MapScene 등) 씬에서 표시될 아이콘. 비워두면 itemIcon을 사용합니다.")]
    public Sprite fantasyIcon;

    [Header("■ 환상 설명")]
    [Tooltip("환상(MapScene 등) 씬에서 표시될 아이템 설명 텍스트")]
    [TextArea(2, 4)]
    public string description;

    [Header("■ 현실 설명")]
    [Tooltip("현실(F키로 볼 때) 표시될 설명. 비워두면 위 설명(description)과 동일하게 표시됩니다.")]
    [TextArea(2, 4)]
    public string realityDescription;

    [Header("■ 카테고리 / 등급 / 인용구")]
    public ItemCategory category;
    public ItemGrade    grade;
    [Tooltip("아이템 하단에 표시되는 짧은 플레이버 텍스트")]
    [TextArea(1, 2)] public string quote;

    [Tooltip("체크하면 고급 아이템 취급을 받습니다. 연속으로 먹이면 급체합니다.")]
    public bool isHighGrade;

    [Header("■ 장착")]
    [Tooltip("장착 슬롯 종류. None 이면 장착할 수 없습니다. 카테고리와는 별개 축입니다.")]
    public EquipSlotType equipSlot;

    [Header("■ 데모 규칙 (C-17)")]
    [Tooltip("마시멜로 섭취로 친다 — 아이템 창에서 먹으면 인형화 +2 · 환상 100 · 환상 고정 60초 · 섭취 제한(C-17 · MarshmallowRules).\n" +
             "마시멜로 · 마시멜로를 띄운 핫코코. 이 경로에서는 아래 fantasyEffect/realityEffect 의 인형화 값을 쓰지 않는다.")]
    public bool countsAsMarshmallow;

    [Tooltip("탐험 중에 여러 번 쓸 수 있다(물 · 핫코코 가루처럼 나눠 먹는 음식 — 아이템 정리 4-1).\n" +
             "끄면 ItemUseTracker 의 「비전투 1회」 제한을 받는다.")]
    public bool allowRepeatUse;

    [Tooltip("남은 횟수가 들어가는 설명. {n} 자리에 소지 수가 들어간다(「3번에 나누어 먹을 수 있을거 같다」 → 쓴 만큼 줄어든다).\n" +
             "비우면 description 을 그대로 쓴다. 환상/현실 구분 없이 같은 문장이다.")]
    [TextArea(2, 4)] public string countedDescription;

    [Tooltip("착용 중인 물건 — 아이템 창에 「착용 중」으로 표시한다. 벗을 수 없다(유의 코트 · E-75-4).")]
    public bool wornFixed;

    [Header("■ 버리기 제한")]
    [Tooltip("체크하면 이 아이템은 버릴 수 없습니다.")]
    public bool isUndroppable;

    [Tooltip("버리려 할 때 표시될 대사 (비워두면 기본 메시지 사용)")]
    [TextArea(1, 3)] public string undiscardableDialogue_ko;
    [TextArea(1, 3)] public string undiscardableDialogue_en;
    [TextArea(1, 3)] public string undiscardableDialogue_jp;

    [Header("■ 사용 대사")]
    [Tooltip("아이템 사용 시 루가 하는 독백 (비워두면 대사 없음)")]
    [TextArea(1, 3)] public string useDialogue_ko;
    [TextArea(1, 3)] public string useDialogue_en;
    [TextArea(1, 3)] public string useDialogue_jp;

    [Header("■ 반복 사용 효과 (전투 중 임계값 이상 사용 시)")]
    [Tooltip("반복 사용 임계값 도달 시 표시할 대사 (비워두면 대사 없음)")]
    [TextArea(1, 3)] public string repeatUseDialogue_ko;
    [TextArea(1, 3)] public string repeatUseDialogue_en;
    [TextArea(1, 3)] public string repeatUseDialogue_jp;

    [Tooltip("반복 사용 임계값 도달 시 발동하는 시각 효과")]
    public SpecialEffectType repeatUseEffect;

    [Tooltip("반복 사용 임계값 도달 시 적용할 체력/멘탈/인형화 수치 및 버프")]
    public ItemEffect repeatItemEffect;

    [Header("■ [현실]에서의 효과")]
    public ItemEffect realityEffect;

    [Header("■ [환상(이면세계)]에서의 효과")]
    public ItemEffect fantasyEffect;

    /// <summary>현재 씬에 맞는 설명을 반환합니다. 현실 씬이고 realityDescription이 있으면 realityDescription, 아니면 description.</summary>
    public string CurrentDescription
    {
        get
        {
            bool isReality = DaggerFilterController.IsRealityView;
            if (isReality && !string.IsNullOrEmpty(realityDescription))
                return realityDescription;
            return description;
        }
    }

    /// <summary>소지 수 <paramref name="count"/> 를 넣은 설명. <see cref="countedDescription"/> 이 비어 있으면 <see cref="CurrentDescription"/>.</summary>
    public string DescriptionFor(int count)
    {
        if (string.IsNullOrEmpty(countedDescription)) return CurrentDescription;
        return countedDescription.Replace("{n}", count.ToString());
    }

    /// <summary>현재 씬에 맞는 아이콘을 반환합니다. 환상 씬이고 fantasyIcon이 있으면 fantasyIcon, 아니면 itemIcon.</summary>
    public Sprite CurrentIcon
    {
        get
        {
            bool isReality = DaggerFilterController.IsRealityView;
            if (!isReality && fantasyIcon != null) return fantasyIcon;
            return itemIcon;
        }
    }

    /// <summary>현재 언어에 맞는 사용 대사를 반환합니다.</summary>
    public string LocalizedUseDialogue =>
        LocalizationHelper.Get(useDialogue_ko, useDialogue_en, useDialogue_jp);

    /// <summary>현재 언어에 맞는 버리기 대사를 반환합니다.</summary>
    public string LocalizedUndiscardableDialogue =>
        LocalizationHelper.Get(undiscardableDialogue_ko, undiscardableDialogue_en, undiscardableDialogue_jp);

    /// <summary>현재 언어에 맞는 반복 사용 대사를 반환합니다.</summary>
    public string LocalizedRepeatUseDialogue =>
        LocalizationHelper.Get(repeatUseDialogue_ko, repeatUseDialogue_en, repeatUseDialogue_jp);

    // 번역된 이름을 가져오는 편의 속성 (프로퍼티)
    public string DisplayName
    {
        get
        {
            if (LocalizationManager.Instance != null && !string.IsNullOrEmpty(localizationKey))
            {
                string key = localizationKey;
                if (!key.Contains(".")) key = "items." + key;

                string localized = LocalizationManager.Instance.GetText(key);
                if (localized == key) return itemName;
                return localized;
            }
            return itemName;
        }
    }
}

// 효과를 묶어서 관리하기 위한 구조체
[System.Serializable]
public struct ItemEffect
{
    [Tooltip("체력 변화량 (+:회복, -:피해)")]
    public float healthChange;

    [Tooltip("멘탈 변화량 (+:회복, -:피해)")]
    public float mentalChange;

    [Tooltip("플레이어 인형화 수치 변화량 (+: 상승, -: 감소)")]
    public float puppetizationChange;

    [Tooltip("공감 게이지 변화량 (+: 증가)")]
    public float empathyChange;

    [Tooltip("적용할 버프/디버프 목록")]
    public System.Collections.Generic.List<BuffInfo> buffs;

    [Tooltip("사용 시 발동하는 특수 효과 (드롭다운으로 선택)")]
    public SpecialEffectType specialEffectCode;
}

// 버프 정보를 담는 구조체
[System.Serializable]
public struct BuffInfo
{
    public BuffType type;
    [Tooltip("수치 (예: 공격력 10 증가면 10, 쿨타임 20% 감소면 20 등)")]
    public float value;
    [Tooltip("지속 시간 (초 단위)")]
    public float duration;
}

// 버프 종류 열거형 (나중에 추가/삭제 용이함)
public enum BuffType
{
    None = 0,

    // --- [버프] ---
    AttackUp,           // 공격력 증가
    DefenseUp,          // 방어력 증가
    SpeedUp,            // 속도 증가
    CritChanceUp,       // 크리티컬 확률 증가
    HealOverTime,       // 지속 치유 (도트 힐)
    Shield,             // 보호막 생성
    Immunity,           // 피해 면역
    DebuffImmunity,     // 디버프 면역
    CooldownReduction,  // 쿨타임 감소
    ReflectDamage,      // 피해 반사
    Taunt,              // 도발 (어그로)

    // --- [디버프 (버프의 반대)] ---
    AttackDown,         // 공격력 감소
    DefenseDown,        // 방어력 감소
    SpeedDown,          // 속도 감소 (슬로우)
    CritChanceDown,     // 크리티컬 확률 감소
    DamageOverTime,     // 지속 피해 (심장 두근거림, 독 등)
    ShieldBreak,        // 보호막 파괴 (또는 받는 피해 증가)
    Vulnerable,         // 받는 피해 증가 (취약)
    CooldownIncrease,   // 쿨타임 증가
    Confusion,          // 혼란 (손 떨림 등 조작 방해)
    Stun                // 기절 (행동 불가)
}
