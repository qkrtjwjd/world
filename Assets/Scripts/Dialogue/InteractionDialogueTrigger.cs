using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 어떤 오브젝트에도 붙일 수 있는 범용 상호작용 대사 컴포넌트.
/// InteractionTrigger 와 함께 사용하며, E키 상호작용 시 지정한 대사를 재생합니다.
///
/// [사용법]
/// 1. 오브젝트에 InteractionTrigger 추가 (message 설정)
/// 2. 이 컴포넌트 추가
/// 3. Dialogue Data 슬롯에 DialogueData ScriptableObject 연결
///
/// [참고] ItemPickup 과 함께 사용할 수 있습니다.
///        ItemPickup 이 감지되면 아이템 획득 대사가 먼저 재생된 후 이 대사가 이어서 재생됩니다.
/// </summary>
[RequireComponent(typeof(InteractionTrigger))]
public class InteractionDialogueTrigger : MonoBehaviour
{
    [Header("대사 설정")]
    [Tooltip("상호작용 시 재생할 대사. 비워두면 아무것도 하지 않음.")]
    public DialogueData dialogueData;

    [Tooltip("playOnce=true 일 때, 두 번째 이후 상호작용에서 재생할 대사 (null 이면 무시).")]
    public DialogueData repeatDialogue;

    [Header("옵션")]
    [Tooltip("대사 중 플레이어 이동을 잠글지 여부.")]
    public bool lockPlayerDuringDialogue = false;

    [Tooltip("체크 시: 첫 번째 대사 한 번만 재생. 이후엔 repeatDialogue 사용 (null 이면 무시).")]
    public bool playOnce = false;

    [Header("이벤트")]
    [Tooltip("대사가 완전히 끝났을 때 호출됩니다. PostDialogueItemSpawner 등과 연결해 사용하세요.")]
    public UnityEvent onDialogueComplete;

    private bool _hasPlayed = false;
    private InteractionTrigger _trigger;

    void Awake()
    {
        _trigger = GetComponent<InteractionTrigger>();
        _trigger.onInteract.RemoveListener(OnInteract); // 중복 리스너 방지
        _trigger.onInteract.AddListener(OnInteract);
    }

    void OnDestroy()
    {
        if (_trigger != null)
            _trigger.onInteract.RemoveListener(OnInteract);
    }

    public void OnInteract()
    {
        var dm = DialogueManager.Instance;
        if (dm == null) return;
        if (dm.isTalking) return;

        DialogueData toPlay;

        if (playOnce && _hasPlayed)
        {
            if (repeatDialogue == null) return;
            toPlay = repeatDialogue;
        }
        else
        {
            if (dialogueData == null)
            {
                Debug.LogWarning($"[InteractionDialogueTrigger] '{gameObject.name}': dialogueData 가 비어 있습니다.");
                return;
            }
            toPlay = dialogueData;
            _hasPlayed = true;
        }

        // ItemPickup이 함께 있으면 오브젝트가 Destroy되므로
        // DialogueManager에서 코루틴을 시작해 ItemPickup 대사가 끝난 후 재생
        if (GetComponent<ItemPickup>() != null)
        {
            dm.StartCoroutine(WaitForAcquisitionThenPlay(toPlay, lockPlayerDuringDialogue));
            return;
        }

        // 이 오브젝트가 Destroy되더라도 코루틴이 중단되지 않도록 DialogueManager에서 실행
        dm.StartCoroutine(PlayAndNotify(toPlay, lockPlayerDuringDialogue));
    }

    // onDialogueComplete 이벤트를 발행하는 래퍼 코루틴
    private IEnumerator PlayAndNotify(DialogueData data, bool lockPlayer)
    {
        yield return DialogueRunner.PlayAndWait(data, lockPlayer);
        onDialogueComplete?.Invoke();
    }

    private IEnumerator WaitForAcquisitionThenPlay(DialogueData data, bool lockPlayer)
    {
        yield return null; // ItemPickup.OnPickUp() 완료까지 1프레임 대기

        // 아이템 획득 UI가 사라지고 대사도 끝날 때까지 대기
        yield return new WaitUntil(() =>
        {
            var ui = ItemAcquisitionUI.Instance;
            bool uiGone = ui == null || !ui.IsShowing;
            bool notTalking = DialogueManager.Instance == null || !DialogueManager.Instance.isTalking;
            return uiGone && notTalking;
        });

        yield return DialogueRunner.PlayAndWait(data, lockPlayer);
        onDialogueComplete?.Invoke();
    }
}
