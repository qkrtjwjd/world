using UnityEngine;

[RequireComponent(typeof(InteractionTrigger))]
public class ItemPickup : MonoBehaviour
{
    [Header("획득할 아이템")]
    public ItemData itemData;

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
        if (LocalizationManager.Instance == null)
            return $"{itemData.DisplayName} 획득하기";

        string format = LocalizationManager.Instance.GetText("interaction.pickup");
        return format.Contains("{0}")
            ? string.Format(format, itemData.DisplayName)
            : $"{format} {itemData.DisplayName}";
    }

    public void OnPickUp()
    {
        if (itemData == null)
        {
            Debug.LogWarning($"[ItemPickup] '{gameObject.name}' 에 ItemData 가 없습니다.");
            return;
        }

        if (InventoryManager.Instance == null)
        {
            Debug.LogError("[ItemPickup] InventoryManager 를 찾을 수 없습니다.");
            return;
        }

        bool added = InventoryManager.Instance.AddItem(itemData);
        if (added)
        {
            InteractionTextUI.Instance?.Hide();
            Destroy(gameObject);
        }
        else
        {
            // 인벤토리가 꽉 참 — 메시지 표시
            string msg = LocalizationManager.Instance != null
                ? LocalizationManager.Instance.GetText("messages.inventory_full")
                : "인벤토리가 가득 찼습니다!";
            InteractionTextUI.Instance?.Show(msg);
        }
    }
}