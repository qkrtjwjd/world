using UnityEngine;

/// <summary>
/// 루의 그림자 — 발각 판정 2초를 표시한다 (C-14-3-2 문단 1117 · F-6 문단 783).
///
/// 세라의 시야에 들어가면 그림자가 <b>루의 의사와 무관하게</b> 세라 쪽으로 끌려간다.
/// 딱딱 소리와 같은 축이다 — 자기 몸이 자기 말을 듣지 않는다.
///
/// ⚠ <b>게이지·아이콘·경고음을 쓰지 않는다.</b> 정본이 이 2초의 표현을 그림자 하나로 못박았다.
///   그래서 색과 진하기는 <b>고정</b>이고 형태만 바뀐다 — 밝아지거나 진해지면 그것이 곧 게이지다.
///
/// ⚠ 발각 진행도는 <see cref="SeraVision.DetectionProgress"/> 에서 읽기만 한다. 여기서 다시
///   재지 않는다. 판정과 표시가 어긋나면 플레이어는 보이는 것을 믿지 않게 된다.
///
/// 세라가 없는 씬(집·숲)에서는 조용히 꺼진다. 마을 스텔스 구간 전용 연출이다.
/// 전 씬 공통 그림자가 필요해지면 <see cref="alwaysVisible"/> 을 켜고 아트를 붙인다.
///
/// 배치: <c>Wizard.prefab</c> 에 붙이면 된다. 표시용 오브젝트는 자동 생성한다.
/// </summary>
public class LuShadow : MonoBehaviour
{
    [Header("모양")]
    [Tooltip("발밑 타원의 가로 반지름(월드 유닛). 캐릭터 폭 0.56 기준.")]
    public float radiusX = 0.20f;
    [Tooltip("발밑 타원의 세로 반지름.")]
    public float radiusY = 0.09f;
    [Tooltip("발끝에서 그림자 중심까지의 오프셋. 피벗이 바닥중앙이므로 살짝 내린다.")]
    public float groundOffset = -0.02f;
    [Range(8, 48)] public int segments = 24;

    [Header("끌림")]
    [Tooltip("발각 판정이 다 찼을 때 세라 쪽으로 늘어나는 길이(월드 유닛).")]
    public float pullDistance = 0.85f;
    [Tooltip("세라 방향으로만 뾰족하게 늘어나도록 하는 지수. 1 이면 전체가 밀리고, 클수록 한쪽만 뻗는다.")]
    [Range(1f, 6f)] public float pullSharpness = 2.5f;
    [Tooltip("판정이 풀렸을 때 원래대로 돌아오는 속도(초당). 끌려갈 때는 판정 값을 그대로 따른다.")]
    public float releaseSpeed = 3f;

    [Header("색 — 진하기는 고정한다 (게이지 금지)")]
    public Color shadowColor = new Color(0.06f, 0.06f, 0.09f);
    [Range(0f, 1f)] public float shadowAlpha = 0.38f;

    [Header("표시")]
    [Tooltip("세라가 없는 씬에서도 그림자를 그린다. 전 씬 공통 그림자를 도입할 때 켠다.")]
    public bool alwaysVisible = false;
    [Tooltip("비우면 URP 2D Sprite-Unlit 셰이더로 즉석에서 만든다.")]
    public Material material;
    [Tooltip("정렬 순서. 바닥에 깔려야 하므로 캐릭터(60~100)보다 낮고 시야 빛(5)보다 높아야 한다. " +
             "⚠ 루의 order 를 따라가게 만들면 안 된다 — 루가 100 이라 그림자가 세라(60) 위를 덮는다.")]
    public int sortingOrder = 10;

    GameObject   _root;
    MeshFilter   _filter;
    MeshRenderer _renderer;
    Mesh         _mesh;
    SpriteRenderer _ownerSprite;

    Vector3[] _verts;
    Color[]   _colors;
    int[]     _tris;
    int       _built = -1;

    float _pull;      // 현재 끌림 0~1. 판정이 풀리면 서서히 0 으로 돌아온다.

    void Awake()
    {
        _ownerSprite = GetComponentInChildren<SpriteRenderer>();
        Build();
    }

    void OnDestroy()
    {
        if (_root != null) Destroy(_root);
    }

