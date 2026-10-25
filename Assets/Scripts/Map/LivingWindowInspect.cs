using System.Collections;
using UnityEngine;

/// <summary>
/// 집_거실_창문 (house_living_window) — 진행도에 따라 다르게 보이는 유일한 물건(C-16-9 예외 · E-72-3 · D-4-1).
///
///   S#04F 이전          대사창 없음 · 오버레이 없음. 각설탕이 아직 마당에 없다.
///   S#04F               진행. 툭 소리 뒤 이 창문을 조사하면 마당의 각설탕 씬이 이어진다(KitchenTriggerCutscene).
///   S#04F 이후 · 녹기 전  대사창 없음. 「마당의 각설탕」 오버레이를 다시 띄운다.
///   녹은 뒤 첫 조사      「마당의 녹은 각설탕」 오버레이 + 루 대사 1줄. 대사는 한 번만.
///   그 이후             아무것도 띄우지 않는다. 녹은 각설탕은 창 너머 배경으로만 보인다.
///
/// 창밖을 보는 조사는 결계 접촉이 아니다 — S#12 제한 시간 중에도 같은 처리이며 접촉 반응을 걸지 않는다(C-14-2-3).
/// 대비 오브젝트가 아니므로 현실 필터에서도 같은 오버레이 1벌이다.
/// 조사로 연 오버레이이므로 확인 키로 닫는다(F-3-9).
/// </summary>
[RequireComponent(typeof(InspectableObject))]
public class LivingWindowInspect : MonoBehaviour, IInspectOverride
{
    [Tooltip("S#04F 에서 KitchenTriggerCutscene 이 기다리는 창문 신호. 같은 오브젝트의 WindowTrigger.")]
    public WindowTrigger yardLook;

    bool _busy;

    public bool TryInspect(InspectableObject obj)
    {
        if (_busy) return true;

        // S#04F — 컷신이 창밖 보기를 기다리는 중이면 이 조사가 그 신호다.
        if (yardLook != null && yardLook.IsArmed)
        {
            yardLook.MarkReached();
            return true;
        }

        // S#04F 이전 — 무반응(각설탕이 아직 없다).
        if (!GameState.isYardSugarSeen) return true;

        // S#04G ~ S#04H 「방으로 돌아가세요」 — 세라가 나가기 전에는 루의 방 문만 반응한다(E-74-1 · D-4-1).
        if (!GameState.isSeraOut) return true;

        if (!HouseClock.SugarMelted)
        {
            StartCoroutine(Run(OverlayIds.HouseYardSugarcube, null));
            return true;
        }

        var gsm = GameStateManager.Instance;
        if (gsm != null && !gsm.GetFlag(HouseClock.FlagMeltLineShown))
        {
            gsm.SetFlag(HouseClock.FlagMeltLineShown, true);
            StartCoroutine(Run(OverlayIds.HouseYardSugarcubeMelted, obj.CodeId));
            return true;
        }

        return true;   // 그 이후 — 아무것도 띄우지 않는다
    }

    IEnumerator Run(string overlayId, string node)
    {
        _busy = true;
        OverlayCut.Instance.Show(overlayId);
        if (!string.IsNullOrEmpty(node))
            yield return YarnDialogue.PlayIfExists(node);
        yield return OverlayCut.Instance.WaitForClose();
        _busy = false;
    }
}
