using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 턴제 전투 중 왼쪽 상단에 캐릭터 얼굴 + 이름 + 대사를 팝업합니다.
///
/// [씬 설정]
/// 전투 Canvas 하위에 Panel을 생성하고 이 컴포넌트를 붙이세요.
/// Panel 앵커/피벗을 top-left (0,1)로 설정하면 슬라이드인/아웃이 자연스럽게 동작합니다.
///
/// Panel 내부 권장 구조:
///   ├─ FaceImage (Image, Mask 컴포넌트, 80×80)   ← faceImage
///   ├─ BubblePanel (Image — 말풍선 배경)
///   │    ├─ NameText (Text)                        ← nameText
///   │    └─ DialogueText (TextMeshProUGUI)          ← dialogueTMP
///   └─ TailImage (Image — 삼각형 말풍선 꼬리, 선택) ← tailImage
/// </summary>
public class BattleCommentaryUI : MonoBehaviour
{
    public static BattleCommentaryUI Instance { get; private set; }

    // ─────────────────────────────────────────────
    //  인스펙터 연결
    // ─────────────────────────────────────────────
    [Header("UI 연결")]
    [Tooltip("슬라이드 애니메이션의 대상 RectTransform (패널 루트).")]
    [SerializeField] private RectTransform panelRoot;
    [Tooltip("캐릭터 얼굴 이미지 (Mask 내부 Image).")]
    [SerializeField] private Image faceImage;
    [Tooltip("캐릭터 이름 텍스트 (TMP_Text).")]
    [SerializeField] private TMP_Text nameText;
    [Tooltip("대사 텍스트 (TextMeshProUGUI).")]
    [SerializeField] private TMP_Text dialogueTMP;
    [Tooltip("말풍선 꼬리 이미지 (없으면 무시).")]
    [SerializeField] private Image tailImage;

    [Header("표시 설정")]
    [Tooltip("대사가 화면에 머무는 시간(초).")]
    public float displayDuration = 3f;
    [Tooltip("타이핑 속도 — 글자당 출력 간격(초). 작을수록 빠름.")]
    public float typingSpeed = 0.04f;
    [Tooltip("true: 새 대사를 큐에 쌓아 순서대로 표시. false: 새 대사가 오면 즉시 교체.")]
    public bool useQueue = true;

    [Header("슬라이드 설정")]
    public float slideInDuration  = 0.25f;
    public float slideOutDuration = 0.25f;
    [Tooltip("표시 위치 — 패널 왼쪽 가장자리의 X 오프셋(px).")]
    public float visibleOffsetX = 10f;

    // ─────────────────────────────────────────────
    //  내부 상태
    // ─────────────────────────────────────────────
    private readonly struct CommentEntry
    {
        public readonly Sprite Face;
        public readonly string CharacterName;
        public readonly string Dialogue;
        public CommentEntry(Sprite face, string name, string dialogue)
        { Face = face; CharacterName = name; Dialogue = dialogue; }
    }

    private Queue<CommentEntry> _queue = new Queue<CommentEntry>();
    private Coroutine _showCoroutine;
    private bool _isShowing;

    // 슬라이드 위치 캐시
    private float _visibleX;
    private float _hiddenX;
    private bool  _positionCached;

    // WaitForSecondsRealtime 캐시
    private WaitForSecondsRealtime _waitTypingSpeed;
    private WaitForSecondsRealtime _waitDisplayDuration;
    private float _cachedTypingSpeed;
    private float _cachedDisplayDuration;

    // ─────────────────────────────────────────────
    //  라이프사이클
    // ─────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _cachedTypingSpeed     = typingSpeed;
        _cachedDisplayDuration = displayDuration;
        _waitTypingSpeed       = new WaitForSecondsRealtime(typingSpeed);
        _waitDisplayDuration   = new WaitForSecondsRealtime(displayDuration);

        HideImmediate();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ─────────────────────────────────────────────
    //  공개 API
    // ─────────────────────────────────────────────

