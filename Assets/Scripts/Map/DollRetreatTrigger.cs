using UnityEngine;

/// <summary>
/// DollObject 태그 오브젝트 반경 내 루 진입 시 "DollRetreat" Animator 트리거 + 시선 방향 반전.
/// 이 컴포넌트를 DollObject 태그 오브젝트에 부착하세요.
/// </summary>
public class DollRetreatTrigger : MonoBehaviour
{
    [SerializeField] private float     radius    = 0.84375f;
    [SerializeField] private float     cooldown  = 3f;
    [SerializeField] private LayerMask playerLayer;

    private float _cooldownRemaining;

    void Update()
    {
        if (_cooldownRemaining > 0f)
        {
            _cooldownRemaining -= Time.deltaTime;
            return;
        }

        Collider2D hit = Physics2D.OverlapCircle(transform.position, radius, playerLayer);
        if (hit == null) return;

        _cooldownRemaining = cooldown;

        // 플레이어 Animator "DollRetreat" 트리거
        Animator anim = hit.GetComponentInChildren<Animator>()
                     ?? hit.GetComponent<Animator>();
        anim?.SetTrigger("DollRetreat");

        // 시선 방향 반전 (localScale.x 부호 전환)
        Transform root = hit.transform.root;
        Vector3 s = root.localScale;
        s.x *= -1f;
        root.localScale = s;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.4f, 0.3f);
        Gizmos.DrawSphere(transform.position, radius);
    }
}
