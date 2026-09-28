using UnityEngine;

/// <summary>
/// 튜토리얼 전투를 발동하는 트리거 컴포넌트.
/// EnemyEncounterTrigger 대신 이 컴포넌트를 사용하세요.
///
/// 사용법:
///   - 첫 번째 적 오브젝트에 붙이고 tutorialStep = 0 설정
///   - 두 번째 적 오브젝트에 붙이고 tutorialStep = 1 설정
///   - 해당 step이 GameState.tutorialBattleStep과 일치할 때만 전투가 발동됩니다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class TutorialEnemyTrigger : MonoBehaviour
{
    [Tooltip("0 = 첫 번째 전투(턴제), 1 = 두 번째 전투(핵앤슬래시)")]
    [Range(0, 1)]
    public int tutorialStep = 0;

    private bool _triggered = false;

    private void OnCollisionEnter2D(Collision2D collision) => TryTrigger(collision.gameObject);
    private void OnTriggerEnter2D(Collider2D other)        => TryTrigger(other.gameObject);

    void TryTrigger(GameObject obj)
    {
        if (_triggered) return;
        if (!obj.CompareTag("Player")) return;

        // 이미 이 단계를 완료했거나 아직 이 단계가 아니면 무시
        if (GameState.tutorialBattleStep != tutorialStep) return;
        // 쿠루가 합류하기 전에는 발동하지 않는다 — S#16D 「따라와」 → S#17(D 789). 2026-09-27: 전에는 쿠루 바로 옆의
        //   첫 늑대가 처음부터 살아 있어, 쿠루에게 다가가다 재회보다 전투가 먼저 날 수 있었다.
        if (!GameState.isKuruJoined) return;

        if (TutorialBattleManager.Instance == null)
        {
            Debug.LogWarning("[TutorialEnemyTrigger] TutorialBattleManager가 씬에 없습니다. " +
                             "씬에 TutorialBattleManager를 배치해주세요.");
            return;
        }

        _triggered = true;
        TutorialBattleManager.Instance.StartTutorialEncounter(tutorialStep, gameObject);
    }
}
