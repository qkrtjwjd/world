using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DialogueManager : MonoBehaviour
{
    // 어디서든 접근 가능하게 싱글톤 패턴 사용
    public static DialogueManager Instance;

    [Header("UI 연결")]
    public GameObject dialoguePanel; // 대화창 전체 패널
    public Image portraitImage;      // 캐릭터 얼굴 이미지
    public Text nameText;            // 캐릭터 이름 텍스트
    public Text dialogueText;        // 대사 텍스트

    private Queue<DialogueLine> sentences; // 대사들을 담아둘 큐

    // 대화 중인지 확인하는 변수 (플레이어 이동 막기 등에 사용)
    public bool isTalking = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        sentences = new Queue<DialogueLine>();
        dialoguePanel.SetActive(false); // 시작할 땐 꺼둠
    }

    public void StartDialogue(DialogueData dialogue)
    {
        isTalking = true;
        dialoguePanel.SetActive(true);
        
        sentences.Clear();

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

        // 1. 텍스트와 이름 갱신
        nameText.text = currentLine.speakerName;
        
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

        dialogueText.text = sentenceToDisplay;

        // 2. 초상화 갱신 (이미지가 있을 때만)
        if (currentLine.portrait != null)
        {
            portraitImage.sprite = currentLine.portrait;
            portraitImage.gameObject.SetActive(true);
        }
        else
        {
            // 이미지가 없으면 얼굴 칸 숨기기 (선택 사항)
            portraitImage.gameObject.SetActive(false); 
        }

        // TODO: 여기서 타이핑 효과(타자 치는 듯한 연출) 코루틴을 넣을 수도 있음
    }

    public void EndDialogue()
    {
        isTalking = false;
        dialoguePanel.SetActive(false);
        Debug.Log("대화 종료");
    }
}