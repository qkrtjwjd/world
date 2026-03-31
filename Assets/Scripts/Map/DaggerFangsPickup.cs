using UnityEngine;

/// <summary>
/// S#7 단검 '송곳니' 전용 픽업.
/// ItemPickup 컴포넌트와 함께 같은 GameObject 에 부착하세요.
/// 아이템 획득 시 ItemPickup 의 인벤토리 추가에 더해 DaggerSystem.Equip() 을 호출합니다.
/// </summary>
[RequireComponent(typeof(ItemPickup))]
public class DaggerFangsPickup : MonoBehaviour
{
    void Start()
    {
        var trigger = GetComponent<InteractionTrigger>();
        if (trigger != null)
            trigger.onInteract.AddListener(OnPickup);
    }

    void OnPickup()
    {
        DaggerSystem.Instance?.Equip();
    }
}
