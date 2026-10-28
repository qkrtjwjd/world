using System.Collections;
using UnityEngine;

/// <summary>
/// 집_루의방_마시멜로그릇 (house_lu_room_marshmallow_bowl) — 획득 확인의 예외 (D-4-1 · C-16-11 · E-76).
///
///   루 「엄마가 심심할때마다 먹으라고 했는데 가져가야 하나」
///   가져간다      → 「그래 가져가는게 좋겠지」 · 마시멜로 ×5 · 인형화 +2
///   안 가져간다    → 「엄마한테 혼나진 않겠지...」 · 첫 거절만 인형화 −1 (2~4번째는 대사만)
///   5번째 거절    → 「엄마한테 혼나진 않겠지...」 대신 「역시 가지고 가자」 · 마시멜로 ×5 · 인형화 +2 (강제 획득)
///                  거절 4번 뒤의 조사는 같은 질문 · 선택지에 그 반응 줄만 바뀐 노드 _choice5 로 연다(2026-10-28 사용자 확인 — 앞 줄을 대신한다)
///   가져간 뒤     → 「다른건 없다」(노드 _again)
///
/// 거절 횟수는 방을 나갔다 와도 누적한다 — 플래그 obj.house_lu_room_marshmallow_bowl.refused1~4 로 센다(플래그가 bool 뿐이라).
/// 획득 여부는 obj.….done. 둘 다 저장 슬롯 · 외출 직후 되감기 지점에 들어간다(1시간 · 침대 경로 복귀 · F-6).
/// 집을 때의 +2 는 먹을 때의 +2 와 별개다(부엌 마시멜로와 같은 근거 · C-2-2).
///
/// 획득 수량 · 아이템 · 인형화 값은 InspectableObject 의 획득 칸(acquireItem · acquireCount · acquireCorruption)을 쓴다.
/// </summary>
[RequireComponent(typeof(InspectableObject))]
public class LuRoomMarshmallowBowl : MonoBehaviour, IInspectOverride
{
    [Tooltip("이 번째 거절에 강제로 가져간다(D-4-1 「안 가져간다 5번째」).")]
    public int forcedAt = 5;
    [Tooltip("첫 거절 인형화 변동(데모 고정값 · C-2-2 · F-2-6).")]
    public float firstRefuseCorruption = -1f;

    bool _busy;

    public static string RefusedKey(string id, int n) => $"obj.{id}.refused{n}";

    public static int Refusals(string id)
    {
        var gsm = GameStateManager.Instance;
        if (gsm == null) return 0;
        int n = 0;
        while (n < 16 && gsm.GetFlag(RefusedKey(id, n + 1))) n++;
        return n;
    }

    public bool TryInspect(InspectableObject obj)
    {
        if (_busy) return true;
        StartCoroutine(Run(obj));
        return true;
    }

    IEnumerator Run(InspectableObject obj)
    {
        _busy = true;
        var gsm = GameStateManager.Instance;
        string id = obj.CodeId;

        if (gsm != null && gsm.GetFlag(InspectableObject.DoneKey(id)))
        {
            yield return YarnDialogue.PlayIfExists(id + "_again");
            _busy = false;
            yield break;
        }

        int before = Refusals(id);
        bool last = before + 1 >= forcedAt;
        if (last) yield return obj.PlayAcquireQuestion($"{id}_choice{forcedAt}");
        else      yield return obj.PlayAcquireQuestion();
        int choice = InspectableObject.LastChoice;

        if (choice == 1)
        {
            obj.GiveAcquired();
        }
        else if (choice == 2)
        {
            int n = before + 1;
            if (last)
            {
                obj.GiveAcquired();
            }
            else
            {
                gsm?.SetFlag(RefusedKey(id, n), true);
                if (n == 1) CorruptionManager.Instance?.AddCorruption(firstRefuseCorruption);
                Dbg.Log($"[루의 방] 마시멜로 그릇 거절 {n}번째");
            }
        }
        _busy = false;
    }
}
