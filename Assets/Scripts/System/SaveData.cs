using System.Collections.Generic;

[System.Serializable]
public class FlagEntry
{
    public string key;
    public bool   value;
}

[System.Serializable]
public class JournalEntrySave
{
    public string header;
    public string body;
    public bool   isCompleted;
    public float  playTimeAtAcquire;
}

[System.Serializable]
public class SaveData
{
    public const int CurrentVersion = 9;
    public int    saveVersion = CurrentVersion;
    public string sceneName;
    public float  playTime;
    public string saveDate;
    public float  corruption;

    public float playerX;
    public float playerY;
    public float playerZ;

    public List<string> inventoryItemNames = new List<string>();

    // 플레이어 스탯
    public float health;
    public float mental;
    public float puppetization;

    // 스토리 플래그
    public bool isNightSequenceWatched;
    public bool isResolved;
    public bool isBreakfastWatched;
    public bool isZombieDefeated;
    public int  tutorialBattleStep;
    public bool isBreadDoughAcquired;
    public bool hasMerchantMetAtSquare;
    public bool isKuruJoined;
    public bool isForestSolMet;

    // v4 추가 필드
    public bool isAtticKeyFound;
    public bool isDaggerAcquired;
    public bool isAtticRadioPlayed;
    public bool isWorkbenchLocked;

    // v5 추가 필드
    public bool isAtticBoxOpened;

    // v6 추가 필드 — 성장/저널/체크포인트
    public int  playerLevel = 1;
    public int  playerExp   = 0;
    public List<JournalEntrySave> journalEntries = new List<JournalEntrySave>();
    /// <summary>저장 시각(DateTime.Ticks). 게임오버 복구 시 PreBattle/Checkpoint 중 최신 판단용.</summary>
    public long saveTicks;

    // v7 추가 필드 — 플레이어가 정한 주인공 이름
    public string playerName = "루";

    // v8 추가 필드 — S#04F~S#13 (2026-08-07 D 정본)
    public bool isYardSugarSeen;
    public bool isSeraOut;
    public bool isDoorknobRefused;
    public bool isFrontDoorKeyFound;
    public bool isDaggerToggleUnlocked;

    /// <summary>
    /// S#13 현관문 통과 여부 (C-14-2-2 · F-6 「타이머·조임 정지 — 현관문 통과」).
    ///
    /// 마당 정문 앞에 세이브 포인트가 있으므로(C-13-2) 이 값을 저장하지 않으면 그 파일을 불러올 때
    /// 마당에서 90초가 다시 시작돼 깰 수 없는 파일이 된다(C-13-2 문단 965).
    ///
    /// ⚠ saveVersion 을 올리지 않았다. 이 값이 없는 예전 세이브는 JsonUtility 가 false 로 두는데,
    ///   그것이 곧 「아직 통과하지 않았다」로 예전과 같은 동작이라 마이그레이션이 필요 없다.
    /// </summary>
    public bool isFrontDoorPassed;

    /// <summary>
    /// 환상/현실 심리 게이지(v9 · F-9-3 「위치 · 인형화 · 아이템 · 심리 게이지」). 단검을 쥐고 있던 일시 값은 넣지 않는다.
    /// v8 이하는 이 필드가 없어 <see cref="GaugeManager.DEFAULT_GAUGE"/> 로 둔다(SaveManager 마이그레이션).
    /// </summary>
    public float fantasyRealityGauge = GaugeManager.DEFAULT_GAUGE;

    /// <summary>
    /// 이 진행의 「마지막 토끼 슬롯」(v9 · F-9-3). 토끼로 저장했거나 불러온 슬롯 번호, 없으면 -1.
    /// 중단 저장에도 같이 들어간다 — 중단 저장은 불러오면 지워지므로 진행 상태로 들고 다닌다(F-9-3 ※ · E-33-6).
    /// </summary>
    public int lastRabbitSlot = -1;

    // 처치된 적 ID (HashSet은 JSON 직렬화 불가 → List 사용)
    public List<string> defeatedEnemyIDs   = new List<string>();

    // 한 번만 선택 가능한 대화 선택지 키
    public List<string> chosenDialogueKeys = new List<string>();

    // 동적 플래그 (GameStateManager.flags 직렬화)
    public List<FlagEntry> dynamicFlags = new List<FlagEntry>();
}