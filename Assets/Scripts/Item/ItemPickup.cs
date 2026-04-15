using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct ExtraItemEntry
{
    public ItemData item;
    [Min(1)] public int quantity;
}

[RequireComponent(typeof(InteractionTrigger))]
public class ItemPickup : MonoBehaviour
{
    [Header("획득할 아이템")]
    public ItemData itemData;
    [Min(1)] public int quantity = 1;                                    // itemData 수량
    public List<ExtraItemEntry> extraItems = new List<ExtraItemEntry>(); // 다른 종류 추가 아이템

    [Header("획득 후 대사 (없으면 비워두세요)")]
    public DialogueData dialogueAfterPickup;

    [Header("순서 설정")]
    [Tooltip("체크 시: 대사를 먼저 재생한 후 아이템을 획득합니다.\n해제 시: 아이템 먼저 획득 후 대사를 재생합니다.")]
    public bool dialogueFirst = false;

    private InteractionTrigger _trigger;

    void Start()
    {
        _trigger = GetComponent<InteractionTrigger>();
        if (_trigger == null) return;

        _trigger.onInteract.AddListener(OnPickUp);

        if (itemData != null)
            _trigger.message = BuildPickupMessage();
    }

    string BuildPickupMessage()
    {
        string name = itemData != null ? itemData.DisplayName : "";
        string baseMsg;
        if (LocalizationManager.Instance == null)
        {
            baseMsg = $"{name} 획득하기";
        }
        else
        {
            string format = LocalizationManager.Instance.GetText("interaction.pickup");
            baseMsg = format.Contains("{0}")
                ? string.Format(format, name)
                : $"{format} {name}";
        }

        int extraTotal = 0;
        foreach (var e in extraItems) extraTotal += e.quantity;
        int total = (itemData != null ? quantity : 0) + extraTotal;
        if (total > 1) baseMsg += $" x{total}";
        return baseMsg;
    }

    public void OnPickUp()
    {
        if (InventoryManager.Instance == null)
        {
            Debug.LogError("[ItemPickup] InventoryManager 를 찾을 수 없습니다.");
            return;
        }

        // 획득할 아이템 목록 구성
        var toAdd = new List<ItemData>();

        if (itemData != null)
            for (int i = 0; i < quantity; i++)
                toAdd.Add(itemData);

        foreach (var entry in extraItems)
            if (entry.item != null)
                for (int i = 0; i < entry.quantity; i++)
                    toAdd.Add(entry.item);

        if (toAdd.Count == 0)
        {
            Debug.LogWarning($"[ItemPickup] '{gameObject.name}' 에 ItemData 가 없습니다.");
            return;
        }

        string fullMsg = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.GetText("messages.inventory_full")
            : "인벤토리가 가득 찼습니다!";

        if (InventoryManager.Instance.FreeSlots < toAdd.Count)
        {
            InteractionTextUI.Instance?.Show(fullMsg);
            return;
        }

        // 대사 먼저: DialogueManager 코루틴에서 실행 (Destroy 후에도 안전)
        if (dialogueFirst && dialogueAfterPickup != null)
        {
            var dm = DialogueManager.Instance;
            if (dm != null)
            {
                dm.StartCoroutine(TalkFirstCoroutine(toAdd, dialogueAfterPickup, gameObject));
                return;
            }
        }

        // 아이템 먼저 (기존 동작)
        int added = InventoryManager.Instance.AddItems(toAdd);
        if (added > 0)
        {
            if (dialogueAfterPickup != null)
                ItemAcquisitionUI.Instance?.SetPendingDialogue(dialogueAfterPickup);
            InteractionTextUI.Instance?.Hide();
            Destroy(gameObject);
        }
        else
        {
            InteractionTextUI.Instance?.Show(fullMsg);
        }
    }

    // 대사 먼저 재생 후 아이템 획득
    private static IEnumerator TalkFirstCoroutine(
        List<ItemData> items, DialogueData dialogue, GameObject pickupObject)
    {
        yield return DialogueRunner.PlayAndWait(dialogue);

        if (InventoryManager.Instance == null) yield break;

        int added = InventoryManager.Instance.AddItems(items);
        if (added > 0)
        {
            InteractionTextUI.Instance?.Hide();
            if (pickupObject != null) Destroy(pickupObject);
        }
        else
        {
            string fullMsg = LocalizationManager.Instance != null
                ? LocalizationManager.Instance.GetText("messages.inventory_full")
                : "인벤토리가 가득 찼습니다!";
            InteractionTextUI.Instance?.Show(fullMsg);
        }
    }
}
