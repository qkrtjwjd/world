using System.Collections;
using UnityEngine;

/// <summary>
/// 적 스프라이트에 "환상 속 이질감" 효과를 적용합니다.
/// - 랜덤 간격으로 알파 깜빡임(flicker)
/// - 랜덤 간격으로 위치 흔들림(shake)
/// - 크로마틱 어베레이션 느낌: 보조 스프라이트를 오프셋으로 표시
/// </summary>
public class EnemyGlitchEffect : MonoBehaviour
{
    [Header("깜빡임 설정")]
    [Tooltip("깜빡임 발동 최소 간격 (초)")]
    public float flickerIntervalMin = 1.5f;
    [Tooltip("깜빡임 발동 최대 간격 (초)")]
    public float flickerIntervalMax = 4f;
    [Tooltip("깜빡임 지속 시간 (초)")]
    public float flickerDuration = 0.15f;
    [Tooltip("깜빡임 중 알파값")]
    public float flickerAlpha = 0.25f;

    [Header("흔들림 설정")]
    [Tooltip("흔들림 발동 최소 간격 (초)")]
    public float shakeIntervalMin = 2f;
    [Tooltip("흔들림 발동 최대 간격 (초)")]
    public float shakeIntervalMax = 5f;
    [Tooltip("흔들림 강도 (픽셀)")]
    public float shakeStrength = 4f;
    [Tooltip("흔들림 지속 시간 (초)")]
    public float shakeDuration = 0.2f;

    [Header("크로마틱 어베레이션")]
    [Tooltip("색 분리 효과용 보조 스프라이트 (없으면 생략)")]
    public SpriteRenderer chromaticSprite;
    [Tooltip("색 분리 오프셋 (픽셀)")]
    public float chromaticOffset = 3f;
    public Color chromaticColor = new Color(1f, 0f, 0.5f, 0.35f);

    // ─────────────────────────────────────────────
    private SpriteRenderer _sr;
    private Vector3 _originPos;

    void Awake()
    {
        _sr = GetComponentInChildren<SpriteRenderer>();
    }

    void Start()
    {
        _originPos = transform.localPosition;

        // 크로마틱 스프라이트 초기 설정
        if (chromaticSprite != null)
        {
            chromaticSprite.color = chromaticColor;
            chromaticSprite.gameObject.SetActive(false);
        }

        StartCoroutine(FlickerLoop());
        StartCoroutine(ShakeLoop());
    }

    void OnDisable()
    {
        // 비활성화 시 원래 상태로 복원
        if (_sr != null)
        {
            Color c = _sr.color;
            c.a = 1f;
            _sr.color = c;
        }
        transform.localPosition = _originPos;
        if (chromaticSprite != null)
            chromaticSprite.gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────────
    //  깜빡임 루프
    // ─────────────────────────────────────────────
    IEnumerator FlickerLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(
                Random.Range(flickerIntervalMin, flickerIntervalMax));

            yield return StartCoroutine(Flicker());
        }
    }

    IEnumerator Flicker()
    {
        if (_sr == null) yield break;

        // 크로마틱 어베레이션 동시 발동
        if (chromaticSprite != null)
        {
            chromaticSprite.gameObject.SetActive(true);
            chromaticSprite.transform.localPosition = new Vector3(
                chromaticOffset / 100f, 0f, 0f);
        }

        Color c = _sr.color;
        float original = c.a;

        // 빠른 알파 진동
        float elapsed = 0f;
        while (elapsed < flickerDuration)
        {
            c.a = (Mathf.Sin(elapsed * 80f) > 0f) ? original : flickerAlpha;
            _sr.color = c;
            elapsed += Time.deltaTime;
            yield return null;
        }

        c.a = original;
        _sr.color = c;

        if (chromaticSprite != null)
            chromaticSprite.gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────────
    //  흔들림 루프
    // ─────────────────────────────────────────────
    IEnumerator ShakeLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(
                Random.Range(shakeIntervalMin, shakeIntervalMax));

            yield return StartCoroutine(Shake());
        }
    }

    IEnumerator Shake()
    {
        float elapsed = 0f;
        float strength = shakeStrength / 100f; // 유니티 단위로 변환

        while (elapsed < shakeDuration)
        {
            float progress = 1f - (elapsed / shakeDuration);
            transform.localPosition = _originPos + (Vector3)Random.insideUnitCircle * strength * progress;
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = _originPos;
    }

    /// <summary>전투 종료 또는 데미지 연출 시 강도 높은 글리치를 1회 실행.</summary>
    public void TriggerGlitch()
    {
        StartCoroutine(Flicker());
        StartCoroutine(Shake());
    }
}
