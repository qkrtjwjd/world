using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 읽는 물건(쪽지 등) 전체화면 오버레이 — F-8-9 · F-4-4 · D-4 [읽기] 표기 규약.
///
/// <para><b>문구를 텍스트로 출력하지 않는다.</b> 읽는 물건은 글씨까지 그려 넣은 한 장짜리 이미지이고
/// 게임은 그 이미지를 띄울 뿐이다. 원고의 [읽기] 블록은 이미지에 그려 넣을 문구의 원본이지
/// 런타임이 읽어오는 문자열이 아니다. 그래서 여기에는 TMP 가 없다.</para>
///
/// <para><b>규격</b> (D-4 [읽기] 주석 · F-8-9)</para>
/// <list type="bullet">
/// <item>전체화면으로 띄운다 — 640×360 에서 손글씨가 읽히려면 화면 일부로는 부족하다.</item>
/// <item>표시 중에는 이동과 다른 상호작용을 잠그고, 닫으면 즉시 돌려준다.</item>
/// <item><b>배경을 어둡게 덮지 않는다.</b> 결계 안의 밝기가 그대로 남아야 쪽지만 이질적으로 보인다.
///   그래서 뒤판(반투명 검정 등)을 만들지 않는다. 종이 밖은 이미지 자체가 투명해야 한다.</item>
/// </list>
///
/// <para>넣는 법 — <c>Assets/Resources/Readables/{id}.png</c> (640×360, 종이 밖 투명).
/// ⚠ 그림이 없으면 <b>아무 일도 일어나지 않는다</b> — 경고 한 줄만 남기고 넘어간다.
/// 일러 컷(<see cref="CutsceneCGView"/>)과 같은 방침이다. 아트가 없다고 진행이 멈추면 안 된다.</para>
/// </summary>
public class ReadableOverlay : MonoBehaviour
{
    const string ResourceDir = "Readables/";

    // 열린 직후 누르고 있던 키로 곧바로 닫히지 않게 두는 최소 표시 시간(초).
    const float MinShowSeconds = 0.3f;

    static ReadableOverlay _instance;

    /// <summary>씬에 없으면 만든다. 씬을 넘기지 않는다 — 읽는 물건은 그 씬 안에서 열리고 닫힌다.</summary>
    public static ReadableOverlay Instance
    {
        get
        {
            if (_instance == null)
                _instance = new GameObject("ReadableOverlay [Auto]").AddComponent<ReadableOverlay>();
            return _instance;
        }
    }

    /// <summary>
    /// 표시 중인가. <see cref="InteractionManager"/> 가 이 값을 보고 상호작용을 건너뛴다 —
    /// 닫는 키가 상호작용 키와 같아서, 막지 않으면 닫는 프레임에 근처 물건이 함께 조사된다.
    /// </summary>
    public static bool IsOpen => _instance != null && _instance._open;

    Canvas _canvas;
    CanvasGroup _group;
    Image _image;
    bool _open;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _instance = null;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            // CutsceneCGView 와 같은 방침 — 같은 GO 의 다른 컴포넌트까지 날리지 않는다.
            Destroy(this);
            return;
        }
        _instance = this;
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    void Build()
    {
        if (_canvas != null) return;

        var go = new GameObject("Readable");
        go.transform.SetParent(transform, false);

        _canvas = go.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 210;              // 대사창 · 토스트(120) · 일러 컷(200)보다 위
        UiCanvasScale.Add(go);                   // 640x360 Expand — 단일 출처

        _group = go.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        var imgGo = new GameObject("Page");
        imgGo.transform.SetParent(go.transform, false);
        _image = imgGo.AddComponent<Image>();
        _image.raycastTarget = false;
        _image.preserveAspect = true;

        var r = imgGo.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero;              // 화면 전체
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// 읽는 물건을 띄우고 플레이어가 닫을 때까지 기다린다. 그림이 없으면 곧바로 끝난다.
    /// </summary>
    public IEnumerator ShowAndWait(string id)
    {
        if (string.IsNullOrEmpty(id) || _open) yield break;

        var sprite = Resources.Load<Sprite>(ResourceDir + id);
        if (sprite == null)
        {
            Debug.LogWarning($"[ReadableOverlay] 읽는 물건 이미지가 없습니다 — 건너뜁니다. " +
                             $"Assets/Resources/{ResourceDir}{id}.png 를 넣으면 뜹니다.");
            yield break;
        }

        Build();
        _image.sprite = sprite;
        _group.alpha = 1f;
        _open = true;

        var inputLock = PlayerInputLock.Instance;
        inputLock.Lock();

        try
        {
            float shown = 0f;
            while (shown < MinShowSeconds)
            {
                shown += Time.unscaledDeltaTime;
                yield return null;
            }

            while (!CloseRequested()) yield return null;

            // 닫는 키의 KeyDown 이 같은 프레임에 대사 넘김으로 흘러가지 않게 한 프레임 소비한다.
            // 이 프레임 동안에도 IsOpen 이 true 라 상호작용도 막힌다.
            yield return null;
        }
        finally
        {
            // 코루틴이 중간에 끊겨도(씬 전환 · 대화 중단) 잠금이 남지 않게 한다.
            _group.alpha = 0f;
            _image.sprite = null;
            _open = false;
            if (inputLock != null) inputLock.Unlock();
        }
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
