using UnityEngine;
using UnityEngine.UI;

public class Unit : MonoBehaviour
{
    public string unitName;
    public int unitLevel;
    public int damage;
    public int maxHP;
    public int currentHP;

    [Header("UI 연결")]
    public Slider hpSlider;

    public bool isDefending = false;

    public void SetHUD()
    {
        if (hpSlider != null)
        {
            hpSlider.maxValue = maxHP;
            hpSlider.value = currentHP;
        }
    }

    public void ResetState()
    {
        isDefending = false;
    }

    public bool TakeDamage(int dmg)
    {
        if (isDefending)
        {
            dmg = Mathf.RoundToInt(dmg * 0.8f); // 방어 시 데미지 20% 경감
        }

        currentHP -= dmg;
        if (currentHP < 0) currentHP = 0;

        if (hpSlider != null)
        {
            hpSlider.value = currentHP;
        }

        // 체력이 0 이하면 true(죽음) 반환
        if (currentHP <= 0)
            return true;
        else
            return false;
    }

    public void Heal(int amount)
    {
        currentHP += amount;
        if (currentHP > maxHP)
            currentHP = maxHP;

        if (hpSlider != null)
        {
            hpSlider.value = currentHP;
        }
    }
}