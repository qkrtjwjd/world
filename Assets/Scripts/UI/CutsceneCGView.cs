using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 일러 컷(CG) 전면 표시 — 규격서 8장 「일러 컷」.
///
/// <para>규격서: *"전부 640×360 전면이야."* 캔버스 기준 해상도가 이미 640×360 이라
/// (CLAUDE.md §11) 컷은 화면을 그대로 덮는다. 데모 컷 12개는 전부 환상 필터라
/// 필터 토글은 컷씬 동안 잠긴 상태를 전제로 한다(규격서 6장).</para>
///
/// <para>넣는 법 — <c>Assets/Resources/CG/{id}.png</c> 에 파일명만 맞춰 넣으면 된다.
/// 목록은 <c>Assets/Docs/일러컷_파일명.md</c> 에 있다.</para>
///
/// <para>⚠ 그림이 없으면 <b>아무 일도 일어나지 않는다</b> — 경고 한 줄만 남기고 컷을 건너뛴다.
/// 초상화와 같은 방침이다(YarnCommandBridge:286-311). 대사 진행이 막히면 안 되기 때문이다.</para>
/// </summary>
public class CutsceneCGView : MonoBehaviour
{
    public static CutsceneCGView Instance { get; private set; }

    const string ResourceDir = "CG/";

    Canvas _canvas;
    Image _image;
    CanvasGroup _group;
    bool _warnedEmpty;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // ⚠ Destroy(gameObject) 를 쓰면 같은 GO 의 다른 매니저까지 날아간다.
            //    SingletonGuard 와 같은 방침으로 컴포넌트만 지운다.
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Build()
    {
        if (_canvas != null) return;

        var go = new GameObject("CutsceneCG");
        go.transform.SetParent(transform, false);

        _canvas = go.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 200;              // 대사창(기본)보다 위, 토스트(120)보다도 위
        UiCanvasScale.Add(go);                   // 640x360 Expand — 단일 출처

        _group = go.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        var imgGo = new GameObject("CG");
        imgGo.transform.SetParent(go.transform, false);
        _image = imgGo.AddComponent<Image>();
        _image.raycastTarget = false;
        _image.preserveAspect = true;

        var r = imgGo.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero;              // 화면 전체를 덮는다
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    /// <summary>컷을 띄운다. 그림이 없으면 아무 일도 하지 않는다.</summary>
    public void Show(string id)
    {
        if (string.IsNullOrEmpty(id)) return;

        var sprite = Resources.Load<Sprite>(ResourceDir + id);
        if (sprite == null)
        {
            if (!_warnedEmpty)
            {
                _warnedEmpty = true;
                Debug.LogWarning(
                    "[CutsceneCGView] 일러 컷 아트가 아직 0장입니다 — 컷 없이 대사만 진행합니다. " +
                    "Assets/Resources/CG/ 에 파일명만 맞춰 넣으면 됩니다 " +
                    "(목록: Assets/Docs/일러컷_파일명.md). 요청된 id: " + id);
            }
            return;
        }

        Build();
        _image.sprite = sprite;
        _group.alpha = 1f;
    }

    /// <summary>컷을 내린다.</summary>
    public void Hide()
    {
        if (_group == null) return;
        _group.alpha = 0f;
        if (_image != null) _image.sprite = null;
    }
}
