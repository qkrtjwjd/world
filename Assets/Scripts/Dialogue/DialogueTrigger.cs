using UnityEngine;

public class DialogueTrigger : MonoBehaviour
{
    [Header("연결할 대화 데이터")]
    public DialogueData dialogueData;

    private bool isPlayerInRange;

    private void Update()
    {
        // 플레이어가 범위 안에 있고, 스페이스바(또는 상호작용 키)를 눌렀을 때
        if (isPlayerInRange && Input.GetKeyDown(KeyCode.Space))
        {
            // 이미 대화 중이 아니라면 시작, 대화 중이라면 다음 문장
            if (!DialogueManager.Instance.isTalking)
            {
                TriggerDialogue();
            }
            else
            {
                DialogueManager.Instance.DisplayNextSentence();
            }
        }
    }

    public void TriggerDialogue()
    {
        DialogueManager.Instance.StartDialogue(dialogueData);
    }

    // 플레이어가 근처에 왔는지 감지 (Collider 필요)
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            isPlayerInRange = true;
            // 여기에 "상호작용 가능" 아이콘 띄우기 가능
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            isPlayerInRange = false;
            // 아이콘 숨기기
            // 멀어지면 대화 강제 종료할 수도 있음: DialogueManager.Instance.EndDialogue();
        }
    }
}