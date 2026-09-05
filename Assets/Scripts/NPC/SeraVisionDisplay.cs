using UnityEngine;

/// <summary>
/// 세라의 시야를 바닥에 깔리는 빛으로 그린다 (C-14-3-2 · F-6-1).
///
/// <see cref="SeraVision"/> 은 판정만 하고 화면에 아무것도 그리지 않는다. 그래서 플레이어는
/// 세라가 지금 어디를 보고 있는지 알 방법이 없고, 스텔스가 실력이 아니라 운이 된다.
/// 이 컴포넌트가 그 판정을 그대로 눈에 보이게 옮긴다.
///
/// ⚠ <b>판정을 여기서 다시 하지 않는다.</b> 각도·거리·차단 규칙은 전부 SeraVision 에서 읽어 온다.
///   표시와 판정이 어긋나면 플레이어는 보이는 것을 믿을 수 없게 되고, 그건 표시가 없느니만 못하다.
///
/// ⚠ <b>UI 도형이 아니다.</b> F-6-1 이 「UI 도형이 아니라 바닥에 깔리는 빛으로 그린다」로 못박았다.
///   그래서 Canvas 가 아니라 월드 공간 메시이며, 캐릭터 아래 · 길 위에 깔린다.
///
/// ⚠ <b>발각 진행도를 밝기로 표시하지 않는다.</b> F-6 문단 783 이 발각 판정 2초의 표현을
///   「루의 그림자가 세라 쪽으로 끌려가는 연출」로 규정하고 <b>게이지·아이콘·경고음을 금지</b>했다.
///   시야 밝기를 누적에 따라 올리면 그것이 곧 게이지다.
///
/// 필터별로 모양이 다르다 (F-6-1) — 환상은 따뜻하고 흐리게, 현실은 차갑고 선명하게.
/// 지금은 정점 컬러로만 구분하며, 전용 아트가 나오면 <see cref="lightTexture"/> 에 꽂는다.
///
/// 배치: <see cref="SeraVision"/> 과 같은 GameObject 에 붙이면 된다. 자식 오브젝트는 자동 생성한다.
/// </summary>
[RequireComponent(typeof(SeraVision))]
public class SeraVisionDisplay : MonoBehaviour
{
    [Header("모양")]
    [Tooltip("부채꼴을 몇 조각으로 나눌지. 엄폐물 경계가 이 해상도로 잘린다.")]
    [Range(8, 64)] public int segments = 28;

    // ⚠ 세로를 눌러 타원으로 그리거나 원점을 발치로 내리고 싶어지지만, 둘 다 넣지 않는다.
    //   SeraVision 은 transform.position 을 원점으로 한 정원(正圓)으로 판정한다. 표시만
    //   타원이 되거나 원점이 어긋나면 "빛 밖에 있었는데 잡혔다" 가 되고, 그때 플레이어는
    //   보이는 것을 믿지 않게 된다. 표시가 없느니만 못한 상태다.

    [Header("색 — 환상 필터 (따뜻하고 흐리다)")]
    public Color fantasyColor = new Color(1f, 0.86f, 0.55f);
    [Range(0f, 1f)] public float fantasyAlpha = 0.34f;

    [Header("색 — 현실 필터 (차갑고 선명하다)")]
    public Color realityColor = new Color(0.62f, 0.84f, 1f);
    [Range(0f, 1f)] public float realityAlpha = 0.50f;

    [Header("상태별 세기")]
    [Tooltip("돌아봄 상태의 배율. 소리를 듣고 그쪽을 보는 중이라 가장 위험하다.")]
    public float lookBackBoost = 1.5f;
    [Tooltip("이동 상태의 배율.")]
    public float movingBoost = 1.15f;

    [Header("가장자리")]
    [Tooltip("바깥 테두리의 알파 비율. 0 이면 끝이 완전히 사라지고 1 이면 단색 부채꼴이 된다.")]
    [Range(0f, 1f)] public float edgeFalloff = 0.60f;
    [Tooltip("좌우 끝 각도의 알파 비율. 부채꼴 옆면도 같이 흐려야 빛처럼 보인다.")]
    [Range(0f, 1f)] public float sideFalloff = 0.45f;

    [Header("렌더링")]
    [Tooltip("비우면 URP 2D Sprite-Unlit 셰이더로 즉석에서 만든다.")]
    public Material material;
    [Tooltip("빛 텍스처. 비우면 정점 컬러만 쓴다. 전용 아트가 나오면 여기에 꽂는다(F-6-1).")]
    public Texture2D lightTexture;
    public string sortingLayer = "Default";
    [Tooltip("길(0)보다 위, 캐릭터(60)보다 아래여야 한다.")]
    public int sortingOrder = 5;

    SeraVision   _vision;
    GameObject   _root;
    MeshFilter   _filter;
    MeshRenderer _renderer;
    Mesh         _mesh;

    Vector3[] _verts;
    Color[]   _colors;
    Vector2[] _uvs;
    int[]     _tris;
    int       _builtSegments = -1;

    void Awake()
    {
        _vision = GetComponent<SeraVision>();
        BuildRenderer();
    }

    void OnDisable()
    {
        if (_renderer != null) _renderer.enabled = false;
    }

    void OnEnable()
    {
        if (_renderer != null) _renderer.enabled = true;
    }

    void OnDestroy()
    {
        // 세라의 자식이 아니므로 씬에 남는다. 직접 치운다.
        if (_root != null) Destroy(_root);
    }

