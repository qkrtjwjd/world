using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 배드 엔딩 화면. BadEndingScene 이 로드되면 자동으로 생성되어 연출을 재생하고 되감기 지점으로 복귀합니다.
///
/// 씬에 아무것도 배치하지 않습니다 — GameOverUI 와 같은 코드 생성 방식입니다.
/// BadEndingScene.unity 는 Main Camera 하나뿐이며, 그대로 두어도 이 컴포넌트가 화면을 채웁니다.
///
/// 표시 문구는 Resources/Endings/BadEndingText.json 에서 읽습니다. 비어 있으면 암전 연출만 재생합니다.
///
/// 연출이 끝나면 「다시 시도해 보시겠습니까?」 프롬프트를 띄우고 입력을 기다립니다
/// (정본 D BE#01-d · BE#02-c 의 [UI] 지시. 두 엔딩이 같은 화면을 공유합니다).
/// </summary>
public class BadEndingSequence : MonoBehaviour
{
    // ─── 연출 타이밍 ──────────────────────────────────────────────
    const float TextFadeDuration = 1.2f;
    const float LineHoldTime     = 2.6f;
    const float SilentHoldTime   = 2.5f;  // 문구가 없을 때 암전만 유지하는 시간
    const float TailHoldTime     = 1.0f;  // 복귀 직전 여백

    static bool _spawned;

    CanvasGroup      _cg;
    TextMeshProUGUI  _text;

    // ─── 「다시 시도해 보시겠습니까?」 ─────────────────────────────
    const string PromptLine = "다시 시도해 보시겠습니까?";
    const string PromptHint = "[ 아무 키나 눌러 계속 ]";
    const float  PromptFadeDuration = 0.6f;

    GameObject  _prompt;      // 프롬프트 묶음 루트. 연출이 끝날 때까지 꺼 둔다
    CanvasGroup _promptCg;
    bool        _promptLive;  // 입력을 받아도 되는 상태
    bool        _accepted;    // 아무 키 / 타이틀 중 하나가 이미 눌렸다
    bool        _goingTitle;

