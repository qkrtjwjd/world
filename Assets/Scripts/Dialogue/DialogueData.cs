using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class DialogueLine
{
    [Tooltip("말하는 캐릭터의 이름 (번역이 필요하면 별도 처리 필요)")]
    public string speakerName;

    [Tooltip("한국어 대사")]
    [TextArea(3, 10)] 
    public string sentence_ko;

    [Tooltip("영어 대사")]
    [TextArea(3, 10)] 
    public string sentence_en;

    [Tooltip("일본어 대사")]
    [TextArea(3, 10)] 
    public string sentence_jp;

    [Tooltip("이 대사를 칠 때의 표정/얼굴 이미지")]
    public Sprite portrait;
}

[CreateAssetMenu(fileName = "New Dialogue", menuName = "Dialogue/Conversation")]
public class DialogueData : ScriptableObject
{
    [Header("대화 설정")]
    [Tooltip("대화 목록. 리스트의 순서대로 출력됩니다.")]
    public List<DialogueLine> lines;
}