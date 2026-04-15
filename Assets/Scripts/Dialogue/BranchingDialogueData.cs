using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPC 분기 대화 데이터.
/// 첫 대사(openingDialogue) 재생 후 플레이어가 선택지 중 하나를 골라
/// 해당 응답 대사(response)를 들을 수 있습니다.
/// </summary>
[CreateAssetMenu(fileName = "New Branching Dialogue", menuName = "Dialogue/Branching Conversation")]
public class BranchingDialogueData : ScriptableObject
{
    [Header("첫 대사")]
    [Tooltip("NPC 와 처음 대화 시 재생될 대사. null 이면 곧바로 선택지를 표시합니다.")]
    public DialogueData openingDialogue;

    [Header("선택지 목록")]
    [Tooltip("플레이어에게 표시될 선택지 목록")]
    public List<DialogueChoice> choices = new List<DialogueChoice>();

    [Header("옵션")]
    [Tooltip("체크 시: 선택지 응답이 끝난 후 선택지를 다시 표시합니다.")]
    public bool loopChoices = true;
}

/// <summary>
/// 대화 선택지 하나를 나타냅니다.
/// </summary>
[System.Serializable]
public class DialogueChoice
{
    [Tooltip("선택지 버튼에 표시될 텍스트 (한국어)")]
    public string label_ko;

    [Tooltip("선택지 버튼에 표시될 텍스트 (영어)")]
    public string label_en;

    [Tooltip("선택지 버튼에 표시될 텍스트 (일본어)")]
    public string label_jp;

    [Tooltip("이 선택지를 골랐을 때 재생될 NPC 응답 대사")]
    public DialogueData response;

    [Tooltip("체크 시: 한 번 선택하면 이후 비활성화(회색) 처리됩니다.")]
    public bool oneTimeOnly = false;

    // 런타임 상태 (저장 안 됨)
    [System.NonSerialized] public bool hasBeenChosen = false;
}
