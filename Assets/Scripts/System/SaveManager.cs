using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;

public class SaveManager : MonoBehaviour
{
    public static SaveManager instance;

    public float currentPlayTime = 0f;
    
    // 로딩 직후에 위치를 옮겨야 하는지 체크하는 변수
    private bool isGameLoading = false; 
    private SaveData pendingLoadData; // 로딩할 데이터를 잠시 들고 있는 변수

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            // 씬이 바뀔 때마다 "OnSceneLoaded" 함수를 실행하라고 등록
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else Destroy(gameObject);
    }

    private void Update()
    {
        if (Time.timeScale > 0) currentPlayTime += Time.unscaledDeltaTime; 
    }

    // 1. 저장하기 (위치 저장 추가됨)
    public void SaveGame(int slotIndex)
    {
        SaveData data = new SaveData();

        data.sceneName = SceneManager.GetActiveScene().name;
        data.playTime = currentPlayTime;
        data.saveDate = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        
        if (CorruptionManager.instance != null)
            data.corruption = CorruptionManager.instance.currentCorruption;

        // ★ [중요] 플레이어 위치 저장
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            data.playerX = player.transform.position.x;
            data.playerY = player.transform.position.y;
            data.playerZ = player.transform.position.z;
        }

        // ★ [추가됨] 인벤토리 저장
        // GameState에 있는 아이템 리스트를 이름 문자열 리스트로 변환
        if (GameState.inventoryItems != null)
        {
            foreach (var item in GameState.inventoryItems)
            {
                if (item != null)
                {
                    data.inventoryItemNames.Add(item.name); // 에셋 이름 저장 (예: "Apple")
                }
            }
        }

        // ★ [추가됨] 스탯 및 게이지 저장 (GameState에서 가져옴)
        // 만약 현재 씬에 PlayerStats가 있다면 최신값일 테니 그것도 반영
        if (GameState.playerHealth >= 0)
        {
            data.health = GameState.playerHealth;
            data.mental = GameState.playerMental;
            data.hunger = GameState.playerHunger;
        }
        else
        {
            // 아직 게임 시작 안 했거나 초기 상태면 기본값 저장
            data.health = 100f; 
            data.mental = 100f; 
            data.hunger = 100f;
        }
        
        // RealitySystem 게이지 (savedGaugeValue)
        data.realityGauge = GameState.savedGaugeValue;

        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString("SaveFile" + slotIndex, json);
        PlayerPrefs.Save();
        Debug.Log(slotIndex + "번 슬롯에 위치까지 저장 완료!");
    }

    // 2. 데이터 가져오기 (UI 표시용)
    public SaveData LoadSaveData(int slotIndex)
    {
        string fileName = "SaveFile" + slotIndex;
        if (PlayerPrefs.HasKey(fileName))
        {
            return JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(fileName));
        }
        return null;
    }

    // 3. 게임 불러오기 (로딩 시작!)
    public void LoadGame(int slotIndex)
    {
        SaveData data = LoadSaveData(slotIndex);
        if (data == null) return;

        // 데이터를 잠시 보관 (씬 로딩 끝나면 쓰려고)
        pendingLoadData = data;
        isGameLoading = true; 

        // 씬 이동 시작
        SceneManager.LoadScene(data.sceneName);
    }

    // ★ 4. 씬 로딩이 끝났을 때 자동으로 실행되는 함수
    // (여기서 위치를 옮겨야 함. 씬 이동 전에는 플레이어가 없으니까!)
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (isGameLoading && pendingLoadData != null)
        {
            // (1) 플레이어 시간 & 타락 수치 복구
            currentPlayTime = pendingLoadData.playTime;
            if (CorruptionManager.instance != null)
                CorruptionManager.instance.currentCorruption = pendingLoadData.corruption;

            // ★ [최적화] 플레이어를 안전하게 찾기 위해 코루틴 사용
            StartCoroutine(RestorePlayerDataRoutine());

            // (3) 정리
            Time.timeScale = 1f; // 시간 흐르게
            // isGameLoading는 코루틴 안에서 false로 바꿈
        }
    }

    System.Collections.IEnumerator RestorePlayerDataRoutine()
    {
        // 0.5초 정도 기다려서 다른 객체들이 초기화될 시간을 줌
        // (너무 빠르면 플레이어를 못 찾을 수도 있음)
        int retryCount = 0;
        GameObject player = null;

        while (player == null && retryCount < 10) // 최대 5번(약 1~2초) 시도
        {
            player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) yield return null; // 1프레임 대기
            retryCount++;
        }

        if (player != null)
        {
            // (2) 플레이어 위치 이동
            player.transform.position = new Vector3(
                pendingLoadData.playerX, 
                pendingLoadData.playerY, 
                pendingLoadData.playerZ
            );
            Debug.Log("플레이어 위치 이동 완료!");
        }
        else
        {
            Debug.LogWarning("로딩 실패: 플레이어를 찾을 수 없습니다.");
        }

        // ★ [추가됨] 스탯 및 게이지 복구
        GameState.playerHealth = pendingLoadData.health;
        GameState.playerMental = pendingLoadData.mental;
        GameState.playerHunger = pendingLoadData.hunger;
        GameState.savedGaugeValue = pendingLoadData.realityGauge; // RealitySystem 복구

        // 현재 씬에 PlayerStats가 있으면 즉시 반영
        if (player != null)
        {
            PlayerStats stats = player.GetComponent<PlayerStats>();
            if (stats != null)
            {
                stats.currentHealth = GameState.playerHealth;
                stats.currentMental = GameState.playerMental;
                stats.currentHunger = GameState.playerHunger;
                stats.UpdateUI();
            }
        }

        // ★ [추가됨] 인벤토리 복구
        // GameState 리스트 초기화
        GameState.inventoryItems = new System.Collections.Generic.List<ItemData>();
        
        if (pendingLoadData.inventoryItemNames != null)
        {
            foreach (string itemName in pendingLoadData.inventoryItemNames)
            {
                // Resources/Items 폴더에서 이름으로 아이템 로드
                ItemData item = Resources.Load<ItemData>("Items/" + itemName);
                if (item != null)
                {
                    GameState.inventoryItems.Add(item);
                }
                else
                {
                    Debug.LogWarning($"아이템을 찾을 수 없습니다: Items/{itemName}");
                }
            }
        }

        // 만약 현재 씬에 InventoryManager가 이미 있다면 UI 갱신 요청
        if (InventoryManager.Instance != null)
        {
            // 인벤토리 매니저가 GameState 리스트를 참조하도록 재설정
            InventoryManager.Instance.inventoryItems = GameState.inventoryItems;
            InventoryManager.Instance.UpdateSlotUI();
        }

        isGameLoading = false;
        pendingLoadData = null;
    }
}