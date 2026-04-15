using UnityEngine;

/// <summary>
/// 특정 대화가 끝난 후 아이템 픽업 오브젝트를 지정 위치에 스폰합니다.
/// InteractionDialogueTrigger 와 함께 사용합니다.
///
/// [사용법]
/// 1. 오브젝트에 InteractionTrigger + InteractionDialogueTrigger 추가
/// 2. 이 컴포넌트 추가
/// 3. pickupPrefab: ItemPickup 컴포넌트가 포함된 프리팹 연결
/// 4. itemToSpawn: 스폰할 아이템 데이터 연결 (프리팹의 ItemData를 덮어씁니다)
/// 5. spawnPoint: 아이템이 생성될 위치의 Transform 연결 (없으면 이 오브젝트 위치)
/// </summary>
[RequireComponent(typeof(InteractionDialogueTrigger))]
public class PostDialogueItemSpawner : MonoBehaviour
{
    [Header("스폰 설정")]
    [Tooltip("ItemPickup 컴포넌트가 포함된 프리팹. 대화 후 이 프리팹을 인스턴스화합니다.")]
    public GameObject pickupPrefab;

    [Tooltip("스폰할 아이템 데이터. 프리팹의 ItemData를 이 값으로 덮어씁니다.")]
    public ItemData itemToSpawn;

    [Tooltip("스폰할 아이템 수량.")]
    [Min(1)] public int quantity = 1;

    [Tooltip("아이템이 생성될 위치. 비워두면 이 오브젝트의 위치에 스폰됩니다.")]
    public Transform spawnPoint;

    [Header("옵션")]
    [Tooltip("체크 시 최초 1회만 스폰합니다. 해제 시 대화가 재생될 때마다 스폰됩니다.")]
    public bool spawnOnce = true;

    private bool _spawned = false;

    void Awake()
    {
        var dialogueTrigger = GetComponent<InteractionDialogueTrigger>();
        dialogueTrigger.onDialogueComplete.AddListener(Spawn);
    }

    void OnDestroy()
    {
        var dialogueTrigger = GetComponent<InteractionDialogueTrigger>();
        if (dialogueTrigger != null)
            dialogueTrigger.onDialogueComplete.RemoveListener(Spawn);
    }

    void Spawn()
    {
        if (spawnOnce && _spawned) return;
        if (pickupPrefab == null)
        {
            Debug.LogWarning($"[PostDialogueItemSpawner] '{gameObject.name}': pickupPrefab 이 연결되지 않았습니다.");
            return;
        }

        Vector3 position = spawnPoint != null ? spawnPoint.position : transform.position;
        GameObject spawned = Instantiate(pickupPrefab, position, Quaternion.identity);

        // 프리팹의 ItemPickup 데이터를 인스펙터 설정값으로 덮어씀
        var pickup = spawned.GetComponent<ItemPickup>();
        if (pickup != null)
        {
            if (itemToSpawn != null) pickup.itemData = itemToSpawn;
            pickup.quantity = quantity;
        }
        else
        {
            Debug.LogWarning($"[PostDialogueItemSpawner] pickupPrefab '{pickupPrefab.name}' 에 ItemPickup 컴포넌트가 없습니다.");
        }

        _spawned = true;
    }
}
