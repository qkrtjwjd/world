using UnityEngine;

/// <summary>
/// 울타리 문 상호작용.
/// - 상호작용 시 문 오브젝트를 비활성화(사라지게)하여 통과할 수 있게 만든다.
/// - 한 번 열리면 재사용 불가.
///
/// [설정 방법]
/// 1. 이 컴포넌트를 울타리 문 GameObject 에 추가
/// 2. InteractionTrigger.onInteract 에 OnGateInteract() 를 연결
/// 3. objectsToDisable 에 사라질 오브젝트들 (문 스프라이트, 콜라이더 등) 연결
/// 4. (선택) objectsToEnable 에 문이 열린 후 활성화할 오브젝트 연결
/// </summary>
public class FenceGateInteraction : MonoBehaviour
{
    [Header("열릴 때 비활성화할 오브젝트 (문 스프라이트, 콜라이더 등)")]
    public GameObject[] objectsToDisable;

    [Header("열릴 때 활성화할 오브젝트 (선택)")]
    public GameObject[] objectsToEnable;

    private bool _opened = false;

    /// <summary>InteractionTrigger.onInteract 에 연결.</summary>
    public void OnGateInteract()
    {
        if (_opened) return;
        _opened = true;

        foreach (var obj in objectsToDisable)
            if (obj != null) obj.SetActive(false);

        foreach (var obj in objectsToEnable)
            if (obj != null) obj.SetActive(true);

        var trigger = GetComponent<InteractionTrigger>();
        if (trigger != null) trigger.enabled = false;
    }
}
