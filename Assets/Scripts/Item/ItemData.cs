using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "Item Data")]
public class ItemData : ScriptableObject
{
    [Tooltip("기본 아이템 이름 (번역 키가 없거나 실패했을 때 표시됨)")]
    public string itemName;      

    [Tooltip("다국어 번역을 위한 키 (예: items.apple). 비워두면 위 이름을 그대로 씁니다.")]
    public string localizationKey;

    [Tooltip("먹였을 때 오르는 수치(경험치)입니다.")]
    public int feedValue;        

    [Tooltip("인벤토리에 표시될 아이콘입니다.")]
    public Sprite itemIcon;      

    [Tooltip("체크하면 고급 아이템 취급을 받습니다. 연속으로 먹이면 급체합니다.")]
    public bool isHighGrade;

    [Header("■ 음식/소모품 설정")]
    [Tooltip("이 아이템이 섭취 가능한 음식인가요?")]
    public bool isConsumable = false;

    [Tooltip("기본 포만감 수치 (배고픔 게이지 회복량)")]
    public float satiety = 0f;

    [Space(10)]
    [Header("■ [현실]에서의 효과")]
    public ItemEffect realityEffect;

    [Header("■ [환상(이면세계)]에서의 효과")]
    public ItemEffect fantasyEffect;

    // 번역된 이름을 가져오는 편의 속성 (프로퍼티)
    public string DisplayName
    {
        get
        {
            // LocalizationManager가 있고, 키가 설정되어 있다면 번역된 이름 반환
            if (LocalizationManager.Instance != null && !string.IsNullOrEmpty(localizationKey))
            {
                // "items." 접두사가 없으면 자동으로 붙여줄 수도 있음 (선택사항)
                string key = localizationKey;
                if (!key.Contains(".")) key = "items." + key;

                string localized = LocalizationManager.Instance.GetText(key);
                
                // 만약 번역을 못 찾아서 키가 그대로 나왔다면? -> 기본 이름 반환
                if (localized == key) return itemName;
                
                return localized;
            }
            // 아니면 그냥 기본 이름 반환
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

    [Tooltip("오염 수치 (먹이는 대상에게 누적되는 수치)")]
    public float pollutionAdded;

    [Tooltip("적용할 버프/디버프 목록")]
    public System.Collections.Generic.List<BuffInfo> buffs;

    [Tooltip("추가 효과 설명 (예: '특수 스크립트 실행')")]
    public string specialEffectCode;
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
    DamageOverTime,     // 지속 피해 (독, 출혈)
    ShieldBreak,        // 보호막 파괴 (또는 받는 피해 증가)
    Vulnerable,         // 받음 피해 증가 (취약)
    CooldownIncrease,   // 쿨타임 증가
    Confusion,          // 혼란 (반대 키 입력 등 - 도발의 역개념으로 넣음)
    Stun                // 기절 (행동 불가)
}