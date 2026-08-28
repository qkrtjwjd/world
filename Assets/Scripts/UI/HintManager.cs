using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼 힌트 표시 매니저 (DontDestroyOnLoad 자동 생성).
///
/// 사용처:
///   - 조작법 안내 힌트 → <see cref="ShowHint(string, string, float)"/>
///   - 같은 id는 게임 통산 1회만 표시 (PlayerPrefs "Hint_Shown_{id}" 기록)
///   - 설정: showTutorialHints = false 이면 표시하지 않음 (기록도 하지 않음)
///
/// Inspector 배치 불필요. 씬에서 <c>HintManager.ShowHint("interact_key", "...")</c> 로 호출.
/// </summary>
public class HintManager : MonoBehaviour
{
    // ── 싱글톤 ──────────────────────────────────────────────────────────────
    public static HintManager Instance
    {
        get
        {
            if (_instance == null) CreateInstance();
            return _instance;
        }
    }
    static HintManager _instance;

    const string PrefsPrefix = "Hint_Shown_";

    static readonly Color PanelBg = new Color(0.10f, 0.10f, 0.12f, 0.85f);
    static readonly Color IconCol = new Color(1.00f, 0.85f, 0.40f);

    // ── 내부 상태 ────────────────────────────────────────────────────────────
    CanvasGroup _group;
    TMP_Text    _bodyText;
    Coroutine   _activeCoroutine;

    // ── 자동 생성 ────────────────────────────────────────────────────────────
    static void CreateInstance()
    {
        var root = new GameObject("HintManager [Auto]");
        DontDestroyOnLoad(root);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 94;   // 게임 UI 위, 테두리 효과(95) 아래
        UiCanvasScale.Add(root);   // 640x360 Expand — 단일 출처

        _instance = root.AddComponent<HintManager>();
        _instance.BuildUI(root.transform);
        SettingsManager.OnShowTutorialHintsChanged += _instance.OnSettingChanged;
    }

    void BuildUI(Transform parent)
    {
        // 좌하단 힌트 패널 (우상단=목표 UI, 하단 중앙=대화 패널과 충돌 회피)
        var panel = new GameObject("HintPanel");
        panel.transform.SetParent(parent, false);
        var bg = panel.AddComponent<Image>();
        bg.color = PanelBg;
        bg.raycastTarget = false;

        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot     = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(24f, 24f);
        rt.sizeDelta = new Vector2(420f, 64f);

        _group = panel.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;

        // 💡 아이콘
        var iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(panel.transform, false);
        var icon = iconGo.AddComponent<TextMeshProUGUI>();
        icon.text      = "!";
        icon.fontSize  = 26f;
        icon.fontStyle = FontStyles.Bold;
        icon.color     = IconCol;
        icon.alignment = TextAlignmentOptions.Center;
        icon.raycastTarget = false;
        var iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0f, 0f);
        iconRt.anchorMax = new Vector2(0f, 1f);
        iconRt.pivot     = new Vector2(0f, 0.5f);
        iconRt.anchoredPosition = new Vector2(8f, 0f);
        iconRt.sizeDelta = new Vector2(36f, 0f);

        // 본문
        var textGo = new GameObject("Body");
        textGo.transform.SetParent(panel.transform, false);
        _bodyText = textGo.AddComponent<TextMeshProUGUI>();
        _bodyText.fontSize  = 15f;
        _bodyText.color     = Color.white;
        _bodyText.alignment = TextAlignmentOptions.MidlineLeft;
        _bodyText.raycastTarget = false;
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0f, 0f);
        textRt.anchorMax = new Vector2(1f, 1f);
        textRt.offsetMin = new Vector2(48f, 6f);
        textRt.offsetMax = new Vector2(-12f, -6f);
    }

    void OnDestroy()
    {
        if (_instance == this)
        {
            SettingsManager.OnShowTutorialHintsChanged -= OnSettingChanged;
            _instance = null;
        }
    }

    void OnSettingChanged(bool isEnabled)
    {
        if (!isEnabled) HideCurrent();
    }

    // ── 퍼블릭 API ──────────────────────────────────────────────────────────

    /// <summary>
    /// 튜토리얼 힌트를 표시한다. 같은 id는 게임 통산 1회만 표시.
    /// 설정 OFF 상태에서는 표시하지 않으며 기록도 남기지 않는다(나중에 켜면 다시 볼 수 있음).
    /// </summary>
    public static void ShowHint(string id, string text, float duration = 4f)
    {
        if (!(SettingsManager.Instance?.showTutorialHints ?? true)) return;
        if (HasShown(id)) return;

        PlayerPrefs.SetInt(PrefsPrefix + id, 1);
        Instance.Play(text, duration);
    }

    /// <summary>표시 중인 힌트를 즉시 숨긴다.</summary>
    public static void HideCurrent()
    {
        if (_instance == null) return;
        if (_instance._activeCoroutine != null)
        {
            _instance.StopCoroutine(_instance._activeCoroutine);
            _instance._activeCoroutine = null;
        }
        if (_instance._group != null) _instance._group.alpha = 0f;
    }

    /// <summary>해당 id의 힌트가 이미 표시된 적 있는지.</summary>
    public static bool HasShown(string id) =>
        PlayerPrefs.GetInt(PrefsPrefix + id, 0) == 1;

    // ── 내부 ────────────────────────────────────────────────────────────────
    void Play(string text, float duration)
    {
        _bodyText.text = text;
        if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
        _activeCoroutine = StartCoroutine(HintRoutine(duration));
    }

    IEnumerator HintRoutine(float duration)
    {
        // 페이드 인 → 유지 → 페이드 아웃 (timeScale 0 상황 대비 unscaled)
        const float fade = 0.3f;

        float t = 0f;
        while (t < fade)
        {
            t += Time.unscaledDeltaTime;
            _group.alpha = Mathf.Clamp01(t / fade);
            yield return null;
        }
        _group.alpha = 1f;

        t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        t = 0f;
        while (t < fade)
        {
            t += Time.unscaledDeltaTime;
            _group.alpha = 1f - Mathf.Clamp01(t / fade);
            yield return null;
        }
        _group.alpha = 0f;
        _activeCoroutine = null;
    }
}