    void BuildRenderer()
    {
        // ⚠ 세라의 자식으로 두지 않는다. 세라는 좌우를 볼 때 localScale.x 의 부호를 뒤집는데
        //   (SeraPatrol.SetFacing · CLAUDE.md §11), 자식이면 그 반전을 그대로 물려받아
        //   시야가 좌우로 뒤집힌다. 세라에 0.5625 배가 걸려 있는 것도 같이 피한다 —
        //   메시는 월드 유닛으로 그려야 SeraVision 의 거리 판정과 1:1 로 맞는다.
        //   그래서 월드 루트에 두고 위치만 따라가게 한다.
        _root = new GameObject("세라_시야빛");
        _root.transform.localScale = Vector3.one;

        _filter   = _root.AddComponent<MeshFilter>();
        _renderer = _root.AddComponent<MeshRenderer>();

        _mesh = new Mesh { name = "SeraVisionCone" };
        _mesh.MarkDynamic();
        _filter.sharedMesh = _mesh;

        _renderer.sharedMaterial   = material != null ? material : CreateDefaultMaterial();
        _renderer.sortingLayerName = sortingLayer;
        _renderer.sortingOrder     = sortingOrder;
        _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows    = false;
        _renderer.lightProbeUsage   = UnityEngine.Rendering.LightProbeUsage.Off;
    }

    Material CreateDefaultMaterial()
    {
        // URP 2D. Sprite-Unlit 은 정점 컬러를 곱하고 Light2D 의 영향을 받지 않는다 —
        // 밤 구간에서도 시야는 똑같이 보여야 하므로 Unlit 이 맞다.
        var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                  ?? Shader.Find("Sprites/Default");
        var mat = new Material(shader) { name = "SeraVisionLight (auto)" };
        if (lightTexture != null) mat.mainTexture = lightTexture;
        return mat;
    }

    void LateUpdate()
    {
        // SeraVision.Update 가 facing 을 갱신한 뒤에 그린다. 같은 프레임의 판정과 그림이
        // 어긋나면 "보이는 곳 밖인데 잡혔다" 가 된다.
        if (_vision == null || _mesh == null) return;
        UpdateMesh();
    }

    void UpdateMesh()
    {
        int seg = Mathf.Clamp(segments, 8, 64);
        if (seg != _builtSegments) Allocate(seg);

        float angle    = _vision.CurrentAngle;
        float distance = _vision.CurrentDistance;
        Vector2 facing = _vision.facing.sqrMagnitude > 0.0001f ? _vision.facing.normalized : Vector2.down;

        // 판정과 같은 원점이어야 한다 — SeraVision 은 transform.position 에서 레이캐스트한다.
        Vector3 origin = transform.position;

        // 표시 오브젝트를 세라 자리로 옮긴다. 메시 정점은 이 원점 기준의 월드 델타다.
        _root.transform.position = origin;

        // 색 — 필터에 따라 다르다 (F-6-1). 상태에 따라 세기만 바뀐다.
        bool reality = DaggerFilterController.IsRealityView;
        Color baseColor = reality ? realityColor : fantasyColor;
        float baseAlpha = reality ? realityAlpha : fantasyAlpha;
        baseAlpha *= _vision.State switch
        {
            SeraVisionState.LookingBack => lookBackBoost,
            SeraVisionState.Moving      => movingBoost,
            _                           => 1f,
        };
        baseAlpha = Mathf.Clamp01(baseAlpha);

        // 중심 정점
        _verts[0]  = Vector3.zero;
        _colors[0] = new Color(baseColor.r, baseColor.g, baseColor.b, baseAlpha);
        _uvs[0]    = new Vector2(0.5f, 0.5f);

        float half = angle * 0.5f;
        float step = seg > 1 ? angle / (seg - 1) : 0f;

        for (int i = 0; i < seg; i++)
        {
            float a = -half + step * i;
            Vector2 dir = Rotate(facing, a);

            // 판정과 같은 규칙으로 자른다 — 엄폐물에 가리면 거기서 끝난다 (F-6).
            float r = distance;
            if (_vision.obstacleMask.value != 0)
            {
                var hit = Physics2D.Raycast(origin, dir, distance, _vision.obstacleMask);
                if (hit.collider != null) r = hit.distance;
            }

            // 표시 오브젝트가 원점에 있고 스케일이 1 이므로 월드 델타를 그대로 쓴다.
            _verts[i + 1] = new Vector3(dir.x * r, dir.y * r, 0f);

            // 좌우 끝으로 갈수록 흐려진다. 빛의 경계는 선이 아니다.
            float t    = seg > 1 ? Mathf.Abs(a) / Mathf.Max(0.0001f, half) : 0f;
            float side = Mathf.Lerp(1f, sideFalloff, t * t);
            _colors[i + 1] = new Color(baseColor.r, baseColor.g, baseColor.b,
                                       baseAlpha * edgeFalloff * side);
            _uvs[i + 1] = new Vector2(0.5f + dir.x * 0.5f, 0.5f + dir.y * 0.5f);
        }

        _mesh.Clear();
        _mesh.vertices  = _verts;
        _mesh.colors    = _colors;
        _mesh.uv        = _uvs;
        _mesh.triangles = _tris;
        _mesh.RecalculateBounds();
    }

    void Allocate(int seg)
    {
        _builtSegments = seg;
        _verts  = new Vector3[seg + 1];
        _colors = new Color[seg + 1];
        _uvs    = new Vector2[seg + 1];
        _tris   = new int[(seg - 1) * 3];
        for (int i = 0; i < seg - 1; i++)
        {
            _tris[i * 3]     = 0;
            _tris[i * 3 + 1] = i + 1;
            _tris[i * 3 + 2] = i + 2;
        }
    }

    static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        float s = Mathf.Sin(r), c = Mathf.Cos(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }
}
