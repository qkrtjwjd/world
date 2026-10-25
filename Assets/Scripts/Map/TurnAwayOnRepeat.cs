using UnityEngine;

/// <summary>
/// 집_부엌_유의의자 — 1회 오브젝트의 두 번째 조사부터 루가 몸을 돌린다(D-4-1 · C-16-11).
/// InspectableObject.onRepeatInspect 에 <see cref="TurnAway"/> 를 연결한다.
/// 전용 모션(의자를 피해 몸을 돌리는 루)은 아직 없어 방향 전환과 반 칸 물러서기로 대신한다(🔴).
/// </summary>
public class TurnAwayOnRepeat : MonoBehaviour
{
    public void TurnAway() => StartCoroutine(LuPose.TurnAwayFrom(transform.position));
}
