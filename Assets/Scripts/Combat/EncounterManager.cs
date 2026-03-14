using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class EncounterManager : MonoBehaviour
{
    public static EncounterManager Instance;
    public static string currentEnemyID; // ★ 현재 부딪힌 적의 고유 이름(ID) 저장용 ★

    [Header("UI 연결")]
    [Tooltip("적과 마주쳤을 때 뜨는 선택창 패널")]
    public GameObject encounterPanel; // 선택창 전체 패널
    [Tooltip("상황 설명 텍스트 (예: 적과 마주쳤다!)")]
    public Text titleText;            // "적과 조우했다!"
    [Tooltip("현실 모드 선택 버튼")]
    public Button realityBtn;         // "현실로 싸우기 (단검/그대로)"
    [Tooltip("환상 모드 선택 버튼")]
    public Button fantasyBtn;         // "환상으로 싸우기 (마시멜로/그대로)"
    
    [Header("버튼 텍스트")]
    public Text realityBtnText;
    public Text fantasyBtnText;

    [Header("필요 아이템 이름")]
    [Tooltip("환상을 보기 위해 필요한 아이템 이름 (ItemData와 일치해야 함)")]
    public string marshmallowItemName = "Marshmallow";
    [Tooltip("현실을 직시하기 위해 필요한 아이템 이름 (ItemData와 일치해야 함)")]
    public string daggerItemName = "Dagger";

    private GameObject currentEnemy; // 필드에 이미 있는 적 (충돌 시)
    public GameObject enemyPrefabToSpawn; // 랜덤 인카운터로 소환될 적 프리팹

    private bool isRealityScene;

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        if (encounterPanel != null) encounterPanel.SetActive(false);
    }

    // [기존] 필드의 적과 부딪혔을 때
    public void StartEncounter(GameObject enemy)
    {
        currentEnemy = enemy;
        currentEnemyID = enemy.name; // ★ 적의 이름을 ID로 기억합니다 ★
        enemyPrefabToSpawn = null; // 스폰할 필요 없음
        
        SetupUI("적과 마주쳤다!");
    }

    // [신규] 랜덤 인카운터 (보이지 않는 적)
    public void StartRandomEncounter(GameObject prefab, string enemyName)
    {
        currentEnemy = null; // 필드에 아직 없음
        enemyPrefabToSpawn = prefab;

        SetupUI($"정체불명의 기척이 느껴진다... ({enemyName})");
    }

    void SetupUI(string title)
    {
        isRealityScene = GameState.IsRealityScene(SceneManager.GetActiveScene().name);

        // 시간 정지
        Time.timeScale = 0f;
        encounterPanel.SetActive(true);
        
        // 제목 설정
        titleText.text = title + (isRealityScene ? " [현실]" : " [환상]");

        UpdateButtons();
    }

    void UpdateButtons()
    {
        bool hasMarshmallow = InventoryManager.Instance.HasItem(marshmallowItemName);
        bool hasDagger = InventoryManager.Instance.HasItem(daggerItemName);

        if (isRealityScene)
        {
            // 1. 현실 -> 현실 (기본)
            realityBtn.interactable = true;
            realityBtnText.text = "그대로 싸운다 (핵앤슬래시)";

            // 2. 현실 -> 환상 (마시멜로 필요)
            if (hasMarshmallow)
            {
                fantasyBtn.interactable = true;
                fantasyBtnText.text = "마시멜로를 먹는다 (환상 모드 진입)";
            }
            else
            {
                fantasyBtn.interactable = false;
                fantasyBtnText.text = "마시멜로가 없다...";
            }
        }
        else // 환상 씬일 때
        {
            // 1. 환상 -> 환상 (기본)
            fantasyBtn.interactable = true;
            fantasyBtnText.text = "전투 개시 (탄막/턴제)";

            // 2. 환상 -> 현실 (단검 필요)
            if (hasDagger)
            {
                realityBtn.interactable = true;
                realityBtnText.text = "단검을 든다 (현실 모드 진입)";
            }
            else
            {
                realityBtn.interactable = false;
                realityBtnText.text = "단검이 없다...";
            }
        }
    }

    // [버튼 1] 현실 모드 선택
    public void OnChooseReality()
    {
        ResumeGame();

        if (isRealityScene)
        {
            // 현실 -> 현실
            if (currentEnemy != null)
            {
                // 이미 있는 적과 싸움 (아무것도 안 해도 됨)
                Debug.Log("현실 전투 시작 (기존 적)");
            }
            else if (enemyPrefabToSpawn != null)
            {
                // ★ 랜덤 인카운터: 적을 플레이어 근처에 소환!
                SpawnEnemyNearPlayer();
            }
        }
        else
        {
            // 환상 -> 현실: 단검 사용(소모X), 현실 씬 로드
            Debug.Log("단검을 꺼내 현실을 직시한다!");
            
            GameState.lastPosition = GameObject.FindGameObjectWithTag("Player").transform.position;
            GameState.hasPositionSaved = true;
            GameState.isComingFromBattle = true;

            SceneManager.LoadScene("DarkReality"); 
        }
    }

    void SpawnEnemyNearPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && enemyPrefabToSpawn != null)
        {
            // 플레이어 주변 랜덤 위치 (반경 2~4m)
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            float distance = Random.Range(2f, 4f);
            Vector3 spawnPos = player.transform.position + (Vector3)(randomDir * distance);

            Instantiate(enemyPrefabToSpawn, spawnPos, Quaternion.identity);
            Debug.Log($"보이지 않던 적이 실체화되었다! 위치: {spawnPos}");
        }
    }

    // [버튼 2] 환상 모드 선택
    public void OnChooseFantasy()
    {
        ResumeGame();

        if (isRealityScene)
        {
            // 현실 -> 환상: 마시멜로 소모 O
            ItemData marshmallow = InventoryManager.Instance.inventoryItems.Find(x => x.itemName == marshmallowItemName);
            if (marshmallow != null)
            {
                InventoryManager.Instance.RemoveItem(marshmallow);
                Debug.Log("마시멜로 냠냠... 환상이 보인다.");
            }
        }

        // 공통: 배틀씬으로 이동
        GameState.lastPosition = GameObject.FindGameObjectWithTag("Player").transform.position;
        GameState.hasPositionSaved = true;
        GameState.isComingFromBattle = true;

        SceneManager.LoadScene("BattleScene");
    }

    void ResumeGame()
    {
        Time.timeScale = 1f;
        encounterPanel.SetActive(false);
    }
}
