using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance;

    public float currentPlayTime = 0f;

    // 로딩 중 임시 보관
    private bool     _isLoading   = false;
    private SaveData _pendingData = null;

    // ─────────────────────────────────────────────
    //  초기화
    // ─────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        if (Time.timeScale > 0)
            currentPlayTime += Time.unscaledDeltaTime;
    }

    // ─────────────────────────────────────────────
    //  저장
    // ─────────────────────────────────────────────
    const string PreBattleKey = "PreBattleSave";

    SaveData BuildSaveData()
    {
        SaveData data = new SaveData
        {
            sceneName  = SceneManager.GetActiveScene().name,
            playTime   = currentPlayTime,
            saveDate   = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            corruption = CorruptionManager.Instance != null
                         ? CorruptionManager.Instance.currentCorruption : 0f,
        };

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            data.playerX = player.transform.position.x;
            data.playerY = player.transform.position.y;
            data.playerZ = player.transform.position.z;
        }

        if (GameState.inventoryItems != null)
            foreach (var item in GameState.inventoryItems)
                if (item != null) data.inventoryItemNames.Add(item.name);

        if (GameState.player.IsInitialized)
        {
            data.health        = GameState.player.health;
            data.mental        = GameState.player.mental;
            data.puppetization = GameState.player.puppetization;
        }
        else
        {
            data.health        = 100f;
            data.mental        = 100f;
            data.puppetization = 0f;
        }

        data.isNightSequenceWatched = GameState.isNightSequenceWatched;
        data.isResolved              = GameState.isResolved;
        data.isBreakfastWatched     = GameState.isBreakfastWatched;
        data.isZombieDefeated        = GameState.isZombieDefeated;

        foreach (string id in GameState.defeatedEnemyIDs)
            data.defeatedEnemyIDs.Add(id);
        foreach (string key in GameState.chosenDialogueKeys)
            data.chosenDialogueKeys.Add(key);

        return data;
    }

    public void SaveGame(int slot)
    {
        SaveData data = BuildSaveData();
        PlayerPrefs.SetString(SlotKey(slot), JsonUtility.ToJson(data));
        PlayerPrefs.Save();
        Dbg.Log($"[SaveManager] 슬롯 {slot} 저장 완료");
    }

    /// <summary>전투 직전 상태를 별도 키에 저장합니다. 사망 시 이 지점으로 복귀합니다.</summary>
    public void SavePreBattle()
    {
        SaveData data = BuildSaveData();
        PlayerPrefs.SetString(PreBattleKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
        Dbg.Log("[SaveManager] 전투 전 상태 저장 완료");
    }

    /// <summary>전투 직전 저장 데이터를 불러옵니다. 데이터가 없으면 경고 후 무시합니다.</summary>
    public void LoadPreBattle()
    {
        if (!PlayerPrefs.HasKey(PreBattleKey))
        {
            Debug.LogWarning("[SaveManager] 전투 전 저장 데이터가 없습니다.");
            return;
        }
        SaveData data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(PreBattleKey));
        _pendingData = data;
        _isLoading   = true;
        if (TransitionManager.Instance != null)
            TransitionManager.Instance.DoSceneTransition(data.sceneName);
        else
            SceneManager.LoadScene(data.sceneName);
    }

    // ─────────────────────────────────────────────
    //  데이터 조회 (UI 표시용)
    // ─────────────────────────────────────────────
    public SaveData LoadSaveData(int slot)
    {
        string key = SlotKey(slot);
        if (!PlayerPrefs.HasKey(key)) return null;
        return JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(key));
    }

    // ─────────────────────────────────────────────
    //  불러오기
    // ─────────────────────────────────────────────
    public void LoadGame(int slot)
    {
        SaveData data = LoadSaveData(slot);
        if (data == null) { Debug.LogWarning($"[SaveManager] 슬롯 {slot} 데이터 없음"); return; }

        _pendingData = data;
        _isLoading   = true;
        if (TransitionManager.Instance != null)
            TransitionManager.Instance.DoSceneTransition(data.sceneName);
        else
            SceneManager.LoadScene(data.sceneName);
    }

    // ─────────────────────────────────────────────
    //  씬 로딩 완료 콜백
    // ─────────────────────────────────────────────
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!_isLoading || _pendingData == null) return;
        StartCoroutine(RestoreRoutine(_pendingData));
    }

    IEnumerator RestoreRoutine(SaveData data)
    {
        // 다른 오브젝트들이 Start()를 끝낼 때까지 최대 10프레임 대기
        GameObject player = null;
        for (int attempt = 0; attempt < 10 && player == null; attempt++)
        {
            player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) yield return null;
        }

        // ── 위치 ──
        if (player != null)
            player.transform.position = new Vector3(data.playerX, data.playerY, data.playerZ);
        else
            Debug.LogWarning("[SaveManager] 플레이어를 찾지 못했습니다.");

        // ── 타락 수치 ──
        if (CorruptionManager.Instance != null)
            CorruptionManager.Instance.LoadCorruption(data.corruption);

        // ── 스탯 ──
        GameState.player = new GameState.PlayerState
        {
            health        = data.health,
            mental        = data.mental,
            puppetization = data.puppetization,
        };

        // ── 스토리 플래그 ──
        GameState.isNightSequenceWatched = data.isNightSequenceWatched;
        GameState.isResolved              = data.isResolved;
        GameState.isBreakfastWatched     = data.isBreakfastWatched;
        GameState.isZombieDefeated        = data.isZombieDefeated;

        // ── 처치된 적 ID ──
        GameState.defeatedEnemyIDs   = new System.Collections.Generic.HashSet<string>(data.defeatedEnemyIDs);
        GameState.chosenDialogueKeys = new System.Collections.Generic.HashSet<string>(data.chosenDialogueKeys);
        // ── PlayerStats 즉시 반영 ──
        if (player != null)
        {
            PlayerStats stats = player.GetComponent<PlayerStats>();
            if (stats != null)
            {
                stats.currentHealth      = data.health;
                stats.currentMental      = data.mental;
                stats.currentPuppetization = data.puppetization;
                stats.UpdateUI(true);
            }
        }

        // ── 인벤토리 복구 ──
        GameState.inventoryItems = new List<ItemData>();
        foreach (string itemName in data.inventoryItemNames)
        {
            ItemData item = ItemDatabase.Find(itemName);
            if (item != null) GameState.inventoryItems.Add(item);
            else Debug.LogWarning($"[SaveManager] 아이템 없음: {itemName}");
        }

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.inventoryItems = GameState.inventoryItems;
            InventoryManager.Instance.UpdateSlotUI();
        }

        // ── 완료 ──
        Time.timeScale = 1f;
        _isLoading     = false;
        _pendingData   = null;
        Dbg.Log("[SaveManager] 불러오기 완료");
    }

    // ─────────────────────────────────────────────
    //  유틸
    // ─────────────────────────────────────────────
    static string SlotKey(int slot) => $"SaveFile{slot}";
}