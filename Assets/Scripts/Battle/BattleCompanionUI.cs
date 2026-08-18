using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
    public TMP_Text   companionDialogue;   // 동료 대사 텍스트

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
    private bool _hasRolledDeath = false;  // 한 전투당 1회만 확률 굴림
    private WaitForSecondsRealtime _wait2_5s;

    public static BattleCompanionUI Instance { get; private set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (dialogueArea == null && companionDialogue != null)
            Debug.LogWarning("[BattleCompanionUI] dialogueArea가 연결되지 않았습니다. 인스펙터에서 연결해주세요.");

        // 다른 Start()보다 먼저 대화창을 확실히 숨기기
        // (인스펙터에 기본 텍스트가 남아있을 경우도 여기서 지워진다)
        SetDialogueVisible(false);

        // 전투 UI 가 생긴 바로 이 순간 대사 출력처를 동료 대화창으로 넘긴다.
        // 프레젠터의 Update 만 믿으면 이 프레임에 시작된 대사가 필드 대화창으로 샌다.
        BattleCompanionLinePresenter.SyncNow();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        BattleCompanionLinePresenter.SyncNow();
    }

    void Start()
    {
        _wait2_5s = new WaitForSecondsRealtime(2.5f);
        // Awake에서 이미 처리했지만 안전을 위해 재확인
        SetDialogueVisible(false);
        SnapToIdle();
    }

    /// <summary>
    /// 대화창(말풍선 영역 + 텍스트)을 통째로 켜고 끈다.
    /// 텍스트 오브젝트는 프리팹에서 꺼진 채로 시작하므로 영역만 켜서는 글자가 안 보인다.
    /// </summary>
    void SetDialogueVisible(bool visible)
    {
        if (dialogueArea != null) dialogueArea.SetActive(visible);
        if (companionDialogue != null)
        {
            companionDialogue.gameObject.SetActive(visible);
            if (!visible) companionDialogue.text = string.Empty;
        }
    }

    /// <summary>
    /// 대사 한 줄을 대화창에 즉시 반영한다. 이동·대기 없이 글자만 바꾼다.
    /// 전투 중 Yarn 대사를 여기로 흘리는 <see cref="BattleCompanionLinePresenter"/> 가 쓴다.
    /// </summary>
    public void ShowLine(string text)
    {
        if (_isDead) return;
        SetDialogueVisible(true);
        if (companionDialogue != null) companionDialogue.text = text ?? string.Empty;
    }

    /// <summary>대사 표시를 끝내고 대화창을 닫는다.</summary>
    public void HideLine()
    {
        if (_isDead) return;
        SetDialogueVisible(false);
    }

    /// <summary>플레이어 HP가 0이 됐을 때 BattleSystem에서 호출.</summary>
    public void OnPlayerDied()
    {
        if (_isDead || _hasRolledDeath) return;
        _hasRolledDeath = true;

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

        SetDialogueVisible(true);
        if (companionDialogue != null) companionDialogue.text = "...미안해. 나 여기까지인 것 같아.";

        yield return _wait2_5s;

        // 페이드 아웃
        if (portraitImage != null)
            yield return StartCoroutine(FadeOut(portraitImage, 1.5f));

        SetDialogueVisible(false);
        gameObject.SetActive(false);
    }

    IEnumerator DialogueRoutine(string text, float duration)
    {
        if (_isDead || _isMoving) yield break;

        yield return StartCoroutine(MoveToPosition(dialoguePosition));

        SetDialogueVisible(true);
        if (companionDialogue != null) companionDialogue.text = text;

        yield return new WaitForSecondsRealtime(duration);

        SetDialogueVisible(false);

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
