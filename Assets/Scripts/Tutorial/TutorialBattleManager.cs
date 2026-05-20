using System.Collections;
using UnityEngine;

/// <summary>
/// 튜토리얼 전투 흐름을 총괄하는 싱글톤.
///
/// - 첫 번째 전투 (step 0): 게이지 무관하게 턴제 강제 진입, 전후 대사 재생
/// - 두 번째 전투 (step 1): 게이지 무관하게 핵앤슬래시 강제 진입, 전후 대사 재생
///
/// 사용법:
///   1. 씬에 빈 GameObject를 만들고 이 컴포넌트를 붙이세요.
///   2. Inspector에서 yarnNode_preBattle1 등 Yarn 노드 이름 4개를 설정하세요.
///   3. 튜토리얼 적 오브젝트에 TutorialEnemyTrigger를 붙이고 tutorialStep을 지정하세요.
/// </summary>
public class TutorialBattleManager : MonoBehaviour
{
    public static TutorialBattleManager Instance { get; private set; }

    [Header("1번 전투 — 턴제")]
    [Tooltip("첫 번째 전투 시작 전 재생할 Yarn 노드 이름")]
    public string       yarnNode_preBattle1;
    [Tooltip("첫 번째 전투에 등장할 적 프리팹 (Unit 컴포넌트 필수)")]
    public GameObject   battle1EnemyPrefab;
    [Tooltip("첫 번째 전투 종료 후 재생할 Yarn 노드 이름")]
    public string       yarnNode_postBattle1;

    [Header("2번 전투 — 핵앤슬래시")]
    [Tooltip("두 번째 전투 시작 전 재생할 Yarn 노드 이름")]
    public string       yarnNode_preBattle2;
    [Tooltip("두 번째 전투에 등장할 적 프리팹 (씬에 이미 있는 적이 없을 때 스폰)")]
    public GameObject   battle2EnemyPrefab;
    [Tooltip("두 번째 전투 종료 후 재생할 Yarn 노드 이름")]
    public string       yarnNode_postBattle2;

    private static readonly WaitForSeconds _wait02 = new WaitForSeconds(0.2f);
    private static readonly WaitForSeconds _wait03 = new WaitForSeconds(0.3f);
    private static readonly WaitForSeconds _wait05 = new WaitForSeconds(0.5f);

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            BattleEvents.OnBattleEnded -= HandleTurnBasedEnded;
        }
    }

    // ────────────────────────────────────────────────────
    //  외부 진입점 (TutorialEnemyTrigger에서 호출)
    // ────────────────────────────────────────────────────

    /// <summary>
    /// 튜토리얼 전투를 시작합니다.
    /// step 0 = 첫 번째 전투(턴제), step 1 = 두 번째 전투(핵앤슬래시).
    /// </summary>
    public void StartTutorialEncounter(int step, GameObject enemyObject = null)
    {
        if (step == 0)
            StartCoroutine(Battle1Flow());
        else if (step == 1)
            StartCoroutine(Battle2Flow(enemyObject));
        else
            Debug.LogWarning($"[TutorialBattleManager] 알 수 없는 step: {step}");
    }

    // ────────────────────────────────────────────────────
    //  1번 전투 흐름 (턴제)
    // ────────────────────────────────────────────────────

    IEnumerator Battle1Flow()
    {
        // 전투 전 대사 재생 (플레이어 이동 잠금 포함)
        if (!string.IsNullOrEmpty(yarnNode_preBattle1))
            yield return YarnDialogue.PlayAndWait(yarnNode_preBattle1, lockPlayer: true);

        if (EncounterManager.Instance == null)
        {
            Debug.LogError("[TutorialBattleManager] EncounterManager가 씬에 없습니다.");
            yield break;
        }

        // 턴제 강제 시작 (내부에서 PlayerInputLock.Lock, timeScale=0 처리)
        EncounterManager.Instance.ForceStartTurnBased(battle1EnemyPrefab, "tutorial_battle_1");

        // 전투 종료를 이벤트로 감지
        BattleEvents.OnBattleEnded += HandleTurnBasedEnded;
    }

    void HandleTurnBasedEnded()
    {
        BattleEvents.OnBattleEnded -= HandleTurnBasedEnded;
        StartCoroutine(AfterBattle1Flow());
    }

    IEnumerator AfterBattle1Flow()
    {
        // BattleUI가 완전히 파괴될 때까지 대기 (BattleSystem이 내부에서 3초 후 파괴)
        while (BattleSystem.Instance != null)
            yield return null;

        // 짧은 딜레이로 화면 전환 안정화
        yield return _wait03;

        // 전투 후 대사 재생
        if (!string.IsNullOrEmpty(yarnNode_postBattle1))
            yield return YarnDialogue.PlayAndWait(yarnNode_postBattle1, lockPlayer: true);

        GameState.tutorialBattleStep = 1;
    }

    // ────────────────────────────────────────────────────
    //  2번 전투 흐름 (핵앤슬래시)
    // ────────────────────────────────────────────────────

    IEnumerator Battle2Flow(GameObject enemyObject)
    {
        // 전투 전 대사 재생
        if (!string.IsNullOrEmpty(yarnNode_preBattle2))
            yield return YarnDialogue.PlayAndWait(yarnNode_preBattle2, lockPlayer: true);

        if (EncounterManager.Instance == null)
        {
            Debug.LogError("[TutorialBattleManager] EncounterManager가 씬에 없습니다.");
            yield break;
        }

        if (HackSlashCombatManager.Instance == null)
        {
            Debug.LogError("[TutorialBattleManager] HackSlashCombatManager가 씬에 없습니다.");
            yield break;
        }

        // 핵앤슬래시 강제 시작
        EncounterManager.Instance.ForceStartHackSlash(
            enemyObject, battle2EnemyPrefab, "tutorial_battle_2");

        // BeginCombat이 동기 실행되므로 1프레임 대기 후 IsActive 확인
        yield return null;

        // 전투가 끝날 때까지 폴링
        while (HackSlashCombatManager.IsActive)
            yield return _wait02;

        // 결과 텍스트 표시 시간 대기
        yield return _wait05;

        // 전투 후 대사 재생
        if (!string.IsNullOrEmpty(yarnNode_postBattle2))
            yield return YarnDialogue.PlayAndWait(yarnNode_postBattle2, lockPlayer: true);

        GameState.tutorialBattleStep = 2;
    }
}
