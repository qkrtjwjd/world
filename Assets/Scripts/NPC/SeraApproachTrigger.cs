using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 세라가 솔 구역에 들어왔음을 알리는 최소 트리거.
/// 이 콜라이더에 세라가 들어오면 onSeraApproach 를 발행한다.
///
/// ※ 세라의 순찰 이동과 솔 오브젝트 소멸은 이 컴포넌트의 범위가 아니다.
///    프로젝트에 해당 로직이 아직 없으므로 여기서는 '진입 이벤트'만 담당한다.
///
/// [사용법]
/// 1. 솔 구역에 빈 GameObject 생성 + Collider2D 추가, Is Trigger 체크
/// 2. 이 컴포넌트 추가
/// 3. seraTag 또는 seraLayer 로 세라를 식별 (기본 태그 이름은 인스펙터에서 지정)
/// 4. onSeraApproach 에 SolTradeUI.ForceClose() 연결
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class SeraApproachTrigger : MonoBehaviour
{
    [Header("세라 식별")]
    [Tooltip("이 태그를 가진 오브젝트만 트리거로 인정한다. 비워두면 레이어만으로 판정한다.")]
    public string seraTag = "";

    [Tooltip("이 레이어에 속한 오브젝트만 트리거로 인정한다.")]
    public LayerMask seraLayer = ~0;

    [Header("발행")]
    [Tooltip("세라가 구역에 들어왔을 때 호출된다. SolTradeUI.ForceClose() 를 연결한다.")]
    public UnityEvent onSeraApproach;

    [Tooltip("체크하면 최초 1회만 발행한다.")]
    public bool once = false;

    private bool _fired;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (once && _fired) return;
        if (!IsSera(other)) return;

        _fired = true;
        onSeraApproach?.Invoke();
    }

    bool IsSera(Collider2D other)
    {
        if (other == null) return false;
        if ((seraLayer.value & (1 << other.gameObject.layer)) == 0) return false;
        if (!string.IsNullOrEmpty(seraTag) && !other.CompareTag(seraTag)) return false;
        return true;
    }

    /// <summary>순찰 로직이 생겼을 때 코드에서 직접 부를 수 있는 진입점.</summary>
    public void NotifyApproach()
    {
        if (once && _fired) return;
        _fired = true;
        onSeraApproach?.Invoke();
    }
}
