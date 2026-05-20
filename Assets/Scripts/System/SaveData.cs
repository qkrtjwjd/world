using System.Collections.Generic;

[System.Serializable]
public class SaveData
{
    public int    saveVersion = 2;
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

    // 처치된 적 ID (HashSet은 JSON 직렬화 불가 → List 사용)
    public List<string> defeatedEnemyIDs   = new List<string>();

    // 한 번만 선택 가능한 대화 선택지 키
    public List<string> chosenDialogueKeys = new List<string>();
}