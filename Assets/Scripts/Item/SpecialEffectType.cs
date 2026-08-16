/// <summary>
/// 아이템 사용 시 발동하는 특수 효과 종류.
/// Unity 인스펙터에서 드롭다운으로 표시됩니다.
///
/// 새 효과 추가 방법:
///   1. 이 enum에 값 추가
///   2. ItemEffectHandler.HandleEffect()의 switch에 case 추가
/// </summary>
public enum SpecialEffectType
{
    None = 0,
    Blur,           // 화면 블러 오버레이 효과
    CaffeineStack,  // 카페인 스택 누적 → 3잔 달성 시 각성 + 중독 효과 동시 발동
    FullHeal,       // HP 전량 회복 (F-2-6 각설탕). 수치를 에셋에 박으면 최대 HP 변경 시 어긋난다
    // 새 효과 추가: 이 enum에 값 추가 → ItemEffectHandler.HandleEffect()에 case 추가
}
