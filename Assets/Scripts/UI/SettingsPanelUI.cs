using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 설정 패널 — DontDestroyOnLoad 자동 생성 싱글톤.
/// Inspector 연결 없이 SettingsPanelUI.Show() / Hide() 만으로 사용.
/// </summary>
public class SettingsPanelUI : MonoBehaviour
{
    public static SettingsPanelUI Instance
    {
        get
        {
            if (_instance == null) CreateInstance();
            return _instance;
        }
    }
    static SettingsPanelUI _instance;

    public static bool IsOpen { get; private set; }

    CanvasGroup _cg;
    Slider _volumeSlider;

    // ─── 자동 생성 ────────────────────────────────────────────────
    static void CreateInstance()
    {
        var root = new GameObject("SettingsPanelUI [Auto]");
        DontDestroyOnLoad(root);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 99;
        root.AddComponent<CanvasScaler>();
        root.AddComponent<GraphicRaycaster>();

        _instance     = root.AddComponent<SettingsPanelUI>();
        _instance._cg = root.AddComponent<CanvasGroup>();
        _instance._cg.alpha = 0f;
        _instance._cg.blocksRaycasts = false;
        root.SetActive(false);

        _instance.BuildUI(root.transform);
    }

    void BuildUI(Transform root)
    {
        // 어두운 배경
        var bg = MakeImage(root, "Dimmer", new Color(0f, 0f, 0f, 0.75f));
        Stretch(bg.rectTransform);

        // 중앙 패널
        var panel = MakeImage(root, "Panel", new Color(0.12f, 0.12f, 0.12f, 1f));
        var pr = panel.rectTransform;
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
        pr.sizeDelta = new Vector2(480f, 360f);
        pr.anchoredPosition = Vector2.zero;

        var pt = panel.transform;

        // 제목
        MakeText(pt, "설정", 40, new Vector2(0f, 135f), new Vector2(380f, 55f));

        // 볼륨
        MakeText(pt, "마스터 볼륨", 22, new Vector2(-95f, 65f), new Vector2(160f, 36f));
        _volumeSlider = MakeSlider(pt, new Vector2(85f, 65f), new Vector2(210f, 28f),
            SettingsManager.Instance?.masterVolume ?? 1f);
        _volumeSlider.onValueChanged.AddListener(v => SettingsManager.Instance?.SetMasterVolume(v));

        // 구분선
        var line = MakeImage(pt, "Divider", new Color(0.35f, 0.35f, 0.35f, 1f));
        line.rectTransform.anchoredPosition = new Vector2(0f, 15f);
        line.rectTransform.sizeDelta        = new Vector2(400f, 2f);

        // 언어
        MakeText(pt, "언어", 22, new Vector2(0f, -20f), new Vector2(380f, 36f));
        MakeButton(pt, "한국어", new Vector2(-140f, -70f), new Vector2(110f, 46f),
            () => SettingsManager.Instance?.SetLanguage(LocalizationManager.Language.KO));
        MakeButton(pt, "English", new Vector2(0f,    -70f), new Vector2(110f, 46f),
            () => SettingsManager.Instance?.SetLanguage(LocalizationManager.Language.EN));
        MakeButton(pt, "日本語", new Vector2( 140f,  -70f), new Vector2(110f, 46f),
            () => SettingsManager.Instance?.SetLanguage(LocalizationManager.Language.JP));

        // 닫기 버튼
        var closeBtn = MakeButton(pt, "닫기", new Vector2(0f, -148f), new Vector2(160f, 46f), Hide);
        closeBtn.GetComponent<Image>().color = new Color(0.32f, 0.18f, 0.18f, 1f);
    }

    // ─── 공개 API ─────────────────────────────────────────────────
    public static void Show()
    {
        var inst = Instance;
        if (IsOpen) return;
        IsOpen = true;
        inst.gameObject.SetActive(true);
        if (inst._volumeSlider != null && SettingsManager.Instance != null)
            inst._volumeSlider.value = SettingsManager.Instance.masterVolume;
        inst.StartCoroutine(inst.FadeIn());
    }

