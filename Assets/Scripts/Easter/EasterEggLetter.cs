using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 이스터에그 편지 컴포넌트.
/// 모든 부엉이를 발견한 상태로 게임을 다시 시작했을 때만 오브젝트가 활성화됩니다.
///
/// 사용법:
///   1. 편지로 쓸 오브젝트를 씬 원하는 위치에 배치합니다.
///   2. 이 컴포넌트를 추가합니다.
///   3. 같은 오브젝트에 InteractionTrigger 컴포넌트를 추가합니다.
///      InteractionTrigger의 yarnNode는 비워두세요 — 대사는 이 컴포넌트가 제어합니다.
///   4. easterImage에 표시할 Sprite를 연결하고, dialogueNode에 Yarn 노드 이름을 입력하세요.
///   5. 오브젝트는 기본적으로 활성화 상태로 두세요.
///      Start()에서 모든 부엉이 발견 여부에 따라 자동으로 비활성화됩니다.
/// </summary>
public class EasterEggLetter : MonoBehaviour
{
    [Header("이미지 표시 설정")]
    [Tooltip("상호작용 시 먼저 표시할 이미지. null이면 이미지 없이 바로 대사 시작.")]
    [SerializeField] private Sprite easterImage;

    [Tooltip("이미지가 사라진 후 재생할 Yarn 대사 노드 이름.")]
    [SerializeField] private string dialogueNode;

    [Tooltip("이미지 자동 닫힘 시간(초). 0이면 키 입력으로만 닫힘.")]
    [SerializeField] private float imageDuration = 3f;

    [Tooltip("true면 키/클릭으로 이미지를 닫을 수 있습니다.")]
    [SerializeField] private bool dismissOnAnyKey = true;

    [Tooltip("이미지 페이드인/아웃 시간(초).")]
    [SerializeField] private float fadeDuration = 0.4f;

    void Start()
    {
        bool show = OwlTracker.AllOwlsFound;
        gameObject.SetActive(show);

        if (!show) return;

        Dbg.Log("[EasterEggLetter] 이스터에그 편지가 활성화되었습니다.");

        var trigger = GetComponent<InteractionTrigger>();
        if (trigger != null)
            trigger.onInteract.AddListener(OnInteract);
        else
            Debug.LogWarning("[EasterEggLetter] InteractionTrigger 컴포넌트가 없습니다.");
    }

    void OnDestroy()
    {
        var trigger = GetComponent<InteractionTrigger>();
        if (trigger != null)
            trigger.onInteract.RemoveListener(OnInteract);
    }

    void OnInteract()
    {
        if (easterImage != null)
            StartCoroutine(ShowImageThenDialogue());
        else if (!string.IsNullOrEmpty(dialogueNode))
            StartCoroutine(YarnDialogue.PlayAndWait(dialogueNode, true));
    }

    IEnumerator ShowImageThenDialogue()
    {
        // 1. 플레이어 잠금
        PlayerInputLock.Instance?.Lock();

        // 2. Canvas 생성
        var canvasGo = new GameObject("EasterEggImageOverlay");
        DontDestroyOnLoad(canvasGo);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        var cg    = canvasGo.AddComponent<CanvasGroup>();
        cg.alpha  = 0f;

        // 검은 배경
        var bgGo  = new GameObject("BG");
        bgGo.transform.SetParent(canvasGo.transform, false);
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.85f);
        var bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;

        // 이미지
        var imgGo  = new GameObject("EasterImage");
        imgGo.transform.SetParent(canvasGo.transform, false);
        var img    = imgGo.AddComponent<Image>();
        img.sprite = easterImage;
        img.preserveAspect = true;
        var imgRect = imgGo.GetComponent<RectTransform>();
        imgRect.anchorMin        = new Vector2(0.1f, 0.1f);
        imgRect.anchorMax        = new Vector2(0.9f, 0.9f);
        imgRect.offsetMin        = imgRect.offsetMax = Vector2.zero;

        // 안내 텍스트 (키 입력 닫기 + 자동 닫힘 없을 때만)
        if (dismissOnAnyKey && imageDuration <= 0f)
        {
            var txtGo  = new GameObject("HintText");
            txtGo.transform.SetParent(canvasGo.transform, false);
            var txt    = txtGo.AddComponent<Text>();
            txt.text      = "아무 키나 누르세요";
            txt.fontSize  = 22;
            txt.color     = new Color(0.8f, 0.8f, 0.8f, 1f);
            txt.alignment = TextAnchor.LowerCenter;
            txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var tr = txtGo.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0f, 0f);
            tr.anchorMax = new Vector2(1f, 0.12f);
            tr.offsetMin = tr.offsetMax = Vector2.zero;
        }

        // 3. 페이드인
        yield return StartCoroutine(Fade(cg, 0f, 1f));

        // 4. 대기 (타이머 + 키 입력)
        float elapsed = 0f;
        while (true)
        {
            elapsed += Time.unscaledDeltaTime;
            bool timerDone = imageDuration > 0f && elapsed >= imageDuration;
            bool keyPressed = dismissOnAnyKey && (Input.anyKeyDown);
            if (timerDone || keyPressed) break;
            yield return null;
        }

        // 5. 페이드아웃
        yield return StartCoroutine(Fade(cg, 1f, 0f));

        // 6. Canvas 제거
        Destroy(canvasGo);

        // 7. 플레이어 잠금 해제
        PlayerInputLock.Instance?.Unlock();

        // 8. 대사 시작
        if (!string.IsNullOrEmpty(dialogueNode))
            yield return StartCoroutine(YarnDialogue.PlayAndWait(dialogueNode, true));
    }

    IEnumerator Fade(CanvasGroup cg, float from, float to)
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
            yield return null;
        }
        cg.alpha = to;
    }

    // ── 개발 편의 ─────────────────────────────────────────────

    [ContextMenu("이스터에그 강제 활성화 (테스트용)")]
    void DevForceShow() => gameObject.SetActive(true);

    [ContextMenu("이스터에그 상태 전체 초기화 (PlayerPrefs)")]
    void DevResetAll() => OwlTracker.ResetAll();
}