    /// <summary>
    /// 대사를 표시합니다. useQueue에 따라 큐에 쌓거나 즉시 교체합니다.
    /// </summary>
    public void ShowComment(Sprite face, string characterName, string dialogue)
    {
        if (string.IsNullOrEmpty(dialogue)) return;

        var entry = new CommentEntry(face, characterName, dialogue);

        if (useQueue)
        {
            _queue.Enqueue(entry);
            if (!_isShowing)
                StartNext();
        }
        else
        {
            // 즉시 교체
            _queue.Clear();
            if (_showCoroutine != null) StopCoroutine(_showCoroutine);
            _isShowing = false;
            _queue.Enqueue(entry);
            StartNext();
        }
    }

    // ─────────────────────────────────────────────
    //  내부 로직
    // ─────────────────────────────────────────────

    void StartNext()
    {
        if (_queue.Count == 0) return;
        var entry = _queue.Dequeue();
        _showCoroutine = StartCoroutine(ShowSequence(entry));
    }

    IEnumerator ShowSequence(CommentEntry entry)
    {
        _isShowing = true;

        if (!Mathf.Approximately(_cachedTypingSpeed, typingSpeed))
        {
            _cachedTypingSpeed = typingSpeed;
            _waitTypingSpeed   = new WaitForSecondsRealtime(typingSpeed);
        }
        if (!Mathf.Approximately(_cachedDisplayDuration, displayDuration))
        {
            _cachedDisplayDuration = displayDuration;
            _waitDisplayDuration   = new WaitForSecondsRealtime(displayDuration);
        }

        CachePositions();

        // 얼굴 / 이름 세팅
        if (faceImage != null)
        {
            faceImage.sprite  = entry.Face;
            faceImage.enabled = entry.Face != null;
        }
        if (nameText    != null) nameText.text    = entry.CharacterName ?? string.Empty;
        if (dialogueTMP != null) dialogueTMP.text = string.Empty;

        // 패널 시작 위치 = 숨김 위치
        SetPanelX(_hiddenX);
        panelRoot.gameObject.SetActive(true);

        // 1. 슬라이드 인
        yield return StartCoroutine(SlideTo(_visibleX, slideInDuration));

        // 2. 타이핑 효과
        if (dialogueTMP != null)
        {
            dialogueTMP.text = entry.Dialogue;
            dialogueTMP.maxVisibleCharacters = 0;
            for (int i = 1; i <= entry.Dialogue.Length; i++)
            {
                dialogueTMP.maxVisibleCharacters = i;
                yield return _waitTypingSpeed;
            }
            dialogueTMP.maxVisibleCharacters = int.MaxValue;
        }

        // 3. 표시 유지
        yield return _waitDisplayDuration;

        // 4. 슬라이드 아웃
        yield return StartCoroutine(SlideTo(_hiddenX, slideOutDuration));

        panelRoot.gameObject.SetActive(false);

        _isShowing = false;
        _showCoroutine = null;

        // 5. 큐 처리
        if (_queue.Count > 0)
            StartNext();
    }

    IEnumerator SlideTo(float targetX, float duration)
    {
        if (panelRoot == null) yield break;
        float startX  = panelRoot.anchoredPosition.x;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // EaseOutQuad
            t = 1f - (1f - t) * (1f - t);
            SetPanelX(Mathf.Lerp(startX, targetX, t));
            yield return null;
        }
        SetPanelX(targetX);
    }

    void SetPanelX(float x)
    {
        if (panelRoot == null) return;
        var pos = panelRoot.anchoredPosition;
        pos.x = x;
        panelRoot.anchoredPosition = pos;
    }

    void CachePositions()
    {
        if (_positionCached || panelRoot == null) return;
        _visibleX = visibleOffsetX;
        _hiddenX  = -(panelRoot.rect.width + 20f);
        // rect.width가 0이면 (레이아웃 미계산) 폴백값 사용
        if (Mathf.Approximately(_hiddenX, -20f))
            _hiddenX = -420f;
        _positionCached = true;
    }

    void HideImmediate()
    {
        if (panelRoot != null)
        {
            CachePositions();
            SetPanelX(_hiddenX);
            panelRoot.gameObject.SetActive(false);
        }
        if (dialogueTMP != null) dialogueTMP.text = string.Empty;
    }
}
