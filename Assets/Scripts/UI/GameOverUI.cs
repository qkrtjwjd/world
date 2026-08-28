using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
        UiCanvasScale.Add(root);   // 640x360 Expand — 단일 출처
        root.AddComponent<GraphicRaycaster>();

        _instance     = root.AddComponent<GameOverUI>();
        _instance._cg = root.AddComponent<CanvasGroup>();
        _instance._cg.alpha = 0f;
        _instance._cg.blocksRaycasts = false;
        root.SetActive(false);

        // 무대 바닥과 같은 불투명 배경.
        // 알파 0.88 이던 시절에는 그 아래 반투명 패널들이 겹쳐 비쳐서
        // "게임 오버" 흰 글자가 밝은 회색에 묻혀 거의 안 보였다.
        AddImage(root.transform, "BG", new Color(0.078f, 0.067f, 0.059f, 1f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // 제목 텍스트
        AddText(root.transform, "Title", "게임 오버", 64, new Vector2(0f, 120f), new Vector2(500f, 90f));

        // 힌트 텍스트 (아무 키 안내)
        AddText(root.transform, "Hint", "[ 아무 키나 눌러 마지막 저장 지점으로 ]", 22,
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
        var txt = go.AddComponent<TextMeshProUGUI>();
        txt.text      = text;
        txt.fontSize  = fontSize;
        txt.color     = new Color(0.949f, 0.937f, 0.941f, 1f);   // #F2EFF0
        txt.alignment = TextAlignmentOptions.Center;
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
        img.color = new Color(0.118f, 0.102f, 0.110f, 1f);   // #1E1A1C
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
        // 마우스 클릭은 제외 — 포함하면 '타이틀로' 버튼 클릭(마우스 다운)이
        // 버튼 이벤트보다 먼저 OnClickLoad를 실행해 버튼이 동작하지 않음
        bool keyboardKeyDown = Input.anyKeyDown
            && !Input.GetMouseButtonDown(0)
            && !Input.GetMouseButtonDown(1)
            && !Input.GetMouseButtonDown(2);

        if (_isShowing && _canAcceptInput && keyboardKeyDown)
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
        var sm = SaveManager.Instance;
        SaveData pre = sm != null ? sm.GetPreBattleData()  : null;
        SaveData cp  = sm != null ? sm.GetCheckpointData() : null;

        // 복구 지점이 하나도 없으면(예: 저장 이전 시점 사망) HP 0 상태로 방치되지 않도록 타이틀로 폴백
        if (pre == null && cp == null)
        {
            Debug.LogWarning("[GameOverUI] 복구할 저장 데이터가 없어 타이틀로 이동합니다.");
            OnClickTitle();
            return;
        }

        Hide();
        Time.timeScale = 1f;

        // 전투 전 저장 vs 체크포인트 중 saveTicks 가 최신인 쪽으로 복귀
        // (v6 이전 저장은 saveTicks=0 → 자연스럽게 상대편이 채택됨)
        if (cp == null || (pre != null && pre.saveTicks >= cp.saveTicks))
            sm.LoadPreBattle();
        else
            sm.LoadCheckpoint();
    }

    public void OnClickTitle()
    {
        Hide();
        Time.timeScale = 1f;
        TransitionManager.Instance?.DoSceneTransition(SceneNames.Title);
    }
}
