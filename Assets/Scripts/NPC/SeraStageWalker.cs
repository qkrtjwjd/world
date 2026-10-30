using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 컷씬에서 세라를 걷게 하고, 돌려 세우고, 숨기는 공용 도구.
/// NightSequenceManager(S#02) 와 KitchenTriggerCutscene(S#04) 이 같이 쓴다.
///
/// Sera.controller 에는 dir(0=아래 1=옆 2=위) 과 Speed 뿐이라 동작 트리거가 없다.
/// 그래서 연출은 「어디로 걸어가서 어느 쪽을 보고 서는가」로만 만든다.
/// 규칙은 <see cref="SeraPatrol"/> 과 같다 — 좌우는 localScale.x 부호만 뒤집고 **양수가 왼쪽**이다
/// (CLAUDE.md §11 · 통째 대입 금지). <see cref="SeraLightDirector"/> 가 같은 값으로 시선을 읽는다.
///
/// 씬에 미리 붙여 둘 필요는 없다. <see cref="On"/> 이 없으면 런타임에 붙인다.
/// </summary>
[DisallowMultipleComponent]
public class SeraStageWalker : MonoBehaviour
{
    [Tooltip("걷는 속도(월드 유닛/초). 호출부가 따로 주지 않을 때 쓴다.")]
    public float walkSpeed = 1.2f;

    const string DirParam   = "dir";
    const string SpeedParam = "Speed";

    Animator _animator;

    Vector3          _savedPosition;
    Vector3          _savedScale;
    int              _savedDir;
    bool             _saved;
    SpriteRenderer[] _renderers;
    bool[]           _rendererWasEnabled;

    /// <summary>세라의 Animator 에서 도구를 얻는다. 없으면 붙인다. Animator 가 비면 null.</summary>
    public static SeraStageWalker On(Animator seraAnimator)
    {
        if (seraAnimator == null) return null;
        var walker = seraAnimator.GetComponent<SeraStageWalker>();
        if (walker == null) walker = seraAnimator.gameObject.AddComponent<SeraStageWalker>();
        return walker;
    }

    Animator Anim => _animator != null ? _animator : (_animator = GetComponent<Animator>());

    public Vector2 Position => transform.position;

    // ─── 저장 · 복원 ──────────────────────────────

