using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class EncounterManager : MonoBehaviour
{
    public static EncounterManager Instance;
    public static string currentEnemyID;

    [Header("UI")]
    public GameObject encounterPanel;
    public Text       titleText;
    public Button     realityBtn;
    public Button     fantasyBtn;

    [Header("버튼 텍스트")]
    public Text realityBtnText;
    public Text fantasyBtnText;

    [Header("필요 아이템 이름")]
    public string marshmallowItemName = "Marshmallow";
    public string daggerItemName      = "Dagger";

    private GameObject _currentEnemy;
    public  GameObject enemyPrefabToSpawn;

    private bool _isRealityScene;

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        if (encounterPanel != null) encounterPanel.SetActive(false);
    }

    public void StartEncounter(GameObject enemy)
    {
        _currentEnemy     = enemy;
        currentEnemyID    = enemy.name;
        enemyPrefabToSpawn = null;
        SetupUI("적과 마주쳤다!");
    }

    public void StartRandomEncounter(GameObject prefab, string enemyName)
    {
        _currentEnemy      = null;
        enemyPrefabToSpawn = prefab;
        SetupUI($"정체불명의 기척이 느껴진다... ({enemyName})");
    }

    void SetupUI(string title)
    {
        _isRealityScene = SceneNames.IsRealityScene(SceneManager.GetActiveScene().name);
        Time.timeScale  = 0f;
        encounterPanel.SetActive(true);
        titleText.text  = title + (_isRealityScene ? " [현실]" : " [환상]");
        UpdateButtons();
    }

    void UpdateButtons()
    {
        bool hasMarshmallow = InventoryManager.Instance.HasItem(marshmallowItemName);
        bool hasDagger      = InventoryManager.Instance.HasItem(daggerItemName);

        if (_isRealityScene)
        {
            realityBtn.interactable = true;
            realityBtnText.text     = "그대로 싸운다 (핵앤슬래시)";

            fantasyBtn.interactable = hasMarshmallow;
            fantasyBtnText.text     = hasMarshmallow
                ? "마시멜로를 먹는다 (환상 모드 진입)"
                : "마시멜로가 없다...";
        }
        else
        {
            fantasyBtn.interactable = true;
            fantasyBtnText.text     = "전투 개시 (탄막/턴제)";

            realityBtn.interactable = hasDagger;
            realityBtnText.text     = hasDagger
                ? "단검을 든다 (현실 모드 진입)"
                : "단검이 없다...";
        }
    }

    public void OnChooseReality()
    {
        ResumeGame();

        if (_isRealityScene)
        {
            if (_currentEnemy == null && enemyPrefabToSpawn != null)
                SpawnEnemyNearPlayer();
            // 이미 있는 적이면 그냥 싸움 (아무것도 안 해도 됨)
        }
        else
        {
            // 환상 -> 현실: 씬 이동
            SavePlayerPosition();
            SceneManager.LoadScene(SceneNames.DarkReality);
        }
    }

    public void OnChooseFantasy()
    {
        ResumeGame();

        if (_isRealityScene)
        {
            // 마시멜로 소모
            ItemData marshmallow = InventoryManager.Instance.inventoryItems
                .Find(x => x != null && x.itemName == marshmallowItemName);
            if (marshmallow != null)
                InventoryManager.Instance.RemoveItem(marshmallow);
        }

        // 공통: 배틀씬 이동
        SavePlayerPosition();
        SceneManager.LoadScene(SceneNames.Battle);
    }

    void SavePlayerPosition()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) return;
        GameState.lastPosition     = p.transform.position;
        GameState.hasPositionSaved = true;
        GameState.isComingFromBattle = true;
    }

    void SpawnEnemyNearPlayer()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null || enemyPrefabToSpawn == null) return;

        Vector2 dir      = Random.insideUnitCircle.normalized;
        float   distance = Random.Range(2f, 4f);
        Instantiate(enemyPrefabToSpawn,
                    p.transform.position + (Vector3)(dir * distance),
                    Quaternion.identity);
    }

    void ResumeGame()
    {
        Time.timeScale = 1f;
        encounterPanel.SetActive(false);
    }
}