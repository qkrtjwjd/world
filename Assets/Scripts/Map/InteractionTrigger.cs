using UnityEngine;
using UnityEngine.Events;

public class InteractionTrigger : MonoBehaviour
{
    [Header("설정")]
    [Tooltip("상호작용 시 화면에 띄울 문구")]
    public string message = "E키를 눌러 상호작용";

    [Tooltip("체크하면 처음 한 번만 글씨를 띄우고, 그 다음부터는 글씨 없이 상호작용만 됩니다.")]
    public bool hideTextAfterFirstView = false;

    // 이미 글씨를 보여줬는지 체크하는 변수
    [HideInInspector] public bool hasShownText = false;

    [Tooltip("E키를 눌렀을 때 실행할 기능들을 여기에 연결하세요.")]
    public UnityEvent onInteract; 

    // 쿨타임 관련 변수
    private bool _canInteract = false;

    // 외부(Manager)에서 호출하는 함수
    public void Interact()
    {
        // 준비되지 않았으면 무시
        if (!_canInteract) return;

        Debug.Log($"[InteractionTrigger] Interact 호출됨. 연결된 이벤트 수: {onInteract.GetPersistentEventCount()}");
        onInteract.Invoke();

        // 만약 '한 번만 보기' 옵션이 켜져있다면, 상호작용 후 즉시 글씨를 끄기
        if (hideTextAfterFirstView && InteractionTextUI.Instance != null)
        {
            InteractionTextUI.Instance.Hide();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // ★ 디버깅: 무엇이 닿았는지 확인
        // Debug.Log($"[InteractionTrigger] 감지됨! 닿은 오브젝트: {other.name}, 태그: {other.tag}");

        if (other.CompareTag("Player"))
        {
            // Debug.Log("[InteractionTrigger] 플레이어 인식 성공! 잠시 후 상호작용 가능 상태로 전환합니다.");
            
            // 즉시 등록하지 않고, 딜레이를 줍니다. (중복 입력 방지)
            StartCoroutine(EnableInteractionRoutine());
        }
    }

    private System.Collections.IEnumerator EnableInteractionRoutine()
    {
        // 딜레이 없이 즉시 상호작용 가능하게 변경
        yield return null; 

        _canInteract = true;

        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.RegisterTrigger(this);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _canInteract = false; // 나가면 즉시 비활성화
            StopAllCoroutines();  // 대기 중이던 루틴 취소

            if (InteractionManager.Instance != null)
            {
                InteractionManager.Instance.UnregisterTrigger(this);
            }
        }
    }

    private void OnDisable()
    {
        // 오브젝트가 꺼지거나 파괴될 때 매니저 목록에서 제거
        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.UnregisterTrigger(this);
        }
    }
}
