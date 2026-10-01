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
    [Tooltip("위 노드에 이어서 재생할 노드. 정본 S#19 는 조우(S#19A) → 필터 전환 → 실체 확인(S#19B) 순서다. " +
             "비워두면 건너뛴다")]
    public string       yarnNode_preBattle2b;
    [Tooltip("두 번째 전투에 등장할 적 프리팹 (씬에 이미 있는 적이 없을 때 스폰)")]
    public GameObject   battle2EnemyPrefab;
    [Tooltip("두 번째 전투 종료 후 재생할 Yarn 노드 이름")]
    public string       yarnNode_postBattle2;

    private static readonly WaitForSeconds _wait02 = new WaitForSeconds(0.2f);
    private static readonly WaitForSeconds _wait03 = new WaitForSeconds(0.3f);
    private static readonly WaitForSeconds _wait05 = new WaitForSeconds(0.5f);

    void Awake()
    {
        // gameObject 가 아니라 이 컴포넌트만 지운다.
        // BattleManager 오브젝트에는 EncounterManager · HackSlashCombatManager ·
        // BattleTutorialDirector 가 함께 붙어 있어서, GameObject 를 지우면 전투 시스템이 통째로 날아간다.
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            BattleEvents.OnBattleEnded -= HandleTurnBasedEnded;
            BattleEvents.OnBattleFinished -= RecordBattle1Outcome;
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
            StartCoroutine(Battle1Flow(enemyObject));
        else if (step == 1)
            StartCoroutine(Battle2Flow(enemyObject));
        else
            Debug.LogWarning($"[TutorialBattleManager] 알 수 없는 step: {step}");
    }

    // ────────────────────────────────────────────────────
    //  1번 전투 흐름 (턴제)
    // ────────────────────────────────────────────────────

    GameObject     _battle1FieldEnemy;
    BattleOutcome? _battle1Outcome;

    IEnumerator Battle1Flow(GameObject enemyObject)
    {
        _battle1FieldEnemy = enemyObject;
        _battle1Outcome    = null;

        // S#17A [CAM] 「개가 화면에 들어오는 순간 고정. 컷을 잇지 않는다. 개에게 카메라를 다가가지 않는다」(D 807).
        CameraDirector.Instance?.Hold();

        // 전투 전 대사 재생 (플레이어 이동 잠금 포함)
        if (!string.IsNullOrEmpty(yarnNode_preBattle1))
            yield return YarnDialogue.PlayAndWait(yarnNode_preBattle1, lockPlayer: true);

        if (EncounterManager.Instance == null)
        {
            Debug.LogError("[TutorialBattleManager] EncounterManager가 씬에 없습니다.");
            yield break;
        }

        // 턴제 강제 시작 (내부에서 PlayerInputLock.Lock, timeScale=0 처리)
        // 안내 힌트를 띄우지 않는다 — 튜토리얼은 쿠루의 대사로만 처리한다(F-2-5 ※).
        EncounterManager.Instance.ForceStartTurnBased(
            battle1EnemyPrefab, "tutorial_battle_1", forestRules: true);

        // 전투 중 대사·보상은 Director 가 맡는다. 전투 UI 가 뜬 뒤에 시작해야 한다.
        StartCoroutine(BeginDirectorNextFrame(BattleTutorialDirector.Encounter.Wolf1TurnBased));

        // 전투 종료를 이벤트로 감지
        BattleEvents.OnBattleFinished += RecordBattle1Outcome;
        BattleEvents.OnBattleEnded += HandleTurnBasedEnded;
    }

    void RecordBattle1Outcome(BattleOutcome outcome) => _battle1Outcome = outcome;

    void HandleTurnBasedEnded()
    {
        BattleEvents.OnBattleEnded -= HandleTurnBasedEnded;
        BattleEvents.OnBattleFinished -= RecordBattle1Outcome;
        StartCoroutine(AfterBattle1Flow());
    }

    IEnumerator AfterBattle1Flow()
    {
        // BattleUI가 완전히 파괴될 때까지 대기 (BattleSystem이 내부에서 3초 후 파괴)
        while (BattleSystem.Instance != null)
            yield return null;

        // 필드의 개를 치운다 — 몰살은 흩어져 사라지고(D 882), 정화는 「늑대는 이미 도망가고 없다」(D 906).
        //   2026-09-28: 전에는 전투용 적을 따로 스폰해 싸우고, 필드의 개는 이긴 뒤에도 그 자리에 남아 있었다.
        //   패배는 게임 오버 → 전투 직전 불러오기로 되돌아가므로 건드리지 않는다.
        bool won = _battle1Outcome == BattleOutcome.Killed || _battle1Outcome == BattleOutcome.Spared;
        if (won && _battle1FieldEnemy != null)
            _battle1FieldEnemy.SetActive(false);

        // 짧은 딜레이로 화면 전환 안정화
        yield return _wait03;

        // 마무리 대사(KillEnd / PurifyEnd)는 Director 가 분기해서 재생한다.
        // 여기서 또 재생하면 두 번 나오므로 끝날 때까지 기다리기만 한다.
        while (YarnDialogue.IsRunning)
            yield return null;

        // 전투 후 대사 재생 — 정본 분기 대사를 쓰지 않는 일반 전투용 폴백이다
        if (!string.IsNullOrEmpty(yarnNode_postBattle1))
            yield return YarnDialogue.PlayAndWait(yarnNode_postBattle1, lockPlayer: true);

        // 고정을 풀고 숲길 추적으로 돌아간다(S#16A 의 진행 방향 치우침 그대로).
        var entrance = FindAnyObjectByType<ForestEntranceDirector>();
        CameraDirector.Instance?.Track(entrance != null ? entrance.forwardOffset : Vector2.zero);

        // 지면 튜토리얼을 끝낸 것으로 치지 않는다 — 쓰러지면 사망 화면을 거쳐 마지막 토끼(없으면 S#01)로 돌아간다(F-9-3).
        if (_battle1Outcome == BattleOutcome.Lost) yield break;
        GameState.tutorialBattleStep = 1;
    }

    /// <summary>
    /// 전투 UI 가 생성된 다음 프레임에 Director 를 켠다.
    /// 같은 프레임에 켜면 진입 대사가 BattleUI 보다 먼저 떠서 화면에 겹친다.
    /// </summary>
    IEnumerator BeginDirectorNextFrame(BattleTutorialDirector.Encounter encounter)
    {
        yield return null;
        var director = BattleTutorialDirector.Instance;
        if (director == null)
        {
            Debug.LogWarning("[TutorialBattleManager] BattleTutorialDirector 가 씬에 없습니다. " +
                             "전투 중 쿠루 대사와 정본 보상이 나오지 않습니다.");
            yield break;
        }
        director.BeginEncounter(encounter);
    }

    // ────────────────────────────────────────────────────
    //  2번 전투 흐름 (핵앤슬래시)
    // ────────────────────────────────────────────────────

    [Header("2번 전투 — S#19A 등장")]
    [Tooltip("두 번째 늑대가 나무 사이에서 걸어나오는 속도(D 942 「천천히」).")]
    public float wolf2ApproachSpeed = 1f;
    [Tooltip("늑대가 루에게서 이 거리까지 걸어나와 멈춘다.")]
    public float wolf2StopDistance = 2.5f;

    IEnumerator Battle2Flow(GameObject enemyObject)
    {
        // S#19A [CAM] 「고정. 늑대와 루 · 쿠루가 한 화면에 들어온 구도를 유지하고 컷을 잇지 않는다」(D 941).
        //   늑대는 나무 사이에서 천천히 걸어나온다(D 942). 그동안 루는 멈춰 선다.
        var ctrl = YarnDialogue.LockPlayer();
        var cd   = CameraDirector.Instance;
        var lu   = PlayerStats.Instance != null ? PlayerStats.Instance.transform : null;
        if (enemyObject != null && lu != null)
        {
            if (cd != null)
            {
                cd.Hold();
                yield return cd.ScrollTo(((Vector2)lu.position + (Vector2)enemyObject.transform.position) * 0.5f);
            }
            var rb = enemyObject.GetComponent<Rigidbody2D>();
            float t = 0f;
            while (t < 3f && Vector2.Distance(enemyObject.transform.position, lu.position) > wolf2StopDistance)
            {
                Vector2 step = ((Vector2)lu.position - (Vector2)enemyObject.transform.position).normalized
                               * wolf2ApproachSpeed * Time.fixedDeltaTime;
                if (rb != null) rb.MovePosition(rb.position + step);
                else enemyObject.transform.position += (Vector3)step;
                t += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }
        YarnDialogue.UnlockPlayer(ctrl);

        // 전투 전 대사 재생 — S#19A(조우) → S#19B(실체 확인)
        if (!string.IsNullOrEmpty(yarnNode_preBattle2))
            yield return YarnDialogue.PlayAndWait(yarnNode_preBattle2, lockPlayer: true);

        // 두 노드 사이에서 필터가 현실로 넘어간다 — 단검 파지 → 필터 현실(R) 전환(D 964).
        // 「한 꺼풀 벗겨지듯」이 지문이므로 페이드를 끼우지 않고 한 프레임에 끊는다 (D 965).
        //   2026-09-28: 전에는 S#19B 첫 줄의 set_filter 만 있었는데, 그건 FilterManager 쪽이라 화면(게이지)이 바뀌지 않았다.
        //   S#19B 대사 내내 환상으로 보이다가 전투가 시작돼서야 현실이 됐다.
        //   여기서 게이지를 올려 두면 액션 전투가 끝날 때 이 값(현실)으로 되돌아간다 — S#20 은 직전 전투의 필터를 유지한다(D 1052).
        GaugeBoundaryMonitor.Instance?.SilentSetZone(100f);
        GaugeManager.Instance?.ForceRealityMax();

        if (!string.IsNullOrEmpty(yarnNode_preBattle2b))
            yield return YarnDialogue.PlayAndWait(yarnNode_preBattle2b, lockPlayer: true);

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

        // 액션 전투는 루를 따라간다 — 고정을 풀고 숲길 추적으로 돌아간다.
        var entrance2 = FindAnyObjectByType<ForestEntranceDirector>();
        cd?.Track(entrance2 != null ? entrance2.forwardOffset : Vector2.zero);

        // 마무리 구간(5% · E키 · 2초 이탈)은 숲 전투에서만 연다 (F-2-6).
        HackSlashCombatManager.Instance.useFinisherWindow = true;

        // 핵앤슬래시 강제 시작
        // 안내 힌트를 띄우지 않는다 — 튜토리얼은 쿠루의 대사로만 처리한다(F-2-5 ※).
        EncounterManager.Instance.ForceStartHackSlash(
            enemyObject, battle2EnemyPrefab, "tutorial_battle_2");

        // BeginCombat이 동기 실행되므로 1프레임 대기 후 IsActive 확인
        yield return null;

        BattleTutorialDirector.Instance?.BeginEncounter(
            BattleTutorialDirector.Encounter.Wolf2Action);

        // 전투가 끝날 때까지 폴링
        while (HackSlashCombatManager.IsActive)
            yield return _wait02;

        // 마무리 구간은 숲 전투 전용이다. 켜둔 채로 두면 이후 일반 액션 전투에도 남는다.
        if (HackSlashCombatManager.Instance != null)
            HackSlashCombatManager.Instance.useFinisherWindow = false;

        // 결과 텍스트 표시 시간 대기
        yield return _wait05;

        // 마무리 대사(S#19D / S#19E)는 Director 가 분기해서 재생한다.
        while (YarnDialogue.IsRunning)
            yield return null;

        // 전투 후 대사 재생 — 정본 분기 대사를 쓰지 않는 일반 전투용 폴백이다
        if (!string.IsNullOrEmpty(yarnNode_postBattle2))
            yield return YarnDialogue.PlayAndWait(yarnNode_postBattle2, lockPlayer: true);

        GameState.tutorialBattleStep = 2;
    }
}
