using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전신도 — 아이템 탭 왼쪽에 상시 표시되는 루의 전신 그림 (F-8-2 / C-16-2).
/// 몸체 · 의상 · 무기 <b>3레이어를 런타임에 겹쳐</b> 만든다.
///
/// 몸체는 인형화 단계로 고른다. <b>경계값을 여기에 적지 않고</b>
/// <see cref="CorruptionManager.GetStage"/> 를 부른다 (CLAUDE.md §2 단일 출처).
/// 데모 범위는 0~30 / 31~60 두 종만 실제로 연결하고 나머지는 폴백한다.
///
/// ⚠ <b>인형화 수치·단계명·게이지를 어떤 형태로도 내보내지 않는다</b>(F-8-4 · C-11).
/// 스프라이트를 고르는 데만 쓰고, 글자·숫자·색으로 드러내지 않는다.
/// 아트가 아직 없으므로 스프라이트가 비면 <b>단색 실루엣 플레이스홀더</b>를 그린다 —
/// 단계에 따라 색을 바꾸지 않는다(간접 표기 금지).
///
/// 무기 레이어는 슬롯이 아니라 <see cref="DaggerSystem.IsEquipped"/> 를 본다 (E-38).
/// 음식·열쇠·재료는 무시한다 (C-16-2).
/// </summary>
public class FullBodyView : MonoBehaviour
{
    // 아트가 들어오면 이 경로에 넣기만 하면 된다. 없으면 null → 플레이스홀더.
    const string BodyPathPrefix     = "UI/FullBody/Body_";
    const string ClothingPathPrefix = "UI/FullBody/Clothing_";
    const string WeaponDaggerPath   = "UI/FullBody/Weapon_Dagger";

    static readonly Color Silhouette = new Color(0.38f, 0.38f, 0.42f, 1f);
    static readonly Color ClothingCol = new Color(0.28f, 0.32f, 0.40f, 1f);
    static readonly Color WeaponCol   = new Color(0.62f, 0.62f, 0.66f, 1f);
    static readonly Color FrameCol    = new Color(0.14f, 0.14f, 0.17f, 1f);

    RectTransform _rt;
    Image _body, _clothing, _weapon;
    GameObject _bodyPlaceholder, _clothingPlaceholder, _weaponPlaceholder;

    // AddComponent 는 Build() 보다 OnEnable 을 먼저 부른다. 그 사이 Refresh 가 돌면 안 된다.
    bool _built;

    /// <summary>부모 밑에 전신도를 만든다. 위치·크기는 호출자가 정한다.</summary>
    public static FullBodyView Create(Transform parent, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject("FullBodyView");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        var frame = go.AddComponent<Image>();
        frame.color = FrameCol;

        var view = go.AddComponent<FullBodyView>();
        view._rt = rt;
        view.Build();
        return view;
    }

    void Build()
    {
        _body     = MakeLayer("BodyLayer");
        _clothing = MakeLayer("ClothingLayer");
        _weapon   = MakeLayer("WeaponLayer");

        // 스프라이트가 없을 때 그릴 실루엣 (몸통 기준 비율)
        _bodyPlaceholder     = MakeSilhouette("BodyShape", Silhouette);
        _clothingPlaceholder = MakeClothingShape();
        _weaponPlaceholder   = MakeWeaponShape();

        _built = true;
        Refresh();
    }

    Image MakeLayer(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var img = go.AddComponent<Image>();
        img.preserveAspect = true;
        img.raycastTarget  = false;
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax  = Vector2.zero;
        img.enabled = false;
        return img;
    }

    // ── 플레이스홀더 도형 ─────────────────────────────────────────────────
    GameObject MakeSilhouette(string name, Color col)
    {
        var root = new GameObject(name);
        root.transform.SetParent(transform, false);
        var rr = root.AddComponent<RectTransform>();
        rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one;
        rr.offsetMin = rr.offsetMax = Vector2.zero;

        //           앵커(비율)                        크기 보정
        Box(root, "Head",      0.42f, 0.72f, 0.58f, 0.92f, col);
        Box(root, "Torso",     0.36f, 0.40f, 0.64f, 0.71f, col);
        Box(root, "ArmLeft",   0.28f, 0.42f, 0.35f, 0.70f, col);
        Box(root, "ArmRight",  0.65f, 0.42f, 0.72f, 0.70f, col);
        Box(root, "LegLeft",   0.40f, 0.08f, 0.48f, 0.39f, col);
        Box(root, "LegRight",  0.52f, 0.08f, 0.60f, 0.39f, col);
        return root;
    }

