using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Objective Panel 중앙 관리 싱글턴.
/// ShowObjective() 호출 시 전체 패널을 5초간 표시한 뒤 자동으로 숨기고,
/// 화면 우상단 HUD 패널에 본문 텍스트를 소형으로 표시한다.
/// HideObjective()를 직접 호출하면 5초 이전에도 즉시 HUD로 전환된다.
/// </summary>
public class ObjectiveManager : MonoBehaviour
{
    public static ObjectiveManager Instance { get; private set; }

    [Header("전체 목표 패널 (5초 후 자동 숨김)")]
    public GameObject objectivePanel;
    public TMP_Text objectiveHeaderText;
    public TMP_Text objectiveBodyText;

    [Header("화면 우상단 HUD")]
    public GameObject hudPanel;
    public TMP_Text hudText;

    private Coroutine _autoHideCoroutine;
    private string _currentBody;
    private int _hudSuppressCount = 0;

    // ── 대사 중 목표 패널 억제 (2026-08-08) ──────────────────────────
    // 목표 패널은 대사창 위에 그려져 대사를 통째로 가린다.
    // yarn 의 <<show_objective>> 는 대사 재생 도중에 실행되므로, 대사가 끝날 때까지
    // 표시를 미뤘다가 종료 시점에 띄운다.
    private bool   _dialogueRunning;
    private bool   _hasPendingObjective;
    private string _pendingHeader;
    private string _pendingBody;

