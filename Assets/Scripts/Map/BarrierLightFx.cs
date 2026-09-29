using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 숲 끝 결계의 빛 — S#21 에서 <see cref="ForestBarrierDirector"/> 가 부른다.
///
/// <para>
/// 결계 3상태(D-3 S#21 신규 에셋 · F-6-1)의 그림이 오기 전까지 <b>코드로 그리는 빛</b>이다.
/// 전부 도트 규격(PPU 32 · 1픽셀 = 1/32 유닛)의 단색 픽셀과 계단형 그라데이션으로 만든다.
/// 아트가 오면 디렉터의 스프라이트 슬롯이 우선이며, 이 빛은 그 위에 얹힌다.
/// </para>
///
/// <para>
/// 화려해도 되는 자리는 <b>강화(③)와 그 잔광뿐</b>이다 — 정본이 「눈이 부실 만큼 찬란한 광명」이라고 쓴다.
/// ① 평상(거의 투명 · 걷는 중 한 번 스침)과 ② 접촉(열쇠 주변만)은 정본이 절제를 못박았으므로 작게 둔다.
/// </para>
///
/// ⚠ 카메라를 건드리지 않는다. 막이 두꺼워지는 것은 스프라이트 높이로만 만든다(F-6 · CLAUDE.md §11).
/// </summary>
public class BarrierLightFx : MonoBehaviour
{
    const float PX = 1f / 32f;   // 내부 해상도 1픽셀 (CLAUDE.md §11)

    // ── 설정 (디렉터가 채운다) ──────────────────────────────────────────────
    public Color gold      = new Color(0.98f, 0.85f, 0.45f);
    public Color keyBlue   = new Color(0.45f, 0.78f, 1f);
    public float idleAlpha = 0.05f;   // ① 평상 판의 진하기 — 「눈엔 안 보이겠지만」
    public float glintAlpha = 0.35f;  // ① 걷는 중 한 번 스치는 빛의 피크
    public float curtainPeakHeight = 2.6f;   // ③ 두꺼워지는 막의 피크 높이(유닛)
    public float curtainRestHeight = 1.5f;   // 잔광에서 막의 높이
    public float curtainAlpha      = 0.75f;

    // ── 내부 ───────────────────────────────────────────────────────────────
    Bounds _bounds;
    int _layerId, _order;
    Material _mat;
    Sprite _px, _vgrad, _hgrad, _radial;

    SpriteRenderer _plate, _curtain, _curtainCore, _touch;
    readonly List<SpriteRenderer> _pillars = new List<SpriteRenderer>();
    readonly List<SpriteRenderer> _windows = new List<SpriteRenderer>();
    readonly List<Mote> _motes = new List<Mote>();

    float _plateAlpha;
    float _curtainHeight, _curtainA;
    float _pillarGain;      // 0~1 — 빛기둥 세기
    float _moteRate;        // 초당 빛 가루 수
    float _shimmer;         // 잔광 반짝임 세기 0~1
    float _moteAcc;
    float _minX, _maxX;     // 효과를 그리는 가로 범위(화면 안쪽 ∩ 결계)
    Color _moteTint;

    class Mote
    {
        public SpriteRenderer sr;
        public Vector2 pos, vel;
        public float life, age, phase;
        public Color color;
    }

    public static BarrierLightFx Create(Transform barrier, Bounds bounds, int sortingLayerId, int sortingOrder)
    {
        var go = new GameObject("S21 BarrierLight [Auto]");
        go.transform.SetParent(barrier.parent, false);
        var fx = go.AddComponent<BarrierLightFx>();
        fx._bounds  = bounds;
        fx._layerId = sortingLayerId;
        fx._order   = sortingOrder;
        fx.Build();
        return fx;
    }

    float Top => _bounds.max.y;

