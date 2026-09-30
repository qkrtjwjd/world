using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 공용 오버레이 컷 — F-3-9 · D-0 · E-64. 클로즈업은 카메라가 아니라 맵 위에 띄우는 한 장짜리 그림이다.
///
/// <para><b>규격</b> (F-3-9 문단 282~288)</para>
/// <list type="bullet">
/// <item>내부 해상도 기준 <b>384×216 한 종</b>. 화면 가로 중앙, 대사창과 겹치지 않는 높이(위에서 <see cref="TopMargin"/>).</item>
/// <item>표시 · 닫힘은 <b>즉시</b>. 페이드를 쓰지 않는다.</item>
/// <item>표시 중 맵 이동 · 상호작용을 잠그고 <b>맵 위 캐릭터의 애니메이션을 멈춘다</b>. 대사창과 효과음은 계속 간다.</item>
/// <item><b>배경을 어둡게 덮지 않는다</b> — 뒤판을 만들지 않는다.</item>
/// <item>그림 안의 움직임은 2~3프레임 반복뿐이다 — 한 파일을 여러 칸으로 자른 스프라이트면 이름 순으로 돌린다.</item>
/// <item>조사로 여는 오버레이는 확인 키로 닫고(<see cref="ShowAndWait"/>), 컷신 중의 오버레이는 스크립트가 닫는다(<see cref="Show"/> · <see cref="Hide"/>).</item>
/// </list>
///
/// <para><b>ID</b> 는 <see cref="OverlayIds"/>(F-8-3 매핑표의 코드 사본)에 있는 것만 받는다. 없는 ID 는 오류 로그만 남기고 띄우지 않는다 —
/// 이름을 지어내지 않는다(CLAUDE.md §0-4).</para>
///
/// <para><b>넣는 법</b> — <c>Assets/Resources/Overlays/{코드ID}.png</c> 384×216. 공용 테두리는 <c>Overlays/_frame.png</c>(있으면 그림 위에 얹는다).
/// 도자기 손은 한 ID 이고 파일만 <c>_2</c>(두 마디 · 인형화 0~30) · <c>_3</c>(세 마디 · 31 이상)으로 나눈다 — 이 클래스가 고른다(F-8-3 문단 955).</para>
///
/// <para><b>그림이 없으면</b> — 에디터 · 개발 빌드에서는 384×216 자리 표시 틀과 코드 ID 를 띄워 화면에서 자리를 확인하게 한다
/// (2026-10-01 사용자 결정). 출시 빌드에서는 아무것도 띄우지 않고 건너뛴다. 어느 쪽이든 대사 진행은 막지 않는다.</para>
///
/// <para>읽는 물건(쪽지)은 오버레이가 아니다 — <see cref="ReadableOverlay"/>(F-8-9 · 전체화면).</para>
/// </summary>
public class OverlayCut : MonoBehaviour
{
    public const int   Width     = 384;
    public const int   Height    = 216;
    /// <summary>위 여백(내부 해상도 픽셀). 아래 136px 을 대사창에 남긴다. S#21 부감 컷의 자리와 같다.</summary>
    public const float TopMargin = 8f;

    const string ResourceDir   = "Overlays/";
    const string FrameFile     = "_frame";
    const float  FrameInterval = 0.3f;    // 그림 안 2~3프레임 반복 간격
    const float  MinShowSeconds = 0.3f;   // 조사 오버레이 — 열린 직후 누르고 있던 키로 곧바로 닫히지 않게
    const int    SortingOrder  = 100;     // 결계 광막(96) 위 · 토스트(120) · 읽는 물건(210) · 암전(999) 아래

    static OverlayCut _instance;

    /// <summary>씬에 없으면 만든다. 씬을 넘기지 않는다 — 오버레이는 그 씬 안에서 열리고 닫힌다.</summary>
    public static OverlayCut Instance
    {
        get
        {
            if (_instance == null)
                _instance = new GameObject("OverlayCut [Auto]").AddComponent<OverlayCut>();
            return _instance;
        }
    }

    /// <summary>표시 중인가. <see cref="InteractionManager"/> 가 이 값을 보고 상호작용을 건너뛴다.</summary>
    public static bool IsOpen => _instance != null && _instance._open;

    Canvas     _canvas;
    Image      _image;
    Image      _frame;
    GameObject _placeholder;
    TMP_Text   _placeholderLabel;