    // 설정 메뉴 "목표 UI 표시" 상태 (false면 모든 ShowObjective를 무시)
    bool _objectiveUIEnabled = true;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DialogueEvents.OnDialogueStarted += OnDialogueStarted;
        DialogueEvents.OnDialogueEnded   += OnDialogueEnded;
        SettingsManager.OnShowObjectiveUIChanged += SetObjectiveUIEnabled;
        _objectiveUIEnabled = SettingsManager.Instance?.showObjectiveUI ?? true;
    }

    void OnDestroy()
    {
        DialogueEvents.OnDialogueStarted -= OnDialogueStarted;
        DialogueEvents.OnDialogueEnded   -= OnDialogueEnded;
        SettingsManager.OnShowObjectiveUIChanged -= SetObjectiveUIEnabled;
    }

    // ── 대사 시작/종료 ───────────────────────────────────────────────
    void OnDialogueStarted()
    {
        _dialogueRunning = true;

        // 이미 떠 있던 목표 패널도 대사창을 가린다. 접어 둔다.
        if (_autoHideCoroutine != null) { StopCoroutine(_autoHideCoroutine); _autoHideCoroutine = null; }
        if (objectivePanel != null) objectivePanel.SetActive(false);

        HideHUD();
    }

    void OnDialogueEnded()
    {
        _dialogueRunning = false;
        RestoreHUD();

        // 대사 중에 밀어 둔 목표가 있으면 이제 띄운다.
        if (!_hasPendingObjective) return;
        _hasPendingObjective = false;
        ShowObjectiveNow(_pendingHeader, _pendingBody);
    }

    void SetObjectiveUIEnabled(bool enabled)
    {
        _objectiveUIEnabled = enabled;
        if (!enabled)
        {
            if (objectivePanel != null) objectivePanel.SetActive(false);
            if (hudPanel       != null) hudPanel.SetActive(false);
        }
        else if (!string.IsNullOrEmpty(_currentBody))
        {
            // 다시 켤 때 HUD 복원
            CollapseToHUD();
        }
    }

    /// <summary>
    /// 전체 패널을 표시하고 잠시 뒤 자동으로 HUD로 전환한다.
    /// 대사가 재생 중이면 대사가 끝날 때까지 표시를 미룬다 — 목표 패널이 대사창을 덮기 때문이다.
    /// </summary>
    public void ShowObjective(string header, string body)
    {
        // 저널 기록은 UI 표시 설정·대사 진행과 무관하게 항상 즉시 남긴다
        JournalManager.Add(header, body);
        if (!_objectiveUIEnabled) return;

        if (_dialogueRunning)
        {
            _pendingHeader       = header;
            _pendingBody         = body;
            _hasPendingObjective = true;
            return;
        }

        ShowObjectiveNow(header, body);
    }

    /// <summary>실제 표시. 저널 기록은 하지 않는다(ShowObjective 가 이미 남겼다).</summary>
    void ShowObjectiveNow(string header, string body)
    {
        _currentBody = body;
        if (objectiveHeaderText != null) objectiveHeaderText.text = header;
        if (objectiveBodyText   != null) objectiveBodyText.text   = body;
        if (objectivePanel != null) objectivePanel.SetActive(true);
        if (hudPanel != null) hudPanel.SetActive(false);

        if (_autoHideCoroutine != null) StopCoroutine(_autoHideCoroutine);
        _autoHideCoroutine = StartCoroutine(AutoHide());
    }

    /// <summary>5초 대기 없이 즉시 패널을 숨기고 HUD로 전환한다.</summary>
    public void HideObjective()
    {
        if (_autoHideCoroutine != null)
        {
            StopCoroutine(_autoHideCoroutine);
            _autoHideCoroutine = null;
        }
        CollapseToHUD();
    }

    IEnumerator AutoHide()
    {
        yield return new WaitForSeconds(3f);
        _autoHideCoroutine = null;
        CollapseToHUD();
    }

    void CollapseToHUD()
    {
        if (objectivePanel != null) objectivePanel.SetActive(false);
        if (hudPanel == null || string.IsNullOrEmpty(_currentBody)) return;
        if (hudText != null) hudText.text = _currentBody;
        hudPanel.SetActive(true);
    }

    /// <summary>HUD 패널을 숨긴다. 중첩 호출을 카운트하여 모든 suppress가 해제돼야 복원된다.</summary>
    public void HideHUD()
    {
        _hudSuppressCount++;
        if (hudPanel != null) hudPanel.SetActive(false);
        if (PlayerStatusUI.Instance != null) PlayerStatusUI.Instance.gameObject.SetActive(false);
        if (InteractionTextUI.Instance != null) InteractionTextUI.Instance.gameObject.SetActive(false);
    }

    /// <summary>suppress 카운터를 하나 줄이고, 0이 되면 HUD 패널을 복원한다.</summary>
    public void RestoreHUD()
    {
        _hudSuppressCount = Mathf.Max(0, _hudSuppressCount - 1);
        if (_hudSuppressCount > 0) return;

        if (!string.IsNullOrEmpty(_currentBody) && hudPanel != null)
            hudPanel.SetActive(true);
        if (PlayerStatusUI.Instance != null) PlayerStatusUI.Instance.gameObject.SetActive(true);
        if (InteractionTextUI.Instance != null) InteractionTextUI.Instance.gameObject.SetActive(true);
    }

    /// <summary>컷씬 종료 시 호출 — Objective 상태를 초기화하고 일반 HUD만 복원한다.</summary>
    public void ResetCutscene()
    {
        if (_autoHideCoroutine != null) { StopCoroutine(_autoHideCoroutine); _autoHideCoroutine = null; }
        if (objectivePanel != null) objectivePanel.SetActive(false);
        if (hudPanel != null) hudPanel.SetActive(false);
        _currentBody = string.Empty;
        _hudSuppressCount = 0;
        if (PlayerStatusUI.Instance != null) PlayerStatusUI.Instance.gameObject.SetActive(true);
        if (InteractionTextUI.Instance != null) InteractionTextUI.Instance.gameObject.SetActive(true);
    }

    /// <summary>퀘스트 완료 시 호출 — 전체 패널 + HUD 패널 모두 숨긴다.</summary>
    public void CompleteObjective()
    {
        JournalManager.CompleteCurrent();
        if (_autoHideCoroutine != null)
        {
            StopCoroutine(_autoHideCoroutine);
            _autoHideCoroutine = null;
        }
        if (objectivePanel != null) objectivePanel.SetActive(false);
        if (hudPanel != null) hudPanel.SetActive(false);
        _currentBody = string.Empty;
        _hudSuppressCount = 0;
    }
}