    GameObject MakeClothingShape()
    {
        var root = new GameObject("ClothingShape");
        root.transform.SetParent(transform, false);
        var rr = root.AddComponent<RectTransform>();
        rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one;
        rr.offsetMin = rr.offsetMax = Vector2.zero;

        Box(root, "Coat",     0.33f, 0.34f, 0.67f, 0.70f, ClothingCol);
        Box(root, "SleeveL",  0.27f, 0.44f, 0.35f, 0.68f, ClothingCol);
        Box(root, "SleeveR",  0.65f, 0.44f, 0.73f, 0.68f, ClothingCol);
        root.SetActive(false);
        return root;
    }

    GameObject MakeWeaponShape()
    {
        var root = new GameObject("WeaponShape");
        root.transform.SetParent(transform, false);
        var rr = root.AddComponent<RectTransform>();
        rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one;
        rr.offsetMin = rr.offsetMax = Vector2.zero;

        // 오른손 언저리에 짧은 날 하나
        Box(root, "Blade", 0.70f, 0.36f, 0.735f, 0.52f, WeaponCol);
        Box(root, "Grip",  0.685f, 0.32f, 0.75f, 0.365f, new Color(0.35f, 0.30f, 0.26f, 1f));
        root.SetActive(false);
        return root;
    }

    static void Box(GameObject parent, string name, float x0, float y0, float x1, float y1, Color col)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var img = go.AddComponent<Image>();
        img.color = col;
        img.raycastTarget = false;
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(x0, y0);
        r.anchorMax = new Vector2(x1, y1);
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    // ── 갱신 ──────────────────────────────────────────────────────────────
    void OnEnable()
    {
        var eq = EquipmentManager.Instance;
        if (eq != null) eq.OnEquipmentChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        var eq = EquipmentManager.Instance;
        if (eq != null) eq.OnEquipmentChanged -= Refresh;
    }

    public void Refresh()
    {
        if (!_built) return;
        RefreshBody();
        RefreshClothing();
        RefreshWeapon();
    }

    void RefreshBody()
    {
        var stage = CurrentStage();
        var sprite = Resources.Load<Sprite>(BodyPathPrefix + stage);

        // 데모는 0~30 / 31~60 두 종만 연결한다. 나머지 단계는 있는 것으로 폴백한다.
        if (sprite == null && stage != CorruptionStage.Autonomy)
            sprite = Resources.Load<Sprite>(BodyPathPrefix + CorruptionStage.Crack);
        if (sprite == null)
            sprite = Resources.Load<Sprite>(BodyPathPrefix + CorruptionStage.Autonomy);

        _body.sprite  = sprite;
        _body.enabled = sprite != null;
        // 아트가 없으면 단색 실루엣. 단계에 따라 색을 바꾸지 않는다(F-8-4).
        _bodyPlaceholder.SetActive(sprite == null);
    }

    void RefreshClothing()
    {
        var eq   = EquipmentManager.Instance;
        var item = eq != null ? eq.Clothing : null;

        if (item == null)
        {
            _clothing.enabled = false;
            _clothing.sprite  = null;
            _clothingPlaceholder.SetActive(false);
            return;
        }

        var sprite = Resources.Load<Sprite>(ClothingPathPrefix + item.name);
        _clothing.sprite  = sprite;
        _clothing.enabled = sprite != null;
        _clothingPlaceholder.SetActive(sprite == null);
    }

    void RefreshWeapon()
    {
        // E-38: 파지 판정은 슬롯이 아니라 단검 기준이다.
        bool holding = DaggerSystem.IsEquipped;
        if (!holding)
        {
            _weapon.enabled = false;
            _weapon.sprite  = null;
            _weaponPlaceholder.SetActive(false);
            return;
        }

        var sprite = Resources.Load<Sprite>(WeaponDaggerPath);
        _weapon.sprite  = sprite;
        _weapon.enabled = sprite != null;
        _weaponPlaceholder.SetActive(sprite == null);
    }

    static CorruptionStage CurrentStage()
    {
        var cm = CorruptionManager.Instance;
        // 경계값을 여기에 적지 않는다. 없으면 가장 낮은 단계로 둔다.
        return cm != null ? CorruptionManager.GetStage(cm.currentCorruption)
                          : CorruptionStage.Autonomy;
    }
}
