using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 일식(Eclipse) 테마 환상/현실 게이지 슬라이더.
/// EclipseGauge 셰이더 한 장이 황금 그라디언트 + 검정 침식 + 코로나 + 테두리를 모두 처리.
/// 게이지값은 _GaugeValue(0=환상/1=현실), 인형화는 _CorruptionLevel 로 매 변경 시 전달.
/// </summary>
public class GaugeSliderUI : MonoBehaviour
{
    [Header("UI 연결")]
    public Slider slider;
    [Tooltip("EclipseGauge 셰이더를 적용할 Image. 비워두면 Slider > Background 자동 탐색.")]
    public Image  eclipseImage;

    [Header("Eclipse 머티리얼")]
    [Tooltip("EclipseGauge 셰이더 머티리얼. 비워두면 셰이더에서 자동 생성.")]
    public Material eclipseMaterial;

    [Header("Glow 이미지 (슬라이더 뒤, 슬라이더보다 위아래로 크게)")]
    [Tooltip("슬라이더 뒤에 배치한 SliderGlow Image. 비워두면 Glow 비활성.")]
    public Image    glowImage;
    [Tooltip("SliderGlow 셰이더 머티리얼. 비워두면 셰이더에서 자동 생성.")]
    public Material glowMaterial;

    [Header("페이드 대상")]
    [Tooltip("슬라이더+레이블을 감싸는 루트 CanvasGroup")]
    public CanvasGroup gaugeRootGroup;
    [Tooltip("루트 없을 때 fallback")]
    public CanvasGroup sliderCanvasGroup;

    private Material  _eclipseMat;
    private Material  _glowMat;

    static readonly int PropGaugeValue      = Shader.PropertyToID("_GaugeValue");
    static readonly int PropCorruptionLevel = Shader.PropertyToID("_CorruptionLevel");

    // ─────────────────────────────────────────────
    //  초기화
    // ─────────────────────────────────────────────
    void Start()
    {
        if (slider == null)
        {
            Debug.LogError("[GaugeSliderUI] Slider가 연결되지 않았습니다.", this);
            enabled = false;
            return;
        }

        if (GaugeManager.Instance == null)
        {
            Debug.LogError("[GaugeSliderUI] GaugeManager를 찾을 수 없습니다.", this);
            enabled = false;
            return;
        }

        slider.interactable = false;

        if (sliderCanvasGroup == null)
            sliderCanvasGroup = slider.GetComponent<CanvasGroup>();

        // ── Eclipse 이미지/머티리얼 설정 ────────────────────────────────────
        if (eclipseImage == null)
            TryFindEclipseImage();

        if (eclipseMaterial == null)
        {
            var shader = Shader.Find("Custom/EclipseGauge");
            if (shader != null)
                eclipseMaterial = new Material(shader);
            else
                Debug.LogWarning("[GaugeSliderUI] Custom/EclipseGauge 셰이더를 찾을 수 없습니다.", this);
        }

        if (eclipseImage != null && eclipseMaterial != null)
        {
            _eclipseMat           = new Material(eclipseMaterial);
            eclipseImage.material = _eclipseMat;
            eclipseImage.color    = Color.white;
        }
        else
        {
            Debug.LogWarning("[GaugeSliderUI] eclipseImage 또는 eclipseMaterial이 준비되지 않았습니다.", this);
        }

        // ── Glow 이미지/머티리얼 설정 ────────────────────────────────────────
        if (glowImage != null)
        {
            glowImage.sprite = null;

            if (glowMaterial == null)
            {
                var glowShader = Shader.Find("Custom/SliderGlow");
                if (glowShader != null)
                    glowMaterial = new Material(glowShader);
                else
                    Debug.LogWarning("[GaugeSliderUI] Custom/SliderGlow 셰이더를 찾을 수 없습니다.", this);
            }

            if (glowMaterial != null)
            {
                _glowMat          = new Material(glowMaterial);
                glowImage.material = _glowMat;
                glowImage.color    = Color.white;
            }
        }

        HideSliderFill();

        // ── 이벤트 구독 ──────────────────────────────────────────────────────
        GaugeManager.Instance.OnGaugeChanged      += OnGaugeChanged;
        GaugeManager.Instance.OnVisibilityChanged += OnVisibilityChanged;

        ApplyGauge(GaugeManager.Instance.fantasyRealityGauge);
        SetAlpha(GaugeManager.Instance.isGaugeVisible ? 1f : 0f);

        if (CorruptionManager.Instance != null)
            CorruptionManager.Instance.OnCorruptionChanged += OnDollificationChanged;
    }

