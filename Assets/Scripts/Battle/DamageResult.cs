/// <summary>
/// 데미지 계산 결과. amount = 0 + isMiss = true 면 회피된 공격을 의미합니다.
/// </summary>
public struct DamageResult
{
    public int  amount;
    public bool isCrit;
    public bool isMiss;

    public static DamageResult Miss => new DamageResult { amount = 0, isCrit = false, isMiss = true };

    public static DamageResult Hit(int amount, bool isCrit = false) =>
        new DamageResult { amount = amount, isCrit = isCrit, isMiss = false };
}