    bool       _open;
    Sprite[]   _frames;
    Coroutine  _loop;
    PlayerInputLock _lock;
    readonly List<(Animator anim, float speed)> _frozen = new List<(Animator, float)>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _instance = null;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);   // ReadableOverlay 와 같은 방침 — 같은 GO 의 다른 컴포넌트까지 날리지 않는다
            return;
        }
        _instance = this;
    }

    void OnDestroy()
    {
        if (_open) Release();   // 씬이 넘어가며 닫히지 않고 사라져도 잠금 · 정지가 남지 않게
        if (_instance == this) _instance = null;
    }

    // ─── 공개 API ─────────────────────────────────────────────────────────

    /// <summary>F-8-3 매핑표에 있는 ID 인가.</summary>
    public static bool IsKnown(string id) => !string.IsNullOrEmpty(id) && System.Array.IndexOf(OverlayIds.All, id) >= 0;

    /// <summary>그림 파일이 들어와 있는가(도자기 손은 지금 인형화 단계의 파일).</summary>
    public static bool HasArt(string id) => IsKnown(id) && LoadFrames(id).Length > 0;

    /// <summary>
    /// 컷신 중의 오버레이를 띄운다. 스크립트가 <see cref="Hide"/> 로 닫는다.
    /// 이미 떠 있으면 그림만 바꾼다(잠금 · 정지는 한 번만 건다).
    /// </summary>
    public void Show(string id)
    {
        if (!IsKnown(id))
        {
            Debug.LogError($"[OverlayCut] F-8-3 매핑표에 없는 ID '{id}' — 띄우지 않습니다. OverlayIds 를 확인하세요.");
            return;
        }

        var frames = LoadFrames(id);
        bool placeholder = frames.Length == 0;
        if (placeholder && !ShowPlaceholder)
        {
            Debug.LogWarning($"[OverlayCut] 그림이 없어 건너뜁니다 — Assets/Resources/{ResourceDir}{FileFor(id)}.png");
            return;
        }

        Build();
        StopLoop();
        _frames = frames;

        _image.enabled = !placeholder;
        _placeholder.SetActive(placeholder);
        if (placeholder)
        {
            _placeholderLabel.text = $"오버레이 그림 없음\n{FileFor(id)}";
            Debug.LogWarning($"[OverlayCut] 그림이 없어 자리 표시 틀을 띄웁니다(출시 빌드에서는 건너뜀) — {FileFor(id)}");
        }
        else
        {
            _image.sprite = frames[0];
            if (frames.Length > 1) _loop = StartCoroutine(Loop());
        }

        var frameSprite = Resources.Load<Sprite>(ResourceDir + FrameFile);
        _frame.sprite  = frameSprite;
        _frame.enabled = frameSprite != null && !placeholder;

        _canvas.enabled = true;
        if (!_open)
        {
            _open = true;
            _lock = PlayerInputLock.Instance;
            _lock.Lock();
            FreezeMapAnimators();
        }
    }

    /// <summary>오버레이를 즉시 닫는다.</summary>
    public void Hide()
    {
        if (!_open) return;
        Release();
    }

    /// <summary>정해진 시간만큼 띄우고 닫는다. 그림이 없고 자리 표시도 하지 않으면 곧바로 끝난다.</summary>
    public IEnumerator ShowForSeconds(string id, float seconds)
    {
        Show(id);
        if (!_open) yield break;
        yield return new WaitForSeconds(seconds);
        Hide();
    }

    /// <summary>조사로 여는 오버레이 — 확인 키로 닫을 때까지 기다린다(F-3-9 문단 287).</summary>
    public IEnumerator ShowAndWait(string id)
    {
        Show(id);
        yield return WaitForClose();
    }

    /// <summary>
    /// 이미 떠 있는 오버레이를 확인 키로 닫을 때까지 기다린다. 띄운 뒤 소리 · 아이템 획득을 먼저 진행하고
    /// 마지막에 플레이어가 닫게 할 때 쓴다(S#08 서랍).
    /// </summary>
    public IEnumerator WaitForClose()
    {
        if (!_open) yield break;

        float shown = 0f;
        while (shown < MinShowSeconds && _open)
        {
            shown += Time.unscaledDeltaTime;
            yield return null;
        }
        while (_open && !CloseRequested()) yield return null;

        // 닫는 키의 KeyDown 이 같은 프레임에 대사 넘김 · 상호작용으로 흘러가지 않게 한 프레임 소비한다.
        yield return null;
        Hide();
    }

    // ─── 내부 ─────────────────────────────────────────────────────────────

    /// <summary>에디터 · 개발 빌드에서만 자리 표시 틀을 띄운다(2026-10-01 사용자 결정).</summary>
    static bool ShowPlaceholder => Application.isEditor || Debug.isDebugBuild;

    /// <summary>ID → 그림 파일 이름. 도자기 손만 인형화 단계로 마디 접미를 붙인다.</summary>
    static string FileFor(string id)
    {
        if (id != OverlayIds.HouseLuPorcelainHand) return id;
        float v = CorruptionManager.Instance != null ? CorruptionManager.Instance.currentCorruption : 0f;
        return id + (CorruptionManager.GetStage(v) == CorruptionStage.Autonomy
            ? OverlayIds.PorcelainTwoJoints : OverlayIds.PorcelainThreeJoints);
    }

    /// <summary>한 파일을 여러 칸으로 잘랐으면 이름 순으로 전부, 아니면 한 장.</summary>
    static Sprite[] LoadFrames(string id)
    {
        var sprites = Resources.LoadAll<Sprite>(ResourceDir + FileFor(id));
        System.Array.Sort(sprites, (a, b) => string.CompareOrdinal(a.name, b.name));
        return sprites;
    }

    IEnumerator Loop()
    {
        int i = 0;
        var wait = new WaitForSeconds(FrameInterval);
        while (true)
        {
            yield return wait;
            i = (i + 1) % _frames.Length;
            _image.sprite = _frames[i];
        }
    }

    void StopLoop()
    {
        if (_loop != null) { StopCoroutine(_loop); _loop = null; }
    }

    void Release()
    {
        StopLoop();
        _open = false;
        if (_canvas != null) _canvas.enabled = false;
        if (_image  != null) _image.sprite = null;
        _frames = null;
        UnfreezeMapAnimators();
        if (_lock != null) { _lock.Unlock(); _lock = null; }
    }

    /// <summary>
    /// 맵 위 캐릭터 애니메이션 정지 — UI 캔버스 밖의 Animator 전부(2026-10-01 사용자 결정).
    /// 원래 speed 를 기억했다가 닫을 때 되돌린다. 이미 멈춰 있던 것은 건드리지 않는다.
    /// </summary>
    void FreezeMapAnimators()
    {
        _frozen.Clear();
        foreach (var a in FindObjectsByType<Animator>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (a == null || !a.isActiveAndEnabled || a.speed == 0f) continue;
            if (a.GetComponentInParent<Canvas>() != null) continue;   // 대사창 · UI 는 계속 간다
            _frozen.Add((a, a.speed));
            a.speed = 0f;
        }
    }

    void UnfreezeMapAnimators()
    {
        foreach (var (anim, speed) in _frozen)
            if (anim != null) anim.speed = speed;
        _frozen.Clear();
    }

    void Build()
    {
        if (_canvas != null) return;

        var root = new GameObject("Overlay");
        root.transform.SetParent(transform, false);
        _canvas = root.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = SortingOrder;
        UiCanvasScale.Add(root);                  // 640×360 Expand — 단일 출처

        var cut = new GameObject("Cut", typeof(RectTransform));
        cut.transform.SetParent(root.transform, false);
        var rt = (RectTransform)cut.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(Width, Height);
        rt.anchoredPosition = new Vector2(0f, -TopMargin);

        _image = NewImage("Picture", cut.transform);
        _frame = NewImage("Frame",   cut.transform);

        // 자리 표시 틀 — 테두리만 긋고 안은 비운다. 배경을 덮지 않는다는 규칙을 틀에도 지킨다.
        _placeholder = new GameObject("Placeholder", typeof(RectTransform));
        _placeholder.transform.SetParent(cut.transform, false);
        Stretch((RectTransform)_placeholder.transform);
        var line = new Color(1f, 1f, 1f, 0.85f);
        Edge("Top",    new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 2), line);
        Edge("Bottom", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 2), line);
        Edge("Left",   new Vector2(0, 0), new Vector2(0, 1), new Vector2(2, 0), line);
        Edge("Right",  new Vector2(1, 0), new Vector2(1, 1), new Vector2(2, 0), line);

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(_placeholder.transform, false);
        Stretch((RectTransform)labelGo.transform);
        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize  = 12;
        label.color     = line;
        label.raycastTarget = false;
        _placeholderLabel = label;
        _placeholder.SetActive(false);

        _canvas.enabled = false;
    }

    Image NewImage(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch((RectTransform)go.transform);
        var img = go.AddComponent<Image>();
        img.raycastTarget  = false;
        img.preserveAspect = true;
        img.enabled = false;
        return img;
    }

    void Edge(string name, Vector2 min, Vector2 max, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(_placeholder.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = min; rt.anchorMax = max;
        rt.pivot     = new Vector2(Mathf.Approximately(min.x, 1f) ? 1f : 0f, Mathf.Approximately(min.y, 1f) ? 1f : 0f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static bool CloseRequested()
    {
        KeyCode interactKey = SettingsManager.Instance?.keyInteract ?? KeyCode.E;
        return Input.GetKeyDown(interactKey)
            || Input.GetKeyDown(KeyCode.Space)
            || Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.KeypadEnter)
            || Input.GetKeyDown(KeyCode.Escape)
            || Input.GetMouseButtonDown(0);
    }
}
