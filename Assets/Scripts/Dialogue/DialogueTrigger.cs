using UnityEngine;

public class DialogueTrigger : MonoBehaviour
{
    [Header("연결할 대화 데이터")]
    public DialogueData dialogueData;

    private bool isPlayerInRange;
    private bool _isOwner = false; // 이 DialogueTrigger가 현재 대사를 시작했는지 여부

    private void Update()
    {
        // 대사가 끝났으면 소유권 반납
        if (_isOwner && (DialogueManager.Instance == null || !DialogueManager.Instance.isTalking))
            _isOwner = false;

        if (!isPlayerInRange) return;
        if (!Input.GetKeyDown(KeyCode.Space) && !Input.GetMouseButtonDown(0)) return;

        var dm = DialogueManager.Instance;
        if (dm == null) return;

        if (!dm.isTalking)
        {
            TriggerDialogue();
        }
        else if (_isOwner)
        {
            // 이 DialogueTrigger가 시작한 대사만 진행 (PlayAndWait로 시작된 대사는 건드리지 않음)
            dm.DisplayNextSentence();
        }
    }

    public void TriggerDialogue()
    {
        var dm = DialogueManager.Instance;
        if (dm == null || dm.isTalking) return;
        if (dialogueData == null) return;
        dm.StartDialogue(dialogueData);
        _isOwner = true;
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