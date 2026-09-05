using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 테두리 효과 컨트롤러 (DontDestroyOnLoad 자동 생성).
///
/// 사용처:
///   - 마시멜로 씹을 때 테두리 번짐 → <see cref="ShowMarshmallow(float)"/>
///   - 심박/전투 붉은 테두리       → <see cref="ShowHeartbeat(float, Color)"/>
///   - 접근성: screenEdgeEffectEnabled = false 이면 모든 효과 무시
///
/// Inspector 배치 불필요. 씬에서 <c>ScreenEdgeEffectController.ShowMarshmallow()</c> 로 호출.
/// </summary>
public class ScreenEdgeEffectController : MonoBehaviour
{
    // ── 싱글톤 ──────────────────────────────────────────────────────────────
    public static ScreenEdgeEffectController Instance
    {
        get
        {
            if (_instance == null) CreateInstance();
            return _instance;
        }
    }
    static ScreenEdgeEffectController _instance;

    // ── 기본 색상 ───────────────────────────────────────────────────────────
    static readonly Color MarshmallowColor = new Color(0.95f, 0.90f, 0.85f, 0.55f); // 따뜻한 흰빛
    static readonly Color HeartbeatColor   = new Color(0.75f, 0.10f, 0.10f, 0.60f); // 붉은색

    // ── 내부 상태 ────────────────────────────────────────────────────────────
    Image   _edgeImage;
    Image   _sustainedImage;   // 지속형 전용. 원샷 연출과 레이어를 분리한다
    Coroutine _activeCoroutine;

    // ── 자동 생성 ────────────────────────────────────────────────────────────
    static void CreateInstance()
    {
        var root = new GameObject("ScreenEdgeEffectController [Auto]");
        DontDestroyOnLoad(root);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95;   // 게임 UI 위, 설정 패널 아래
        UiCanvasScale.Add(root);   // 640x360 Expand — 단일 출처

        _instance = root.AddComponent<ScreenEdgeEffectController>();
        _instance.BuildOverlay(root.transform);
        _instance.BuildSustainedOverlay(root.transform);
        _instance.SubscribeEvents();
    }

    void BuildOverlay(Transform parent)
    {
        // 전체 화면 크기 이미지 (테두리만 불투명, 중앙은 투명)
        // 실제 테두리 번짐 효과는 스프라이트 또는 셰이더로 구현 가능하나
        // 기본 구현에서는 단색 비네트 Image를 사용 (Inspector에서 Radial Gradient 스프라이트로 교체 권장)
        var go  = new GameObject("EdgeOverlay");
        go.transform.SetParent(parent, false);
        _edgeImage = go.AddComponent<Image>();
        _edgeImage.color = Color.clear;
        _edgeImage.raycastTarget = false;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// 지속형 효과 전용 레이어. 원샷 연출(EdgeRoutine)이 끝날 때 Color.clear 로 되돌리므로
    /// 같은 Image 를 공유하면 90초 압박 중에 마시멜로·심박 연출 하나만 재생돼도 압박 표시가 지워진다.
    /// </summary>
    void BuildSustainedOverlay(Transform parent)
    {
        var go = new GameObject("SustainedOverlay");
        go.transform.SetParent(parent, false);
        go.transform.SetAsFirstSibling();   // 원샷 연출이 위에 오도록
        _sustainedImage = go.AddComponent<Image>();
        _sustainedImage.color = Color.clear;
        _sustainedImage.raycastTarget = false;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    void SubscribeEvents()
    {
        SettingsManager.OnScreenEdgeEffectChanged += OnSettingChanged;
    }

    void OnDestroy()
    {
        SettingsManager.OnScreenEdgeEffectChanged -= OnSettingChanged;
    }

    void OnSettingChanged(bool isEnabled)
    {
        if (!isEnabled)
        {
            // 즉시 끄기
            if (_activeCoroutine != null) { StopCoroutine(_activeCoroutine); _activeCoroutine = null; }
            if (_edgeImage != null) _edgeImage.color = Color.clear;
            if (_sustainedImage != null) _sustainedImage.color = Color.clear;
        }
    }

    // ── 퍼블릭 API ──────────────────────────────────────────────────────────

    /// <summary>마시멜로 씹기 연출: 따뜻한 흰빛 테두리 번짐 (duration초 페이드 인/아웃).</summary>
    public static void ShowMarshmallow(float duration = 1.5f)
    {
        if (!IsEnabled()) return;
        Instance.PlayEdge(MarshmallowColor, duration);
    }

    /// <summary>심박/전투 붉은 테두리 펄스 (duration초마다 깜빡).</summary>
    public static void ShowHeartbeat(float duration = 0.4f, Color? color = null)
    {
        if (!IsEnabled()) return;
        Instance.PlayEdge(color ?? HeartbeatColor, duration);
    }

    /// <summary>임의 색상 테두리 비네팅 (duration초 페이드 인/아웃).</summary>
    public static void ShowEdge(Color color, float duration = 1.5f)
    {
        if (!IsEnabled()) return;
        Instance.PlayEdge(color, duration);
    }

    /// <summary>진행 중인 테두리 효과를 즉시 숨깁니다.</summary>
    public static void HideEdge()
    {
        if (_instance == null) return;
        if (_instance._activeCoroutine != null)
        {
            _instance.StopCoroutine(_instance._activeCoroutine);
            _instance._activeCoroutine = null;
        }
        if (_instance._edgeImage != null)
            _instance._edgeImage.color = Color.clear;
    }

    /// <summary>
    /// 지속형 비네팅 강도를 즉시 설정합니다. 코루틴을 쓰지 않으므로 매 프레임 호출해도 깜빡이지 않습니다.
    /// 집 90초 탈출 압박처럼 시간에 비례해 서서히 짙어지는 연출에 사용합니다.
    /// </summary>
    /// <param name="color">기준 색. 알파는 무시되고 <paramref name="alpha01"/> 로 대체됩니다.</param>
    /// <param name="alpha01">0~1. 0 이면 완전히 투명합니다.</param>
    /// <remarks>
    /// ⚠ 접근성 설정(screenEdgeEffectEnabled)이 꺼져 있으면 아무 일도 하지 않습니다.
    /// 이 채널에만 정보를 실으면 설정을 끈 플레이어에게 압박이 전혀 전달되지 않으므로,
    /// 호출하는 쪽에서 소리·공간 축소 등 다른 채널에 정보를 반드시 중복시켜야 합니다.
    /// </remarks>
    public static void SetSustainedLevel(Color color, float alpha01)
        => SetSustainedLevel(color, alpha01, 0f);

    /// <param name="edgeRatio">
    /// 가장자리가 안쪽으로 파고드는 비율 0~1 (F-6 「가장자리 화면비 18% / 30% / 44%」).
    /// 0 이면 예전처럼 화면 전체를 고르게 덮는다.
    /// </param>
    public static void SetSustainedLevel(Color color, float alpha01, float edgeRatio)
    {
        if (!IsEnabled()) return;
        var img = Instance._sustainedImage;
        if (img == null) return;

        // 폭이 지정되면 가장자리 그라디언트를, 아니면 단색을 쓴다.
        img.sprite = edgeRatio > 0.001f ? GetVignette(edgeRatio) : null;
        img.type   = Image.Type.Simple;
        img.color  = new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha01));
    }

