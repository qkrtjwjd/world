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

    void Update()
    {
        // 실시간 업데이트 (패널이 켜져 있을 때 매 프레임 혹은 이벤트 기반 갱신)
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