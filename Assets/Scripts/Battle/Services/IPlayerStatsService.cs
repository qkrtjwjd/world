/// <summary>
/// 배틀 시스템이 PlayerStats에 접근하는 추상 인터페이스.
/// 구체 구현(<see cref="PlayerStats"/>)을 직접 참조하지 않게 하여 테스트/모킹을 가능하게 합니다.
/// </summary>
public interface IPlayerStatsService
{
    float MaxHealth     { get; set; }
    float CurrentHealth { get; set; }

    void TakeDamage(float amount);
    void RecoverHealth(float amount);

    void AddTrauma(float amount);
    void RecoverMental(float amount);

    void AddPuppetization(float amount);
    void ReducePuppetization(float amount);
    void AddPuppetizationOnKill(float multiplier = 1f);
}
