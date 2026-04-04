using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DialogueManager : MonoBehaviour
{
    // 어디서든 접근 가능하게 싱글톤 패턴 사용
    public static DialogueManager Instance;

    private static readonly Color COLOR_REALITY = new Color(0.75f, 0.85f, 0.9f);    // 차가운 청백색
    private static readonly Color COLOR_FANTASY = new Color(0.196f, 0.196f, 0.196f); // 기존 환상 대사 색상

    [Header("속마음 스타일")]
    public Color innerMonologueColor = new Color(0.4f, 0.4f, 0.58f); // 어두운 청보라
    public bool  innerMonologueItalic = true;

    [Header("UI 연결")]
    public GameObject dialoguePanel;      // 대화창 전체 패널
    public Image portraitImage;           // 좌측 초상화
    public Image portraitImageRight;      // 우측 초상화 (대화 중 주인공용)
    public Text  nameText;                // 발언자 이름 텍스트 (단일)
    public Text  dialogueText;            // 대사 텍스트

    [Header("플레이어 식별")]
    [Tooltip("대화/독백 판정에 사용할 주인공 이름")]
    public string playerName = "루";

    private Queue<DialogueLine> sentences;   // 대사들을 담아둘 큐
    private bool _isConversation = false;    // 복수 발화자 여부

    // 대화 중인지 확인하는 변수 (플레이어 이동 막기 등에 사용)
    public bool isTalking = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        if (dialoguePanel != null) dialoguePanel.SetActive(false); // 첫 프레임 전에 숨김
    }

    void Start()
    {
        sentences = new Queue<DialogueLine>();
    }

    public void StartDialogue(DialogueData dialogue)
    {
        isTalking = true;
        dialoguePanel.SetActive(true);
        ObjectiveManager.Instance?.HideHUD();
        nameText.gameObject.SetActive(true);
        dialogueText.gameObject.SetActive(true);
        
        sentences.Clear();

        // 대화(복수 발화자) vs 독백(단일 발화자) 자동 감지
        _isConversation = false;
        if (dialogue.lines != null && dialogue.lines.Count > 0)
        {
            string first = dialogue.lines[0].speakerName;
            foreach (DialogueLine line in dialogue.lines)
            {
                if (line.speakerName != first) { _isConversation = true; break; }
            }
        }

        // ScriptableObject에 있는 대사들을 큐에 담기
        foreach (DialogueLine line in dialogue.lines)
        {
            sentences.Enqueue(line);
        }

        DisplayNextSentence();
    }

    public void DisplayNextSentence()
    {
        // 더 이상 할 대사가 없으면 종료
        if (sentences.Count == 0)
        {
            EndDialogue();
            return;
        }

        DialogueLine currentLine = sentences.Dequeue();

        // 1. 이름 및 초상화 방향 결정
        bool useRight  = _isConversation && currentLine.speakerName == playerName;
        nameText.text  = currentLine.speakerName;
        
        // 언어 설정에 따라 대사 선택
        string sentenceToDisplay = currentLine.sentence_ko; // 기본값

        if (LocalizationManager.Instance != null)
        {
            switch (LocalizationManager.Instance.currentLanguage)
            {
                case LocalizationManager.Language.EN:
                    sentenceToDisplay = string.IsNullOrEmpty(currentLine.sentence_en) ? currentLine.sentence_ko : currentLine.sentence_en;
                    break;
                case LocalizationManager.Language.JP:
                    sentenceToDisplay = string.IsNullOrEmpty(currentLine.sentence_jp) ? currentLine.sentence_ko : currentLine.sentence_jp;
                    break;
                // KO는 기본값이므로 생략
            }
        }

        // 현실 상태이면 현실 대사 우선 출력 (속마음보다 우선)
        bool isReality = DaggerFilterController.Instance != null && DaggerFilterController.Instance.IsReality;
        if (isReality)
        {
            string realitySentence = currentLine.sentence_reality_ko;

            if (LocalizationManager.Instance != null)
            {
                switch (LocalizationManager.Instance.currentLanguage)
                {
                    case LocalizationManager.Language.EN:
                        realitySentence = string.IsNullOrEmpty(currentLine.sentence_reality_en)
                            ? currentLine.sentence_reality_ko : currentLine.sentence_reality_en;
                        break;
                    case LocalizationManager.Language.JP:
                        realitySentence = string.IsNullOrEmpty(currentLine.sentence_reality_jp)
                            ? currentLine.sentence_reality_ko : currentLine.sentence_reality_jp;
                        break;
                }
            }

            if (!string.IsNullOrEmpty(realitySentence))
            {
                sentenceToDisplay = realitySentence;
                dialogueText.color = COLOR_REALITY;
            }
            else
            {
                dialogueText.color = COLOR_FANTASY;
            }
            dialogueText.fontStyle = FontStyle.Normal;
            nameText.fontStyle     = FontStyle.Normal;
        }
        else if (currentLine.isInnerMonologue)
        {
            // 속마음: 별도 색상 + 이탤릭
            dialogueText.color     = innerMonologueColor;
            FontStyle style        = innerMonologueItalic ? FontStyle.Italic : FontStyle.Normal;
            dialogueText.fontStyle = style;
            nameText.fontStyle     = style;
        }
        else
        {
            dialogueText.color     = COLOR_FANTASY;
            dialogueText.fontStyle = FontStyle.Normal;
            nameText.fontStyle     = FontStyle.Normal;
        }

        dialogueText.text = sentenceToDisplay;

        // 2. 초상화 갱신
        // 독백 → 좌측 / 대화 중 주인공 → 우측 / 대화 중 상대방 → 좌측
        Image activePortrait   = useRight ? portraitImageRight : portraitImage;
        Image inactivePortrait = useRight ? portraitImage      : portraitImageRight;

        if (inactivePortrait != null) inactivePortrait.gameObject.SetActive(false);

        if (currentLine.portrait != null)
        {
            activePortrait.sprite = currentLine.portrait;
            activePortrait.gameObject.SetActive(true);
        }
        else
        {
            activePortrait.gameObject.SetActive(false);
        }

        // TODO: 여기서 타이핑 효과(타자 치는 듯한 연출) 코루틴을 넣을 수도 있음
    }

    public void EndDialogue()
    {
        isTalking = false;
        dialoguePanel.SetActive(false);
        nameText.gameObject.SetActive(false);
        dialogueText.gameObject.SetActive(false);
        portraitImage.gameObject.SetActive(false);
        if (portraitImageRight != null) portraitImageRight.gameObject.SetActive(false);
        ObjectiveManager.Instance?.RestoreHUD();
        Debug.Log("대화 종료");
    }
}