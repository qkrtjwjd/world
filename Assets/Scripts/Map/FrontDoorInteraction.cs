using UnityEngine;

/// <summary>
/// S#6 현관문 상호작용.
/// - hasResolve == false : "나가면 안 돼" 대사 출력 (문 차단)
/// - hasResolve == true  : MapScene 으로 씬 전환 (마을 퀘스트 시작)
/// InteractionTrigger.onInteract UnityEvent 에 OnDoorInteract() 를 연결하세요.
/// </summary>
public class FrontDoorInteraction : MonoBehaviour
{
    [Header("결심 전 차단 대사 (DialogueData 에셋 연결)")]
    public DialogueData blockedDialogue;

    /// <summary>InteractionTrigger.onInteract 에 연결.</summary>
    public void OnDoorInteract()
    {
        if (!GameState.hasResolve)
        {
            DialogueManager.Instance?.StartDialogue(blockedDialogue);
        }
        else
        {
            TransitionManager.Instance?.DoSceneTransition(SceneNames.Map);
        }
    }
}
