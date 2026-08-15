using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 아이템 관련 알림을 화면에 1.5~2초간 표시하는 UI.
/// - 아이템 버리기 알림 ("OO를 버렸습니다.")
/// - 사용 불가 / 버리기 불가 메시지
/// - 아이템 사용 시 루의 독백 대사
/// </summary>
public class ItemNotificationUI : MonoBehaviour
{
    public static ItemNotificationUI Instance { get; private set; }

    [Header("UI 연결")]
    [SerializeField] private GameObject notificationPanel;
    [SerializeField] private TMP_Text   messageText;

    [Header("표시 시간(초)")]
    [SerializeField] private float discardDisplayTime  = 1.5f;
    [SerializeField] private float messageDisplayTime  = 1.5f;
    [SerializeField] private float dialogueDisplayTime = 2.0f;

    private Coroutine _hideCoroutine;

    private WaitForSecondsRealtime _waitDiscard;
    private WaitForSecondsRealtime _waitMessage;
    private WaitForSecondsRealtime _waitDialogue;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);
        _waitDiscard  = new WaitForSecondsRealtime(discardDisplayTime);
        _waitMessage  = new WaitForSecondsRealtime(messageDisplayTime);
        _waitDialogue = new WaitForSecondsRealtime(dialogueDisplayTime);
        if (notificationPanel != null) notificationPanel.SetActive(false);
    }

    // ─────────────────────────────────────────────
    //  공개 메서드
    // ─────────────────────────────────────────────

    /// <summary>"OO를 버렸습니다." 알림 (1.5초)</summary>
    public void ShowDiscard(string itemName)
    {
        ShowInternal($"{itemName}을(를) 버렸습니다.", _waitDiscard);
    }

    /// <summary>일반 안내 메시지 (전투 중 버리기 불가, 버릴 수 없는 아이템 등)</summary>
    public void Show(string message)
    {
        ShowInternal(message, _waitMessage);
    }

    /// <summary>아이템 사용 시 루의 독백 대사 (2초)</summary>
    public void ShowDialogue(string dialogue)
    {
        ShowInternal(dialogue, _waitDialogue);
    }

    // ─────────────────────────────────────────────
    //  내부
    // ─────────────────────────────────────────────

    private void ShowInternal(string message, WaitForSecondsRealtime wait)
    {
        if (notificationPanel == null || messageText == null) return;

        messageText.text = message;
        notificationPanel.SetActive(true);

        if (_hideCoroutine != null) StopCoroutine(_hideCoroutine);
        _hideCoroutine = StartCoroutine(AutoHide(wait));
    }

    private IEnumerator AutoHide(WaitForSecondsRealtime wait)
    {
        yield return wait;
        if (notificationPanel != null) notificationPanel.SetActive(false);
        _hideCoroutine = null;
    }
}
