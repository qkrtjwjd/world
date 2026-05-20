using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class GameOverUI : MonoBehaviour
{
    public static GameOverUI Instance
    {
        get
        {
            if (_instance == null) CreateInstance();
            return _instance;
        }
    }
    static GameOverUI _instance;

    CanvasGroup _cg;
    bool _isShowing;
    bool _canAcceptInput;

    // ─── 자동 생성 ────────────────────────────────────────────────
    static void CreateInstance()
    {
        var root = new GameObject("GameOverUI [Auto]");
        DontDestroyOnLoad(root);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        root.AddComponent<CanvasScaler>();
        root.AddComponent<GraphicRaycaster>();

        _instance     = root.AddComponent<GameOverUI>();
        _instance._cg = root.AddComponent<CanvasGroup>();
        _instance._cg.alpha = 0f;
        _instance._cg.blocksRaycasts = false;
        root.SetActive(false);

        // 반투명 배경
        AddImage(root.transform, "BG", new Color(0f, 0f, 0f, 0.88f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // 제목 텍스트
        AddText(root.transform, "Title", "게임 오버", 64, new Vector2(0f, 120f), new Vector2(500f, 90f));

        // 힌트 텍스트 (아무 키 안내)
        AddText(root.transform, "Hint", "[ 아무 키나 눌러 전투 전으로 돌아가기 ]", 22,
            new Vector2(0f, -20f), new Vector2(600f, 40f));

        // 타이틀 버튼
        AddButton(root.transform, "BtnTitle", "타이틀로", new Vector2(0f, -80f), () => _instance.OnClickTitle());
    }

    static void AddImage(Transform parent, string name, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img  = go.AddComponent<Image>();
        img.color = color;
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    static void AddText(Transform parent, string name, string text, int fontSize,
        Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        var txt = go.AddComponent<Text>();
        txt.text      = text;
        txt.fontSize  = fontSize;
        txt.color     = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var rect = go.GetComponent<RectTransform>();
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta        = sizeDelta;
    }

    static void AddButton(Transform parent, string name, string label,
        Vector2 anchoredPos, Action onClick)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.18f, 0.18f, 0.18f, 1f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick());
        var rect = go.GetComponent<RectTransform>();
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta        = new Vector2(280f, 58f);

        // 레이블 텍스트
        AddText(go.transform, "Label", label, 26, Vector2.zero, new Vector2(260f, 50f));
        var lr = go.transform.Find("Label").GetComponent<RectTransform>();
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = lr.offsetMax = Vector2.zero;
    }

    // ─── 입력 감지 ────────────────────────────────────────────────
    void Update()
    {
        if (_isShowing && _canAcceptInput && Input.anyKeyDown)
            OnClickLoad();
    }

    // ─── 공개 API ─────────────────────────────────────────────────
    public void Show()
    {
        if (_isShowing) return;
        _isShowing = true;
        gameObject.SetActive(true);
        StartCoroutine(FadeIn());
    }

    IEnumerator FadeIn()
    {
        float elapsed = 0f;
        const float duration = 0.5f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            _cg.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }
        _cg.alpha = 1f;
        _cg.blocksRaycasts = true;
        _canAcceptInput = true;
    }

    void Hide()
    {
        _isShowing         = false;
        _canAcceptInput    = false;
        _cg.alpha          = 0f;
        _cg.blocksRaycasts = false;
        gameObject.SetActive(false);
    }

    public void OnClickLoad()
    {
        Hide();
        Time.timeScale = 1f;
        SaveManager.Instance?.LoadPreBattle();
    }

    public void OnClickTitle()
    {
        Hide();
        Time.timeScale = 1f;
        TransitionManager.Instance?.DoSceneTransition(SceneNames.Title);
    }
}
