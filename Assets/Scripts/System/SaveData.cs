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
    public const int CurrentVersion = 8;
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

    // 처치된 적 ID (HashSet은 JSON 직렬화 불가 → List 사용)
    public List<string> defeatedEnemyIDs   = new List<string>();

    // 한 번만 선택 가능한 대화 선택지 키
    public List<string> chosenDialogueKeys = new List<string>();

    // 동적 플래그 (GameStateManager.flags 직렬화)
    public List<FlagEntry> dynamicFlags = new List<FlagEntry>();
}