using UnityEngine;
using UnityEngine.UI;

public class StaticUIManager : MonoBehaviour
{
    public static StaticUIManager Instance;

    [Header("HP Sliders (Main Menu Panel)")]
    public Slider playerHPSlider;
    public Slider allyHPSlider;

    void Awake()
    {
        if (Instance == null) 
        {
            Instance = this;
        }
        else 
        {
            Destroy(gameObject);
        }
    }

    void OnEnable()
    {
        UpdateHealthBars();
    }

    public void UpdateHealthBars()
    {
        if (PlayerStats.Instance != null)
        {
            // 조건: Slider.value = currentHP / maxHP 공식 사용
            if (playerHPSlider != null && PlayerStats.Instance.maxHealth > 0)
            {
                playerHPSlider.value = PlayerStats.Instance.currentHealth / PlayerStats.Instance.maxHealth;
            }

            if (allyHPSlider != null && PlayerStats.Instance.allyMaxHP > 0)
            {
                allyHPSlider.value = PlayerStats.Instance.allyCurrentHP / PlayerStats.Instance.allyMaxHP;
            }
        }
    }
}