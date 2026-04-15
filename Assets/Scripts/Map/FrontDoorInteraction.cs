using System.Collections;
using UnityEngine;

/// <summary>
/// S#6 현관문 상호작용.
/// - 단검 미장착 : blockedDialogue 대사 출력 (문 차단)
/// - 단검 장착   : departureDialogue 재생 후 MapScene 전환
/// InteractionTrigger.onInteract UnityEvent 에 OnDoorInteract() 를 연결하세요.
/// </summary>
public class FrontDoorInteraction : MonoBehaviour
{
    [Header("단검 미획득 시 차단 대사 (DialogueData 에셋 연결)")]
    public DialogueData blockedDialogue;

    [Header("출발 직전 대사 (단검 획득 후 나갈 때)")]
    public DialogueData departureDialogue;

    private bool _isBusy = false;

    /// <summary>InteractionTrigger.onInteract 에 연결.</summary>
    public void OnDoorInteract()
    {
        if (_isBusy) return;

        if (!DaggerSystem.IsEquipped)
        {
            if (blockedDialogue != null)
                StartCoroutine(DialogueRunner.PlayAndWait(blockedDialogue));
            return;
        }

        // 단검 장착 완료 → 출발 대사 후 씬 전환
        _isBusy = true;
        StartCoroutine(DepartRoutine());
    }

    private IEnumerator DepartRoutine()
    {
        if (departureDialogue != null)
            yield return StartCoroutine(DialogueRunner.PlayAndWait(departureDialogue));
        TransitionManager.Instance?.DoSceneTransition(SceneNames.Map);
    }
}
