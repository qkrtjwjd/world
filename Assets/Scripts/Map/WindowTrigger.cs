using UnityEngine;

/// <summary>
/// 플레이어가 창문에 다다른 것을 알린다.
///
/// 두 곳에서 쓴다.
///   1. S#01 루의 방 창문 — NightSequenceManager 에 알려 S#02 로 넘어간다. 트리거존 진입이 신호다.
///   2. S#04F 거실 창문   — KitchenTriggerCutscene 이 HasReached 를 폴링해 마당의 각설탕을 보여준다.
///      신호는 창문 <b>조사</b>다(D-S#04F 「[상호작용] 거실 창문 → 창밖 보기」 · E-72-2).
///      <see cref="reachOnInspectOnly"/> 를 켜고 같은 오브젝트의 LivingWindowInspect 가 <see cref="MarkReached"/> 를 부른다.
///
/// 2번은 sequenceManager 를 비워 둔 채로 쓴다.
/// </summary>
public class WindowTrigger : MonoBehaviour
{
    [Tooltip("S#01 전용. S#04F 거실 창문에서는 비워 둡니다.")]
    public NightSequenceManager sequenceManager;

    [Tooltip("트리거존 진입을 신호로 쓰지 않는다 — 조사(MarkReached)만 받는다. S#04F 거실 창문.")]
    public bool reachOnInspectOnly = false;

    /// <summary>Arm() 이후 플레이어가 이 창문에 다다랐는지 여부.</summary>
    public bool HasReached { get; private set; }

    /// <summary>컷신이 이 창문을 기다리는 중인가(Arm 뒤 아직 다다르지 않음).</summary>
    public bool IsArmed { get; private set; }

    /// <summary>대기를 시작하기 직전에 호출해 이전 진입 기록을 지웁니다.</summary>
    public void Arm()
    {
        HasReached = false;
        IsArmed = true;
    }

    /// <summary>조사로 다다랐다. 기다리는 중일 때만 의미가 있다.</summary>
    public void MarkReached()
    {
        if (!IsArmed) return;
        HasReached = true;
        IsArmed = false;
    }

    /// <summary>컷신이 기다리기를 그만둘 때(시간 초과로 자동 진행).</summary>
    public void Disarm() => IsArmed = false;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (reachOnInspectOnly) return;
        if (!other.CompareTag("Player")) return;

        HasReached = true;
        IsArmed = false;
        sequenceManager?.OnWindowReached();
    }
}
