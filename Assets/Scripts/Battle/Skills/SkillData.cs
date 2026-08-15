using UnityEngine;

/// <summary>
/// 스킬 데이터 (ScriptableObject). 에디터에서 자산으로 만들어 유닛에 할당.
/// </summary>
[CreateAssetMenu(fileName = "Skill", menuName = "Battle/Skill")]
public class SkillData : ScriptableObject
{
    [Header("기본")]
    [Tooltip("UI에 표시될 이름.")]
    public string displayName = "Skill";
    [Tooltip("툴팁 설명.")]
    [TextArea] public string description;

    [Header("자원/쿨다운")]
    [Tooltip("MP 소모량.")]
    public int mpCost = 5;
    [Tooltip("쿨다운 (턴 단위).")]
    public int cooldownTurns = 0;

    [Header("효과")]
    [Tooltip("DamageCalculator의 skillMultiplier로 전달. 1.5 = 기본 데미지 ×1.5.")]
    public float damageMultiplier = 1.0f;
    [Tooltip("회복형 스킬일 경우 회복량 (양수). 0이면 회복 효과 없음.")]
    public int   healAmount = 0;
    [Tooltip("시전 시 플레이어에게 적용할 버프/디버프 목록 (BuffManager). duration은 초 단위 — 턴제에서는 1턴 ≈ 2초.")]
    public System.Collections.Generic.List<BuffInfo> buffs = new System.Collections.Generic.List<BuffInfo>();
    [Tooltip("공감 게이지 증가량 (평화 루트 스킬). 0이면 효과 없음. 기본 교감(특수 행동)은 +20.")]
    public int   empathyGain = 0;

    [Header("타깃")]
    public SkillTargetType targetType = SkillTargetType.SingleEnemy;

    [Header("연출")]
    [Tooltip("애니메이터 트리거 키 (선택).")]
    public string animationKey;
}

public enum SkillTargetType { Self, SingleEnemy, AllEnemies, SingleAlly, AllAllies }