    void OnDestroy()
    {
        if (_eclipseMat != null) { Destroy(_eclipseMat); _eclipseMat = null; }
        if (_glowMat    != null) { Destroy(_glowMat);    _glowMat    = null; }

        if (GaugeManager.Instance != null)
        {
            GaugeManager.Instance.OnGaugeChanged      -= OnGaugeChanged;
            GaugeManager.Instance.OnVisibilityChanged -= OnVisibilityChanged;
        }

        if (CorruptionManager.Instance != null)
            CorruptionManager.Instance.OnCorruptionChanged -= OnDollificationChanged;
    }

    // ─────────────────────────────────────────────
    //  이벤트 핸들러
    // ─────────────────────────────────────────────
    void OnGaugeChanged(float gauge) => ApplyGauge(gauge);

    void OnVisibilityChanged(bool visible)
    {
        SetAlpha(visible ? 1f : 0f);
        if (visible)
            ApplyGauge(GaugeManager.Instance.fantasyRealityGauge);
    }

    void SetAlpha(float alpha)
    {
        if (gaugeRootGroup != null)
            gaugeRootGroup.alpha = alpha;
        else if (sliderCanvasGroup != null)
            sliderCanvasGroup.alpha = alpha;
    }

    // ─────────────────────────────────────────────
    //  게이지 적용 (_GaugeValue: 0=환상, 1=현실)
    // ─────────────────────────────────────────────
    void ApplyGauge(float gauge)
    {
        float newVal = gauge / 100f;

        if (_eclipseMat == null) return;

        _eclipseMat.SetFloat(PropGaugeValue, newVal);
        _glowMat?.SetFloat(PropGaugeValue, newVal);
    }

    // ─────────────────────────────────────────────
    //  자동 탐색
    // ─────────────────────────────────────────────
    void TryFindEclipseImage()
    {
        // 기존 Background 이미지를 Eclipse 용도로 재사용
        var bg = slider.transform.Find("Background");
        if (bg != null) eclipseImage = bg.GetComponent<Image>();

        // fallback: 첫 번째 자식 Image
        if (eclipseImage == null)
            eclipseImage = slider.GetComponentInChildren<Image>(true);

        if (eclipseImage == null)
            Debug.LogWarning("[GaugeSliderUI] eclipseImage를 자동 탐색하지 못했습니다. " +
                             "Inspector에서 직접 연결해주세요.", this);
    }

    void HideSliderFill()
    {
        // Fill Area 비활성화
        var fillArea = slider.transform.Find("Fill Area");
        if (fillArea != null) fillArea.gameObject.SetActive(false);

        // fillRect 이미지 비활성화 (Safety)
        if (slider.fillRect != null)
        {
            var fillImg = slider.fillRect.GetComponent<Image>();
            if (fillImg != null) fillImg.enabled = false;
        }
    }

    // ─────────────────────────────────────────────
    //  인형화 연동
    // ─────────────────────────────────────────────
    void OnDollificationChanged(float delta)
    {
        if (GaugeManager.Instance == null) return;

        float corruption = GaugeManager.Instance.dollificationGauge;

        if (GaugeManager.Instance.isGaugeVisible)
            SetAlpha(corruption >= 81f ? 0.4f : 1f);

        if (_eclipseMat != null)
            _eclipseMat.SetFloat(PropCorruptionLevel, corruption);
        _glowMat?.SetFloat(PropCorruptionLevel, corruption);
    }

}
