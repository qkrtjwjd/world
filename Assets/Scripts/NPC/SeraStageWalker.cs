using System.Collections;
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

    /// <summary>걷던 중이면 그 자리에 세운다.</summary>
    public void Stop()
    {
        StopAllCoroutines();
        Anim?.SetFloat(SpeedParam, 0f);
    }
}
