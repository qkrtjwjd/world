using UnityEngine;

/// <summary>
/// S#6 다락방 문 잠금.
/// - AtticKey 미보유 시 잠겨있다는 대사를 출력한다.
/// - AtticKey 보유 시 문 오브젝트를 비활성화하고 다락방 진입을 활성화한다.
/// InteractionTrigger.onInteract UnityEvent 에 OnAtticDoorInteract() 를 연결하세요.
/// </summary>
public class LockedDoorInteraction : MonoBehaviour
{
    [Header("잠금 해제에 필요한 아이템 이름 (ItemData.itemName 과 일치해야 함)")]
    public string requiredItemName = "AtticKey";

    [Header("잠겨있을 때 대사 (DialogueData 에셋 연결)")]
    public DialogueData lockedDialogue;

    [Header("문 열릴 때 활성화할 오브젝트 (다락방 RoomTransfer 등)")]
    public GameObject[] objectsToEnable;

    [Header("문 열릴 때 비활성화할 오브젝트 (문 스프라이트, 이 콜라이더 등)")]
    public GameObject[] objectsToDisable;

    private bool _unlocked = false;

    /// <summary>InteractionTrigger.onInteract 에 연결.</summary>
    public void OnAtticDoorInteract()
    {
        if (_unlocked) return;

        bool hasKey = InventoryManager.Instance != null
                      && InventoryManager.Instance.HasItem(requiredItemName);

        if (hasKey)
            UnlockAttic();
        else
            DialogueManager.Instance?.StartDialogue(lockedDialogue);
    }

    void UnlockAttic()
    {
        _unlocked = true;

        foreach (var obj in objectsToEnable)
            if (obj != null) obj.SetActive(true);

        foreach (var obj in objectsToDisable)
            if (obj != null) obj.SetActive(false);

        var trigger = GetComponent<InteractionTrigger>();
        if (trigger != null) trigger.enabled = false;
    }
}
