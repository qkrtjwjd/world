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

    [Tooltip("1차 전투(S#17A)는 닿을 때가 아니라 개가 화면에 들어오는 순간 시작한다(D 807). " +
             "뷰포트 가장자리에서 이만큼 안쪽에 들어와야 「들어왔다」로 본다.")]
    [Range(0f, 0.3f)]
    public float sightViewportMargin = 0.1f;

    private bool _triggered = false;

    void Start()
    {
        // 튜토리얼 늑대는 필드에서 쫓아오지 않는다 — 「커다란 개가 길 한가운데 서 있었다」(D 808).
        //   2026-09-28: 전에는 EnemyAI 가 맵이 열리는 순간부터 루를 쫓아와 필드에서 때렸다(S#17 이 HP 80~90 으로 시작했다).
        //   2차(S#19 액션) 는 HackSlashCombatManager.CombatLoop 가 전투 시작 때 추적을 다시 켠다.
        GetComponent<EnemyAI>()?.SetChase(false);
    }

    void Update()
    {
        // S#17A — 「루를 따라가다 개가 화면에 들어오는 순간 고정」(D 807). 1차 전투만 시야로 시작한다.
        if (tutorialStep != 0 || _triggered) return;
        if (GameState.tutorialBattleStep != tutorialStep || !GameState.isKuruJoined) return;
        if (YarnDialogue.IsRunning || BattleSystem.IsActive) return;
        var cam = Camera.main;
        if (cam == null || PlayerStats.Instance == null) return;
        Vector3 v = cam.WorldToViewportPoint(transform.position);
        float m = sightViewportMargin;
        if (v.x < m || v.x > 1f - m || v.y < m || v.y > 1f - m) return;
        TryTrigger(PlayerStats.Instance.gameObject);
    }

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
