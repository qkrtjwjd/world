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

        if (memoryDialogue != null)
            DialogueManager.Instance?.StartDialogue(memoryDialogue);

        yield return null;
        while (DialogueManager.Instance != null && DialogueManager.Instance.isTalking)
            yield return null;

        if (atticKeyItem != null)
            InventoryManager.Instance?.AddItem(atticKeyItem);

        UnlockPlayer(ctrl);
    }

    static ClearSky.SimplePlayerController LockPlayer()
    {
        var ctrl = Object.FindAnyObjectByType<ClearSky.SimplePlayerController>();
        if (ctrl == null) return null;
        var rb = ctrl.GetComponent<Rigidbody2D>();
        ctrl.enabled = false;
        if (rb != null) rb.linearVelocity = Vector2.zero;
        return ctrl;
    }

    static void UnlockPlayer(ClearSky.SimplePlayerController ctrl)
    {
        if (ctrl != null) ctrl.enabled = true;
    }
}
