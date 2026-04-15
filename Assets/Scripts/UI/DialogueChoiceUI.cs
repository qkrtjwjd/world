using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// NPC 분기 대화 선택지 UI.
/// 버튼 목록을 동적으로 생성해 표시하고, 선택 콜백을 호출합니다.
///
/// [Unity 에디터 세팅]
/// 1. Canvas 하위에 Panel 생성 → DialogueChoiceUI 컴포넌트 추가
/// 2. panel        : 루트 패널 GameObject 연결
/// 3. buttonContainer : 버튼들이 들어갈 부모 Transform (VerticalLayoutGroup 권장)
/// 4. choiceButtonPrefab : Button + Text(또는 TMP) 가 포함된 프리팹 연결
/// 5. closeButton  : "대화 종료" 버튼 연결
/// </summary>
public class DialogueChoiceUI : MonoBehaviour
{
    public static DialogueChoiceUI Instance { get; private set; }

    [Header("UI 연결")]
    public GameObject panel;
    public Transform  buttonContainer;
    public GameObject choiceButtonPrefab;
    public Button     closeButton;

    [Header("닫기 버튼 텍스트")]
    public string closeLabel_ko = "대화 종료";

    private readonly List<GameObject> _spawnedButtons = new List<GameObject>();
    private Action<int> _onSelected;
    private Action      _onClose;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);
        if (panel != null) panel.SetActive(false);

        if (closeButton != null)
            closeButton.onClick.AddListener(OnClose);
    }

    /// <summary>
    /// 선택지 UI를 표시합니다.
    /// onSelected: 인덱스를 인자로 받는 선택 콜백
    /// onClose   : 닫기 버튼 콜백
    /// </summary>
    public void Show(List<DialogueChoice> choices, Action<int> onSelected, Action onClose)
    {
        _onSelected = onSelected;
        _onClose    = onClose;

        ClearButtons();

        if (choiceButtonPrefab == null)
        {
            Debug.LogError("[DialogueChoiceUI] choiceButtonPrefab 이 연결되지 않았습니다. 인스펙터에서 설정해주세요.");
            return;
        }

        for (int i = 0; i < choices.Count; i++)
        {
            var choice = choices[i];
            int index  = i; // 클로저 캡처용

            GameObject btn = Instantiate(choiceButtonPrefab, buttonContainer);
            _spawnedButtons.Add(btn);

            // 버튼 텍스트 설정
            string label = GetLabel(choice);
            var btnText = btn.GetComponentInChildren<Text>();
            if (btnText != null) btnText.text = label;

            // oneTimeOnly 이고 이미 선택한 경우 비활성화
            var button = btn.GetComponent<Button>();
            if (button != null)
            {
                bool disabled = choice.oneTimeOnly && choice.hasBeenChosen;
                button.interactable = !disabled;
                button.onClick.AddListener(() => OnChoiceSelected(index, choices));
            }
        }

        // 닫기 버튼 텍스트
        if (closeButton != null)
        {
            var closeTxt = closeButton.GetComponentInChildren<Text>();
            if (closeTxt != null) closeTxt.text = closeLabel_ko;
        }

        if (panel != null) panel.SetActive(true);
    }

    public void Hide()
    {
        ClearButtons();
        if (panel != null) panel.SetActive(false);
    }

    // ─────────────────────────────────────────────
    //  내부 처리
    // ─────────────────────────────────────────────

    void OnChoiceSelected(int index, List<DialogueChoice> choices)
    {
        if (index >= 0 && index < choices.Count)
        {
            choices[index].hasBeenChosen = true;
        }
        Hide();
        _onSelected?.Invoke(index);
    }

    void OnClose()
    {
        Hide();
        _onClose?.Invoke();
    }

    void ClearButtons()
    {
        foreach (var btn in _spawnedButtons)
            if (btn != null) Destroy(btn);
        _spawnedButtons.Clear();
    }

    string GetLabel(DialogueChoice choice)
    {
        if (LocalizationManager.Instance != null)
        {
            switch (LocalizationManager.Instance.currentLanguage)
            {
                case LocalizationManager.Language.EN:
                    if (!string.IsNullOrEmpty(choice.label_en)) return choice.label_en;
                    break;
                case LocalizationManager.Language.JP:
                    if (!string.IsNullOrEmpty(choice.label_jp)) return choice.label_jp;
                    break;
            }
        }
        return string.IsNullOrEmpty(choice.label_ko) ? "(선택지)" : choice.label_ko;
    }
}