    /// <summary>지금 자리·방향·표시 상태를 기억한다. 컷씬이 끝나면 <see cref="Restore"/> 로 되돌린다.</summary>
    public void Capture()
    {
        _savedPosition = transform.position;
        _savedScale    = transform.localScale;
        _savedDir      = Anim != null ? Anim.GetInteger(DirParam) : 0;
        _renderers     = GetComponentsInChildren<SpriteRenderer>(true);
        _rendererWasEnabled = new bool[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
            _rendererWasEnabled[i] = _renderers[i].enabled;
        _saved = true;
    }

    public void Restore()
    {
        if (!_saved) return;
        StopAllCoroutines();
        transform.position   = _savedPosition;
        transform.localScale = _savedScale;
        if (Anim != null)
        {
            Anim.SetFloat(SpeedParam, 0f);
            Anim.SetInteger(DirParam, _savedDir);
        }
        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i] != null) _renderers[i].enabled = _rendererWasEnabled[i];
        _saved = false;
    }

    // ─── 표시 ─────────────────────────────────────

    /// <summary>모습만 켜고 끈다. 빛(자식 Light2D)은 건드리지 않는다 — 문 밖에서는 빛만 새어 들어와야 한다.</summary>
    public void SetVisible(bool visible)
    {
        if (_renderers == null)
        {
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
            _rendererWasEnabled = new bool[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++) _rendererWasEnabled[i] = _renderers[i].enabled;
        }
        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i] != null && _rendererWasEnabled[i]) _renderers[i].enabled = visible;
    }

    // ─── 방향 · 이동 ──────────────────────────────

    /// <summary>delta 방향을 보게 한다. 가로가 우세하면 옆모습, 아니면 위·아래.</summary>
    public void Face(Vector2 delta)
    {
        if (Mathf.Abs(delta.x) < 0.001f && Mathf.Abs(delta.y) < 0.001f) return;

        Vector3 s = transform.localScale;
        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
        {
            Anim?.SetInteger(DirParam, 1);
            s.x = Mathf.Abs(s.x) * (delta.x > 0f ? -1f : 1f);
        }
        else
        {
            Anim?.SetInteger(DirParam, delta.y > 0f ? 2 : 0);
            s.x = Mathf.Abs(s.x);   // 위·아래 스프라이트는 뒤집지 않는다
        }
        transform.localScale = s;
    }

    public void FaceToward(Vector2 worldPoint) => Face(worldPoint - Position);

    public void Teleport(Vector2 worldPoint)
    {
        transform.position = new Vector3(worldPoint.x, worldPoint.y, transform.position.z);
    }

    /// <summary>
    /// target 까지 곧게 걷는다. speed 가 0 이하면 <see cref="walkSpeed"/>.
    /// 세라 자신의 코루틴으로 돈다 — 호출부는 <c>yield return</c> 로 기다리거나 흘려보내고, <see cref="Stop"/> 으로 세운다.
    /// </summary>
    public Coroutine WalkTo(Vector2 target, float speed = 0f) => StartCoroutine(WalkRoutine(target, speed));

    /// <summary>target 쪽으로 걷되 stopShort 만큼 못 미쳐 멈추고 target 을 본다. 사람에게 다가갈 때 쓴다.</summary>
    public Coroutine WalkNear(Vector2 target, float stopShort, float speed = 0f) => StartCoroutine(WalkNearRoutine(target, stopShort, speed));

    IEnumerator WalkRoutine(Vector2 target, float speed)
    {
        Vector3 start = transform.position;
        Vector3 end   = new Vector3(target.x, target.y, start.z);
        float distance = Vector2.Distance(start, end);
        if (distance < 0.001f) yield break;

        Face(end - start);
        Anim?.SetFloat(SpeedParam, 1f);

        float duration = distance / Mathf.Max(0.01f, speed > 0f ? speed : walkSpeed);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(start, end, elapsed / duration);
            yield return null;
        }
        transform.position = end;
        Anim?.SetFloat(SpeedParam, 0f);
    }

    IEnumerator WalkNearRoutine(Vector2 target, float stopShort, float speed)
    {
        Vector2 d = target - Position;
        if (d.magnitude > stopShort)
            yield return WalkRoutine(target - d.normalized * stopShort, speed);
        FaceToward(target);
    }

    // ─── 가구 돌아가기 ────────────────────────────

    /// <summary>
    /// target 까지 걷되 avoid 사각형(식탁 · 의자 묶음)의 둘레로 돌아간다. 곧게 가도 안 걸리면 <see cref="WalkTo"/> 와 같다.
    /// 출발점이나 target 이 사각형 안이면(식탁 자리에 앉아 있다 · 앉은 루에게 간다) 가장 가까운 변으로 나와서 / 들어가서 걷는다.
    /// stopShort 가 0 보다 크면 마지막 구간에서 그만큼 못 미쳐 멈추고 target 을 본다 — <see cref="WalkNear"/> 와 같다.
    /// </summary>
    public Coroutine WalkAround(Vector2 target, Rect avoid, float speed = 0f, float stopShort = 0f)
        => StartCoroutine(WalkAroundRoutine(target, avoid, speed, stopShort));

    IEnumerator WalkAroundRoutine(Vector2 target, Rect avoid, float speed, float stopShort)
    {
        var path = Route(Position, target, avoid);
        for (int i = 0; i < path.Count; i++)
        {
            Vector2 p = path[i];
            if (i == path.Count - 1 && stopShort > 0f)
            {
                Vector2 d = target - Position;
                if (d.magnitude <= stopShort) break;
                p = target - d.normalized * stopShort;
            }
            yield return WalkRoutine(p, speed);
        }
        if (stopShort > 0f) FaceToward(target);
    }

    /// <summary>from → to 경로의 꺾는 점들(to 포함). 사각형 꼭짓점을 최대 둘 거친다.</summary>
    public static List<Vector2> Route(Vector2 from, Vector2 to, Rect r)
    {
        var path = new List<Vector2>();
        if (r.width <= 0f || r.height <= 0f) { path.Add(to); return path; }

        Vector2 a = r.Contains(from) ? EdgePoint(r, from, to) : from;
        Vector2 b = r.Contains(to)   ? EdgePoint(r, to, from) : to;
        if (a != from) path.Add(a);

        if (Crosses(a, b, r))
        {
            var corners = new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax) };
            Vector2[] best = null;
            float bestCost = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                var c = corners[i];
                if (!Crosses(a, c, r) && !Crosses(c, b, r))
                    Keep(new[] { c }, Vector2.Distance(a, c) + Vector2.Distance(c, b), ref best, ref bestCost);
                foreach (int j in new[] { (i + 1) % 4, (i + 3) % 4 })
                {
                    var c2 = corners[j];
                    if (!Crosses(a, c, r) && !Crosses(c2, b, r))
                        Keep(new[] { c, c2 }, Vector2.Distance(a, c) + Vector2.Distance(c, c2) + Vector2.Distance(c2, b), ref best, ref bestCost);
                }
            }
            if (best != null) path.AddRange(best);
        }

        path.Add(b);
        if (b != to) path.Add(to);
        return path;
    }

    const float InsideWeight = 4f;

    static void Keep(Vector2[] pts, float cost, ref Vector2[] best, ref float bestCost)
    {
        if (cost < bestCost) { best = pts; bestCost = cost; }
    }

    /// <summary>
    /// 사각형 안의 p 에서 변으로 수직으로 나가는 점. 안쪽(가구 위)을 걷는 거리를 <see cref="InsideWeight"/> 배로 쳐서
    /// 가까운 변을 고르고, 비슷하면 toward 쪽 변을 고른다 — 앉은 루에게 갈 때 식탁 반대편 변으로 들어와 식탁을 가로지르지 않게.
    /// </summary>
    static Vector2 EdgePoint(Rect r, Vector2 p, Vector2 toward)
    {
        var cands = new[] { new Vector2(r.xMin, p.y), new Vector2(r.xMax, p.y), new Vector2(p.x, r.yMin), new Vector2(p.x, r.yMax) };
        Vector2 best = cands[0];
        float bestCost = float.MaxValue;
        foreach (var c in cands)
        {
            float cost = InsideWeight * Vector2.Distance(p, c) + Vector2.Distance(c, toward);
            if (cost < bestCost) { best = c; bestCost = cost; }
        }
        return best;
    }

    /// <summary>선분이 사각형 안쪽을 지나는가(변 위를 따라가는 것은 지나지 않는 것으로 본다).</summary>
    static bool Crosses(Vector2 a, Vector2 b, Rect r)
    {
        const float eps = 0.001f;
        float x0 = r.xMin + eps, x1 = r.xMax - eps, y0 = r.yMin + eps, y1 = r.yMax - eps;
        float t0 = 0f, t1 = 1f;
        Vector2 d = b - a;
        float[] p = { -d.x, d.x, -d.y, d.y };
        float[] q = { a.x - x0, x1 - a.x, a.y - y0, y1 - a.y };
        for (int i = 0; i < 4; i++)
        {
            if (Mathf.Abs(p[i]) < 1e-6f) { if (q[i] < 0f) return false; continue; }
            float t = q[i] / p[i];
            if (p[i] < 0f) t0 = Mathf.Max(t0, t); else t1 = Mathf.Min(t1, t);
            if (t0 > t1) return false;
        }
        return t1 - t0 > 1e-4f;
    }

    /// <summary>걷던 중이면 그 자리에 세운다.</summary>
    public void Stop()
    {
        StopAllCoroutines();
        Anim?.SetFloat(SpeedParam, 0f);
    }
}
