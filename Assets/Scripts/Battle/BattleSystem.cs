using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public enum BattleState { START, PLAYERTURN, ENEMYTURN, WON, LOST }

public class BattleSystem : MonoBehaviour
{
    public BattleState state;

    [Header("유닛 및 위치")]
    public GameObject playerPrefab;
    public GameObject companionPrefab;
    public GameObject enemyPrefab;
    public Transform  playerBattleStation;
    public Transform  companionBattleStation;
    public Transform  enemyBattleStation;

    [Header("HP 슬라이더 (배틀씬 전용 — 직접 연결하세요)")]
    public Slider playerHPSlider;
    public Slider companionHPSlider;
    public Slider enemyHPSlider;

    [Header("게이지 설정")]
    public float funGauge             = 0f;
    public float fatigueGauge         = 0f;
    public float maxGauge             = 100f;
    public float playerDamageMultiplier = 0.1f;

    [Header("UI")]
    public Text dialogueText;

    [Header("메뉴 패널")]
    public GameObject mainMenuPanel;
    public GameObject actionMenuPanel;
    public GameObject itemMenuPanel;

    [Header("아이템 버튼 동적 생성")]
    [Tooltip("아이템 버튼 프리팹 (Text 또는 TMP 가 포함된 Button)")]
    public GameObject itemButtonPrefab;
    [Tooltip("버튼들이 생성될 부모 Transform (ScrollView > Content 등)")]
    public Transform  itemButtonContainer;
    [Tooltip("아이템 없을 때 보여줄 Text")]
    public Text       noItemText;

    // ─ 내부 상태 ─
    private List<Unit>       _playerParty        = new List<Unit>();
    private Unit             _enemyUnit;
    private int              _currentUnitIndex   = 0;
    private List<GameObject> _spawnedItemButtons = new List<GameObject>();

    // 자주 쓰는 WaitForSeconds 캐싱
    private WaitForSeconds _wait1s;
    private WaitForSeconds _wait1_5s;
    private WaitForSeconds _wait2s;

    // ════════════════════════════════════════
    //  초기화
    // ════════════════════════════════════════
    void Start()
    {
        _wait1s   = new WaitForSeconds(1f);
        _wait1_5s = new WaitForSeconds(1.5f);
        _wait2s   = new WaitForSeconds(2f);

        state = BattleState.START;
        StartCoroutine(SetupBattle());
    }

    IEnumerator SetupBattle()
    {
        // 플레이어 소환
        SpawnUnit(playerPrefab, playerBattleStation, playerHPSlider, out Unit playerUnit);
        if (playerUnit != null) _playerParty.Add(playerUnit);

        // 동료 소환 (선택)
        SpawnUnit(companionPrefab, companionBattleStation, companionHPSlider, out Unit companionUnit);
        if (companionUnit != null) _playerParty.Add(companionUnit);

        // 적 소환 (EncounterManager 우선, 없으면 인스펙터 프리팹)
        GameObject enemyPrefabToUse =
            (EncounterManager.Instance != null && EncounterManager.Instance.enemyPrefabToSpawn != null)
            ? EncounterManager.Instance.enemyPrefabToSpawn
            : enemyPrefab;

        SpawnUnit(enemyPrefabToUse, enemyBattleStation, enemyHPSlider, out _enemyUnit);

        BattleData.nextEnemyPrefab = null;

        string eName = _enemyUnit != null ? _enemyUnit.unitName : "적";
        ShowDialogue("battle.wild_enemy_appear", $"야생의 {eName}이(가) 나타났다!", eName);

        SetPanelsActive(false, false, false);
        yield return _wait2s;

        state = BattleState.PLAYERTURN;
        _currentUnitIndex = 0;
        ProcessPartyTurn();
    }

