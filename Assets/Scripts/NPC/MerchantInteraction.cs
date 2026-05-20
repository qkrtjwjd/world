using System.Collections;
using UnityEngine;

/// <summary>
/// 상인 NPC 상호작용 컴포넌트.
/// E키 상호작용 시 인사 대사(선택)를 재생한 후 물물교환 UI를 엽니다.
///
/// [사용법]
/// 1. 상인 NPC 오브젝트에 InteractionTrigger 추가
/// 2. 이 컴포넌트 추가
/// 3. merchantData: MerchantData ScriptableObject 연결
///    - 메뉴: Assets > Create > NPC > Merchant Data
/// 4. greetingDialogue: 처음 대화 시 재생할 대사 (선택, 없으면 바로 거래창 열림)
/// 5. lockPlayerDuringGreeting: 인사 대사 중 이동 잠금 여부
/// </summary>
[RequireComponent(typeof(InteractionTrigger))]
public class MerchantInteraction : MonoBehaviour
{
    [Header("상인 데이터")]
    public MerchantData merchantData;

    [Header("인사 대사 (선택)")]
    [Tooltip("상인과 처음 접촉 시 재생할 Yarn 노드 이름. 비워두면 바로 거래창이 열립니다.")]
    public string yarnNode_greeting;

    [Tooltip("인사 대사 중 플레이어 이동 잠금 여부")]
    public bool lockPlayerDuringGreeting = false;

    private bool _isOpen = false;

    void Awake()
    {
        GetComponent<InteractionTrigger>().onInteract.AddListener(OnInteract);
    }

    void OnDestroy()
    {
        var trigger = GetComponent<InteractionTrigger>();
        if (trigger != null)
            trigger.onInteract.RemoveListener(OnInteract);
    }

    void OnInteract()
    {
        if (_isOpen) return;
        if (merchantData == null)
        {
            Debug.LogWarning($"[MerchantInteraction] '{gameObject.name}': merchantData 가 비어 있습니다.");
            return;
        }

        if (YarnDialogue.IsRunning) return;

        StartCoroutine(OpenMerchant());
    }

    IEnumerator OpenMerchant()
    {
        _isOpen = true;

        // 인사 대사 재생
        if (!string.IsNullOrEmpty(yarnNode_greeting))
            yield return YarnDialogue.PlayAndWait(yarnNode_greeting, lockPlayerDuringGreeting);

        // 거래 UI 열기
        if (MerchantUI.Instance != null)
            MerchantUI.Instance.Open(merchantData);
        else
            Debug.LogWarning("[MerchantInteraction] MerchantUI 인스턴스를 찾을 수 없습니다. Canvas 에 MerchantUI 를 배치해주세요.");

        _isOpen = false;
    }
}