    // ── 가장자리 그라디언트 ─────────────────────────────────────────────────
    //
    // 예전에는 지속형 오버레이가 화면 전체 단색이었다. 그래서 F-6 의 「가장자리 화면비」를
    // 폭으로 쓰지 못하고 비율만 알파에 옮겨 담았고, 결과적으로 화면이 통째로 어두워졌다.
    // C-14-2 문단 1018 은 「사방에서 안쪽. 쫓기는 느낌이 아니라 갇히는 느낌」이라고 못박았으므로
    // 가운데는 열려 있고 테두리가 조여드는 그림이어야 한다.

    static readonly System.Collections.Generic.Dictionary<int, Sprite> _vignetteCache = new();

    /// <summary>폭을 0.02 단위로 반올림해 캐시한다. 전환 중에 매 프레임 텍스처를 만들지 않기 위해서다.</summary>
    static Sprite GetVignette(float ratio)
    {
        int key = Mathf.RoundToInt(Mathf.Clamp(ratio, 0.02f, 0.9f) * 50f);
        if (_vignetteCache.TryGetValue(key, out var cached) && cached != null) return cached;
        var made = BuildVignette(key / 50f);
        _vignetteCache[key] = made;
        return made;
    }

    static Sprite BuildVignette(float ratio)
    {
        // 640x360 을 5분의 1로 줄인 크기. 부드러운 그라디언트라 이 해상도로 충분하고,
        // 종횡비가 같아 늘려도 왜곡되지 않는다.
        const int W = 128, H = 72;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
        {
            wrapMode   = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name       = $"Vignette{ratio:F2}",
        };

        var px = new Color32[W * H];
        float r = Mathf.Max(0.0001f, ratio);
        for (int y = 0; y < H; y++)
        {
            float v = (y + 0.5f) / H;
            float dy = Mathf.Min(v, 1f - v) / r;
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W;
                float dx = Mathf.Min(u, 1f - u) / r;

                // 가장자리에서 0, 안쪽 경계에서 1. 두 축 중 가까운 쪽을 따른다.
                float d = Mathf.Clamp01(Mathf.Min(dx, dy));
                float a = 1f - Mathf.SmoothStep(0f, 1f, d);
                px[y * W + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, false);

        return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f,
                             0, SpriteMeshType.FullRect);
    }

    /// <summary>지속형 비네팅을 즉시 걷어냅니다. 원샷 연출에는 영향을 주지 않습니다.</summary>
    public static void ClearSustained()
    {
        if (_instance == null || _instance._sustainedImage == null) return;
        _instance._sustainedImage.color = Color.clear;
    }

    // ── 내부 ────────────────────────────────────────────────────────────────
    static bool IsEnabled() =>
        SettingsManager.Instance == null || SettingsManager.Instance.screenEdgeEffectEnabled;

    void PlayEdge(Color targetColor, float duration)
    {
        if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
        _activeCoroutine = StartCoroutine(EdgeRoutine(targetColor, duration));
    }

    IEnumerator EdgeRoutine(Color targetColor, float duration)
    {
        // 페이드 인 (30%)
        float fadeIn  = duration * 0.30f;
        float fadeOut = duration * 0.70f;

        float t = 0f;
        while (t < fadeIn)
        {
            t += Time.unscaledDeltaTime;
            _edgeImage.color = Color.Lerp(Color.clear, targetColor, t / fadeIn);
            yield return null;
        }
        _edgeImage.color = targetColor;

        // 페이드 아웃 (70%)
        t = 0f;
        while (t < fadeOut)
        {
            t += Time.unscaledDeltaTime;
            _edgeImage.color = Color.Lerp(targetColor, Color.clear, t / fadeOut);
            yield return null;
        }
        _edgeImage.color = Color.clear;
        _activeCoroutine = null;
    }
}
