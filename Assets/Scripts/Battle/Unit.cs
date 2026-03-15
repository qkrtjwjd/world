using UnityEngine;
using UnityEngine.UI;

public class Unit : MonoBehaviour
{
    public string unitName;
    public int    unitLevel;
    public int    damage;
    public int    maxHP;
    public int    currentHP;

    [Header("HP 슬라이더 (인스펙터 또는 BattleSystem 에서 주입)")]
    public Slider hpSlider;

    public bool isDefending = false;

    /// <summary>
    /// 슬라이더에 초기 HP 값을 세팅합니다.
    /// BattleSystem.SetupBattle() 에서 유닛 생성 직후 반드시 호출하세요.
    /// </summary>
    public void SetHUD()
    {
        if (hpSlider == null)
            hpSlider = GetComponentInChildren<Slider>();

        if (hpSlider != null)
        {
            hpSlider.minValue = 0;
            hpSlider.maxValue = maxHP;
            hpSlider.value    = currentHP;
        }
    }

    public void ResetState()
    {
        isDefending = false;
    }

    /// <returns>사망 여부</returns>
    public bool TakeDamage(int dmg)
    {
        if (isDefending)
            dmg = Mathf.RoundToInt(dmg * 0.8f);

        currentHP = Mathf.Max(0, currentHP - dmg);
        RefreshSlider();
        return currentHP <= 0;
    }

    public void Heal(int amount)
    {
        currentHP = Mathf.Min(maxHP, currentHP + amount);
        RefreshSlider();
    }

    private void RefreshSlider()
    {
        if (hpSlider != null)
            hpSlider.value = currentHP;
    }
}