    void Build()
    {
        // 언릿 — 빛이 2D 조명에 다시 곱해지면 황금색이 죽는다. 선례: LuShadow · SeraVisionDisplay.
        var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                  ?? Shader.Find("Sprites/Default");
        _mat = new Material(shader);

        _px     = MakeSprite(1, 1, (x, y) => 1f, new Vector2(0.5f, 0.5f));
        // 아래가 밝고 위로 사라지는 계단형 세로 그라데이션(8단) — 도트 느낌을 살린다.
        _vgrad  = MakeSprite(1, 32, (x, y) => Mathf.Floor((1f - y / 31f) * 8f) / 8f, new Vector2(0.5f, 0f));
        // 가운데가 밝은 가로 띠(스침용)
        _hgrad  = MakeSprite(32, 1, (x, y) => Mathf.Floor((1f - Mathf.Abs(x - 15.5f) / 15.5f) * 6f) / 6f, new Vector2(0.5f, 0.5f));
        // 둥근 빛(접촉 자리) — 계단형 동심원
        _radial = MakeSprite(32, 32, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f)) / 15.5f;
            return Mathf.Floor(Mathf.Clamp01(1f - d) * 5f) / 5f;
        }, new Vector2(0.5f, 0.5f));

        RefreshRange();

        // 판 — 평상에는 거의 보이지 않는다
        _plate = NewRenderer("Plate", _px, 0);
        _plate.transform.position = new Vector3(_bounds.center.x, _bounds.center.y, 0f);
        _plate.transform.localScale = new Vector3(_bounds.size.x / PX, _bounds.size.y / PX, 1f);
        _plateAlpha = idleAlpha;

        _curtain     = NewRenderer("Curtain", _vgrad, 2);
        _curtainCore = NewRenderer("CurtainCore", _vgrad, 3);
        _touch       = NewRenderer("Touch", _radial, 4);
        _touch.enabled = false;

        var rng = new System.Random(21);
        for (int i = 0; i < 9; i++)
        {
            var p = NewRenderer("Pillar" + i, _vgrad, 5);
            float x = Mathf.Lerp(_minX, _maxX, (i + 0.5f) / 9f) + (float)(rng.NextDouble() - 0.5) * 0.8f;
            p.transform.position = new Vector3(Snap(x), Top, 0f);
            p.enabled = false;
            _pillars.Add(p);
        }

        ApplyCurtain();
    }

    /// <summary>화면 안쪽만 그린다. 결계 판은 화면보다 훨씬 길다(26.9 유닛).</summary>
    void RefreshRange()
    {
        _minX = _bounds.min.x; _maxX = _bounds.max.x;
        var cam = Camera.main;
        if (cam != null && cam.orthographic)
        {
            float half = cam.orthographicSize * cam.aspect + 1f;
            _minX = Mathf.Max(_minX, cam.transform.position.x - half);
            _maxX = Mathf.Min(_maxX, cam.transform.position.x + half);
            if (_maxX <= _minX) { _minX = _bounds.min.x; _maxX = _bounds.max.x; }
        }
    }

    // ═══ ① 평상 — 걷는 중 한 번 스친다 ═════════════════════════════════════
    /// <summary>
    /// 황금색이 판을 따라 한 번 언뜻 지나간다. 강조하지 않는다 — 플레이어만 본다(정본 1256 · 1261).
    /// </summary>
    public IEnumerator Glint(float duration)
    {
        RefreshRange();
        var band = NewRenderer("Glint", _hgrad, 1);
        band.transform.localScale = new Vector3(2.2f / (32 * PX), _bounds.size.y / PX, 1f);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float x = Mathf.Lerp(_minX - 1f, _maxX + 1f, k);
            band.transform.position = new Vector3(Snap(x), _bounds.center.y, 0f);
            band.color = WithA(gold, glintAlpha * Mathf.Sin(k * Mathf.PI));
            yield return null;
        }
        Destroy(band.gameObject);
    }

    // ═══ ② 접촉 — 열쇠 주변만 드러난다 · 푸른 창 ═══════════════════════════
    /// <summary>열쇠를 댄 자리 주변만 황금빛으로 드러난다. 범위와 밝기를 크게 쓰지 않는다(정본 1266).</summary>
    public void ShowTouch(Vector2 contact)
    {
        _touch.enabled = true;
        _touch.transform.position = new Vector3(Snap(contact.x), Snap(contact.y), 0f);
        StartCoroutine(TouchPulse());
    }

    IEnumerator TouchPulse()
    {
        float t = 0f;
        while (_touch != null && _touch.enabled)
        {
            t += Time.deltaTime;
            float grow = Mathf.Clamp01(t / 0.4f);
            float s = Mathf.Lerp(0.4f, 1.6f, grow) / (32 * PX);
            _touch.transform.localScale = new Vector3(s * 1.4f, s, 1f);
            _touch.color = WithA(gold, (0.35f + 0.12f * Mathf.Sin(t * 5f)) * grow);
            yield return null;
        }
    }

    /// <summary>
    /// 열쇠에서 푸른빛이 흘러나와 창들이 뜬다. <b>내용을 판독 가능하게 그리지 않는다</b>(정본 1255) —
    /// 글줄처럼 보이는 가로 픽셀뿐이다.
    /// </summary>
    public void OpenKeyWindows(Vector2 contact)
    {
        Vector2[] offs  = { new Vector2(-0.55f, 0.55f), new Vector2(0.45f, 0.75f), new Vector2(-0.15f, 1.05f), new Vector2(0.7f, 0.3f) };
        Vector2Int[] sz = { new Vector2Int(14, 9), new Vector2Int(11, 8), new Vector2Int(9, 6), new Vector2Int(8, 6) };
        for (int i = 0; i < offs.Length; i++)
        {
            var sp = MakeWindowSprite(sz[i].x, sz[i].y, i);
            var w = NewRenderer("KeyWindow" + i, sp, 6);
            w.transform.position = new Vector3(Snap(contact.x + offs[i].x), Snap(contact.y + offs[i].y), 0f);
            w.color = WithA(Color.white, 0f);
            _windows.Add(w);
            StartCoroutine(WindowLife(w, i * 0.12f));
        }
    }

    IEnumerator WindowLife(SpriteRenderer w, float delay)
    {
        yield return new WaitForSeconds(delay);
        float t = 0f;
        Vector3 basePos = w.transform.position;
        while (w != null && _windows.Contains(w))
        {
            t += Time.deltaTime;
            float open = Mathf.Clamp01(t / 0.2f);
            w.transform.localScale = new Vector3(1f, Mathf.Max(PX, open), 1f);
            float flicker = (Mathf.Repeat(t * 7.3f + delay * 10f, 1f) < 0.08f) ? 0.55f : 0.85f;
            w.color = WithA(Color.white, open * flicker);
            w.transform.position = basePos + new Vector3(0f, Snap(Mathf.Sin(t * 1.7f + delay * 9f) * 0.05f), 0f);
            yield return null;
        }
    }

    /// <summary>② → ③ 사이 — 결계가 푸른빛을 전부 튕겨낸다. 창이 부서져 흩어진다.</summary>
    public void RepelKeyLight(Vector2 contact)
    {
        foreach (var w in _windows)
        {
            if (w == null) continue;
            Vector2 away = ((Vector2)w.transform.position - contact).normalized;
            if (away.sqrMagnitude < 0.01f) away = Vector2.up;
            for (int i = 0; i < 10; i++)
            {
                Vector2 v = (away + Random.insideUnitCircle * 0.8f).normalized * Random.Range(2.5f, 5f);
                SpawnMote((Vector2)w.transform.position + Random.insideUnitCircle * 0.2f, v, Random.Range(0.35f, 0.7f),
                          Color.Lerp(keyBlue, Color.white, Random.value * 0.4f), Random.value < 0.3f ? 2 : 1);
            }
            Destroy(w.gameObject);
        }
        _windows.Clear();
        // 접촉 빛은 황금빛에 삼켜진다 — 한 번 크게 번쩍이고 판 전체로 넘어간다
        _touch.enabled = false;
    }

    // ═══ ③ 강화 ════════════════════════════════════════════════════════════
    /// <summary>결계가 황금색으로 확실하게 눈에 들어온다 — 판 전체가 드러난다.</summary>
    public IEnumerator RevealPlate(float to, float duration)
    {
        float from = _plateAlpha, t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            _plateAlpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }
        _plateAlpha = to;
    }

    /// <summary>
    /// 「눈이 부실 만큼 찬란한 광명이 되어 두꺼워지기 시작한다.」 막이 솟고, 빛기둥이 서고, 빛 가루가 오른다.
    /// 끝나면 잔광으로 내려앉아 데모가 끝날 때까지 반짝인다.
    /// </summary>
    public IEnumerator Surge(float duration)
    {
        RefreshRange();
        foreach (var p in _pillars) p.enabled = true;
        _moteTint = Color.white;

        // 터지는 순간의 빛 가루 한 무더기
        for (int i = 0; i < 70; i++)
        {
            float x = Random.Range(_minX, _maxX);
            SpawnMote(new Vector2(x, Top + Random.Range(0f, 0.3f)),
                      new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(1.5f, 4.2f)),
                      Random.Range(0.8f, 2.0f), Color.Lerp(gold, Color.white, Random.value * 0.7f), Random.value < 0.25f ? 2 : 1);
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float rise = 1f - Mathf.Pow(1f - k, 3f);             // 빠르게 솟았다가 느려진다
            _curtainHeight = Mathf.Lerp(0.2f, curtainPeakHeight, rise);
            _curtainA      = Mathf.Lerp(0.3f, 1f, rise);
            _plateAlpha    = Mathf.Lerp(_plateAlpha, 0.95f, k);
            _pillarGain    = rise;
            _moteRate      = Mathf.Lerp(60f, 30f, k);
            yield return null;
        }
    }

    /// <summary>피크에서 잔광으로 내려앉는다. 이후 대사가 그 위에 뜬다 — 글자를 가리지 않는 밝기로 둔다.</summary>
    public IEnumerator Settle(float duration)
    {
        float h0 = _curtainHeight, a0 = _curtainA, p0 = _plateAlpha, g0 = _pillarGain, r0 = _moteRate;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            _curtainHeight = Mathf.Lerp(h0, curtainRestHeight, k);
            _curtainA      = Mathf.Lerp(a0, curtainAlpha, k);
            _plateAlpha    = Mathf.Lerp(p0, 0.7f, k);
            _pillarGain    = Mathf.Lerp(g0, 0.45f, k);
            _moteRate      = Mathf.Lerp(r0, 9f, k);
            _shimmer       = k;
            yield return null;
        }
        _moteTint = gold;
    }

    /// <summary>끝 — 황금빛이 서서히 가라앉는다. 암전 전에 부른다.</summary>
    public IEnumerator Sink(float duration)
    {
        float h0 = _curtainHeight, a0 = _curtainA, p0 = _plateAlpha, g0 = _pillarGain, r0 = _moteRate;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            _curtainHeight = Mathf.Lerp(h0, h0 * 0.35f, k);
            _curtainA      = Mathf.Lerp(a0, 0.15f, k);
            _plateAlpha    = Mathf.Lerp(p0, 0.35f, k);
            _pillarGain    = Mathf.Lerp(g0, 0f, k);
            _moteRate      = Mathf.Lerp(r0, 2f, k);
            yield return null;
        }
    }

    // ═══ 매 프레임 ═════════════════════════════════════════════════════════
    void Update()
    {
        float time = Time.time;

        // 판
        float plateShimmer = _shimmer * 0.12f * Mathf.Sin(time * 2.1f);
        _plate.color = WithA(Color.Lerp(gold, Color.white, _plateAlpha > 0.8f ? 0.35f : 0f),
                             Mathf.Clamp01(_plateAlpha + plateShimmer));

        ApplyCurtain();

        // 빛기둥 — 저마다 다른 박자로 깜빡인다
        for (int i = 0; i < _pillars.Count; i++)
        {
            var p = _pillars[i];
            if (!p.enabled) continue;
            float flick = 0.55f + 0.45f * Mathf.Sin(time * (2.3f + i * 0.61f) + i * 1.7f);
            float h = _curtainHeight * (1.2f + 0.9f * Mathf.Abs(Mathf.Sin(i * 2.39f))) * (0.8f + 0.2f * flick);
            float w = (i % 3 == 0 ? 3 : 2);
            p.transform.localScale = new Vector3(w, Snap(h) / (32 * PX), 1f);
            p.color = WithA(Color.Lerp(gold, Color.white, 0.5f), _pillarGain * flick * 0.8f);
        }

        // 빛 가루
        if (_moteRate > 0f && _maxX > _minX)
        {
            _moteAcc += _moteRate * Time.deltaTime;
            while (_moteAcc >= 1f)
            {
                _moteAcc -= 1f;
                SpawnMote(new Vector2(Random.Range(_minX, _maxX), Top + Random.Range(0f, 0.2f)),
                          new Vector2(Random.Range(-0.15f, 0.15f), Random.Range(0.5f, 1.4f)),
                          Random.Range(1.2f, 2.8f), Color.Lerp(gold, _moteTint, Random.value * 0.6f), Random.value < 0.2f ? 2 : 1);
            }
        }
        for (int i = _motes.Count - 1; i >= 0; i--)
        {
            var m = _motes[i];
            m.age += Time.deltaTime;
            if (m.age >= m.life) { Destroy(m.sr.gameObject); _motes.RemoveAt(i); continue; }
            m.vel *= 1f - 0.9f * Time.deltaTime;             // 공기 저항 — 터진 조각이 멈춰 떠오른다
            m.vel.y += 0.25f * Time.deltaTime;
            m.pos += m.vel * Time.deltaTime;
            float sway = Mathf.Sin(m.age * 3f + m.phase) * 0.08f;
            m.sr.transform.position = new Vector3(Snap(m.pos.x + sway), Snap(m.pos.y), 0f);
            float k = m.age / m.life;
            float twinkle = (Mathf.Repeat(m.age * 6f + m.phase, 1f) < 0.15f) ? 1.3f : 1f;
            m.sr.color = WithA(m.color, Mathf.Clamp01((1f - k * k) * twinkle));
        }
    }

    void ApplyCurtain()
    {
        float width = Mathf.Max(PX, _maxX - _minX);
        float cx = (_minX + _maxX) * 0.5f;
        float h = Mathf.Max(0f, _curtainHeight);
        _curtain.enabled = _curtainA > 0.001f && h > PX;
        _curtainCore.enabled = _curtain.enabled;
        if (!_curtain.enabled) return;

        // 막 — 잔광에서는 느린 파도처럼 높이가 출렁인다
        float wave = _shimmer * 0.15f * Mathf.Sin(Time.time * 1.3f);
        _curtain.transform.position = new Vector3(Snap(cx), Snap(Top - PX * 4), 0f);
        _curtain.transform.localScale = new Vector3(width / PX, Snap(h + wave) / (32 * PX), 1f);
        _curtain.color = WithA(gold, _curtainA * 0.8f);

        // 속 — 낮고 흰빛에 가까운 층. 두께감을 만든다
        _curtainCore.transform.position = _curtain.transform.position;
        _curtainCore.transform.localScale = new Vector3(width / PX, Snap((h + wave) * 0.4f) / (32 * PX), 1f);
        _curtainCore.color = WithA(Color.Lerp(gold, Color.white, 0.6f), _curtainA * 0.7f);
    }

    void SpawnMote(Vector2 pos, Vector2 vel, float life, Color color, int sizePx)
    {
        if (_motes.Count > 260) return;
        var sr = NewRenderer("Mote", _px, 7);
        sr.transform.localScale = new Vector3(sizePx, sizePx, 1f);
        _motes.Add(new Mote { sr = sr, pos = pos, vel = vel, life = life, color = color, phase = Random.value * 10f });
        sr.transform.position = new Vector3(Snap(pos.x), Snap(pos.y), 0f);
        sr.color = WithA(color, 0f);
    }

    // ═══ 도구 ══════════════════════════════════════════════════════════════
    SpriteRenderer NewRenderer(string name, Sprite sprite, int orderOffset)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sharedMaterial = _mat;
        sr.sortingLayerID = _layerId;
        sr.sortingOrder = _order + orderOffset;
        return sr;
    }

    static Sprite MakeSprite(int w, int h, System.Func<int, int, float> alpha, Vector2 pivot)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha(x, y)));
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), pivot, 32f);
    }

    /// <summary>푸른 창 한 장 — 테두리와 글줄처럼 보이는 가로 픽셀. 읽을 수 있는 글자는 없다.</summary>
    Sprite MakeWindowSprite(int w, int h, int seed)
    {
        var rng = new System.Random(seed * 31 + 7);
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[w * h];
        Color fill = WithA(keyBlue, 0.28f);
        Color edge = WithA(Color.Lerp(keyBlue, Color.white, 0.5f), 0.95f);
        Color line = WithA(Color.Lerp(keyBlue, Color.white, 0.3f), 0.7f);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool border = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                Color c = border ? edge : fill;
                // 위에서 한 칸 띄우고 두 칸 간격으로 글줄 — 길이는 제멋대로
                if (!border && y < h - 2 && (h - 2 - y) % 2 == 1 && x >= 2)
                {
                    int len = 2 + rng.Next(w - 4);
                    if (x < 2 + len) c = line;
                }
                px[y * w + x] = c;
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 32f);
    }

    static float Snap(float v) => Mathf.Round(v / PX) * PX;
    static Color WithA(Color c, float a) { c.a = a; return c; }

    // ⚠ OnDestroy 에서 런타임 머티리얼·텍스처를 파괴하지 않는다. 씬과 함께 내려가며, 데모 끝에 한 번 생기는 것이라
    //   남는 양이 작다. 이 오브젝트를 직접 Destroy 한 프레임에 씬을 넘기면 멈췄던 일이 있어(ForestBarrierDirector.EndDemo)
    //   파괴 경로를 늘리지 않는다.
}
