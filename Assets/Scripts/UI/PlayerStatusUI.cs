using UnityEngine;
using UnityEngine.UI; // Legacy Text 사용

public class PlayerStatusUI : MonoBehaviour
{
    [Header("■ 연결할 슬라이더 (UI)")]
    public Slider hpSlider;         // 체력바
    public Slider mentalSlider;     // 정신력바
    public Slider hungerSlider;     // 배고픔바

    [Header("■ 연결할 텍스트 (Legacy Text)")]
    public Text hpText;
    public Text mentalText;
    public Text hungerText;

    // 플레이어 스탯 스크립트 (나중에 만들어서 연결해야 함)
    // private PlayerStats playerStats; 

    public static PlayerStatusUI Instance;

    void Awake()
    {
        Instance = this;
    }

    // 최적화: 이전 값을 기억해서 변했을 때만 텍스트 갱신
    private float _lastHP = -1f;
    private float _lastMental = -1f;
    private float _lastHunger = -1f;

    // 외부에서 값을 업데이트할 때 부르는 함수들
    public void UpdateHP(float current, float max)
    {
        if (hpSlider != null)
        {
            hpSlider.maxValue = max;
            hpSlider.value = current;
        }

        // 값이 변했을 때만 텍스트 갱신 (GC Alloc 최소화)
        if (hpText != null && Mathf.Abs(_lastHP - current) > 0.1f)
        {
            hpText.text = $"{current:F0} / {max:F0}";
            _lastHP = current;
        }
    }

    public void UpdateMental(float current, float max)
    {
        if (mentalSlider != null)
        {
            mentalSlider.maxValue = max;
            mentalSlider.value = current;
        }

        if (mentalText != null && Mathf.Abs(_lastMental - current) > 0.1f)
        {
            mentalText.text = $"{current:F0} / {max:F0}";
            _lastMental = current;
        }
    }

    public void UpdateHunger(float current, float max)
    {
        if (hungerSlider != null)
        {
            hungerSlider.maxValue = max;
            hungerSlider.value = current;
        }

        if (hungerText != null && Mathf.Abs(_lastHunger - current) > 0.1f)
        {
            hungerText.text = $"{current:F0} / {max:F0}";
            _lastHunger = current;
        }
    }
}
