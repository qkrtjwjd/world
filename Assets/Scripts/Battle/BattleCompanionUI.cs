using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전투 화면의 동료 캐릭터 UI를 관리합니다.
/// - 평소: 좌측 상단 초상화 위치에 머뭄
/// - 스토리 이벤트: 대화창 위치로 내려와서 대사
/// - 플레이어 HP 0: 낮은 확률로 죽음 이벤트
/// </summary>
public class BattleCompanionUI : MonoBehaviour
{
    [Header("UI 연결")]
    public Image      portraitImage;       // 좌측 상단 초상화
    public GameObject dialogueArea;        // 하단 대화창 영역
    public Text       companionDialogue;   // 동료 대사 텍스트

    [Header("위치")]
    public RectTransform companionRoot;    // 동료 오브젝트 루트
    public RectTransform idlePosition;     // 평소 위치 (좌측 상단)
    public RectTransform dialoguePosition; // 대화 위치 (하단)

    [Header("죽음 이벤트")]
    [Tooltip("플레이어 HP 0 시 동료 죽음 발동 확률 (0~1)")]
    [Range(0f, 1f)]
    public float deathChance = 0.15f;
    public float moveSpeed = 3f;

    private bool _isDead = false;
    private bool _isMoving = false;

    public static BattleCompanionUI Instance { get; private set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (dialogueArea != null) dialogueArea.SetActive(false);
        SnapToIdle();
    }

    /// <summary>플레이어 HP가 0이 됐을 때 BattleSystem에서 호출.</summary>
    public void OnPlayerDied()
    {
        if (_isDead) return;

        if (Random.value < deathChance)
            StartCoroutine(CompanionDeathEvent());
    }

    /// <summary>스토리 이벤트 대사. 동료가 대화창으로 내려와서 말합니다.</summary>
    public void SpeakDialogue(string text, float duration = 3f)
    {
        StartCoroutine(DialogueRoutine(text, duration));
    }

    IEnumerator CompanionDeathEvent()
    {
        _isDead = true;

        // 대화창으로 이동 후 마지막 대사
        yield return StartCoroutine(MoveToPosition(dialoguePosition));

        if (dialogueArea != null)   dialogueArea.SetActive(true);
        if (companionDialogue != null) companionDialogue.text = "...미안해. 나 여기까지인 것 같아.";

        yield return new WaitForSecondsRealtime(2.5f);

        // 페이드 아웃
        if (portraitImage != null)
            yield return StartCoroutine(FadeOut(portraitImage, 1.5f));

        if (dialogueArea != null) dialogueArea.SetActive(false);
        gameObject.SetActive(false);
    }

    IEnumerator DialogueRoutine(string text, float duration)
    {
        if (_isDead || _isMoving) yield break;

        yield return StartCoroutine(MoveToPosition(dialoguePosition));

        if (dialogueArea != null)      dialogueArea.SetActive(true);
        if (companionDialogue != null) companionDialogue.text = text;

        yield return new WaitForSecondsRealtime(duration);

        if (dialogueArea != null) dialogueArea.SetActive(false);

        yield return StartCoroutine(MoveToPosition(idlePosition));
    }

    IEnumerator MoveToPosition(RectTransform target)
    {
        if (companionRoot == null || target == null) yield break;

        _isMoving = true;
        Vector2 start = companionRoot.anchoredPosition;
        Vector2 end   = target.anchoredPosition;
        float elapsed = 0f;
        float duration = Vector2.Distance(start, end) / (moveSpeed * 100f);
        duration = Mathf.Max(duration, 0.3f);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            companionRoot.anchoredPosition = Vector2.Lerp(start, end, elapsed / duration);
            yield return null;
        }

        companionRoot.anchoredPosition = end;
        _isMoving = false;
    }

    IEnumerator FadeOut(Image img, float duration)
    {
        Color c = img.color;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            c.a = Mathf.Lerp(1f, 0f, elapsed / duration);
            img.color = c;
            yield return null;
        }
        c.a = 0f;
        img.color = c;
    }

    void SnapToIdle()
    {
        if (companionRoot != null && idlePosition != null)
            companionRoot.anchoredPosition = idlePosition.anchoredPosition;
    }
}
