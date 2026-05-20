using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 씬 간 유지해야 하는 전역 상태를 모아두는 정적 클래스.
/// </summary>
public static class GameState
{
    // ──────────────────────────────────────────
    //  플레이어 스탯
    // ──────────────────────────────────────────
    public struct PlayerState
    {
        public float health;
        public float mental;
        public float puppetization;

        public static PlayerState Default => new PlayerState
        {
            health        = -1f,
            mental        = -1f,
            puppetization = -1f,
        };

        public bool IsInitialized => health >= 0f;
    }

    public static PlayerState player = PlayerState.Default;

    // ──────────────────────────────────────────
    //  전투 복귀 상태
    // ──────────────────────────────────────────
    public struct BattleReturnState
    {
        public bool   isComingFromBattle;
        public float  cooldown;
        public string returnSceneName;

        public static BattleReturnState Default => new BattleReturnState
        {
            isComingFromBattle = false,
            cooldown           = 0f,
            returnSceneName    = SceneNames.Map,
        };

        /// <summary>전투 종료 시 호출.</summary>
        public void SetReturning(string sceneName, float cooldownSeconds = 2.5f)
        {
            isComingFromBattle = true;
            cooldown           = cooldownSeconds;
            returnSceneName    = sceneName;
        }

        /// <summary>매 프레임 호출. 쿨타임 감소 및 플래그 해제.</summary>
        public void Tick(float deltaTime)
        {
            if (cooldown <= 0f) return;
            cooldown -= deltaTime;
            if (cooldown <= 0f)
            {
                cooldown           = 0f;
                isComingFromBattle = false;
            }
        }

        /// <summary>재조우 차단 여부.</summary>
        public bool IsBlocked => isComingFromBattle || cooldown > 0f;
    }

    public static BattleReturnState battleReturn = BattleReturnState.Default;

    // ──────────────────────────────────────────
    //  스토리 진행 플래그
    // ──────────────────────────────────────────
    /// <summary>튜토리얼 전투 진행 단계. 0=미시작, 1=첫번째(턴제) 완료, 2=두번째(핵앤슬래시) 완료.</summary>
    public static int tutorialBattleStep = 0;

    /// <summary>S#2~S#4 야간 시퀀스(튜토리얼) 시청 여부. 한 번만 발동.</summary>
    public static bool isNightSequenceWatched = false;
    /// <summary>S#8 아버지 메시지 이후 루의 결심 여부. true 이면 현관문이 열린다.</summary>
    public static bool isResolved = false;
    /// <summary>S#5 아침 식사 컷씬 시청 여부. 한 번만 발동.</summary>
    public static bool isBreakfastWatched = false;

    // ──────────────────────────────────────────
    //  위치 / 멘탈 붕괴 / 기타
    // ──────────────────────────────────────────
    public static Vector3 lastPosition      = Vector3.zero;
    public static bool    hasPositionSaved  = false;
    public static float   mentalBreakdownTimer = 0f;
    public static bool    isZombieDefeated  = false;

    // ──────────────────────────────────────────
    //  처치된 적 ID
    // ──────────────────────────────────────────
    public static HashSet<string> defeatedEnemyIDs    = new HashSet<string>();
    public static HashSet<string> chosenDialogueKeys  = new HashSet<string>();

    public static void RegisterDefeatedEnemy(string id)
    {
        if (!string.IsNullOrEmpty(id))
            defeatedEnemyIDs.Add(id);
    }

    // ──────────────────────────────────────────
    //  인벤토리
    // ──────────────────────────────────────────
    public static List<ItemData> inventoryItems = null;

    // SceneNames 위임
    public static string GetFantasyScene(string scene) => SceneNames.GetFantasyScene(scene);
    public static bool   IsRealityScene(string scene)  => SceneNames.IsRealityScene(scene);

    // ──────────────────────────────────────────
    //  플레이 시작 시 정적 변수 초기화
    //  (Unity 에디터에서 정적 변수는 플레이 세션 사이에 유지되므로 명시적으로 리셋)
    // ──────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ResetOnPlay()
    {
        player                   = PlayerState.Default;
        mentalBreakdownTimer     = 0f;
        battleReturn             = BattleReturnState.Default;
        lastPosition             = Vector3.zero;
        hasPositionSaved         = false;
        pendingSwitchToHackSlash = false;
        pendingEnemyPrefab       = null;
        pendingModeSelection     = false;
        tutorialBattleStep       = 0;
        isNightSequenceWatched  = false;
        isResolved               = false;
        isBreakfastWatched      = false;
        isZombieDefeated         = false;
        defeatedEnemyIDs         = new HashSet<string>();
        chosenDialogueKeys       = new HashSet<string>();
    }

    // ──────────────────────────────────────────
    //  전투 모드 강제 전환
    // ──────────────────────────────────────────
    /// <summary>씬 복귀 후 핵앤슬래시를 자동 시작해야 하는지 여부.</summary>
    public static bool pendingSwitchToHackSlash = false;
    /// <summary>모드 전환 시 넘겨줄 적 프리팹 (에셋 참조이므로 씬 로드에서 유지됨).</summary>
    public static GameObject pendingEnemyPrefab = null;

    /// <summary>PendingMode 선택 UI 대기 중. BattleSystem이 전투를 즉시 시작하지 않도록 막음.</summary>
    public static bool pendingModeSelection = false;
}