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
        public float hunger;
        public float puppetization;

        public static PlayerState Default => new PlayerState
        {
            health        = -1f,
            mental        = -1f,
            hunger        = -1f,
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
        public float  savedGaugeValue;
        public string returnSceneName;

        public static BattleReturnState Default => new BattleReturnState
        {
            isComingFromBattle = false,
            cooldown           = 0f,
            savedGaugeValue    = 0f,
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
    //  위치 / 멘탈 붕괴 / 기타
    // ──────────────────────────────────────────
    public static Vector3 lastPosition      = Vector3.zero;
    public static bool    hasPositionSaved  = false;
    public static float   mentalBreakdownTimer = 0f;
    public static bool    isZombieDefeated  = false;

    // ──────────────────────────────────────────
    //  처치된 적 ID
    // ──────────────────────────────────────────
    public static List<string> defeatedEnemyIDs = new List<string>();

    public static void RegisterDefeatedEnemy(string id)
    {
        if (!string.IsNullOrEmpty(id) && !defeatedEnemyIDs.Contains(id))
            defeatedEnemyIDs.Add(id);
    }

    // ──────────────────────────────────────────
    //  인벤토리
    // ──────────────────────────────────────────
    public static List<ItemData> inventoryItems = null;

    // ──────────────────────────────────────────
    //  하위 호환 프로퍼티 (기존 코드 참조 유지)
    // ──────────────────────────────────────────
    public static bool   isComingFromBattle
    {
        get => battleReturn.isComingFromBattle;
        set => battleReturn.isComingFromBattle = value;
    }
    public static float  savedGaugeValue
    {
        get => battleReturn.savedGaugeValue;
        set => battleReturn.savedGaugeValue = value;
    }
    public static string returnSceneName
    {
        get => battleReturn.returnSceneName;
        set => battleReturn.returnSceneName = value;
    }
    public static float battleReturnCooldown
    {
        get => battleReturn.cooldown;
        set => battleReturn.cooldown = value;
    }

    public static float playerHealth
    {
        get => player.health;        set => player.health = value;
    }
    public static float playerMental
    {
        get => player.mental;        set => player.mental = value;
    }
    public static float playerHunger
    {
        get => player.hunger;        set => player.hunger = value;
    }
    public static float playerPuppetization
    {
        get => player.puppetization; set => player.puppetization = value;
    }

    // SceneNames 위임
    public static string GetFantasyScene(string scene) => SceneNames.GetFantasyScene(scene);
    public static bool   IsRealityScene(string scene)  => SceneNames.IsRealityScene(scene);
}