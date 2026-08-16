using System.Collections;
using UnityEngine;

/// <summary>
/// 숲 전투(정본 S#17 · S#19) 중에 쿠루의 대사를 조건별로 재생하고,
/// 몰살/불살 보상을 정본 고정값으로 지급한다.
///
/// <para>
/// <see cref="TutorialBattleManager"/> 는 전투 <b>전/후</b> 대사만 담당한다.
/// 이 컴포넌트는 전투 <b>중</b>에만 관여하며 <see cref="BattleEvents"/> 를 구독할 뿐
/// 전투 로직을 직접 호출하지 않는다.
/// </para>
///
/// <para>
/// 정본 근거 — D S#17A~E · S#19A~E / F-2-5 · F-2-6 / C-6-3 · 6-4 · 6-5.
/// <b>UI 안내창을 만들지 않는다.</b> 튜토리얼은 전부 쿠루의 대사로 처리한다(F-2-5 ※).
/// </para>
/// </summary>
public class BattleTutorialDirector : MonoBehaviour
{
    public static BattleTutorialDirector Instance { get; private set; }

    /// <summary>어느 전투의 대사를 쓸지. 1차는 턴제(S#17), 2차는 액션(S#19)이다.</summary>
    public enum Encounter { None, Wolf1TurnBased, Wolf2Action }

    [Header("보상 (F-2-6 — 데모 고정값. 범위값을 굴리지 않는다)")]
    [Tooltip("불살 성립 시 지급. Resources/Items 아래 경로")]
    public string redCrystalPath = "Items/RedCrystal";
    [Tooltip("몰살 시 지급. 원고의 「검은 구체」와 같은 물건이다")]
    public string blackOrbPath   = "Items/BlackOrb";

    [Tooltip("몰살 시 인형화 증가량")]
    public float corruptionOnKill  = +2f;
    [Tooltip("불살 시 인형화 감소량")]
    public float corruptionOnSpare = -2f;
    [Tooltip("불살 시 심리 게이지 증가량 (C-3-3)")]
    public float gaugeOnSpare      = +5f;

    // ── 전투 단위 상태 ───────────────────────────────────────────────
    Encounter _encounter = Encounter.None;
    bool _firstTurnDone;      // 첫 턴 [방어] 칭찬은 한 번뿐이다
    bool _saidAttack, _saidGuard, _saidPet;   // 행동별 반응은 각 1회 (정본 ▶ 행동별 반응)
    bool _saidHurt;           // 피격 대사는 전투당 1회
    bool _rewardGiven;        // 보상 중복 지급 방지

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnEnable()
    {
        BattleEvents.OnPlayerAction   += OnPlayerAction;
        BattleEvents.OnUnitDamaged    += OnUnitDamaged;
        BattleEvents.OnBattleFinished += OnBattleFinished;
    }

