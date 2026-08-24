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
    [Tooltip("평소 위치(좌측 상단) 마커. 비워도 된다 — 비우면 프리팹이 저작한 companionRoot 위치를 쓴다. " +
             "꽂을 거라면 반드시 companionRoot 의 형제여야 한다")]
    public RectTransform idlePosition;
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

    // 프리팹이 저작한 평소 위치. idlePosition 마커가 없거나 못 쓰는 배선일 때 여기로 돌아온다.
    // BattleUI 는 전투마다 Instantiate 되므로(EncounterManager.StartTurnBased) 이 값은 항상 저작값과 같다.
    private Vector2 _authoredIdle;
    private bool    _hasAuthoredIdle;

    public static BattleCompanionUI Instance { get; private set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (dialogueArea == null && companionDialogue != null)
            Debug.LogWarning("[BattleCompanionUI] dialogueArea가 연결되지 않았습니다. 인스펙터에서 연결해주세요.");

        // 아무것도 움직이기 전에 저작 위치를 잡아둔다.
        if (companionRoot != null)
        {
            _authoredIdle    = companionRoot.anchoredPosition;
            _hasAuthoredIdle = true;
        }

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

    /// <summary>
    /// 동료 초상화를 갈아 끼운다. 전투 중 <c>&lt;&lt;showSprite&gt;&gt;</c> 가 여기로 넘어온다
    /// (<see cref="YarnCommandBridge"/>) — 필드 대화창 캔버스는 BattleUI 뒤에 깔려 안 보이기 때문이다.
    /// </summary>
    public void SetPortrait(Sprite sprite)
    {
        if (_isDead || portraitImage == null) return;

        portraitImage.sprite  = sprite;
        portraitImage.enabled = sprite != null;
        portraitImage.preserveAspect = true;

        // 죽음 이벤트의 FadeOut 이 알파를 0 으로 남겨 둘 수 있다
        Color c = portraitImage.color;
        c.a = 1f;
        portraitImage.color = c;
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

        yield return StartCoroutine(MoveToAnchored(IdleAnchored()));
    }

    /// <summary>
    /// 위치 마커가 있는 자리로 <see cref="companionRoot"/> 를 옮기려면 얼마나 되는지 계산한다.
    /// <para><b>anchoredPosition 을 그대로 복사하면 안 된다.</b> 그 값은 "자기 부모의 앵커 기준 오프셋"이라
    /// 앵커나 피벗이 다르면 같은 숫자가 다른 자리를 뜻한다. 실제로 <c>dialoguePosition</c> 은 앵커가
    /// (0.5, 0.5) 인데 <see cref="companionRoot"/> 는 (0, 1) 이라, 생값을 넣으면 동료가 대화창으로
    /// 내려오지 않고 좌상단에서 위로 잘려 나갔다.</para>
    /// <para>그래서 사각형의 <b>중심끼리</b> 맞춘다. 두 중심의 차이를 companionRoot 의 부모 좌표계로
    /// 환산해 지금 값에 더하므로 앵커·피벗이 무엇이든 성립한다.</para>
    /// </summary>
    bool TryResolveAnchored(RectTransform target, out Vector2 anchored)
    {
        anchored = default;
        if (companionRoot == null || target == null) return false;

        // 자기 자신이나 자식을 기준으로 삼으면 자식이 부모의 자리를 정의하는 순환이 된다.
        if (target == companionRoot || target.IsChildOf(companionRoot))
        {
            Debug.LogWarning("[BattleCompanionUI] 위치 마커가 companionRoot 안에 있습니다. " +
                             "저작 위치로 대신 돌아갑니다 — 인스펙터에서 형제 마커로 바꿔주세요.");
            return false;
        }

        var parent = companionRoot.parent as RectTransform;
        if (parent == null) return false;

        Vector2 targetCenter  = parent.InverseTransformPoint(target.TransformPoint(target.rect.center));
        Vector2 currentCenter = parent.InverseTransformPoint(companionRoot.TransformPoint(companionRoot.rect.center));

        anchored = companionRoot.anchoredPosition + (targetCenter - currentCenter);
        return true;
    }

    /// <summary>평소 위치의 anchoredPosition. 쓸 만한 마커가 없으면 저작값으로 떨어진다.</summary>
    Vector2 IdleAnchored()
    {
        if (TryResolveAnchored(idlePosition, out var p)) return p;
        if (_hasAuthoredIdle) return _authoredIdle;
        return companionRoot != null ? companionRoot.anchoredPosition : Vector2.zero;
    }

    IEnumerator MoveToPosition(RectTransform target)
    {
        if (companionRoot == null || target == null) yield break;
        if (!TryResolveAnchored(target, out var end)) yield break;
        yield return StartCoroutine(MoveToAnchored(end));
    }

    IEnumerator MoveToAnchored(Vector2 end)
    {
        if (companionRoot == null) yield break;

        _isMoving = true;
        Vector2 start = companionRoot.anchoredPosition;
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
        if (companionRoot == null) return;
        companionRoot.anchoredPosition = IdleAnchored();
    }
}
