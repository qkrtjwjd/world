using UnityEngine;

[RequireComponent(typeof(InteractionTrigger))]
public class ItemPickup : MonoBehaviour
{
    [Header("획득할 아이템 설정")]
    [Tooltip("이 오브젝트를 먹었을 때 인벤토리에 들어올 아이템 데이터")]
    public ItemData itemData;

    private InteractionTrigger _trigger;

    void Start()
    {
        _trigger = GetComponent<InteractionTrigger>();
        
        // InteractionTrigger의 이벤트에 OnPickUp 함수를 연결합니다.
        // 이렇게 하면 에디터에서 일일이 연결하지 않아도 자동으로 동작합니다.
        if (_trigger != null)
        {
            _trigger.onInteract.AddListener(OnPickUp);

            // 편의 기능: 메시지를 따로 설정하지 않아도 아이템 이름으로 자동 변경
            if (itemData != null)
            {
                // 번역 매니저가 있으면 번역된 포맷 사용 (예: "{0} 획득하기")
                if (LocalizationManager.Instance != null)
                {
                    string format = LocalizationManager.Instance.GetText("interaction.pickup");
                    // 만약 포맷에 {0}이 없다면 그냥 뒤에 붙이기
                    if (!format.Contains("{0}")) format += " " + itemData.DisplayName;
                    else format = string.Format(format, itemData.DisplayName);

                    _trigger.message = format;
                }
                else
                {
                    // 번역 매니저 없으면 그냥 기본 이름 + " 획득하기"
                    _trigger.message = $"{itemData.DisplayName} 획득하기"; 
                }
            }
        }
    }

    public void OnPickUp()
    {
        // 안전장치: ItemData가 없으면 아무것도 하지 않음 (오브젝트 파괴 방지)
        if (itemData == null)
        {
            Debug.LogWarning($"[ItemPickup] '{gameObject.name}' 오브젝트에 Item Data가 연결되지 않았습니다! 스크립트가 잘못 붙어있는지 확인하세요.");
            return;
        }

        // 인벤토리 매니저가 존재하는지 확인
        if (InventoryManager.Instance != null)
        {
            // 아이템 추가 시도
            bool isAdded = InventoryManager.Instance.AddItem(itemData);
            
            if (isAdded)
            {
                Debug.Log($"아이템 획득 성공: {itemData.itemName}");
                
                // 상호작용 텍스트 숨기기 (오브젝트가 사라지면 OnTriggerExit이 호출되지 않을 수 있으므로)
                if (InteractionTextUI.Instance != null)
                {
                    InteractionTextUI.Instance.Hide();
                }

                // 월드에서 오브젝트 제거
                Destroy(gameObject);
            }
            else
            {
                // 인벤토리가 꽉 찼을 때 처리 (예: 메시지 띄우기)
                // InteractionTextUI 등을 통해 사용자에게 알릴 수도 있습니다.
                Debug.Log("인벤토리가 가득 차서 아이템을 획득할 수 없습니다.");
            }
        }
        else
        {
            Debug.LogError("InventoryManager 인스턴스를 찾을 수 없습니다! 씬에 InventoryManager가 있는지 확인하세요.");
        }
    }
}
