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

    [Tooltip("현실(DarkReality) 씬 아이콘 겸 기본 폴백 아이콘입니다.")]
    public Sprite itemIcon;

    [Header("■ 환상 아이콘")]
    [Tooltip("환상(MapScene 등) 씬에서 표시될 아이콘. 비워두면 itemIcon을 사용합니다.")]
    public Sprite fantasyIcon;

    [Header("■ 환상 설명")]
    [Tooltip("환상(MapScene 등) 씬에서 표시될 아이템 설명 텍스트")]
    [TextArea(2, 4)]
    public string description;

    [Header("■ 현실 설명")]
    [Tooltip("현실(DarkReality) 씬에서 표시될 설명. 비워두면 위 설명(description)과 동일하게 표시됩니다.")]
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
            bool isReality = SceneNames.IsRealityScene(SceneManager.GetActiveScene().name);
            if (isReality && !string.IsNullOrEmpty(realityDescription))
                return realityDescription;
            return description;
        }
    }

    /// <summary>현재 씬에 맞는 아이콘을 반환합니다. 환상 씬이고 fantasyIcon이 있으면 fantasyIcon, 아니면 itemIcon.</summary>
    public Sprite CurrentIcon
    {
        get
        {
            bool isReality = SceneNames.IsRealityScene(SceneManager.GetActiveScene().name);
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