    /// <summary>유닛 소환 + 슬라이더 주입 + SetHUD 호출을 한번에 처리.</summary>
    void SpawnUnit(GameObject prefab, Transform station, Slider slider, out Unit unit)
    {
        unit = null;
        if (prefab == null || station == null) return;

        GameObject go = Instantiate(prefab, station);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        unit = go.GetComponent<Unit>();
        if (unit == null) return;

        if (slider != null) unit.hpSlider = slider;
        unit.SetHUD();
    }

    // ════════════════════════════════════════
    //  턴 흐름
    // ════════════════════════════════════════
    void ProcessPartyTurn()
    {
        if (_currentUnitIndex < _playerParty.Count)
        {
            Unit cur = _playerParty[_currentUnitIndex];
            if (cur.currentHP > 0)
            {
                cur.ResetState();
                ShowDialogue("battle.player_turn_prompt",
                             $"{cur.unitName}의 턴: 무엇을 할까?", cur.unitName);
                ShowMainMenu();
            }
            else
            {
                _currentUnitIndex++;
                ProcessPartyTurn();
            }
        }
        else
        {
            state = BattleState.ENEMYTURN;
            StartCoroutine(EnemyTurn());
        }
    }

    void NextPartyMember()
    {
        _currentUnitIndex++;
        ProcessPartyTurn();
    }

    // ════════════════════════════════════════
    //  UI 제어
    // ════════════════════════════════════════
    void ShowMainMenu()   => SetPanelsActive(true,  false, false);
    void ShowActionMenu() => SetPanelsActive(false, true,  false);

    void ShowItemMenu()
    {
        SetPanelsActive(false, false, true);
        PopulateItemButtons();
    }

    void SetPanelsActive(bool main, bool action, bool item)
    {
        if (mainMenuPanel   != null) mainMenuPanel.SetActive(main);
        if (actionMenuPanel != null) actionMenuPanel.SetActive(action);
        if (itemMenuPanel   != null) itemMenuPanel.SetActive(item);
    }

    void PopulateItemButtons()
    {
        // 이전 버튼 제거
        foreach (var btn in _spawnedItemButtons)
            if (btn != null) Destroy(btn);
        _spawnedItemButtons.Clear();

        if (itemButtonContainer == null) return;

        List<ItemData> items = InventoryManager.Instance?.inventoryItems;
        bool hasItems = items != null && items.Count > 0;

        if (noItemText != null) noItemText.gameObject.SetActive(!hasItems);
        if (!hasItems) return;

        foreach (ItemData item in items)
        {
            if (item == null) continue;

            GameObject btnObj = itemButtonPrefab != null
                ? Instantiate(itemButtonPrefab, itemButtonContainer)
                : CreateFallbackButton(item.itemName, itemButtonContainer);

            SetButtonLabel(btnObj, item.DisplayName);

            Button btn = btnObj.GetComponent<Button>();
            if (btn != null)
            {
                ItemData captured = item;
                btn.onClick.AddListener(() => OnItemSelected(captured));
            }

            _spawnedItemButtons.Add(btnObj);
        }
    }

    /// <summary>itemButtonPrefab 이 없을 때 기본 버튼 생성.</summary>
    GameObject CreateFallbackButton(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                                typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 40);

