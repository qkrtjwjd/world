using UnityEngine;

/// <summary>
/// 빵 반죽 오브젝트. 상호작용 시 인벤토리에 추가 + 플래그 설정.
/// InteractionTrigger.onInteract에 OnInteract()를 연결하세요.
/// </summary>
public class BreadDoughInteractable : MonoBehaviour
{
    [SerializeField] private ItemData breadDoughItem;

    public void OnInteract()
    {
        if (GameState.isBreadDoughAcquired) return;
        if (InventoryManager.Instance == null) return;

        bool added = InventoryManager.Instance.AddItem(breadDoughItem);
        if (added)
        {
            GameState.isBreadDoughAcquired = true;
            gameObject.SetActive(false);
        }
    }
}