    void Build()
    {
        // ⚠ 플레이어의 자식으로 두지 않는다. 루도 좌우를 볼 때 localScale.x 의 부호를 뒤집으므로
        //   (CLAUDE.md §11) 자식이면 그림자가 함께 뒤집힌다. 세라 쪽으로 끌려가는 방향이
        //   반대로 꺾이면 연출이 거짓말을 하게 된다.
        _root = new GameObject("루_그림자");
        _filter   = _root.AddComponent<MeshFilter>();
        _renderer = _root.AddComponent<MeshRenderer>();

        _mesh = new Mesh { name = "LuShadow" };
        _mesh.MarkDynamic();
        _filter.sharedMesh = _mesh;

        _renderer.sharedMaterial = material != null ? material : MakeMaterial();
        _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows    = false;
        _renderer.lightProbeUsage   = UnityEngine.Rendering.LightProbeUsage.Off;

        // 바닥에 깔린다. 레이어는 루에서 가져오되 순서는 고정값을 쓴다 —
        // 루의 order(100)를 따라가면 그림자가 세라(60)와 솔(60) 위를 덮는다.
        if (_ownerSprite != null) _renderer.sortingLayerID = _ownerSprite.sortingLayerID;
        _renderer.sortingOrder = sortingOrder;
    }

    Material MakeMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                  ?? Shader.Find("Sprites/Default");
        return new Material(shader) { name = "LuShadow (auto)" };
    }

    void LateUpdate()
    {
        if (_mesh == null) return;

        var vision = SeraPatrol.Instance != null ? SeraPatrol.Instance.Vision : null;

        if (vision == null && !alwaysVisible)
        {
            if (_renderer.enabled) _renderer.enabled = false;
            return;
        }
        if (!_renderer.enabled) _renderer.enabled = true;

        // 끌림의 세기와 방향
        Vector2 toSera = Vector2.zero;
        float target = 0f;
        if (vision != null)
        {
            Vector2 d = (Vector2)(vision.transform.position - transform.position);
            if (d.sqrMagnitude > 0.0001f) toSera = d.normalized;
            target = vision.DetectionProgress;
        }

        // 끌려갈 때는 판정을 그대로 따르고(즉시), 풀릴 때만 서서히 돌아온다.
        // 판정이 0 으로 리셋되는 순간 그림자가 튕기듯 돌아오면 그것도 신호가 되어버린다.
        _pull = target >= _pull ? target : Mathf.MoveTowards(_pull, target, releaseSpeed * Time.deltaTime);

        _root.transform.position = transform.position + new Vector3(0f, groundOffset, 0f);
        Rebuild(toSera, _pull);
    }

    void Rebuild(Vector2 toSera, float pull)
    {
        int seg = Mathf.Clamp(segments, 8, 48);
        if (seg != _built) Allocate(seg);

        var color = new Color(shadowColor.r, shadowColor.g, shadowColor.b, shadowAlpha);

        _verts[0]  = Vector3.zero;
        _colors[0] = color;

        for (int i = 0; i < seg; i++)
        {
            float a = (float)i / seg * Mathf.PI * 2f;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Vector2 p   = new Vector2(dir.x * radiusX, dir.y * radiusY);

            // 세라 쪽 반구만 뻗어 나간다. 지수를 올릴수록 한 방향으로 뾰족해진다.
            if (pull > 0f && toSera.sqrMagnitude > 0.0001f)
            {
                float dot = Vector2.Dot(dir, toSera);
                if (dot > 0f) p += toSera * (Mathf.Pow(dot, pullSharpness) * pullDistance * pull);
            }

            _verts[i + 1]  = new Vector3(p.x, p.y, 0f);
            _colors[i + 1] = color;   // 진하기 고정 — 끝까지 같은 색이다
        }

        _mesh.Clear();
        _mesh.vertices  = _verts;
        _mesh.colors    = _colors;
        _mesh.triangles = _tris;
        _mesh.RecalculateBounds();
    }

    void Allocate(int seg)
    {
        _built  = seg;
        _verts  = new Vector3[seg + 1];
        _colors = new Color[seg + 1];
        _tris   = new int[seg * 3];
        for (int i = 0; i < seg; i++)
        {
            _tris[i * 3]     = 0;
            _tris[i * 3 + 1] = i + 1;
            _tris[i * 3 + 2] = (i + 1) % seg + 1;   // 링을 닫는다
        }
    }
}
