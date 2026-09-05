using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveManager : PersistentSingleton<SaveManager>
{
    public float currentPlayTime = 0f;

    // 로딩 중 임시 보관
    private bool     _isLoading   = false;
    private SaveData _pendingData = null;

    // ─────────────────────────────────────────────
    //  초기화
    // ─────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    protected override void OnAwake()
    {
        SceneManager.sceneLoaded       += OnSceneLoaded;
        DialogueEvents.OnDialogueEnded += OnDialogueEnded;
    }

    protected override void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded       -= OnSceneLoaded;
            DialogueEvents.OnDialogueEnded -= OnDialogueEnded;
        }
        base.OnDestroy();
    }

    // 대화 중 설정된 목표(yarn <<showObjective>> 등)는 IsRunning 가드에 막혀 체크포인트가 스킵됨.
    // 대화 경계에서 한 번 보정 저장해 컷씬 진행 구간의 체크포인트 공백을 메운다.
    void OnDialogueEnded() => SaveCheckpoint("대화 종료");

    private void Update()
    {
        if (Time.timeScale > 0)
            currentPlayTime += Time.unscaledDeltaTime;
    }

    // ─────────────────────────────────────────────
    //  저장
    // ─────────────────────────────────────────────
    const string PreBattleKey = "PreBattleSave";
    // 탈출 압박 실패(배드 엔딩) 전용 되감기 지점.
    // CheckpointKey 를 재사용하지 않는 이유 — 체크포인트는 대화 종료마다 덮어써지므로(OnDialogueEnded)
    // 90초 도중 S#12 단검 대사가 끝나면 밀린다. '집 = S#11 직후 / 마을 = 진입 지점' 을 보존해야 한다.
    const string RewindKey    = "RewindSave";
    // 중단 저장(F-5-5) — 메뉴 '그만두기' 전용 슬롯 1개.
    // 슬롯 3개(SaveFile0~2) 목록에 섞지 않고, 불러온 즉시 삭제한다.
    const string SuspendKey   = "SuspendSave";

    SaveData BuildSaveData()
    {
        SaveData data = new SaveData
        {
            sceneName  = SceneManager.GetActiveScene().name,
            playTime   = currentPlayTime,
            saveDate   = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            saveTicks  = System.DateTime.Now.Ticks,
            corruption = CorruptionManager.Instance != null
                         ? CorruptionManager.Instance.currentCorruption : 0f,
        };

        data.playerLevel    = PlayerGrowth.Level;
        data.playerExp      = PlayerGrowth.Exp;
        data.journalEntries = JournalManager.BuildSaveList();
        data.playerName     = PlayerIdentity.Name;

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
            data.health = GameState.player.health;
            data.mental = GameState.player.mental;
        }
        else
        {
            data.health = 100f;
            data.mental = 100f;
        }
        // 인형화는 CorruptionManager 가 단일 소스 — corruption 과 동일 값 기록 (구버전 빌드 호환용 필드 유지)
        data.puppetization = data.corruption;

        data.isNightSequenceWatched = GameState.isNightSequenceWatched;
        data.isResolved              = GameState.isResolved;
        data.isBreakfastWatched     = GameState.isBreakfastWatched;
        data.isZombieDefeated        = GameState.isZombieDefeated;
        data.tutorialBattleStep      = GameState.tutorialBattleStep;
        data.isBreadDoughAcquired    = GameState.isBreadDoughAcquired;
        data.hasMerchantMetAtSquare  = GameState.hasMerchantMetAtSquare;
        data.isAtticKeyFound         = GameState.isAtticKeyFound;
        data.isDaggerAcquired        = GameState.isDaggerAcquired;
        data.isAtticBoxOpened        = GameState.isAtticBoxOpened;
        data.isAtticRadioPlayed      = GameState.isAtticRadioPlayed;
        data.isWorkbenchLocked       = GameState.isWorkbenchLocked;
        data.isYardSugarSeen         = GameState.isYardSugarSeen;
        data.isSeraOut               = GameState.isSeraOut;
        data.isDoorknobRefused       = GameState.isDoorknobRefused;
        data.isFrontDoorKeyFound     = GameState.isFrontDoorKeyFound;
        data.isDaggerToggleUnlocked  = GameState.isDaggerToggleUnlocked;
        data.isFrontDoorPassed       = GameState.isFrontDoorPassed;

        foreach (string id in GameState.defeatedEnemyIDs)
            data.defeatedEnemyIDs.Add(id);
        foreach (string key in GameState.chosenDialogueKeys)
            data.chosenDialogueKeys.Add(key);

        if (GameStateManager.Instance != null)
            foreach (var kvp in GameStateManager.Instance.flags)
                data.dynamicFlags.Add(new FlagEntry { key = kvp.Key, value = kvp.Value });

        return data;
    }

    public void SaveGame(int slot)
    {
        SaveData data = BuildSaveData();
        PlayerPrefs.SetString(SlotKey(slot), JsonUtility.ToJson(data));
        PlayerPrefs.Save();
        // 토끼로 저장하면 중단 저장을 지운다 — 복귀 지점이 둘이 되지 않게 (CLAUDE.md §8)
        DeleteSuspend();
        Dbg.Log($"[SaveManager] 슬롯 {slot} 저장 완료");
    }

    // ─────────────────────────────────────────────
    //  중단 저장 (F-5-5) — 메뉴 '그만두기' 전용
    // ─────────────────────────────────────────────

    /// <summary>중단 저장이 존재하는지 여부.</summary>
    public bool HasSuspendSave => PlayerPrefs.HasKey(SuspendKey);

    /// <summary>현재 상태를 중단 저장 전용 키에 저장합니다. 슬롯 3개와는 분리됩니다.</summary>
    public void SaveSuspend()
    {
        SaveData data = BuildSaveData();
        PlayerPrefs.SetString(SuspendKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
        Dbg.Log("[SaveManager] 중단 저장 완료");
    }

    /// <summary>중단 저장 데이터. 표시용(위치·플레이 시간)으로 읽습니다. 없으면 null.</summary>
    public SaveData GetSuspendData()
        => PlayerPrefs.HasKey(SuspendKey)
           ? JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SuspendKey)) : null;

    /// <summary>중단 저장을 삭제합니다.</summary>
    public void DeleteSuspend()
    {
        if (!PlayerPrefs.HasKey(SuspendKey)) return;
        PlayerPrefs.DeleteKey(SuspendKey);
        PlayerPrefs.Save();
        Dbg.Log("[SaveManager] 중단 저장 삭제");
    }

    /// <summary>
    /// 중단 저장을 불러옵니다. <b>불러오는 즉시 삭제합니다</b>(F-5-5) —
    /// 씬 전환 전에 지워서, 전환 도중 죽어도 저장본이 남지 않게 합니다.
    /// </summary>
    public void LoadSuspend()
    {
        if (!PlayerPrefs.HasKey(SuspendKey))
        {
            Debug.LogWarning("[SaveManager] 중단 저장 데이터가 없습니다.");
            return;
        }
        SaveData data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SuspendKey));
        DeleteSuspend();

        _pendingData = data;
        _isLoading   = true;
        if (TransitionManager.Instance != null)
            TransitionManager.Instance.DoSceneTransition(data.sceneName);
        else
            SceneManager.LoadScene(data.sceneName);
    }

    /// <summary>전투 직전 저장 데이터가 존재하는지 여부. GameOverUI가 불러오기 가능 여부 판단에 사용.</summary>
    public bool HasPreBattleSave => PlayerPrefs.HasKey(PreBattleKey);

    /// <summary>전투 직전 상태를 별도 키에 저장합니다. 사망 시 이 지점으로 복귀합니다.</summary>
    public void SavePreBattle()
    {
        SaveData data = BuildSaveData();
        PlayerPrefs.SetString(PreBattleKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
        Dbg.Log("[SaveManager] 전투 전 상태 저장 완료");
    }

    /// <summary>되감기 지점 데이터가 존재하는지 여부.</summary>
    public bool HasRewindSave => PlayerPrefs.HasKey(RewindKey);

    /// <summary>
    /// 탈출 압박 구간에 진입하는 시점의 상태를 별도 키에 저장합니다.
    /// 배드 엔딩 후 이 지점으로 되돌립니다(집 = S#11 직후 / 마을 = 진입 지점).
    /// </summary>
    public void SaveRewindPoint()
    {
        SaveData data = BuildSaveData();
        PlayerPrefs.SetString(RewindKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
        Dbg.Log("[SaveManager] 되감기 지점 저장 완료");
    }

    /// <summary>되감기 지점 데이터를 불러옵니다. 데이터가 없으면 경고 후 무시합니다.</summary>
    public void LoadRewindPoint()
    {
        if (!PlayerPrefs.HasKey(RewindKey))
        {
            Debug.LogWarning("[SaveManager] 되감기 지점 저장 데이터가 없습니다.");
            return;
        }
        SaveData data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(RewindKey));
        _pendingData = data;
        _isLoading   = true;
        if (TransitionManager.Instance != null)
            TransitionManager.Instance.DoSceneTransition(data.sceneName);
        else
            SceneManager.LoadScene(data.sceneName);
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
    //  체크포인트 자동 저장
    // ─────────────────────────────────────────────
    // PreBattleKey 와 별도 키 — 전투 전 저장은 '전투 직전' 의미를 보존해야 하므로 덮어쓰지 않음
    const string CheckpointKey      = "CheckpointSave";
    const float  CheckpointDebounce = 5f;
    float _lastCheckpointRealtime = -999f;

    /// <summary>체크포인트 저장 데이터가 존재하는지 여부.</summary>
    public bool HasCheckpointSave => PlayerPrefs.HasKey(CheckpointKey);

    public enum CheckpointResult { Saved, Busy, NotGameplay, Debounced }

    /// <summary>
    /// 씬 이동·목표 갱신·대화 종료·빠른 저장 시점의 자동 저장.
    /// </summary>
    /// <param name="bypassDebounce">수동 빠른 저장처럼 명시적 요청이면 true — 5초 디바운스를 건너뜁니다.</param>
    /// <returns>저장 결과. 빠른 저장 토스트 문구 분기에 사용.</returns>
    public CheckpointResult SaveCheckpoint(string reason, bool bypassDebounce = false)
    {
        if (_isLoading || BattleSystem.IsActive || HackSlashCombatManager.IsActive || YarnDialogue.IsRunning)
            return CheckpointResult.Busy;
        if (!IsGameplayScene(SceneManager.GetActiveScene().name))
            return CheckpointResult.NotGameplay;
        // 플레이어가 아직 씬에 없으면(스폰 지연) 위치가 (0,0,0)으로 저장되는 것을 방지
        if (GameObject.FindGameObjectWithTag("Player") == null)
            return CheckpointResult.Busy;
        if (!bypassDebounce && Time.realtimeSinceStartup - _lastCheckpointRealtime < CheckpointDebounce)
            return CheckpointResult.Debounced;

        _lastCheckpointRealtime = Time.realtimeSinceStartup;
        SaveData data = BuildSaveData();
        PlayerPrefs.SetString(CheckpointKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
        Dbg.Log($"[SaveManager] 체크포인트 저장 완료 ({reason})");
        return CheckpointResult.Saved;
    }

    /// <summary>체크포인트 저장 데이터를 불러옵니다. 데이터가 없으면 경고 후 무시합니다.</summary>
    public void LoadCheckpoint()
    {
        if (!PlayerPrefs.HasKey(CheckpointKey))
        {
            Debug.LogWarning("[SaveManager] 체크포인트 저장 데이터가 없습니다.");
            return;
        }
        SaveData data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(CheckpointKey));
        _pendingData = data;
        _isLoading   = true;
        if (TransitionManager.Instance != null)
            TransitionManager.Instance.DoSceneTransition(data.sceneName);
        else
            SceneManager.LoadScene(data.sceneName);
    }

    /// <summary>복구 지점 비교용 데이터 조회 (적용하지 않음). GameOverUI가 saveTicks 최신 판정에 사용.</summary>
    public SaveData GetPreBattleData()
        => PlayerPrefs.HasKey(PreBattleKey)
           ? JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(PreBattleKey)) : null;

    public SaveData GetCheckpointData()
        => PlayerPrefs.HasKey(CheckpointKey)
           ? JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(CheckpointKey)) : null;

    public SaveData GetRewindData()
        => PlayerPrefs.HasKey(RewindKey)
           ? JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(RewindKey)) : null;

    static bool IsGameplayScene(string name)
        => name == SceneNames.Home || name == SceneNames.Map
        || name == SceneNames.Shelter;

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
        if (_isLoading && _pendingData != null)
        {
            StartCoroutine(RestoreRoutine(_pendingData));
            return;
        }

        // 일반 씬 이동(로딩 아님) 완료 시 체크포인트 자동 저장.
        // RoomTransfer(같은 씬 내 방 이동)는 sceneLoaded 를 발생시키지 않아 자연 제외.
        if (IsGameplayScene(scene.name))
            StartCoroutine(CheckpointAfterSceneLoad());
    }

    IEnumerator CheckpointAfterSceneLoad()
    {
        // 플레이어 스폰까지 대기 — 위치가 (0,0,0)으로 저장되는 것 방지 (RestoreRoutine 과 같은 정책)
        for (int i = 0; i < 15; i++)
        {
            if (GameObject.FindGameObjectWithTag("Player") != null) break;
            yield return null;
        }
        SaveCheckpoint("씬 이동");
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
        {
            player.transform.position = new Vector3(data.playerX, data.playerY, data.playerZ);
            CameraFollow.Instance?.SnapCameraToFollow();
        }
        else
            Debug.LogWarning("[SaveManager] 플레이어를 찾지 못했습니다.");

        // ── 타락 수치 ──
        if (CorruptionManager.Instance != null)
            CorruptionManager.Instance.LoadCorruption(data.corruption);

        // ── 스탯 ── (인형화는 corruption 필드가 진실 — 구버전 세이브에서 두 값이 다르면 corruption 채택)
        GameState.player = new GameState.PlayerState
        {
            health        = data.health,
            mental        = data.mental,
            puppetization = data.corruption,
        };

        // ── 마이그레이션: 구버전 세이브 누락 필드 기본값 채우기 ──
        if (data.saveVersion < 4)
        {
            data.isAtticKeyFound    = false;
            data.isDaggerAcquired   = false;
            data.isAtticRadioPlayed = false;
            data.isWorkbenchLocked  = true;
        }
        if (data.saveVersion < 5)
        {
            data.isAtticBoxOpened = false;
        }
        if (data.saveVersion < 6)
        {
            data.playerLevel    = 1;
            data.playerExp      = 0;
            data.journalEntries = new List<JournalEntrySave>();
            data.saveTicks      = 0;
        }
        if (data.saveVersion < 7)
        {
            // 이름 입력 기능 이전의 세이브 — 주인공은 항상 "루" 였다
            data.playerName = PlayerIdentity.DefaultName;
        }
        if (data.saveVersion < 8)
        {
            // D 정본(2026-08-07) 이전의 세이브. S#04F~S#13 구조가 통째로 바뀌었으므로
            // 진행 상태를 추정하지 않고 전부 false 로 둔다.
            // 단검을 이미 얻은 세이브라면 필터 토글은 열려 있어야 하므로 그것만 승계한다.
            data.isYardSugarSeen         = false;
            data.isSeraOut               = false;
            data.isDoorknobRefused       = false;
            data.isFrontDoorKeyFound     = false;
            data.isDaggerToggleUnlocked  = data.isDaggerAcquired;
        }

        // ── 성장 복원 ──
        PlayerGrowth.Load(data.playerLevel, data.playerExp);

        // ── 주인공 이름 복원 ──
        PlayerIdentity.Load(data.playerName);

        // ── 저널 복원 ──
        JournalManager.Load(data.journalEntries);

        // ── 스토리 플래그 ──
        GameState.isNightSequenceWatched = data.isNightSequenceWatched;
        GameState.isResolved              = data.isResolved;
        GameState.isBreakfastWatched     = data.isBreakfastWatched;
        GameState.isZombieDefeated        = data.isZombieDefeated;
        GameState.tutorialBattleStep      = data.tutorialBattleStep;
        GameState.isBreadDoughAcquired    = data.isBreadDoughAcquired;
        GameState.hasMerchantMetAtSquare  = data.hasMerchantMetAtSquare;
        GameState.isAtticKeyFound         = data.isAtticKeyFound;
        GameState.isDaggerAcquired        = data.isDaggerAcquired;
        GameState.isAtticBoxOpened        = data.isAtticBoxOpened;
        GameState.isAtticRadioPlayed      = data.isAtticRadioPlayed;
        GameState.isWorkbenchLocked       = data.isWorkbenchLocked;
        GameState.isYardSugarSeen         = data.isYardSugarSeen;
        GameState.isSeraOut               = data.isSeraOut;
        GameState.isDoorknobRefused       = data.isDoorknobRefused;
        GameState.isFrontDoorKeyFound     = data.isFrontDoorKeyFound;
        GameState.isDaggerToggleUnlocked  = data.isDaggerToggleUnlocked;
        GameState.isFrontDoorPassed       = data.isFrontDoorPassed;

        // ── 단검 장착 상태 복원 (DontDestroyOnLoad 라 세션 상태가 세이브와 어긋날 수 있음) ──
        if (DaggerSystem.Instance != null)
        {
            if (GameState.isDaggerAcquired) DaggerSystem.Instance.Equip();
            else                            DaggerSystem.Instance.Unequip();
        }

        // ── 처치된 적 ID ──
        GameState.defeatedEnemyIDs   = new System.Collections.Generic.HashSet<string>(data.defeatedEnemyIDs);
        GameState.chosenDialogueKeys = new System.Collections.Generic.HashSet<string>(data.chosenDialogueKeys);

        // ── 동적 플래그 ──
        if (GameStateManager.Instance != null)
        {
            if (data.dynamicFlags != null && data.dynamicFlags.Count > 0)
            {
                var dict = new Dictionary<string, bool>();
                foreach (var entry in data.dynamicFlags)
                    dict[entry.key] = entry.value;
                GameStateManager.Instance.LoadFlags(dict);
            }
            else
            {
                // 구버전 세이브(dynamicFlags 없음): 현재 세션 플래그가 남지 않게 초기값으로 리셋
                if (FlagManager.Instance != null)
                    FlagManager.Instance.ResetToDefaults();
                else
                    GameStateManager.Instance.flags.Clear();
            }
        }
        // ── PlayerStats 즉시 반영 ──
        if (player != null)
        {
            PlayerStats stats = player.GetComponent<PlayerStats>();
            if (stats != null)
            {
                stats.currentHealth = data.health;
                stats.currentMental = data.mental;
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

        // ── 플레이타임 복원 (재저장 시 누적 시간이 세션 시간으로 덮이는 것 방지) ──
        currentPlayTime = data.playTime;

        // ── 완료 ──
        Time.timeScale = 1f;
        _isLoading     = false;
        _pendingData   = null;
        Dbg.Log("[SaveManager] 불러오기 완료");
    }

    // ─────────────────────────────────────────────
    //  삭제
    // ─────────────────────────────────────────────
    /// <summary>지정 슬롯의 저장 데이터를 삭제합니다.</summary>
    public void DeleteSlot(int slot)
    {
        string key = SlotKey(slot);
        if (PlayerPrefs.HasKey(key))
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            Dbg.Log($"[SaveManager] 슬롯 {slot} 삭제 완료");
        }
    }

    /// <summary>모든 슬롯(0~2) 및 전투 전·체크포인트·되감기 저장 데이터를 삭제합니다.</summary>
    public void DeleteAllSlots()
    {
        for (int i = 0; i < 3; i++)
            PlayerPrefs.DeleteKey(SlotKey(i));
        PlayerPrefs.DeleteKey(PreBattleKey);
        PlayerPrefs.DeleteKey(CheckpointKey);
        // 남겨두면 새 게임에서 배드 엔딩에 걸렸을 때 이전 플레이의 되감기 지점으로 복귀한다
        PlayerPrefs.DeleteKey(RewindKey);
        // 같은 이유 — 남겨두면 새 게임에서 이전 플레이의 중단 지점을 이어받는다
        PlayerPrefs.DeleteKey(SuspendKey);
        PlayerPrefs.Save();
        Dbg.Log("[SaveManager] 모든 슬롯 삭제 완료");
    }

    // ─────────────────────────────────────────────
    //  유틸
    // ─────────────────────────────────────────────
    static string SlotKey(int slot) => $"SaveFile{slot}";
}