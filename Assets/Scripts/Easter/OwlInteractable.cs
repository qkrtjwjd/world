using UnityEngine;

/// <summary>
/// 이스터에그 부엉이 컴포넌트.
///
/// [checkOnProximity = true]  트리거 범위에 플레이어가 들어오면 자동 체크.
/// [checkOnProximity = false] 같은 오브젝트의 InteractionTrigger로 E키를 눌러야 체크.
///                            → InteractionTrigger 컴포넌트를 함께 붙이고
///                              message 필드에 안내 문구를 입력하세요.
///
/// 사용법:
///   1. 부엉이 오브젝트에 이 컴포넌트를 추가합니다.
///   2. owlId를 씬 내에서 겹치지 않게 지정합니다 (예: owl_1, owl_2 …).
///   3. checkOnProximity 방식을 선택합니다.
///   4. Collider2D(Trigger)가 부엉이 오브젝트에 있어야 합니다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class OwlInteractable : MonoBehaviour
{
    [Tooltip("씬 내에서 고유한 부엉이 ID. 예: owl_1, owl_forest_2")]
    public string owlId = "owl_1";

    [Tooltip("true  : 플레이어가 범위 안에 들어오면 자동으로 체크됩니다.\n" +
             "false : 같은 오브젝트에 InteractionTrigger를 추가하고 E키로 상호작용해야 체크됩니다.")]
    public bool checkOnProximity = true;

    private bool _alreadyFound;

    void Start()
    {
        OwlTracker.Register(owlId);
        _alreadyFound = OwlTracker.IsFound(owlId);

        if (!checkOnProximity)
        {
            var trigger = GetComponent<InteractionTrigger>();
            if (trigger != null)
                trigger.onInteract.AddListener(MarkFound);
            else
                Debug.LogWarning($"[OwlInteractable] '{owlId}': checkOnProximity가 false이면 " +
                                 "같은 오브젝트에 InteractionTrigger 컴포넌트가 필요합니다.");
        }
    }

    void OnDestroy()
    {
        if (!checkOnProximity)
        {
            var trigger = GetComponent<InteractionTrigger>();
            if (trigger != null)
                trigger.onInteract.RemoveListener(MarkFound);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!checkOnProximity || _alreadyFound) return;
        if (!other.CompareTag("Player")) return;
        MarkFound();
    }

    /// <summary>외부(InteractionTrigger.onInteract 또는 코드)에서 직접 호출 가능.</summary>
    public void MarkFound()
    {
        if (_alreadyFound) return;
        _alreadyFound = true;
        OwlTracker.MarkFound(owlId);
    }

    // ── 개발 편의 ─────────────────────────────────────────────

    [ContextMenu("이스터에그 상태 전체 초기화 (PlayerPrefs)")]
    void DevResetAll() => OwlTracker.ResetAll();
}
