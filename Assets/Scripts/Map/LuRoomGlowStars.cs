using UnityEngine;

/// <summary>
/// 집_루의방_야광별 (house_lu_room_glow_stars) — 밀도 (D-4-1).
///
///   첫 조사       루 「비가 와서 별을 볼 수 없는 날이면 …」 · 인형화 −1
///   두 번째부터    루 「요즘은 별을 본적이 없네」 「그땐 좋았었지」 — 노드 _2. 세 번째부터도 같다
///
/// 장난감 상자를 밀기 전에는 상자에 가려 보이지 않으며 조사할 수 없다 — 이 오브젝트는 LuRoomToybox 가 켠다.
/// 대비 오브젝트가 아니므로 필터와 무관하다. 첫 조사 상태는 obj.house_lu_room_glow_stars.done 이고
/// 되감기 지점(S#11 직후)으로 돌아가면 인형화와 함께 돌아간다(F-6).
/// </summary>
[RequireComponent(typeof(InspectableObject))]
public class LuRoomGlowStars : MonoBehaviour, IInspectOverride
{
    [Tooltip("첫 조사 인형화 변동(데모 고정값 · C-2-2 · F-2-6).")]
    public float firstCorruption = -1f;

    bool _busy;

    public bool TryInspect(InspectableObject obj)
    {
        if (_busy) return true;
        StartCoroutine(Run(obj));
        return true;
    }

    System.Collections.IEnumerator Run(InspectableObject obj)
    {
        _busy = true;
        var gsm = GameStateManager.Instance;
        string key = InspectableObject.DoneKey(obj.CodeId);
        bool seen = gsm != null && gsm.GetFlag(key);
        if (!seen)
        {
            gsm?.SetFlag(key, true);
            CorruptionManager.Instance?.AddCorruption(firstCorruption);
            yield return YarnDialogue.PlayIfExists(obj.CodeId);
        }
        else
        {
            yield return YarnDialogue.PlayIfExists(obj.CodeId + "_2");
        }
        _busy = false;
    }
}
