using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 씬 내 배경/NPC/UI 오브젝트에 부착.
/// ZoneType으로 GlitchZoneObjectController의 활성화 제어를 받고,
/// 스프라이트가 설정된 경우 게이지에 따라 이미지를 교체한다.
///
/// 지원 컴포넌트:
///   - SpriteRenderer  → Sprite 필드 사용
///   - UI Image        → Sprite 필드 사용
///
/// gauge ≤30 → fantasy 이미지, gauge ≥70 → reality 이미지, 31~69 → 현재 유지.
/// 일반 전환: 0.2초 페이드. 강제 전환(immediate=true): 즉시 교체.
/// </summary>
public class WorldObject : MonoBehaviour
{
    public enum ZoneType { Fantasy, Reality }

    [Tooltip("Fantasy: 게이지 0~69 구간에서 표시 / Reality: 게이지 31~100 구간에서 표시")]
    public ZoneType zoneType = ZoneType.Fantasy;

    [Header("Sprite (SpriteRenderer / UI Image 용)")]
    public Sprite fantasySprite;
    public Sprite realitySprite;

    private SpriteRenderer _sr;
    private Image          _img;
    private Coroutine      _fadeCoroutine;
    private bool           _showingReality = false;

    void Awake()
    {
        _sr  = GetComponent<SpriteRenderer>();
        _img = GetComponent<Image>();

        if (_sr == null && _img == null)
            Debug.LogWarning($"[WorldObject] '{gameObject.name}': SpriteRenderer 또는 Image 컴포넌트가 필요합니다.");
    }

    void OnEnable()  => GaugeManager.RegisterWorldObject(this);
    void OnDisable() => GaugeManager.UnregisterWorldObject(this);

    void Start()
    {
        if (GaugeManager.Instance != null)
            UpdateSprite(GaugeManager.Instance.fantasyRealityGauge, true);
    }

    /// <summary>GaugeManager에서 호출. immediate=true이면 페이드 없이 즉시 교체.</summary>
    public void UpdateSprite(float gauge, bool immediate)
    {
        bool targetReality;

        if (gauge <= 30f)
            targetReality = false;
        else if (gauge >= 70f)
            targetReality = true;
        else
            return; // 31~69 구간: 현재 유지

        if (targetReality == _showingReality) return;

        _showingReality = targetReality;

        if (immediate)
        {
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            ApplyImage();
            SetAlpha(1f);
        }
        else
        {
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            bool hasTarget = (_showingReality ? realitySprite : fantasySprite) != null;
            if (!hasTarget) return;
            _fadeCoroutine = StartCoroutine(FadeSwap());
        }
    }

    IEnumerator FadeSwap()
    {
        float half = 0.1f;

        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            SetAlpha(1f - (t / half));
            yield return null;
        }
        ApplyImage();
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            SetAlpha(t / half);
            yield return null;
        }
        SetAlpha(1f);
    }

    private void ApplyImage()
    {
        Sprite spr = _showingReality ? realitySprite : fantasySprite;
        if (spr == null) return;
        if (_sr  != null) _sr.sprite  = spr;
        else if (_img != null) _img.sprite = spr;
    }

    private void SetAlpha(float a)
    {
        if (_sr != null)
        {
            Color c = _sr.color;  c.a = a; _sr.color  = c;
        }
        else if (_img != null)
        {
            Color c = _img.color; c.a = a; _img.color = c;
        }
    }
}
