using System.Collections;
using UnityEngine;
using UnityEngine.UI;

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
    public Text objectiveHeaderText;
    public Text objectiveBodyText;

    [Header("화면 우상단 HUD")]
    public GameObject hudPanel;
    public Text hudText;

    private Coroutine _autoHideCoroutine;
    private string _currentBody;
    private int _hudSuppressCount = 0;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>전체 패널을 표시하고 5초 후 자동으로 HUD로 전환한다.</summary>
    public void ShowObjective(string header, string body)
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
        PlayerStatusUI.Instance?.gameObject.SetActive(false);
        InteractionTextUI.Instance?.gameObject.SetActive(false);
    }

    /// <summary>suppress 카운터를 하나 줄이고, 0이 되면 HUD 패널을 복원한다.</summary>
    public void RestoreHUD()
    {
        _hudSuppressCount = Mathf.Max(0, _hudSuppressCount - 1);
        if (_hudSuppressCount > 0) return;

        if (!string.IsNullOrEmpty(_currentBody) && hudPanel != null)
            hudPanel.SetActive(true);
        PlayerStatusUI.Instance?.gameObject.SetActive(true);
        InteractionTextUI.Instance?.gameObject.SetActive(true);
    }

    /// <summary>컷씬 종료 시 호출 — Objective 상태를 초기화하고 일반 HUD만 복원한다.</summary>
    public void ResetCutscene()
    {
        if (_autoHideCoroutine != null) { StopCoroutine(_autoHideCoroutine); _autoHideCoroutine = null; }
        if (objectivePanel != null) objectivePanel.SetActive(false);
        if (hudPanel != null) hudPanel.SetActive(false);
        _currentBody = string.Empty;
        _hudSuppressCount = 0;
        PlayerStatusUI.Instance?.gameObject.SetActive(true);
        InteractionTextUI.Instance?.gameObject.SetActive(true);
    }

    /// <summary>퀘스트 완료 시 호출 — 전체 패널 + HUD 패널 모두 숨긴다.</summary>
    public void CompleteObjective()
    {
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
