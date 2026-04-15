using System.Collections;
using UnityEngine;

/// <summary>
/// NPC 분기 대화 트리거.
/// 첫 대사 재생 후 플레이어가 선택지 중 하나를 골라 대화를 진행합니다.
/// loopChoices=true 이면 응답 후 선택지를 다시 표시합니다.
///
/// [사용법]
/// 1. NPC 오브젝트에 InteractionTrigger 추가
/// 2. 이 컴포넌트 추가
/// 3. BranchingDialogueData ScriptableObject 연결
///    - 메뉴: Assets > Create > Dialogue > Branching Conversation
/// </summary>
[RequireComponent(typeof(InteractionTrigger))]
public class NPCBranchingDialogueTrigger : MonoBehaviour
{
    [Header("분기 대화 데이터")]
    public BranchingDialogueData branchingData;

    [Header("옵션")]
    [Tooltip("대사 중 플레이어 이동 잠금 여부")]
    public bool lockPlayerDuringDialogue = true;

    private bool _isRunning = false;
    private ClearSky.SimplePlayerController _playerCtrl;

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
        if (_isRunning) return;
        if (branchingData == null)
        {
            Debug.LogWarning($"[NPCBranchingDialogueTrigger] '{gameObject.name}': branchingData 가 비어 있습니다.");
            return;
        }
        var dm = DialogueManager.Instance;
        if (dm != null && dm.isTalking) return;

        StartCoroutine(RunBranchingDialogue());
    }

    IEnumerator RunBranchingDialogue()
    {
        _isRunning = true;

        if (lockPlayerDuringDialogue)
            _playerCtrl = DialogueRunner.LockPlayer();

        // 1. 첫 대사 재생
        if (branchingData.openingDialogue != null)
            yield return DialogueRunner.PlayAndWait(branchingData.openingDialogue, lockPlayer: false);

        // 2. 선택지 루프
        if (DialogueChoiceUI.Instance == null)
        {
            Debug.LogWarning("[NPCBranchingDialogueTrigger] DialogueChoiceUI 인스턴스가 없습니다. Canvas 에 DialogueChoiceUI 를 배치해주세요.");
            if (lockPlayerDuringDialogue && _playerCtrl != null)
                DialogueRunner.UnlockPlayer(_playerCtrl);
            _isRunning = false;
            yield break;
        }

        bool keepLooping = true;
        while (keepLooping)
        {
            bool waitingForChoice = true;
            int  selectedIndex    = -1;
            bool closed           = false;

            DialogueChoiceUI.Instance.Show(
                branchingData.choices,
                onSelected: (idx) => { selectedIndex = idx; waitingForChoice = false; },
                onClose:    ()    => { closed = true;        waitingForChoice = false; }
            );

            // 플레이어가 선택하거나 닫을 때까지 대기
            yield return new WaitUntil(() => !waitingForChoice);

            if (closed)
            {
                keepLooping = false;
                break;
            }

            // 선택한 응답 대사 재생
            if (selectedIndex >= 0 && selectedIndex < branchingData.choices.Count)
            {
                var choice = branchingData.choices[selectedIndex];
                if (choice.response != null)
                    yield return DialogueRunner.PlayAndWait(choice.response, lockPlayer: false);
            }

            // loopChoices=false 이면 한 번만 선택
            if (!branchingData.loopChoices)
                keepLooping = false;
        }

        if (lockPlayerDuringDialogue && _playerCtrl != null)
            DialogueRunner.UnlockPlayer(_playerCtrl);

        _isRunning = false;
    }
}