    // ─── 부트스트랩 ───────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        // 에디터에서 BadEndingScene 을 직접 재생한 경우 sceneLoaded 가 이미 지나갔다
        if (SceneManager.GetActiveScene().name == SceneNames.BadEnding)
            Spawn();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == SceneNames.BadEnding) Spawn();
        else                                    _spawned = false;
    }

    static void Spawn()
    {
        if (_spawned) return;
        _spawned = true;

        var root = new GameObject("BadEndingSequence [Auto]");

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;   // TransitionManager 의 페이드 오버레이(999)보다 아래
        root.AddComponent<CanvasScaler>();
        root.AddComponent<GraphicRaycaster>();   // '타이틀로' 버튼이 클릭을 받으려면 필요하다

        var seq = root.AddComponent<BadEndingSequence>();
        seq._cg = root.AddComponent<CanvasGroup>();
        // 씬 전환 페이드가 걷히는 순간 이미 암전이어야 한다. 페이드 인 하는 것은 문구 쪽이다.
        seq._cg.alpha          = 1f;
        seq._cg.blocksRaycasts = false;

        AddBlackout(root.transform);
        seq._text = AddText(root.transform);
        seq.BuildPrompt(root.transform);

        // BadEndingScene 에는 EventSystem 이 없다(Main Camera 하나뿐인 씬이다).
        // 없으면 '타이틀로' 버튼이 클릭을 받지 못하므로 여기서 만들어 준다.
        EnsureEventSystem();
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var go = new GameObject("EventSystem [Auto]");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }

    static void AddBlackout(Transform parent)
    {
        var go  = new GameObject("Blackout");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color         = Color.black;
        img.raycastTarget = false;

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static TextMeshProUGUI AddText(Transform parent)
    {
        var go  = new GameObject("Line");
        go.transform.SetParent(parent, false);
        var txt = go.AddComponent<TextMeshProUGUI>();
        txt.text          = "";
        txt.fontSize      = 20;
        txt.alignment     = TextAlignmentOptions.Center;
        txt.raycastTarget = false;
        txt.color         = new Color(1f, 1f, 1f, 0f);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.1f, 0.3f);
        rect.anchorMax = new Vector2(0.9f, 0.7f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return txt;
    }

    // ─── 연출 ─────────────────────────────────────────────────────
    void Start() => StartCoroutine(PlayRoutine());

    IEnumerator PlayRoutine()
    {
        BadEndingType type  = EndingManager.PendingBadEnding;
        EndingEntry   entry = LoadEntry(type);

        if (entry != null && !string.IsNullOrEmpty(entry.sfx))
            AudioManager.Instance?.Play(entry.sfx);

        string[] lines = entry?.lines;
        if (lines == null || lines.Length == 0)
        {
            // 문구 미작성 상태 — 암전 연출만으로 진행한다. 원고는 사용자가 채운다.
            yield return new WaitForSecondsRealtime(SilentHoldTime);
        }
        else
        {
            foreach (string line in lines)
            {
                if (string.IsNullOrEmpty(line)) continue;
                _text.text = line;
                yield return FadeText(0f, 1f);
                yield return new WaitForSecondsRealtime(LineHoldTime);
                yield return FadeText(1f, 0f);
            }
        }

        yield return new WaitForSecondsRealtime(TailHoldTime);

        // 정본 D BE#01-d · BE#02-c 의 [UI] 지시 — 두 엔딩이 이 화면을 공유한다.
        yield return PromptRoutine();
        if (_goingTitle) yield break;

        ReturnToRewindPoint(type);
    }

    // ─── 프롬프트 ─────────────────────────────────────────────────
    void BuildPrompt(Transform parent)
    {
        _prompt = new GameObject("Prompt");
        _prompt.transform.SetParent(parent, false);

        var rect = _prompt.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        _promptCg = _prompt.AddComponent<CanvasGroup>();
        _promptCg.alpha          = 0f;
        _promptCg.blocksRaycasts = false;

        // 색·크기는 GameOverUI 와 맞춘다. 두 화면이 같은 계열로 보여야 한다.
        AddLabel(_prompt.transform, "Question", PromptLine, 34, new Vector2(0f, 40f),  new Vector2(700f, 60f));
        AddLabel(_prompt.transform, "Hint",     PromptHint, 22, new Vector2(0f, -20f), new Vector2(700f, 40f));
        AddButton(_prompt.transform, "BtnTitle", "타이틀로", new Vector2(0f, -100f), OnClickTitle);

        _prompt.SetActive(false);
    }

    IEnumerator PromptRoutine()
    {
        if (_prompt == null) yield break;   // 방어 — 구성에 실패해도 복귀는 막지 않는다

        _prompt.SetActive(true);
        _cg.blocksRaycasts       = true;
        _promptCg.blocksRaycasts = true;

        float elapsed = 0f;
        while (elapsed < PromptFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _promptCg.alpha = Mathf.Clamp01(elapsed / PromptFadeDuration);
            yield return null;
        }
        _promptCg.alpha = 1f;

        // 페이드가 끝난 뒤에 입력을 연다. 연출 중 눌린 키가 그대로 삼켜지지 않게 한다.
        _promptLive = true;
        yield return new WaitUntil(() => _accepted);
        _promptLive = false;
    }

    /// <remarks>
    /// 마우스 버튼은 제외한다 — 포함하면 '타이틀로' 클릭의 마우스 다운이
    /// 버튼 이벤트보다 먼저 복귀를 실행해 버튼이 동작하지 않는다(GameOverUI 와 같은 이유).
    /// </remarks>
    void Update()
    {
        if (!_promptLive || _accepted) return;

        bool keyboardKeyDown = Input.anyKeyDown
            && !Input.GetMouseButtonDown(0)
            && !Input.GetMouseButtonDown(1)
            && !Input.GetMouseButtonDown(2);

        if (keyboardKeyDown) _accepted = true;
    }

    void OnClickTitle()
    {
        if (_accepted) return;
        _goingTitle = true;
        _accepted   = true;
        GoTitle();
    }

    static void AddLabel(Transform parent, string name, string text, int fontSize,
        Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        var txt = go.AddComponent<TextMeshProUGUI>();
        txt.text          = text;
        txt.fontSize      = fontSize;
        txt.color         = new Color(0.949f, 0.937f, 0.941f, 1f);   // #F2EFF0 — GameOverUI 와 같은 값
        txt.alignment     = TextAlignmentOptions.Center;
        txt.raycastTarget = false;

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0.5f, 0.5f);
        rect.anchorMax        = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta        = sizeDelta;
    }

    static void AddButton(Transform parent, string name, string label,
        Vector2 anchoredPos, Action onClick)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.118f, 0.102f, 0.110f, 1f);   // #1E1A1C — GameOverUI 와 같은 값
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick());

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0.5f, 0.5f);
        rect.anchorMax        = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta        = new Vector2(280f, 58f);

        AddLabel(go.transform, "Label", label, 26, Vector2.zero, new Vector2(260f, 50f));
        var lr = go.transform.Find("Label").GetComponent<RectTransform>();
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = lr.offsetMax = Vector2.zero;
    }

    IEnumerator FadeText(float from, float to)
    {
        float elapsed = 0f;
        while (elapsed < TextFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float a  = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / TextFadeDuration));
            _text.color = new Color(1f, 1f, 1f, a);
            yield return null;
        }
        _text.color = new Color(1f, 1f, 1f, to);
    }

    // ─── 복귀 ─────────────────────────────────────────────────────
    /// <remarks>
    /// 인형화 페널티는 붙이지 않습니다(CLAUDE.md §2). 복귀 자체가 처벌이며 되감기와 이중 처벌이 됩니다.
    /// </remarks>
    void ReturnToRewindPoint(BadEndingType type)
    {
        var sm = SaveManager.Instance;
        if (sm == null)
        {
            Debug.LogWarning("[BadEndingSequence] SaveManager 가 없어 타이틀로 이동합니다.");
            GoTitle();
            return;
        }

        // 인형화 100 은 '마지막 저장 지점'으로 돌아간다(C-2-6). 탈출 압박 2종만 전용 되감기 지점을 쓴다.
        if (type != BadEndingType.Doll && sm.HasRewindSave)
        {
            sm.LoadRewindPoint();
            return;
        }

        // 되감기 지점이 없으면 게임 오버와 같은 판정으로 최신 저장 지점을 고른다(GameOverUI.OnClickLoad 와 동일 규칙)
        SaveData pre = sm.GetPreBattleData();
        SaveData cp  = sm.GetCheckpointData();

        if (pre == null && cp == null)
        {
            Debug.LogWarning("[BadEndingSequence] 복구할 저장 데이터가 없어 타이틀로 이동합니다.");
            GoTitle();
            return;
        }

        if (cp == null || (pre != null && pre.saveTicks >= cp.saveTicks)) sm.LoadPreBattle();
        else                                                             sm.LoadCheckpoint();
    }

    static void GoTitle()
    {
        if (TransitionManager.Instance != null)
            TransitionManager.Instance.DoSceneTransition(SceneNames.Title);
        else
            SceneManager.LoadScene(SceneNames.Title);
    }

    // ─── 문구 로드 ────────────────────────────────────────────────
    const string TextResourcePath = "Endings/BadEndingText";

    static Dictionary<string, EndingEntry> _cache;

    static EndingEntry LoadEntry(BadEndingType type)
    {
        if (_cache == null)
        {
            _cache = new Dictionary<string, EndingEntry>();
            var asset = Resources.Load<TextAsset>(TextResourcePath);
            if (asset == null)
            {
                Debug.LogWarning($"[BadEndingSequence] '{TextResourcePath}' 를 찾을 수 없습니다. 암전 연출만 재생합니다.");
                return null;
            }

            var file = JsonUtility.FromJson<EndingTextFile>(asset.text);
            if (file?.entries != null)
                foreach (var e in file.entries)
                    if (e != null && !string.IsNullOrEmpty(e.type)) _cache[e.type] = e;
        }

        return _cache.TryGetValue(type.ToString(), out var entry) ? entry : null;
    }

    [Serializable]
    class EndingTextFile
    {
        public EndingEntry[] entries;
    }

    [Serializable]
    class EndingEntry
    {
        public string   type;
        public string[] lines;
        public string   sfx;
    }
}
