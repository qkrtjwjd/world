using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveManager : MonoBehaviour
{
    public static SaveManager instance;

    public float currentPlayTime = 0f;

    // 로딩 중 임시 보관
    private bool     _isLoading   = false;
    private SaveData _pendingData = null;

    // ─────────────────────────────────────────────
    //  초기화
    // ─────────────────────────────────────────────
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else Destroy(gameObject);
    }

    private void OnDestroy()
    {
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
    public void SaveGame(int slot)
    {
        SaveData data = new SaveData
        {
            sceneName = SceneManager.GetActiveScene().name,
            playTime  = currentPlayTime,
            saveDate  = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            corruption = CorruptionManager.instance != null
                         ? CorruptionManager.instance.currentCorruption : 0f,
        };

        // 플레이어 위치
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            data.playerX = player.transform.position.x;
            data.playerY = player.transform.position.y;
            data.playerZ = player.transform.position.z;
        }

        // 인벤토리 (에셋 이름 목록으로 직렬화)
        if (GameState.inventoryItems != null)
        {
            foreach (var item in GameState.inventoryItems)
                if (item != null) data.inventoryItemNames.Add(item.name);
        }

        // 스탯 (GameState.player 구조체에서 한번에 읽기)
        if (GameState.player.IsInitialized)
        {
            data.health  = GameState.player.health;
            data.mental  = GameState.player.mental;
            data.hunger  = GameState.player.hunger;
        }
        else
        {
            data.health = 100f;
            data.mental = 100f;
            data.hunger = 100f;
        }

        data.realityGauge = GameState.battleReturn.savedGaugeValue;

        PlayerPrefs.SetString(SlotKey(slot), JsonUtility.ToJson(data));
        PlayerPrefs.Save();
        Debug.Log($"[SaveManager] 슬롯 {slot} 저장 완료");
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
        if (CorruptionManager.instance != null)
            CorruptionManager.instance.currentCorruption = data.corruption;

        // ── 스탯 (GameState 구조체에 한번에 쓰기) ──
        GameState.player = new GameState.PlayerState
        {
            health        = data.health,
            mental        = data.mental,
            hunger        = data.hunger,
            puppetization = GameState.player.puppetization, // 저장 안 한 항목은 유지
        };
        GameState.battleReturn.savedGaugeValue = data.realityGauge;

        // ── PlayerStats 즉시 반영 ──
        if (player != null)
        {
            PlayerStats stats = player.GetComponent<PlayerStats>();
            if (stats != null)
            {
                stats.currentHealth = data.health;
                stats.currentMental = data.mental;
                stats.currentHunger = data.hunger;
                stats.UpdateUI(true);
            }
        }

        // ── 인벤토리 복구 ──
        GameState.inventoryItems = new List<ItemData>();
        foreach (string itemName in data.inventoryItemNames)
        {
            ItemData item = Resources.Load<ItemData>($"Items/{itemName}");
            if (item != null) GameState.inventoryItems.Add(item);
            else Debug.LogWarning($"[SaveManager] 아이템 없음: Items/{itemName}");
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
        Debug.Log("[SaveManager] 불러오기 완료");
    }

    // ─────────────────────────────────────────────
    //  유틸
    // ─────────────────────────────────────────────
    static string SlotKey(int slot) => $"SaveFile{slot}";
}