        var textGo = new GameObject("Text", typeof(RectTransform),
                                    typeof(CanvasRenderer), typeof(Text));
        textGo.transform.SetParent(go.transform, false);
        var rt = textGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        var t = textGo.GetComponent<Text>();
        t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.alignment = TextAnchor.MiddleCenter;
        t.color     = Color.black;
        return go;
    }

    void SetButtonLabel(GameObject btnObj, string label)
    {
        var tmp = btnObj.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (tmp  != null) { tmp.text  = label; return; }
        var leg = btnObj.GetComponentInChildren<Text>();
        if (leg  != null)   leg.text  = label;
    }

    void OnItemSelected(ItemData item)
    {
        if (state != BattleState.PLAYERTURN) return;
        SetPanelsActive(false, false, false);
        InventoryManager.Instance?.RemoveItem(item);
        StartCoroutine(UseItemInBattle(item));
    }

    // ════════════════════════════════════════
    //  메인 메뉴 버튼 (인스펙터에서 연결)
    // ════════════════════════════════════════
    public void OnActionMenuButton()
    {
        if (state != BattleState.PLAYERTURN) return;
        ShowActionMenu();
    }

    public void OnItemMenuButton()
    {
        if (state != BattleState.PLAYERTURN) return;
        ShowItemMenu();
    }

    public void OnEscapeButton()
    {
        if (state != BattleState.PLAYERTURN) return;
        StartCoroutine(TryEscape());
    }

    // ════════════════════════════════════════
    //  행동 메뉴 버튼 (인스펙터에서 연결)
    // ════════════════════════════════════════
    public void OnAttackButton()
    {
        if (!IsPlayerTurn()) return;
        actionMenuPanel?.SetActive(false);
        StartCoroutine(PlayerAttack());
    }

    public void OnDefendButton()
    {
        if (!IsPlayerTurn()) return;
        actionMenuPanel?.SetActive(false);
        StartCoroutine(PlayerDefend());
    }

    public void OnSpecialButton()
    {
        if (!IsPlayerTurn()) return;
        actionMenuPanel?.SetActive(false);
        StartCoroutine(PlayerSpecialAction());
    }

    bool IsPlayerTurn() =>
        state == BattleState.PLAYERTURN && _currentUnitIndex < _playerParty.Count;

    // ════════════════════════════════════════
    //  전투 코루틴
    // ════════════════════════════════════════
    IEnumerator TryEscape()
    {
        mainMenuPanel?.SetActive(false);
        dialogueText.text = GetText("battle.escape", "도망") + "...";
        yield return _wait1_5s;

        if (Random.Range(0, 2) == 0)
        {
            ShowDialogue("battle.escape_success", "도망에 성공했다!");
            yield return _wait1s;
            state = BattleState.WON;
            EndBattle();
        }
        else
        {
            ShowDialogue("battle.escape_fail", "도망칠 수 없었다!");
            yield return _wait1s;
            NextPartyMember();
        }
    }

    IEnumerator PlayerAttack()
    {
        Unit cur = _playerParty[_currentUnitIndex];
        int  dmg = Mathf.Max(1, Mathf.RoundToInt(cur.damage * playerDamageMultiplier));
        bool dead = _enemyUnit.TakeDamage(dmg);

        dialogueText.text = $"{cur.unitName}의 공격! {dmg}의 데미지를 입혔다.";
        yield return _wait2s;

        if (dead) { state = BattleState.WON; EndBattle(); }
        else NextPartyMember();
    }

    IEnumerator PlayerSpecialAction()
    {
        Unit  cur = _playerParty[_currentUnitIndex];
        float inc = 20f;

        if (Random.value > 0.5f)
        {
            funGauge += inc;
            dialogueText.text = $"{cur.unitName}의 재롱! 적의 재미가 {inc}만큼 올랐다.";
        }
        else
        {
            fatigueGauge += inc;
            dialogueText.text = $"{cur.unitName}의 지루한 설교... 피로도가 {inc}만큼 올랐다.";
        }

        yield return _wait2s;

        if (funGauge >= maxGauge || fatigueGauge >= maxGauge)
        { state = BattleState.WON; EndBattle(); }
        else NextPartyMember();
    }

    IEnumerator PlayerDefend()
    {
        Unit cur = _playerParty[_currentUnitIndex];
        cur.isDefending = true;
        ShowDialogue("battle.defend_action", $"{cur.unitName}은(는) 방어 태세를 취했다.", cur.unitName);
        yield return _wait1_5s;
        NextPartyMember();
    }

    IEnumerator UseItemInBattle(ItemData item)
    {
        Unit cur = _playerParty[_currentUnitIndex];
        float healHP  = item.fantasyEffect.healthChange;
        bool  used    = false;

        if (healHP > 0)
        {
            cur.Heal(Mathf.RoundToInt(healHP));
            dialogueText.text = $"{item.DisplayName} 사용! {cur.unitName}의 HP가 {healHP}만큼 회복됐다.";
            used = true;
        }
        else if (healHP < 0)
        {
            cur.TakeDamage(Mathf.RoundToInt(-healHP));
            dialogueText.text = $"{item.DisplayName}... 이상한 맛이다. {cur.unitName}이(가) {-healHP}의 피해를 받았다.";
            used = true;
        }

        // 멘탈 처리
        float mentalChange = item.fantasyEffect.mentalChange;
        if (mentalChange != 0 && PlayerStats.Instance != null)
        {
            if (mentalChange > 0) PlayerStats.Instance.RecoverMental(mentalChange);
            else                  PlayerStats.Instance.AddTrauma(-mentalChange);
        }

        // 배고픔 처리
        if (item.satiety > 0 && PlayerStats.Instance != null)
            PlayerStats.Instance.EatFood(item.satiety);

        if (!used)
            dialogueText.text = $"{item.DisplayName}을(를) 사용했지만 별다른 효과가 없었다.";

        yield return _wait2s;

        if (IsPartyDead()) { state = BattleState.LOST; EndBattle(); }
        else NextPartyMember();
    }

    IEnumerator EnemyTurn()
    {
        dialogueText.text = $"{_enemyUnit.unitName}의 턴!";
        yield return _wait1s;

        Unit target = GetRandomAlivePartyMember();
        if (target != null)
        {
            int  dmg    = _enemyUnit.damage;
            bool dead   = target.TakeDamage(dmg);
            int  actual = target.isDefending ? Mathf.RoundToInt(dmg * 0.8f) : dmg;
            dialogueText.text =
                $"{_enemyUnit.unitName}가 {target.unitName}를 공격하여 {actual}의 데미지를 주었다!";
            yield return _wait2s;
        }

        if (IsPartyDead()) { state = BattleState.LOST; EndBattle(); }
        else
        {
            state = BattleState.PLAYERTURN;
            _currentUnitIndex = 0;
            ProcessPartyTurn();
        }
    }

    // ════════════════════════════════════════
    //  전투 종료
    // ════════════════════════════════════════
    void EndBattle() => StartCoroutine(EndBattleCoroutine());

    IEnumerator EndBattleCoroutine()
    {
        if (state == BattleState.WON)
        {
            dialogueText.text = "승리했다!";
            GameState.RegisterDefeatedEnemy(EncounterManager.currentEnemyID);
            PlayerStats.Instance?.AddPuppetization(10f);
        }
        else
        {
            ShowDialogue("battle.lose", "패배했다...");
        }

        yield return new WaitForSeconds(3f);

        // 복귀 씬 결정 후 GameState 에 세팅
        string target = string.IsNullOrEmpty(GameState.returnSceneName)
            ? SceneNames.DarkReality
            : GameState.returnSceneName;

        GameState.battleReturn.SetReturning(target, 2.5f);
        SceneManager.LoadScene(target);
    }

    // ════════════════════════════════════════
    //  유틸리티
    // ════════════════════════════════════════
    Unit GetRandomAlivePartyMember()
    {
        int alive = 0;
        foreach (var u in _playerParty) if (u.currentHP > 0) alive++;
        if (alive == 0) return null;

        int idx = Random.Range(0, alive);
        int cur = 0;
        foreach (var u in _playerParty)
        {
            if (u.currentHP > 0)
            {
                if (cur == idx) return u;
                cur++;
            }
        }
        return null;
    }

    bool IsPartyDead()
    {
        foreach (var u in _playerParty)
            if (u.currentHP > 0) return false;
        return true;
    }

    // ── 로컬라이제이션 헬퍼 ──
    string GetText(string key, string fallback, params object[] args)
    {
        if (LocalizationManager.Instance == null) return fallback;
        string result = LocalizationManager.Instance.GetText(key, args);
        return result == key ? fallback : result;
    }

    void ShowDialogue(string key, string fallback, params object[] args)
    {
        if (dialogueText != null)
            dialogueText.text = GetText(key, fallback, args);
    }
}