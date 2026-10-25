using UnityEngine;

/// <summary>
/// 집_다락방_상자 (house_attic_box) — 진행 오브젝트. 여는 것은 AtticBoxInteraction(InteractionTrigger)이 맡고,
/// 이쪽은 S#12 이후 재조사 「다른건 없다」만 맡는다(D-4-1 · E-75-1). 재조사 반응이 없으면 C-16-4 무반응 금지에 걸린다.
/// </summary>
[RequireComponent(typeof(InspectableObject))]
public class AtticBoxAgainInspect : MonoBehaviour, IInspectOverride
{
    public bool TryInspect(InspectableObject obj)
    {
        // 상자를 열기 전은 진행 쪽(InteractionTrigger)이 받는다. 컷신이 끝나 단검까지 받은 뒤에만 재조사다.
        if (GameState.isDaggerAcquired)
            StartCoroutine(YarnDialogue.PlayIfExists(obj.CodeId + "_again"));
        return true;
    }
}
