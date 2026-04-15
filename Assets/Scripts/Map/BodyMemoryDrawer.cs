using System.Collections;
using UnityEngine;

/// <summary>
/// S#6 부엌 서랍 — 몸의 기억.
/// - 상호작용 시 루의 독백 대사를 재생하고 AtticKey 를 인벤토리에 추가한다.
/// - 한 번 사용하면 InteractionTrigger 가 비활성화되어 재사용 불가.
/// InteractionTrigger.onInteract UnityEvent 에 OnDrawerInteract() 를 연결하세요.
/// </summary>
public class BodyMemoryDrawer : MonoBehaviour
{
    [Header("몸의 기억 독백 대사 (DialogueData 에셋 연결)")]
    public DialogueData memoryDialogue;

    [Header("획득할 열쇠 아이템 (AtticKey ItemData 에셋 연결)")]
    public ItemData atticKeyItem;

    private bool _used = false;

    /// <summary>InteractionTrigger.onInteract 에 연결.</summary>
    public void OnDrawerInteract()
    {
        if (_used) return;
        _used = true;

        var trigger = GetComponent<InteractionTrigger>();
        if (trigger != null) trigger.enabled = false;

        StartCoroutine(DrawerRoutine());
    }

    IEnumerator DrawerRoutine()
    {
        var ctrl = LockPlayer();

        // 아이템 획득 → 알림 표시
        if (atticKeyItem != null)
        {
            Debug.Log($"[Debug] ItemAcquisitionUI.Instance = {ItemAcquisitionUI.Instance}");
            Debug.Log($"[Debug] InventoryManager.Instance = {InventoryManager.Instance}");
            InventoryManager.Instance?.AddItem(atticKeyItem);
        }

        // 알림 표시 시간만큼 대기
        float wait = ItemAcquisitionUI.Instance != null
            ? ItemAcquisitionUI.Instance.displayDuration
            : 2f;
        yield return new WaitForSeconds(wait);

        // 대사 직접 시작 (ItemAcquisitionUI 체인에 의존하지 않음)
        if (memoryDialogue != null && DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartDialogue(memoryDialogue);
            yield return null; // isTalking 활성화 보장
            while (DialogueManager.Instance.isTalking)
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
                    DialogueManager.Instance.DisplayNextSentence();
                yield return null;
            }
        }

        UnlockPlayer(ctrl);
    }

    static ClearSky.SimplePlayerController LockPlayer()
    {
        var ctrl = Object.FindAnyObjectByType<ClearSky.SimplePlayerController>();
        if (ctrl == null) return null;
        ctrl.Lock();
        return ctrl;
    }

    static void UnlockPlayer(ClearSky.SimplePlayerController ctrl)
    {
        if (ctrl != null) ctrl.Unlock();
    }


}
