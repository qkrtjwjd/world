using UnityEngine;

/// <summary>
/// 환상(턴제) ↔ 현실(액션) 전투 모드 전환을 중재하는 싱글톤.
///
/// 기존에는 <see cref="BattleSystem"/>의 정적 필드 (<c>showEnemyAppearPanel</c>, <c>readyToStart</c>)를
/// <see cref="HackSlashCombatManager"/>·<c>EncounterManager</c>가 직접 조작하여 결합도가 높았습니다.
/// 이 컨트롤러를 통해 의도된 진입 시점만 노출하고, race condition을 가드합니다.
///
/// BattleSystem의 정적 필드는 backward-compat용으로 유지되며,
/// 이 컨트롤러가 같은 필드를 동시에 갱신합니다.
/// </summary>
public class BattleModeController : MonoBehaviour
{
    public static BattleModeController Instance { get; private set; }

    /// <summary>전환 진행 중 여부. true 일 때 중복 요청 차단.</summary>
    public bool IsTransitioning { get; private set; } = false;

    /// <summary>이번 전투 세션에서 이미 모드 전환이 발생했으면 true. 추가 전환 차단에 사용.</summary>
    public bool HasSwitchedMode { get; private set; } = false;

    /// <summary>다음 턴제 전투 시 적 등장 패널을 표시할지 여부.</summary>
    public bool ShowEnemyAppearPanelOnNext { get; private set; } = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 컨트롤러 자동 생성 (씬에 미리 배치할 필요 없음).
    /// </summary>
    public static BattleModeController GetOrCreate()
    {
        if (Instance == null)
        {
            var go = new GameObject("BattleModeController [Auto]");
            Instance = go.AddComponent<BattleModeController>();
        }
        return Instance;
    }

    /// <summary>
    /// 턴제 전투 시작 요청. 기존의
    /// <c>BattleSystem.showEnemyAppearPanel = X; BattleSystem.readyToStart = true;</c> 패턴을 대체합니다.
    /// </summary>
    /// <param name="showAppearPanel">적 등장 연출 패널을 표시할지.</param>
    /// <returns>요청이 수락됐으면 true. 이미 전환 중이면 false.</returns>
    public bool RequestTurnBasedStart(bool showAppearPanel)
    {
        if (IsTransitioning)
        {
            Debug.LogWarning("[BattleModeController] 전환 중인 요청을 무시합니다.");
            return false;
        }

        IsTransitioning            = true;
        ShowEnemyAppearPanelOnNext = showAppearPanel;

        // Backward-compat: 정적 필드도 갱신
        BattleSystem.showEnemyAppearPanel = showAppearPanel;
        BattleSystem.readyToStart         = true;

        return true;
    }

    /// <summary>BattleSystem.SetupBattle 진입 시 호출하여 전환 플래그를 해제합니다.</summary>
    public void NotifyTurnBasedStarted()
    {
        IsTransitioning = false;
        BattleSystem.readyToStart = false;
    }

    /// <summary>전환을 취소/리셋. 외부에서 명시적으로 호출.</summary>
    public void ResetTransition()
    {
        IsTransitioning            = false;
        ShowEnemyAppearPanelOnNext = false;
        BattleSystem.showEnemyAppearPanel = false;
        BattleSystem.readyToStart         = false;
    }

    /// <summary>모드 전환이 일어났음을 기록합니다. 이후 추가 전환 요청이 차단됩니다.</summary>
    public void SetSwitched()
    {
        HasSwitchedMode = true;
        Dbg.Log("[BattleModeController] 모드 전환 완료 — 이번 전투에서 추가 전환이 잠깁니다.");
    }

    /// <summary>새 전투 세션 시작 시 호출. 전환 잠금 및 모든 상태를 초기화합니다.</summary>
    public void ResetBattleSession()
    {
        HasSwitchedMode = false;
        ResetTransition();
        Dbg.Log("[BattleModeController] 전투 세션 초기화 — 모드 전환 잠금 해제.");
    }
}