    public static void Hide()
    {
        if (!IsOpen || _instance == null) return;
        IsOpen = false;
        _instance._cg.alpha = 0f;
        _instance._cg.blocksRaycasts = false;
        _instance.gameObject.SetActive(false);
    }

    IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < 0.15f)
        {
            t += Time.unscaledDeltaTime;
            _cg.alpha = Mathf.Clamp01(t / 0.15f);
            yield return null;
        }
        _cg.alpha = 1f;
        _cg.blocksRaycasts = true;
    }

    // ─── UI 헬퍼 ──────────────────────────────────────────────────
    static Image MakeImage(Transform p, string name, Color col)
    {
        var go   = new GameObject(name); go.transform.SetParent(p, false);
        var img  = go.AddComponent<Image>(); img.color = col;
        return img;
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    static Text MakeText(Transform p, string text, int size, Vector2 pos, Vector2 sz)
    {
        var go  = new GameObject("Txt_" + text); go.transform.SetParent(p, false);
        var txt = go.AddComponent<Text>();
        txt.text = text; txt.fontSize = size; txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var r = go.GetComponent<RectTransform>();
        r.anchoredPosition = pos; r.sizeDelta = sz;
        return txt;
    }

    static Button MakeButton(Transform p, string label, Vector2 pos, Vector2 sz,
        System.Action onClick)
    {
        var go  = new GameObject("Btn_" + label); go.transform.SetParent(p, false);
        var img = go.AddComponent<Image>(); img.color = new Color(0.22f, 0.22f, 0.22f, 1f);
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick());
        var r = go.GetComponent<RectTransform>(); r.anchoredPosition = pos; r.sizeDelta = sz;

        var lt = MakeText(go.transform, label, 22, Vector2.zero, sz);
        Stretch(lt.GetComponent<RectTransform>());
        return btn;
    }

    static Slider MakeSlider(Transform p, Vector2 pos, Vector2 sz, float value)
    {
        var go = new GameObject("Slider"); go.transform.SetParent(p, false);
        var r = go.GetComponent<RectTransform>(); r.anchoredPosition = pos; r.sizeDelta = sz;

        // 배경
        var bgImg = MakeImage(go.transform, "Background", new Color(0.3f, 0.3f, 0.3f));
        Stretch(bgImg.rectTransform);

        // Fill Area
        var fa = new GameObject("Fill Area"); fa.transform.SetParent(go.transform, false);
        var far = fa.AddComponent<RectTransform>();
        far.anchorMin = new Vector2(0f, 0.25f); far.anchorMax = new Vector2(1f, 0.75f);
        far.offsetMin = new Vector2(5f, 0f);    far.offsetMax = new Vector2(-15f, 0f);

        var fillImg = MakeImage(fa.transform, "Fill", new Color(0.35f, 0.65f, 1f));
        var fillR   = fillImg.rectTransform;
        fillR.anchorMin = Vector2.zero; fillR.anchorMax = Vector2.one;
        fillR.offsetMin = fillR.offsetMax = Vector2.zero;

        // Handle Slide Area
        var ha = new GameObject("Handle Slide Area"); ha.transform.SetParent(go.transform, false);
        var har = ha.AddComponent<RectTransform>();
        har.anchorMin = Vector2.zero; har.anchorMax = Vector2.one;
        har.offsetMin = new Vector2(10f, 0f); har.offsetMax = new Vector2(-10f, 0f);

        var handleImg = MakeImage(ha.transform, "Handle", Color.white);
        var handleR   = handleImg.rectTransform;
        handleR.anchorMin = new Vector2(0f, 0f); handleR.anchorMax = new Vector2(0f, 1f);
        handleR.sizeDelta = new Vector2(20f, 0f);

        var slider           = go.AddComponent<Slider>();
        slider.fillRect      = fillR;
        slider.handleRect    = handleR;
        slider.targetGraphic = handleImg;
        slider.direction     = Slider.Direction.LeftToRight;
        slider.minValue      = 0f;
        slider.maxValue      = 1f;
        slider.value         = value;
        return slider;
    }
}