    void OnDisable()
    {
        BattleEvents.OnPlayerAction   -= OnPlayerAction;
        BattleEvents.OnUnitDamaged    -= OnUnitDamaged;
        BattleEvents.OnBattleFinished -= OnBattleFinished;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ────────────────────────────────────────────────────
    //  진입 / 종료
    // ────────────────────────────────────────────────────

    /// <summary>전투 진입 직후 호출한다. 전투 단위 상태를 모두 초기화한다.</summary>
    public void BeginEncounter(Encounter encounter)
    {
        _encounter     = encounter;
        _firstTurnDone = false;
        _saidAttack    = _saidGuard = _saidPet = false;
        _saidHurt      = false;
        _rewardGiven   = false;

        // 1차 전투만 진입 대사가 있다. 2차는 진입 전 S#19B 가 이미 끝나 있다.
        if (encounter == Encounter.Wolf1TurnBased)
            Play(YarnNodes.Forest_Wolf_Tutorial);
    }

    // ────────────────────────────────────────────────────
    //  전투 중 대사
    // ────────────────────────────────────────────────────

    void OnPlayerAction(BattleActionKind kind, int sootheCount)
    {
        if (_encounter != Encounter.Wolf1TurnBased) return;

        // 쓰다듬기는 누적 회차가 대사를 정한다. 3회차 대사 직후 정화가 발동한다(F-2-6).
        if (kind == BattleActionKind.Soothe)
        {
            if      (sootheCount >= 3) Play(YarnNodes.Forest_Wolf_Pet3);
            else if (sootheCount == 2) Play(YarnNodes.Forest_Wolf_Pet2);
            else if (!_saidPet)      { _saidPet = true; Play(YarnNodes.Forest_Wolf_React_Pet); }
            _firstTurnDone = true;
            return;
        }

        // 첫 턴에 [방어]를 고르면 칭찬이 먼저 나온다(정본 S#17B).
        // 행동별 반응과 겹치지 않게 이쪽을 우선한다.
        if (!_firstTurnDone && kind == BattleActionKind.Defend)
        {
            _firstTurnDone = true;
            _saidGuard     = true;
            Play(YarnNodes.Forest_Wolf_Guard_Praise);
            return;
        }
        _firstTurnDone = true;

        if (kind == BattleActionKind.Attack && !_saidAttack)
        {
            _saidAttack = true;
            Play(YarnNodes.Forest_Wolf_React_Attack);
        }
        else if (kind == BattleActionKind.Defend && !_saidGuard)
        {
            _saidGuard = true;
            Play(YarnNodes.Forest_Wolf_React_Guard);
        }
    }

    void OnUnitDamaged(Unit unit, DamageResult result)
    {
        if (_encounter != Encounter.Wolf1TurnBased) return;
        if (_saidHurt || unit == null || result.isMiss) return;
        if (unit != BattleSystem.Instance?.PlayerUnit) return;

        // S#17C — 루가 피해를 입은 직후 1회만. 아이템이 턴을 소모한다는 규칙을 여기서 가르친다.
        _saidHurt = true;
        Play(YarnNodes.Forest_Wolf_Hurt);
    }

    /// <summary>액션 전투에서 누적 피해가 임계를 넘었을 때 1회 (S#19C).</summary>
    public void OnWeakpointRevealed()
    {
        if (_encounter != Encounter.Wolf2Action) return;
        Play(YarnNodes.Forest_Wolf2_Weakpoint);
    }

    /// <summary>액션 전투에서 적 HP 가 5% 이하로 떨어져 마무리 구간에 들어갔을 때 1회 (S#19C-2).</summary>
    public void OnFinisherWindowOpened()
    {
        if (_encounter != Encounter.Wolf2Action) return;
        Play(YarnNodes.Forest_Wolf2_Finisher);
    }

    // ────────────────────────────────────────────────────
    //  종료 분기와 보상
    // ────────────────────────────────────────────────────

    void OnBattleFinished(BattleOutcome outcome) => HandleOutcome(outcome);

    /// <summary>
    /// 액션 전투는 <see cref="BattleSystem"/> 을 거치지 않으므로
    /// <see cref="HackSlashCombatManager"/> 가 직접 호출한다.
    /// </summary>
    public void HandleOutcome(BattleOutcome outcome)
    {
        if (_encounter == Encounter.None) return;
        if (_rewardGiven) return;
        if (outcome != BattleOutcome.Killed && outcome != BattleOutcome.Spared) return;

        _rewardGiven = true;
        StartCoroutine(OutcomeRoutine(outcome));
    }

    IEnumerator OutcomeRoutine(BattleOutcome outcome)
    {
        bool spared = outcome == BattleOutcome.Spared;

        // 보상은 턴제·액션 공통이다 (F-2-6). 차이는 성립 난이도뿐이며 보상에 차등을 두지 않는다.
        GiveItem(spared ? redCrystalPath : blackOrbPath);
        CorruptionManager.Instance?.AddCorruption(spared ? corruptionOnSpare : corruptionOnKill);
        if (spared) GaugeManager.Instance?.ChangeGauge(gaugeOnSpare);

        // 전투 UI 가 정리될 때까지 기다렸다가 마무리 대사를 재생한다.
        while (BattleSystem.Instance != null)
            yield return null;
        yield return null;

        string node = _encounter == Encounter.Wolf1TurnBased
            ? (spared ? YarnNodes.Forest_Wolf_PurifyEnd  : YarnNodes.Forest_Wolf_KillEnd)
            : (spared ? YarnNodes.Forest_Wolf2_SpareEnd  : YarnNodes.Forest_Wolf2_KillEnd);

        yield return YarnDialogue.PlayAndWait(node, lockPlayer: true);
        _encounter = Encounter.None;
    }

    void GiveItem(string resourcePath)
    {
        var item = Resources.Load<ItemData>(resourcePath);
        if (item == null)
        {
            Debug.LogError($"[BattleTutorialDirector] 아이템을 찾지 못했습니다: Resources/{resourcePath}");
            return;
        }
        InventoryManager.Instance?.AddItem(item);
    }

    // ────────────────────────────────────────────────────

    /// <summary>노드가 있을 때만 재생한다. 없는 노드는 조용히 넘어가지 않고 로그로 드러낸다.</summary>
    void Play(string node)
    {
        if (!YarnDialogue.NodeExists(node))
        {
            Debug.LogWarning($"[BattleTutorialDirector] Yarn 노드가 없습니다: {node}");
            return;
        }
        YarnDialogue.StartCoroutine(YarnDialogue.PlayAndWait(node, lockPlayer: false));
    }
}
