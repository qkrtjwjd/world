using System.Collections;
using UnityEngine;
using UnityEngine.UI;

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
    [SerializeField] private Text       messageText;

    [Header("표시 시간(초)")]
    [SerializeField] private float discardDisplayTime  = 1.5f;
    [SerializeField] private float messageDisplayTime  = 1.5f;
    [SerializeField] private float dialogueDisplayTime = 2.0f;

    private Coroutine _hideCoroutine;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);
        if (notificationPanel != null) notificationPanel.SetActive(false);
    }

    // ─────────────────────────────────────────────
    //  공개 메서드
    // ─────────────────────────────────────────────

    /// <summary>"OO를 버렸습니다." 알림 (1.5초)</summary>
    public void ShowDiscard(string itemName)
    {
        Show($"{itemName}을(를) 버렸습니다.", discardDisplayTime);
    }

    /// <summary>일반 안내 메시지 (전투 중 버리기 불가, 버릴 수 없는 아이템 등)</summary>
    public void Show(string message)
    {
        Show(message, messageDisplayTime);
    }

    /// <summary>아이템 사용 시 루의 독백 대사 (2초)</summary>
    public void ShowDialogue(string dialogue)
    {
        Show(dialogue, dialogueDisplayTime);
    }

    // ─────────────────────────────────────────────
    //  내부
    // ─────────────────────────────────────────────

    private void Show(string message, float duration)
    {
        if (notificationPanel == null || messageText == null) return;

        messageText.text = message;
        notificationPanel.SetActive(true);

        if (_hideCoroutine != null) StopCoroutine(_hideCoroutine);
        _hideCoroutine = StartCoroutine(AutoHide(duration));
    }

    private IEnumerator AutoHide(float duration)
    {
        yield return new WaitForSecondsRealtime(duration);
        if (notificationPanel != null) notificationPanel.SetActive(false);
        _hideCoroutine = null;
    }
}
