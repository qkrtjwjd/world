using System.Collections;
using UnityEngine;

/// <summary>
/// 루가 어느 쪽을 보는지 바꾸는 작은 도구. 애니메이터 dir(0=아래 1=옆 2=위)과
/// localScale.x 부호(양수가 왼쪽 — CLAUDE.md §11)를 함께 맞춘다. FrontDoorInteraction.FaceLuTowardDoor 와 같은 규칙이다.
///
/// 전용 모션(의자를 피해 몸을 돌리는 루 · 소파에 앉은 루)이 아직 없어서(🔴) 방향 전환과 짧은 걸음으로 대신한다.
/// 그림이 오면 애니메이터 트리거를 이 자리에서 걸면 된다.
/// </summary>
public static class LuPose
{
    public static Transform Lu =>
        PlayerStats.Instance != null ? PlayerStats.Instance.transform : GameObject.FindWithTag("Player")?.transform;

    public static void Face(Transform lu, Vector2 d)
    {
        if (lu == null || d.sqrMagnitude < 0.0001f) return;
        int dir = Mathf.Abs(d.x) > Mathf.Abs(d.y) ? 1 : (d.y > 0f ? 2 : 0);
        Vector3 s = lu.localScale;
        s.x = dir == 1 ? Mathf.Abs(s.x) * (d.x > 0f ? -1f : 1f) : Mathf.Abs(s.x);
        lu.localScale = s;
        var anim = lu.GetComponent<Animator>();
        if (anim != null && anim.isActiveAndEnabled)
        {
            foreach (var p in anim.parameters)
            {
                if (p.name == "dir" && p.type == AnimatorControllerParameterType.Int) anim.SetInteger("dir", dir);
                if (p.name == "isRun" && p.type == AnimatorControllerParameterType.Bool) anim.SetBool("isRun", false);
            }
        }
    }

    /// <summary>
    /// 물건을 피해 몸을 돌린다 — 유의 의자 2회째부터(D-4-1 · C-16-11). 물건 반대쪽을 보고 반 칸 물러선다.
    /// 조작을 잠깐(0.4초) 막아 모션이 반응이라는 것이 보이게 한다.
    /// </summary>
    public static IEnumerator TurnAwayFrom(Vector2 thing, float step = 0.25f)
    {
        var lu = Lu;
        if (lu == null) yield break;
        Vector2 away = (Vector2)lu.position - thing;
        if (away.sqrMagnitude < 0.0001f) away = Vector2.down;
        away.Normalize();

        var ctrl = YarnDialogue.LockPlayer();
        Face(lu, away);
        var rb = lu.GetComponent<Rigidbody2D>();
        Vector2 from = lu.position, to = from + away * step;
        const float dur = 0.25f;
        for (float t = 0f; t < dur; t += Time.fixedDeltaTime)
        {
            yield return new WaitForFixedUpdate();
            Vector2 p = Vector2.Lerp(from, to, t / dur);
            // MovePosition 은 쓰지 않는다 — 동적 바디에서는 속도로 바뀌는데 SimplePlayerController.FixedUpdate 가
            // 매 스텝 속도를 덮어써 움직이지 않는다(2026-10-25 실측). 위치를 직접 옮긴다.
            if (rb != null) rb.position = p;
            lu.position = p;
        }
        yield return new WaitForSeconds(0.15f);
        YarnDialogue.UnlockPlayer(ctrl);
    }
}
