using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HP 바를 부드럽게 애니메이션합니다.
/// - 피격 시 텍스트는 즉시 업데이트
/// - Slider는 Lerp로 부드럽게 감소
/// </summary>
public class SmoothHPBar : MonoBehaviour
{
    [Header("UI 연결")]
    public Slider   hpSlider;
    public Text     hpText;       // "HP 00000" 형식
    public Text     lvText;       // "LV 00" 형식

    [Header("애니메이션")]
    [Tooltip("HP 바 감소 속도 (높을수록 빠름)")]
    public float lerpSpeed = 5f;

    private float _targetValue;
    private float _maxHP;
    private Coroutine _lerpCoroutine;

    public void Init(float maxHP, float currentHP, int level = 1)
    {
        _maxHP        = maxHP;
        _targetValue  = currentHP / maxHP;

        if (hpSlider != null)
        {
            hpSlider.minValue = 0f;
            hpSlider.maxValue = 1f;
            hpSlider.value    = _targetValue;
        }

        UpdateText(currentHP, level);
    }

    /// <summary>피격 시 호출. 텍스트 즉시 업데이트, 슬라이더는 부드럽게.</summary>
    public void SetHP(float currentHP, int level = 1)
    {
        _targetValue = Mathf.Clamp01(currentHP / _maxHP);
        UpdateText(currentHP, level);

        if (_lerpCoroutine != null) StopCoroutine(_lerpCoroutine);
        _lerpCoroutine = StartCoroutine(LerpSlider());
    }

    void UpdateText(float currentHP, int level)
    {
        if (hpText != null)
            hpText.text = $"HP {Mathf.CeilToInt(currentHP):D5}";
        if (lvText != null)
            lvText.text = $"LV {level:D2}";
    }

    IEnumerator LerpSlider()
    {
        if (hpSlider == null) yield break;

        while (!Mathf.Approximately(hpSlider.value, _targetValue))
        {
            hpSlider.value = Mathf.Lerp(hpSlider.value, _targetValue,
                lerpSpeed * Time.unscaledDeltaTime);

            if (Mathf.Abs(hpSlider.value - _targetValue) < 0.001f)
            {
                hpSlider.value = _targetValue;
                break;
            }
            yield return null;
        }
    }
}
