using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using System.Text;
using TMPro;

public enum BattleState { START, PLAYERTURN, ENEMYTURN, WON, LOST }

// Awake()가 EventSystem.OnEnable()보다 반드시 먼저 실행되어야 중복 경고를 막을 수 있음
[DefaultExecutionOrder(-32000)]
public class BattleSystem : MonoBehaviour
{
    public BattleState State { get; private set; }

    /// <summary>BattleSystem이 씬에 활성화돼 있는지 여부. 전투 중 여부 판단에 사용.</summary>
    public static bool IsActive { get; private set; }

    /// <summary>현재 활성 BattleSystem 인스턴스.</summary>
    public static BattleSystem Instance { get; private set; }

    [Header("유닛")]
    public GameObject companionPrefab;
    public GameObject enemyPrefab;

    [Header("1인칭 플레이어 설정")]
    [Tooltip("전투 대화문에 표시될 플레이어 이름")]
    public string playerUnitName   = "플레이어";
    [Tooltip("플레이어 기본 공격력 (PlayerStats에 공격력 스탯이 없으므로 직접 설정)")]
    public int    playerBaseDamage = 10;
    [Tooltip("플레이어 최대 MP. 스킬 사용에 소모됩니다.")]
    public int    playerMaxMP      = 30;
    [Tooltip("플레이어가 전투에서 사용할 스킬 목록. SkillQuickSlotUI에 표시됩니다.")]
    public List<SkillData> playerDefaultSkills = new List<SkillData>();

    [System.Serializable]
    public class LevelUnlockSkill
    {
        [Tooltip("해금할 스킬.")]
        public SkillData skill;
        [Tooltip("PlayerGrowth 레벨이 이 값 이상이면 전투 시 장착됩니다.")]
        public int unlockLevel = 2;
    }

    [Tooltip("레벨업으로 해금되는 스킬 목록 (성장 보상).")]
    public List<LevelUnlockSkill> levelUnlockSkills = new List<LevelUnlockSkill>();

    [Header("HP 슬라이더 (배틀씬 전용 — 직접 연결하세요)")]
    public Slider playerHPSlider;
    public Slider companionHPSlider;
    public Slider enemyHPSlider;

    [Header("적 등장 연출 패널")]
    [Tooltip("전투 시작 시 표시할 연출 패널")]
    public GameObject enemyAppearPanel;
    [Tooltip("패널 안 텍스트 (예: 야생의 슬라임이(가) 나타났다!)")]
    public TMP_Text   enemyAppearText;
    [Tooltip("적 등장 Animator 트리거 이름. 비어있으면 생략.")]
    public string     enemyAppearTrigger = "Appear";

    [Header("적 HUD (HP바 + 이름)")]
    [Tooltip("적 HP 슬라이더와 이름 텍스트를 묶는 부모 오브젝트 — 연출 후 표시")]
    public GameObject enemyHudGroup;
    [Tooltip("적 HP 슬라이더 옆에 표시할 이름 텍스트")]
    public TMP_Text   enemyNameLabel;

    [Header("게이지 설정")]
    public float empathyGauge          = 0f;
    public float maxGauge              = 100f;
    /// <summary>SetupBattle 에서 적 등장 패널을 표시할지 여부. EncounterManager가 설정합니다.</summary>
    public static bool showEnemyAppearPanel = false;

    /// <summary>EncounterManager가 전투를 시작할 준비가 됐을 때 true로 설정합니다.
    /// Start()에서 SetupBattle() 자동 실행을 제어합니다.</summary>
    public static bool readyToStart = false;

    [Header("UI")]
    public TMP_Text dialogueText;

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
    public TMP_Text   noItemText;

    [Header("아이템 설명 패널 (배틀 전용)")]
    [Tooltip("아이템 선택 시 표시될 설명 패널")]
    public GameObject battleItemDescPanel;
    [Tooltip("설명 패널 아이템 이름 텍스트")]
    public TMP_Text   battleItemDescName;
    [Tooltip("설명 패널 아이템 설명 텍스트")]
    public TMP_Text   battleItemDescText;
    [Tooltip("아이템 사용 버튼 (onClick 에 OnBattleItemUseButton 연결)")]
    public Button     battleItemUseButton;

    // ─ 내부 상태 ─
    private BattleGlitchTransition _glitchTransition;
    private List<Unit>       _playerParty        = new List<Unit>();
    private Unit             _enemyUnit;
    private int              _currentUnitIndex   = 0;
    private struct ItemButtonCache
    {
        public GameObject                  go;
        public Button                      button;
        public TMP_Text                    tmp;
        public Image                       iconImage;
    }

    private List<ItemButtonCache> _itemButtonPool = new List<ItemButtonCache>();
    private ItemData         _selectedBattleItem;

    // timeScale = 0 에서도 동작하도록 Realtime 버전 사용
    private WaitForSecondsRealtime _wait1s;
    private WaitForSecondsRealtime _wait1_5s;
    private WaitForSecondsRealtime _wait2s;
    private WaitForSecondsRealtime _wait3s;

    [Header("적 등장 위치")]
    [Tooltip("적 스프라이트가 나타날 화면 위치. X: 왼쪽(0)~오른쪽(1), Y: 아래(0)~위(1). 예) (0.7, 0.5) = 화면 오른쪽 중앙.")]
    [SerializeField] private Vector2 _enemyViewportPosition = new Vector2(0.7f, 0.5f);

    [Header("글리치 구간 전환")]
    [Tooltip("글리치 구간(30~69)에서 언제든지 단검을 활성화하여 핵앤슬래시로 전환하는 버튼")]
    public Button daggerActivateButton;

    [Header("부가 UI 컴포넌트")]
    [Tooltip("플레이어 HP 바 애니메이션 (SmoothHPBar)")]
    public SmoothHPBar    playerHPBar;
    [Tooltip("동료 UI (BattleCompanionUI)")]
    public BattleCompanionUI companionUI;
    [Tooltip("적 글리치 이펙트 (EnemyGlitchEffect)")]
    public EnemyGlitchEffect enemyGlitch;
    [Tooltip("퀵슬롯 UI (ItemQuickSlotUI)")]
    public ItemQuickSlotUI   quickSlotUI;
    [Tooltip("스킬 퀵슬롯 UI (SkillQuickSlotUI). 미연결 시 자식에서 자동 탐색.")]
    public SkillQuickSlotUI  skillQuickSlotUI;
    [Tooltip("공감 게이지 슬라이더 (미연결 시 무시)")]
    public Slider empathySlider;

    [Header("HP 바 컨트롤러 (이벤트 기반 — 선택)")]
    [Tooltip("연결되면 BattleEvents 구독으로 자동 갱신. null이면 레거시 hpSlider 직접 갱신 경로 사용.")]
    public HPBarController playerHPBarController;
    [Tooltip("연결되면 BattleEvents 구독으로 자동 갱신.")]
    public HPBarController enemyHPBarController;
    [Tooltip("연결되면 BattleEvents 구독으로 자동 갱신. 1인칭 전투에서는 미사용.")]
    public HPBarController companionHPBarController;

    // 공감 게이지 달성으로 이긴 경우 true
    private bool _wonByEmpathy = false;

    // 도망으로 전투를 벗어난 경우 true — 승리 보상(처치 등록·전리품·인형화)을 지급하지 않음
    private bool _escaped = false;

    // ── 숲 전투 규약 (F-2-6) ─────────────────────────────────────────────
    // 이 셋은 튜토리얼 전투 진입부(EncounterManager.ForceStartTurnBased)에서만 켠다.
    // 일반 전투는 기본값 그대로이므로 기존 동작이 바뀌지 않는다.

    [Header("숲 전투 규약 (F-2-6)")]
    [Tooltip("끄면 [도주] 버튼을 숨긴다. 정본 S#17A — 튜토리얼이므로 회피를 열지 않는다")]
    public bool allowEscape = true;
    [Tooltip("켜면 [쓰다듬기] 버튼을 띄우고 누적 정화 판정을 건다")]
    public bool allowSoothe = false;
    [Tooltip("켜면 처치 시 인형화 랜덤 굴림과 전리품 테이블을 건너뛴다. " +
             "인형화는 데모 고정값(±2)이라 호출자가 직접 준다 (F-2-6 ※)")]
    public bool useFixedOutcome = false;

    [Tooltip("정화가 성립하는 [쓰다듬기] 누적 횟수. 1~2회에는 아무 판정도 일어나지 않는다")]
    public int  soothePurifyCount = 3;

    [Tooltip("[도주] 버튼. allowEscape 가 꺼지면 숨긴다")]
    public Button escapeButton;
    [Tooltip("[쓰다듬기] 버튼. allowSoothe 가 켜져야 보인다")]
    public Button sootheButton;
    [Tooltip("[특수] 버튼. 숲 전투는 선택지가 셋이라 [쓰다듬기] 가 이 자리를 대신한다")]
    public Button specialButton;

    /// <summary>[쓰다듬기] 누적 횟수. 전투 단위이며 SetupBattle 에서 0 으로 돌아간다.</summary>
    public int SootheCount { get; private set; }

    /// <summary>이번 전투가 불살(정화)로 끝났는가. 몰살과 보상이 다르다.</summary>
    public bool SparedByPurify { get; private set; }

    /// <summary>플레이어 유닛(1인칭이라 하나뿐). 피격 대상 판별용.</summary>
    public Unit PlayerUnit => _playerParty.Count > 0 ? _playerParty[0] : null;

    // EndBattle 중복 실행 방지
    private bool _isBattleEnding = false;

    // SetupBattle 코루틴 중복 실행 방지
    private Coroutine _setupBattleCoroutine;

    // 플레이어 행동 중 입력 잠금 (연타로 인한 코루틴 중복 방지)
    private bool _isPlayerActionInProgress = false;

    // ════════════════════════════════════════
    //  초기화
    // ════════════════════════════════════════
    void Awake()
    {
        if (Instance != null)
        {
            Debug.LogWarning("[BattleSystem] 중복 인스턴스 감지 — 자동 파괴.");
            Destroy(gameObject.transform.root.gameObject);
            return;
        }
        Instance = this;

        _glitchTransition = GetComponent<BattleGlitchTransition>();
        if (_glitchTransition == null)
            _glitchTransition = gameObject.AddComponent<BattleGlitchTransition>();

        // 코루틴 시작 전 첫 프레임부터 모든 패널을 숨겨 깜박임 방지
        SetPanelsActive(false, false, false);
        if (battleItemDescPanel != null) battleItemDescPanel.SetActive(false);
        if (enemyAppearPanel    != null) enemyAppearPanel.SetActive(false);

        // 적 HUD도 첫 프레임부터 숨겨 흰 슬라이더 배경이 보이지 않도록 방지
        if (enemyHudGroup != null)
            enemyHudGroup.SetActive(false);
        else
        {
            if (enemyHPSlider  != null) enemyHPSlider.gameObject.SetActive(false);
            if (enemyNameLabel != null) enemyNameLabel.gameObject.SetActive(false);
        }

        // 패널 배경 이미지가 없거나 투명하면 기본 배경 설정
        EnsurePanelBackground(mainMenuPanel);
        EnsurePanelBackground(actionMenuPanel);
        EnsurePanelBackground(itemMenuPanel);

        // itemButtonContainer가 ScrollRect의 Content로 연결되지 않은 경우 자동 할당
        if (itemButtonContainer != null)
        {
            var sr = itemButtonContainer.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            if (sr != null && sr.content == null)
                sr.content = itemButtonContainer as RectTransform;
        }
    }

    void Start()
    {
        _wait1s   = new WaitForSecondsRealtime(1f);
        _wait1_5s = new WaitForSecondsRealtime(1.5f);
        _wait2s   = new WaitForSecondsRealtime(2f);
        _wait3s   = new WaitForSecondsRealtime(3f);

        State = BattleState.START;
        if (GameState.pendingModeSelection) return;
        if (!readyToStart) return;
        readyToStart = false;
        _setupBattleCoroutine = StartCoroutine(SetupBattle());
    }

    /// <summary>단검/마시멜로 선택 후 마시멜로를 선택했을 때 EncounterManager에서 호출.</summary>
    public void StartBattleAfterModeSelection()
    {
        if (_setupBattleCoroutine != null) StopCoroutine(_setupBattleCoroutine);
        _setupBattleCoroutine = StartCoroutine(SetupBattle());
    }

    void Update()
    {
        // 아이템 메뉴가 열려 있을 때 일시정지 키 → 메인 메뉴로 복귀
        KeyCode pauseKey = SettingsManager.Instance?.keyPause ?? KeyCode.Escape;
        if (Input.GetKeyDown(pauseKey)
            && State == BattleState.PLAYERTURN
            && itemMenuPanel != null && itemMenuPanel.activeSelf)
        {
            ShowMainMenu();
        }

    }

    IEnumerator SetupBattle()
    {
        // PendingModeUI 파괴가 완료될 때까지 1프레임 대기 (동시 표시 방지)
        yield return null;

        // 모드 컨트롤러에 전환 완료 통보 (race condition 가드 해제)
        if (BattleModeController.Instance != null)
            BattleModeController.Instance.NotifyTurnBasedStarted();

        // 단검 버튼 초기화
        if (daggerActivateButton != null)
        {
            daggerActivateButton.onClick.RemoveAllListeners();
            daggerActivateButton.onClick.AddListener(OnDaggerActivateButton);
            UpdateDaggerButtonVisibility(GaugeManager.Instance?.fantasyRealityGauge ?? 0f);
        }
        if (GaugeManager.Instance != null)
        {
            GaugeManager.Instance.OnGaugeChanged -= UpdateDaggerButtonVisibility;
            GaugeManager.Instance.OnGaugeChanged += UpdateDaggerButtonVisibility;
        }

        // 이전 전투 상태 초기화 (재사용 인스턴스 대비)
        _playerParty.Clear();
        _currentUnitIndex = 0;
        _isBattleEnding   = false;
        _wonByEmpathy     = false;
        _escaped          = false;
        // 쓰다듬기 카운터는 전투 단위다 (F-2-6). 전투가 새로 시작할 때 초기화한다.
        SootheCount       = 0;
        SparedByPurify    = false;
        ApplyForestBattleRules();

        // 플레이어 — 1인칭: 스프라이트 없이 PlayerStats 데이터로 가상 유닛 생성
        Unit playerUnit = CreateVirtualPlayerUnit();
        _playerParty.Add(playerUnit);

        // 스킬 퀵슬롯 배선 — 미연결 시 자식에서 자동 탐색 후 캐스터 주입
        if (skillQuickSlotUI == null)
            skillQuickSlotUI = GetComponentInChildren<SkillQuickSlotUI>(true);
        if (skillQuickSlotUI != null)
        {
            if (skillQuickSlotUI.battleSystem == null)
                skillQuickSlotUI.battleSystem = this;
            skillQuickSlotUI.SetCaster(playerUnit);
        }
        BattleEvents.RaiseUnitMPChanged(playerUnit);

        // 동료는 초상화(BattleCompanionUI) 역할만 — 전투 파티에 참여하지 않음

        // 적 소환 (EncounterManager 우선, 없으면 인스펙터 프리팹)
        GameObject enemyPrefabToUse =
            (EncounterManager.Instance != null && EncounterManager.Instance.enemyPrefabToSpawn != null)
            ? EncounterManager.Instance.enemyPrefabToSpawn
            : enemyPrefab;

        SpawnUnit(enemyPrefabToUse, out _enemyUnit);

        // 신규 이벤트 기반 HP UI 바인딩 (인스펙터에 컨트롤러 연결 시)
        if (enemyHPBarController != null && _enemyUnit != null)
            enemyHPBarController.SetTarget(_enemyUnit);

        // HP 바 초기화 — 컨트롤러가 연결되지 않은 환경의 레거시 경로
        if (playerUnit != null && playerHPBarController == null)
            playerHPBar?.Init(playerUnit.maxHP, playerUnit.currentHP, playerUnit.unitLevel);

        // 적에 글리치 이펙트 활성화 (비활성 자식 포함하여 미리 획득)
        if (_enemyUnit != null && enemyGlitch == null)
            enemyGlitch = _enemyUnit.GetComponentInChildren<EnemyGlitchEffect>(true);

        // ── 적 이미지 숨기기 (연출 전, 글리치 참조 획득 후) ──
        if (_enemyUnit != null) _enemyUnit.gameObject.SetActive(false);

        // 공감 게이지 슬라이더 초기화
        empathyGauge = 0f;
        if (empathySlider != null)
        {
            empathySlider.minValue = 0;
            empathySlider.maxValue = maxGauge;
            empathySlider.value    = 0;
        }

        // 퀵슬롯 초기화
        quickSlotUI?.Refresh();

        string eName = _enemyUnit != null ? _enemyUnit.unitName : "적";

        // ── 적 HUD 숨기기 (연출 전) ──────────────────────────
        if (enemyHudGroup != null)
            enemyHudGroup.SetActive(false);
        else
        {
            if (enemyHPSlider  != null) enemyHPSlider.gameObject.SetActive(false);
            if (enemyNameLabel != null) enemyNameLabel.gameObject.SetActive(false);
        }

        // 공감(fun) 슬라이더는 적 등장 전까지 숨김
        if (empathySlider != null) empathySlider.gameObject.SetActive(false);

        // 이름 텍스트 미리 설정
        if (enemyNameLabel != null) enemyNameLabel.text = eName;

        // ── 등장 패널 표시 ──────────────────────────────────
        if (showEnemyAppearPanel && enemyAppearPanel != null)
        {
            if (enemyAppearText != null)
                enemyAppearText.text = GetText("battle.wild_enemy_appear",
                                               $"{eName}이(가) 나타났다!", eName);
            enemyAppearPanel.SetActive(true);
        }
        else
        {
            ShowDialogue("battle.wild_enemy_appear", $"{eName}이(가) 나타났다!", eName);
        }

        SetPanelsActive(false, false, false);
        yield return _wait2s;

        // ── 등장 패널 숨기기 ─────────────────────────────────
        if (enemyAppearPanel != null) enemyAppearPanel.SetActive(false);

        // ── 적 이미지 + HUD 표시 (HP바 + 이름 + 공감 슬라이더) ──
        ShowEnemyWithAppearance();

        if (enemyHudGroup != null)
            enemyHudGroup.SetActive(true);
        else
        {
            if (enemyHPSlider  != null) enemyHPSlider.gameObject.SetActive(true);
            if (enemyNameLabel != null) enemyNameLabel.gameObject.SetActive(true);
        }

        // 공감(fun) 슬라이더를 적 HUD와 함께 표시
        if (empathySlider != null)
        {
            empathySlider.gameObject.SetActive(true);
            empathySlider.value = empathyGauge;
        }

        State = BattleState.PLAYERTURN;
        _currentUnitIndex = 0;
        ProcessPartyTurn();

    }

    /// <summary>유닛 소환 후 Unit 컴포넌트를 반환합니다. HP UI는 호출자가 HPBarController.SetTarget으로 처리합니다.</summary>
    void SpawnUnit(GameObject prefab, out Unit unit)
    {
        unit = null;
        if (prefab == null) return;

        // _enemyViewportPosition (0~1)을 월드 좌표로 변환하여 스폰.
        // Camera.main이 없으면 원점에 스폰.
        GameObject go = Instantiate(prefab);
        if (Camera.main != null)
        {
            float   depth    = Mathf.Abs(Camera.main.transform.position.z);
            Vector3 worldPos = Camera.main.ViewportToWorldPoint(
                new Vector3(_enemyViewportPosition.x, _enemyViewportPosition.y, depth));
            worldPos.z = 0f;
            go.transform.position = worldPos;
        }
        go.transform.rotation = Quaternion.identity;
        // BattleUI Canvas(scale 0)에 넣으면 월드 스케일 0이 되어 스프라이트 소멸 — 루트에 등록
        go.transform.SetParent(transform.root, worldPositionStays: true);

        // 프리팹 내부 Canvas·Image 컴포넌트 비활성화 (GameObject가 아닌 컴포넌트를 끔)
        // GameObject를 끄면 ShowEnemyWithAppearance()에서 SetActive(true) 시 같이 켜져 흰 네모 재발생
        foreach (Canvas c in go.GetComponentsInChildren<Canvas>(true))
        {
            c.enabled = false;
            foreach (Image img in c.GetComponentsInChildren<Image>(true))
                img.enabled = false;
        }

        // 핵앤슬래시 전용 컴포넌트 비활성화 — 턴제 전투 중 AI 추격·공격 코루틴 방지
        var spawnedAI     = go.GetComponent<EnemyAI>();
        var spawnedHealth = go.GetComponent<EnemyHealth>();
        if (spawnedAI     != null) spawnedAI.enabled     = false;
        if (spawnedHealth != null) spawnedHealth.enabled = false;

        unit = go.GetComponent<Unit>();
        if (unit == null)
        {
            Debug.LogWarning($"[BattleSystem] {go.name}에 Unit 컴포넌트가 없어 자동 추가합니다. 프리팹에 Unit을 붙여주세요.");
            unit           = go.AddComponent<Unit>();
            unit.unitName  = go.name;
            unit.maxHP     = 50;
            unit.currentHP = 50;
            unit.unitLevel = 1;
        }

        // HP UI는 호출자(SetupBattle)에서 HPBarController.SetTarget으로 처리합니다.
    }

    /// <summary>
    /// 1인칭 전투용: 스프라이트 없이 PlayerStats 데이터로만 구성된 가상 플레이어 유닛 생성.
    /// </summary>
    Unit CreateVirtualPlayerUnit()
    {
        var go = new GameObject("Player [Virtual]");
        go.transform.SetParent(transform, false);

        var unit       = go.AddComponent<Unit>();
        unit.unitName  = playerUnitName;
        unit.unitLevel = PlayerGrowth.Level;
        unit.level     = PlayerGrowth.Level;
        // 성장 시스템 스탯 적용 (인스펙터 값은 레벨 1 기준 폴백)
        unit.attack    = PlayerGrowth.Level > 1 ? PlayerGrowth.CurrentAttack : playerBaseDamage;
        unit.maxMP     = PlayerGrowth.Level > 1 ? PlayerGrowth.CurrentMaxMP  : playerMaxMP;
        unit.currentMP = unit.maxMP;

        foreach (SkillData skill in playerDefaultSkills)
            if (skill != null) unit.equippedSkills.Add(skill);

        // 레벨 해금 스킬
        foreach (LevelUnlockSkill unlock in levelUnlockSkills)
            if (unlock != null && unlock.skill != null
                && PlayerGrowth.Level >= unlock.unlockLevel
                && !unit.equippedSkills.Contains(unlock.skill))
                unit.equippedSkills.Add(unlock.skill);

        if (PlayerStats.Instance != null)
        {
            unit.maxHP     = Mathf.RoundToInt(PlayerStats.Instance.maxHealth);
            unit.currentHP = Mathf.RoundToInt(PlayerStats.Instance.currentHealth);
        }
        else
        {
            unit.maxHP     = 100;
            unit.currentHP = 100;
        }

        if (playerHPBarController != null) playerHPBarController.SetTarget(unit);

        return unit;
    }

    // ════════════════════════════════════════
    //  턴 흐름
    // ════════════════════════════════════════
    void ProcessPartyTurn()
    {
        while (_currentUnitIndex < _playerParty.Count)
        {
            Unit cur = _playerParty[_currentUnitIndex];
            if (cur.currentHP > 0)
            {
                cur.ResetState();
                cur.TickCooldowns();
                BattleEvents.RaiseTurnStarted(cur);

                // 기절 상태 — 이번 턴 행동 불가
                if (BuffManager.Instance != null && BuffManager.Instance.IsStunned)
                {
                    StartCoroutine(SkipStunnedTurn(cur));
                    return;
                }

                ShowDialogue("battle.player_turn_prompt",
                             $"{cur.unitName}의 턴: 무엇을 할까?", cur.unitName);
                ShowMainMenu();
                return;
            }
            _currentUnitIndex++;
        }
        State = BattleState.ENEMYTURN;
        BattleEvents.RaiseTurnStarted(_enemyUnit);
        StartCoroutine(EnemyTurn());
    }

    IEnumerator SkipStunnedTurn(Unit cur)
    {
        SetPanelsActive(false, false, false);
        ShowDialogue("", $"{cur.unitName}은(는) 기절해서 움직일 수 없다!");
        yield return _wait1_5s;
        NextPartyMember();
    }

    void NextPartyMember()
    {
        _currentUnitIndex++;
        ProcessPartyTurn();
    }

    // ════════════════════════════════════════
    //  UI 제어
    // ════════════════════════════════════════
    void ShowMainMenu()
    {
        SetPanelsActive(true, false, false);
        SelectFirstButton(mainMenuPanel);
    }

    void ShowActionMenu()
    {
        SetPanelsActive(false, true, false);
        SelectFirstButton(actionMenuPanel);
    }

    void ShowItemMenu()
    {
        SetPanelsActive(false, false, true);
        PopulateItemButtons();
        SelectFirstButton(itemMenuPanel);
    }

    void SetPanelsActive(bool main, bool action, bool item)
    {
        if (mainMenuPanel   != null) mainMenuPanel.SetActive(main);
        if (actionMenuPanel != null) actionMenuPanel.SetActive(action);
        if (itemMenuPanel   != null) itemMenuPanel.SetActive(item);
        // 아이템 패널이 닫힐 때 설명창도 함께 닫기
        if (!item) HideBattleItemDesc();
    }

    void PopulateItemButtons()
    {
        if (itemButtonContainer == null)
        {
            Debug.LogWarning("[BattleSystem] itemButtonContainer가 연결되지 않았습니다. 인스펙터에서 연결해주세요.");
            return;
        }

        var inventory = BattleServices.Inventory;
        if (inventory == null)
        {
            Debug.LogWarning("[BattleSystem] IInventoryService 미등록 — 정적 버튼만 숨김.");
            foreach (Transform child in itemButtonContainer)
                child.gameObject.SetActive(false);
            if (noItemText != null) noItemText.gameObject.SetActive(true);
            return;
        }

        IReadOnlyList<ItemData> items = inventory.Items;
        bool hasItems = items != null && items.Count > 0;

        // 인스펙터에서 정적으로 배치된 버튼을 포함해 컨테이너의 모든 자식을 먼저 숨김
        foreach (Transform child in itemButtonContainer)
            child.gameObject.SetActive(false);

        if (noItemText != null) noItemText.gameObject.SetActive(!hasItems);

        // Destroy된 항목 정리
        _itemButtonPool.RemoveAll(b => b.go == null);

        if (!hasItems) return;

        int poolIndex = 0;
        foreach (ItemData item in items)
        {
            if (item == null) continue;

            ItemButtonCache cache;
            if (poolIndex < _itemButtonPool.Count)
            {
                cache = _itemButtonPool[poolIndex];
                cache.button?.onClick.RemoveAllListeners();
            }
            else
            {
                GameObject btnObj = itemButtonPrefab != null
                    ? Instantiate(itemButtonPrefab, itemButtonContainer)
                    : CreateFallbackButton(item.itemName, itemButtonContainer);
                cache = BuildButtonCache(btnObj);
                _itemButtonPool.Add(cache);
            }

            cache.go.SetActive(true);
            SetButtonLabel(cache, item.DisplayName);
            SetButtonIcon(cache, item.CurrentIcon);

            if (cache.button != null)
            {
                ItemData captured = item;
                // 클릭 시 바로 사용이 아닌 설명창 표시 후 사용 버튼으로 확정
                cache.button.onClick.AddListener(() => OnBattleItemHighlighted(captured));
            }

            poolIndex++;
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
                                    typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        var rt = textGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        var t = textGo.GetComponent<TextMeshProUGUI>();
        t.alignment = TextAlignmentOptions.Center;
        t.color     = Color.black;
        return go;
    }

    ItemButtonCache BuildButtonCache(GameObject btnObj)
    {
        var cache = new ItemButtonCache { go = btnObj };
        cache.button     = btnObj.GetComponent<Button>();
        cache.tmp        = btnObj.GetComponentInChildren<TMP_Text>();

        var images = btnObj.GetComponentsInChildren<Image>(true);
        foreach (var img in images)
        {
            if (img.gameObject != btnObj) { cache.iconImage = img; break; }
        }
        return cache;
    }

    void SetButtonLabel(ItemButtonCache cache, string label)
    {
        if (cache.tmp != null) cache.tmp.text = label;
    }

    /// <summary>버튼 자식 중 루트 배경이 아닌 첫 번째 Image에 아이템 아이콘을 설정합니다.</summary>
    void SetButtonIcon(ItemButtonCache cache, Sprite icon)
    {
        if (cache.iconImage == null) return;
        cache.iconImage.sprite  = icon;
        cache.iconImage.enabled = icon != null;
    }

    /// <summary>아이템 버튼 클릭 시 설명창 표시 (즉시 사용 X).</summary>
    void OnBattleItemHighlighted(ItemData item)
    {
        if (item == null) return;
        _selectedBattleItem = item;

        if (battleItemDescName != null)  battleItemDescName.text  = item.DisplayName;
        if (battleItemDescText != null)  battleItemDescText.text  = BuildBattleItemDescription(item);
        if (battleItemDescPanel != null) battleItemDescPanel.SetActive(true);

        // 사용 버튼에 키보드 포커스
        if (battleItemUseButton != null)
            EventSystem.current?.SetSelectedGameObject(battleItemUseButton.gameObject);
    }

    /// <summary>인스펙터의 사용 버튼 onClick에 연결합니다.</summary>
    public void OnBattleItemUseButton()
    {
        if (_selectedBattleItem == null) return;
        OnItemSelected(_selectedBattleItem);
    }

    void HideBattleItemDesc()
    {
        _selectedBattleItem = null;
        if (battleItemDescPanel != null) battleItemDescPanel.SetActive(false);
    }

    string BuildBattleItemDescription(ItemData item)
    {
        var sb = new StringBuilder();
        var fx = item.fantasyEffect;

        if (fx.healthChange  != 0) sb.AppendLine($"체력 {(fx.healthChange  > 0 ? "+" : "")}{fx.healthChange:0.##}");
        if (fx.mentalChange  != 0) sb.AppendLine($"멘탈 {(fx.mentalChange  > 0 ? "+" : "")}{fx.mentalChange:0.##}");
        if (fx.empathyChange != 0) sb.AppendLine($"공감 {(fx.empathyChange > 0 ? "+" : "")}{fx.empathyChange:0.##}");

        if (!string.IsNullOrEmpty(item.CurrentDescription))
        {
            if (sb.Length > 0) sb.AppendLine();
            sb.Append(item.CurrentDescription);
        }

        return sb.Length > 0 ? sb.ToString().TrimEnd() : "(효과 없음)";
    }

    void OnItemSelected(ItemData item)
    {
        if (State != BattleState.PLAYERTURN || _isPlayerActionInProgress) return;
        _isPlayerActionInProgress = true;
        HideBattleItemDesc();
        SetPanelsActive(false, false, false);
        BattleServices.Inventory?.RemoveItem(item);
        StartCoroutine(UseItemInBattle(item));
    }

    // ════════════════════════════════════════
    //  메인 메뉴 버튼 (인스펙터에서 연결)
    // ════════════════════════════════════════
    public void OnActionMenuButton()
    {
        if (State != BattleState.PLAYERTURN || _isPlayerActionInProgress) return;
        ShowActionMenu();
    }

    public void OnItemMenuButton()
    {
        if (State != BattleState.PLAYERTURN || _isPlayerActionInProgress) return;
        ShowItemMenu();
    }

    public void OnEscapeButton()
    {
        // 버튼을 숨겨도 키보드 내비게이션으로 닿을 수 있어 여기서도 막는다
        if (!allowEscape) return;
        if (State != BattleState.PLAYERTURN || _isPlayerActionInProgress) return;
        _isPlayerActionInProgress = true;
        StartCoroutine(TryEscape());
    }

    // ════════════════════════════════════════
    //  행동 메뉴 버튼 (인스펙터에서 연결)
    // ════════════════════════════════════════
    public void OnAttackButton()
    {
        if (!IsPlayerTurn() || _isPlayerActionInProgress) return;
        _isPlayerActionInProgress = true;
        actionMenuPanel?.SetActive(false);
        BattleEvents.RaisePlayerAction(BattleActionKind.Attack, SootheCount);
        StartCoroutine(PlayerAttack());
    }

    public void OnDefendButton()
    {
        if (!IsPlayerTurn() || _isPlayerActionInProgress) return;
        _isPlayerActionInProgress = true;
        actionMenuPanel?.SetActive(false);
        BattleEvents.RaisePlayerAction(BattleActionKind.Defend, SootheCount);
        StartCoroutine(PlayerDefend());
    }

    /// <summary>
    /// [쓰다듬기] — 데미지도 판정도 없고 턴만 소모한다 (F-2-6).
    /// 정화는 누적 판정이며 <see cref="soothePurifyCount"/> 회째 선택에서 발동한다.
    /// 1~2회에는 아무 판정도 일어나지 않는다.
    /// </summary>
    public void OnSootheButton()
    {
        if (!allowSoothe) return;
        if (!IsPlayerTurn() || _isPlayerActionInProgress) return;
        _isPlayerActionInProgress = true;
        actionMenuPanel?.SetActive(false);
        SootheCount++;
        BattleEvents.RaisePlayerAction(BattleActionKind.Soothe, SootheCount);
        StartCoroutine(PlayerSoothe());
    }

    /// <summary>SkillQuickSlotUI 가 호출하는 스킬 사용 진입점.</summary>
    public void OnSkillButton(SkillData skill)
    {
        if (!IsPlayerTurn() || _isPlayerActionInProgress) return;
        if (skill == null) return;

        Unit caster = GetCurrentPartyMember();
        if (caster == null) return;

        if (!SkillExecutor.CanUse(caster, skill, caster.GetCooldown(skill))) return;

        _isPlayerActionInProgress = true;
        actionMenuPanel?.SetActive(false);
        StartCoroutine(UseSkillInBattle(caster, ResolveSkillTarget(skill, caster), skill));
    }

    Unit ResolveSkillTarget(SkillData skill, Unit caster)
    {
        switch (skill.targetType)
        {
            case SkillTargetType.Self:        return caster;
            case SkillTargetType.SingleAlly:  return caster;          // 1인칭 전투 — 동료 미참여
            case SkillTargetType.AllAllies:   return caster;
            case SkillTargetType.SingleEnemy: return _enemyUnit;
            case SkillTargetType.AllEnemies:  return _enemyUnit;
            default:                          return _enemyUnit;
        }
    }

    IEnumerator UseSkillInBattle(Unit caster, Unit target, SkillData skill)
    {
        try
        {
            // 혼란 상태 — 25% 확률로 행동 실패 (MP 소모 없음)
            if (RollConfusionFail())
            {
                ShowDialogue("", $"{caster.unitName}은(는) 혼란스러워 허공을 휘저었다!");
                yield return _wait2s;
                NextPartyMember();
                yield break;
            }

            DamageResult result = SkillExecutor.ExecuteSingle(caster, target, skill);
            caster.StartCooldown(skill);

            // 공감형 스킬 — 공감 게이지 가산 (기본 교감(PlayerSpecialAction)과 동일 경로)
            if (skill.empathyGain > 0)
            {
                empathyGauge = Mathf.Min(maxGauge, empathyGauge + skill.empathyGain);
                if (empathySlider != null) empathySlider.value = empathyGauge;
            }

            BattleEvents.RaiseSkillUsed(caster, skill, result);
            BattleEvents.RaiseUnitMPChanged(caster);

            if (result.isMiss)
                ShowDialogue("", $"{caster.unitName}의 {skill.displayName}! 빗나갔다...");
            else if (skill.healAmount > 0)
                ShowDialogue("", $"{caster.unitName}의 {skill.displayName}! HP {result.amount} 회복.");
            else if (skill.empathyGain > 0)
                ShowDialogue("", $"{caster.unitName}의 {skill.displayName}! 공감 게이지 +{skill.empathyGain}");
            else if (skill.damageMultiplier <= 0f)
                ShowDialogue("", $"{caster.unitName}의 {skill.displayName}!");
            else if (result.isCrit)
                ShowDialogue("", $"{caster.unitName}의 {skill.displayName}! 크리티컬 {target?.LastDamageResult.amount ?? result.amount}!");
            else
                ShowDialogue("", $"{caster.unitName}의 {skill.displayName}! {target?.LastDamageResult.amount ?? result.amount}의 데미지.");

            yield return _wait2s;

            // 공감 달성 승리 체크
            if (empathyGauge >= maxGauge)
            {
                _wonByEmpathy = true;
                State = BattleState.WON;
                EndBattle();
                yield break;
            }

            if (_enemyUnit == null || _enemyUnit.currentHP <= 0)
            {
                State = BattleState.WON;
                EndBattle();
                yield break;
            }

            NextPartyMember();
        }
        finally
        {
            _isPlayerActionInProgress = false;
        }
    }

    /// <summary>혼란 디버프 활성 시 25% 확률로 행동 실패.</summary>
    static bool RollConfusionFail() =>
        BuffManager.Instance != null && BuffManager.Instance.IsConfused && Random.Range(0, 100) < 25;

    public void OnSpecialButton()
    {
        if (!IsPlayerTurn() || _isPlayerActionInProgress) return;
        _isPlayerActionInProgress = true;
        actionMenuPanel?.SetActive(false);
        StartCoroutine(PlayerSpecialAction());
    }

    bool IsPlayerTurn() =>
        State == BattleState.PLAYERTURN && _currentUnitIndex < _playerParty.Count;

    /// <summary>현재 행동 중인 파티원을 반환. 인덱스 범위 밖이거나 파티가 비어 있으면 null.</summary>
    Unit GetCurrentPartyMember()
    {
        if (_playerParty == null || _currentUnitIndex < 0 || _currentUnitIndex >= _playerParty.Count)
            return null;
        return _playerParty[_currentUnitIndex];
    }

    // ════════════════════════════════════════
    //  전투 코루틴
    // ════════════════════════════════════════
    IEnumerator TryEscape()
    {
        try
        {
            mainMenuPanel?.SetActive(false);
            if (dialogueText != null) dialogueText.text = GetText("battle.escape", "도망") + "...";
            yield return _wait1_5s;

            if (Random.Range(0, 2) == 0)
            {
                ShowDialogue("battle.escape_success", "도망에 성공했다!");
                yield return _wait1s;
                _escaped = true;
                State = BattleState.WON;
                EndBattle();
            }
            else
            {
                ShowDialogue("battle.escape_fail", "도망칠 수 없었다!");
                yield return _wait1s;
                NextPartyMember();
            }
        }
        finally
        {
            _isPlayerActionInProgress = false;
        }
    }

    IEnumerator PlayerAttack()
    {
        try
        {
            if (_enemyUnit == null) { NextPartyMember(); yield break; }

            Unit cur = GetCurrentPartyMember();
            if (cur == null) { NextPartyMember(); yield break; }

            // 혼란 상태 — 25% 확률로 행동 실패
            if (RollConfusionFail())
            {
                ShowDialogue("", $"{cur.unitName}은(는) 혼란스러워 허공을 휘저었다!");
                yield return _wait2s;
                NextPartyMember();
                yield break;
            }

            float atkMul    = BuffManager.Instance != null ? BuffManager.Instance.AttackMultiplier : 1f;
            int   critBonus = BuffManager.Instance != null ? Mathf.RoundToInt(BuffManager.Instance.CritBonus) : 0;
            DamageResult result = DamageCalculator.Calculate(
                cur, _enemyUnit, attackMultiplier: atkMul, critBonus: critBonus);
            bool dead = _enemyUnit.TakeDamage(result);
            enemyGlitch?.TriggerGlitch();

            if (result.isMiss)
                ShowDialogue("", $"{cur.unitName}의 공격! 빗나갔다...");
            else if (result.isCrit)
                ShowDialogue("", $"{cur.unitName}의 공격! 크리티컬! {_enemyUnit.LastDamageResult.amount}의 데미지를 입혔다.");
            else
                ShowDialogue("", $"{cur.unitName}의 공격! {_enemyUnit.LastDamageResult.amount}의 데미지를 입혔다.");

            yield return _wait2s;

            if (dead) { State = BattleState.WON; EndBattle(); }
            else NextPartyMember();
        }
        finally
        {
            _isPlayerActionInProgress = false;
        }
    }

    IEnumerator PlayerSpecialAction()
    {
        try
        {
            Unit  cur = GetCurrentPartyMember();
            if (cur == null) { NextPartyMember(); yield break; }
            float inc = 20f;

            empathyGauge = Mathf.Min(maxGauge, empathyGauge + inc);
            if (empathySlider != null) empathySlider.value = empathyGauge;

            ShowDialogue("", $"{cur.unitName}이(가) 교감을 시도했다! 공감 게이지 +{inc}");
            yield return _wait2s;

            if (empathyGauge >= maxGauge)
            {
                _wonByEmpathy = true;
                State = BattleState.WON;
                EndBattle();
            }
            else NextPartyMember();
        }
        finally
        {
            _isPlayerActionInProgress = false;
        }
    }

    IEnumerator PlayerDefend()
    {
        try
        {
            Unit cur = GetCurrentPartyMember();
            if (cur == null) { NextPartyMember(); yield break; }
            cur.isDefending = true;
            ShowDialogue("battle.defend_action", $"{cur.unitName}은(는) 방어 태세를 취했다.", cur.unitName);
            yield return _wait1_5s;
            NextPartyMember();
        }
        finally
        {
            _isPlayerActionInProgress = false;
        }
    }

    /// <summary>
    /// [쓰다듬기] 실행. 적을 공격하지 않고 턴만 넘긴다.
    /// 누적이 <see cref="soothePurifyCount"/> 에 닿으면 정화가 성립해 전투가 끝난다.
    /// 대사(Pet2 / Pet3)는 BattleTutorialDirector 가 행동 이벤트를 받아 재생하며,
    /// 정본상 3회차 대사를 출력한 "직후" 정화가 발동하므로 대사가 끝날 때까지 기다린다.
    /// </summary>
    IEnumerator PlayerSoothe()
    {
        try
        {
            Unit cur = GetCurrentPartyMember();
            if (cur == null) { NextPartyMember(); yield break; }

            ShowDialogue("battle.soothe_action", $"{cur.unitName}은(는) 손을 뻗어 쓰다듬었다.", cur.unitName);
            yield return _wait1_5s;

            // 대사 재생 중에는 턴을 넘기지 않는다
            while (YarnDialogue.IsRunning)
                yield return null;

            if (SootheCount >= soothePurifyCount)
            {
                SparedByPurify = true;
                _wonByEmpathy  = true;   // 살해가 아님 — 기존 승리 처리와 같은 취급
                State = BattleState.WON;
                EndBattle();
                yield break;
            }

            NextPartyMember();
        }
        finally
        {
            _isPlayerActionInProgress = false;
        }
    }

    IEnumerator UseItemInBattle(ItemData item)
    {
        try
        {
            Unit cur = GetCurrentPartyMember();
            if (cur == null) { NextPartyMember(); yield break; }

            // BattleEvents.OnItemUsed 구독자(ItemUseTracker, ItemEffectHandler, BuffManager)가 자동 처리
            // (직접 매니저 호출 3개를 이벤트 발행 1개로 대체)
            BattleEvents.RaiseItemUsed(item, cur);

            float healHP = item.fantasyEffect.healthChange;
            bool  used   = false;

            // 각설탕은 HP 를 전량 회복한다 (F-2-6). 회복량을 에셋에 큰 수로 박아두면
            // 최대 HP 가 성장으로 바뀔 때 어긋나므로 효과 코드로 처리한다.
            if (item.fantasyEffect.specialEffectCode == SpecialEffectType.FullHeal)
            {
                int missing = Mathf.Max(0, cur.maxHP - cur.currentHP);
                if (missing > 0) cur.Heal(missing);
                ShowDialogue("", $"{item.DisplayName} 사용! {cur.unitName}의 상처가 전부 아물었다.");
                used = true;
            }
            else if (healHP > 0)
            {
                cur.Heal(Mathf.RoundToInt(healHP));
                ShowDialogue("", $"{item.DisplayName} 사용! {cur.unitName}의 HP가 {healHP}만큼 회복됐다.");
                used = true;
            }
            else if (healHP < 0)
            {
                cur.TakeDamage(Mathf.RoundToInt(-healHP));
                ShowDialogue("", $"{item.DisplayName}... 이상한 맛이다. {cur.unitName}이(가) {-healHP}의 피해를 받았다.");
                used = true;
            }

            // 멘탈 처리
            float mentalChange = item.fantasyEffect.mentalChange;
            if (mentalChange != 0 && PlayerStats.Instance != null)
            {
                if (mentalChange > 0) PlayerStats.Instance.RecoverMental(mentalChange);
                else                  PlayerStats.Instance.AddTrauma(-mentalChange);
                used = true;
            }

            // 인형화 처리
            float puppetChange = item.fantasyEffect.puppetizationChange;
            if (puppetChange != 0 && PlayerStats.Instance != null)
            {
                PlayerStats.Instance.AddPuppetization(puppetChange);
                used = true;
            }

            // 공감 게이지 처리
            float empathyChange = item.fantasyEffect.empathyChange;
            if (empathyChange != 0)
            {
                empathyGauge = Mathf.Clamp(empathyGauge + empathyChange, 0f, maxGauge);
                if (empathySlider != null) empathySlider.value = empathyGauge;
                used = true;
            }

            if (!used)
                ShowDialogue("", $"{item.DisplayName}을(를) 사용했지만 별다른 효과가 없었다.");

            yield return _wait2s;

            // 공감 달성 승리 체크
            if (empathyGauge >= maxGauge)
            {
                _wonByEmpathy = true;
                State = BattleState.WON;
                EndBattle();
                yield break;
            }

            if (IsPartyDead()) { State = BattleState.LOST; EndBattle(); }
            else NextPartyMember();
        }
        finally
        {
            _isPlayerActionInProgress = false;
        }
    }

    IEnumerator EnemyTurn()
    {
        if (_enemyUnit == null)
        {
            State = BattleState.WON;
            EndBattle();
            yield break;
        }

        ShowDialogue("", $"{_enemyUnit.unitName}의 턴!");
        yield return _wait1s;

        // 라운드 틱 — 턴제 전투 중 Time.timeScale=0 이라 BuffManager.Update()가 정지하므로
        // 적 턴마다(=1라운드) 지속시간 차감 + DoT/HoT를 여기서 적용
        BuffManager.Instance?.TickTurn();

        Unit target = GetRandomAlivePartyMember();
        if (target != null)
        {
            float defMul = BuffManager.Instance != null ? BuffManager.Instance.DefenseMultiplier : 1f;
            DamageResult result = DamageCalculator.Calculate(_enemyUnit, target, defenseMultiplier: defMul);

            // 면역 / 취약 / 보호막 (플레이어 버프) 반영
            if (!result.isMiss && BuffManager.Instance != null)
                result = DamageResult.Hit(
                    Mathf.RoundToInt(BuffManager.Instance.ModifyIncomingDamage(result.amount)),
                    result.isCrit);

            bool dead = target.TakeDamage(result);

            // HP 바 부드럽게 감소 + PlayerStats 실시간 동기화 (PlayerStatusUI 반영)
            // playerHPBarController가 연결돼 있으면 BattleEvents.OnUnitDamaged로 자동 갱신되므로 직접 호출 생략
            if (_playerParty.Count > 0 && target == _playerParty[0])
            {
                if (playerHPBarController == null)
                    playerHPBar?.SetHP(target.currentHP, target.unitLevel);
                if (PlayerStats.Instance != null)
                    PlayerStats.Instance.currentHealth = target.currentHP;
            }

            if (result.isMiss)
                ShowDialogue("", $"{_enemyUnit.unitName}의 공격! {target.unitName}이(가) 피했다!");
            else if (result.isCrit)
                ShowDialogue("", $"{_enemyUnit.unitName}의 크리티컬! {target.unitName}에게 {target.LastDamageResult.amount}의 데미지!");
            else
                ShowDialogue("", $"{_enemyUnit.unitName}가 {target.unitName}를 공격하여 {target.LastDamageResult.amount}의 데미지를 주었다!");

            yield return _wait2s;
        }

        if (IsPartyDead())
        {
            // 동료 죽음 이벤트 확률 발동
            companionUI?.OnPlayerDied();
            State = BattleState.LOST;
            EndBattle();
        }
        else
        {
            State = BattleState.PLAYERTURN;
            _currentUnitIndex = 0;
            ProcessPartyTurn();
        }
    }

    // ════════════════════════════════════════
    //  전투 종료
    // ════════════════════════════════════════
    void EndBattle()
    {
        if (_isBattleEnding) return;
        _isBattleEnding = true;
        BattleEvents.RaiseBattleFinished(ResolveOutcome());
        BattleEvents.RaiseBattleEnded();
        StartCoroutine(EndBattleCoroutine());
    }

    /// <summary>
    /// 선택지 구성을 규약에 맞춘다 (F-2-6).
    /// 숲 튜토리얼은 [방어] [쓰다듬기] [공격] 셋이고 회피를 열지 않는다 (정본 S#17A ※).
    /// 일반 전투는 두 플래그가 기본값이라 아무것도 바뀌지 않는다.
    /// </summary>
    void ApplyForestBattleRules()
    {
        // BattleUI 는 프리팹 Instantiate 로 생기므로 진입부가 정적 플래그로 넘긴다.
        if (EncounterManager.pendingForestRules)
        {
            allowEscape     = false;   // 정본 S#17A ※ 튜토리얼이므로 회피를 열지 않는다
            allowSoothe     = true;
            useFixedOutcome = true;
            EncounterManager.pendingForestRules = false;
        }

        if (escapeButton != null) escapeButton.gameObject.SetActive(allowEscape);

        // 선택지는 셋이다. [쓰다듬기] 를 열면 [특수] 가 그 자리를 비켜준다.
        if (specialButton != null) specialButton.gameObject.SetActive(!allowSoothe);
        if (sootheButton != null)
        {
            sootheButton.gameObject.SetActive(allowSoothe);
            sootheButton.onClick.RemoveListener(OnSootheButton);
            sootheButton.onClick.AddListener(OnSootheButton);
        }
    }

    /// <summary>전투가 어떻게 끝났는지 판정합니다. 보상은 이 값으로 갈립니다 (F-2-6).</summary>
    BattleOutcome ResolveOutcome()
    {
        if (State != BattleState.WON) return BattleOutcome.Lost;
        if (_escaped)                 return BattleOutcome.Escaped;
        if (SparedByPurify)           return BattleOutcome.Spared;
        return BattleOutcome.Killed;
    }

    /// <summary>레벨업 시 PlayerStats 최대 HP를 성장 곡선에 맞추고 증가분만큼 회복합니다.</summary>
    public static void ApplyLevelUpToPlayerStats(int levelUps)
    {
        if (levelUps <= 0 || PlayerStats.Instance == null) return;
        float prevMax = PlayerStats.Instance.maxHealth;
        PlayerStats.Instance.maxHealth = PlayerGrowth.CurrentMaxHP;
        PlayerStats.Instance.RecoverHealth(Mathf.Max(0f, PlayerGrowth.CurrentMaxHP - prevMax));
        PlayerStats.Instance.UpdateUI(true);
    }

    IEnumerator EndBattleCoroutine()
    {
        if (State == BattleState.WON && _escaped)
        {
            // 도망: 적을 처치한 것이 아니므로 처치 등록·전리품·인형화 없음
            ShowDialogue("battle.escape_success", "도망에 성공했다!");
        }
        else if (State == BattleState.WON)
        {
            GameState.RegisterDefeatedEnemy(EncounterManager.currentEnemyID);

            // 숲 전투는 인형화가 데모 고정값(±2)이라 여기서 굴리지 않는다 (F-2-6 ※).
            // C-3-2 의 범위값을 데모에서 굴리지 말라는 조항이며, 값은 호출자가 준다.
            if (!useFixedOutcome)
            {
                // 공감 승리는 살해가 아니므로 인형화 페널티 절반만 적용
                BattleServices.PlayerStats?.AddPuppetizationOnKill(_wonByEmpathy ? 0.5f : 1f);
            }

            // 경험치 — 공감 승리는 평화 루트 장려로 +20% 보너스
            int xp = EncounterManager.Instance?.enemyDatabase
                         ?.GetXpReward(EncounterManager.currentEnemyTypeID) ?? 0;
            if (_wonByEmpathy) xp = Mathf.RoundToInt(xp * 1.2f);
            int levelUps = PlayerGrowth.AddExp(xp);
            ApplyLevelUpToPlayerStats(levelUps);

            string winMsg = SparedByPurify ? "늑대가 물러났다."
                          : _wonByEmpathy  ? "적이 스스로 물러났다!"
                                           : "적을 쓰러뜨렸다!";
            if (xp > 0)       winMsg += $" 경험치 +{xp}";
            if (levelUps > 0) winMsg += $"\n레벨이 올랐다! (Lv.{PlayerGrowth.Level})";
            ShowDialogue("", winMsg);

            // 전리품 — 씬 오브젝트 이름이 아닌 EnemyDatabase 의 타입 ID 로 조회
            // 숲 전투는 몰살/불살에 따라 떨어지는 것이 정본에 못박혀 있어 테이블을 굴리지 않는다.
            var loot = useFixedOutcome
                       ? null
                       : EncounterManager.Instance?.enemyDatabase
                             ?.GetLootTable(EncounterManager.currentEnemyTypeID);
            if (loot != null)
            {
                var drops = loot.RollDrops();
                if (drops.Count > 0) InventoryManager.Instance?.AddItems(drops);
            }
        }
        else
        {
            ShowDialogue("battle.lose", "패배했다...");
        }

        yield return _wait3s;

        // 아이템 사용 횟수 초기화 (다음 비전투 구간에서 각 아이템 재사용 가능)
        ItemUseTracker.Instance?.ResetAll();

        // 재조우 방지 쿨타임
        GameState.battleReturn.SetReturning(GameState.battleReturn.returnSceneName, 2.5f);

        // 적 트리거 리셋
        EncounterManager.Instance?.OnBattleEnded();

        // 가상 플레이어 유닛 HP → PlayerStats 동기화
        if (_playerParty.Count > 0 && PlayerStats.Instance != null)
            PlayerStats.Instance.currentHealth = _playerParty[0].currentHP;

        // 플레이어 입력 잠금 해제 + timeScale 복구
        var pil = PlayerInputLock.Instance;
        if (pil != null && pil.IsLocked) pil.Unlock();
        Time.timeScale = 1f;

        // 패배 → 게임오버 UI 표시 후 종료
        if (State == BattleState.LOST)
        {
            GameOverUI.Instance?.Show();
            Destroy(gameObject.transform.root.gameObject);
            yield break;
        }

        // 현실씬(DarkReality) 위 오버레이로 실행된 턴제 → 환상 맵으로 이동
        string origin = GameState.battleReturn.returnSceneName;
        if (SceneNames.IsRealityScene(origin))
        {
            Destroy(gameObject.transform.root.gameObject);
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                SceneNames.GetFantasyScene(origin));
            yield break;
        }

        Destroy(gameObject.transform.root.gameObject);
    }

    // ════════════════════════════════════════
    //  모드 전환 (판타지 턴제 → 핵앤슬래시) — 이벤트 구독 방식
    // ════════════════════════════════════════

    void OnEnable()
    {
        if (Instance == null) Instance = this;
        IsActive = true;
    }

    void OnDisable()
    {
        if (Instance != this) return;
        Instance = null;
        IsActive = false;
        if (GaugeManager.Instance != null)
            GaugeManager.Instance.OnGaugeChanged -= UpdateDaggerButtonVisibility;
    }

    /// <summary>글리치 구간 단검 활성화 버튼 클릭 시 호출됩니다 (언제든지 전환 가능).</summary>
    public void OnDaggerActivateButton()
    {
        if (_isBattleEnding) return;
        ForceSwitchToHackSlash();
    }

    void UpdateDaggerButtonVisibility(float gauge)
    {
        if (daggerActivateButton == null) return;
        bool inGlitch = gauge > GaugeBoundaryMonitor.FantasyBoundary
                     && gauge < GaugeBoundaryMonitor.RealityBoundary;
        daggerActivateButton.gameObject.SetActive(inGlitch);
    }

    /// <summary>GaugeBoundaryMonitor가 Glitch→Reality 전환 시 호출합니다.</summary>
    public void ForceSwitchToHackSlash()
    {
        if (_isBattleEnding) return;

        var ctrl = BattleModeController.Instance;
        if (ctrl != null && ctrl.HasSwitchedMode)
        {
            Debug.LogWarning("[BattleSystem] 이미 모드 전환이 발생했습니다. 턴제→핵앤슬래시 전환이 차단됩니다.");
            return;
        }

        if (_glitchTransition == null)
        {
            Debug.LogError("[BattleSystem] BattleGlitchTransition 컴포넌트가 없습니다. 이 게임오브젝트에 Add Component해주세요.");
            return;
        }

        _isBattleEnding = true;
        ctrl?.SetSwitched();

        if (_playerParty.Count > 0 && PlayerStats.Instance != null)
            PlayerStats.Instance.currentHealth = _playerParty[0].currentHP;

        GameObject[] panels = { mainMenuPanel, actionMenuPanel, itemMenuPanel };
        _glitchTransition.StartGlitchSwitch(
            "현실이 밀려온다...",
            panels, dialogueText);
    }

    // ════════════════════════════════════════
    //  적 등장
    // ════════════════════════════════════════

    /// <summary>
    /// 적 프리팹을 활성화하고 등장 연출을 실행합니다.
    /// 나중에 등장 애니메이션을 추가하려면 이 메서드 안에 작성하세요.
    ///
    /// 추가 예시:
    ///   Animator anim = _enemyUnit.GetComponent&lt;Animator&gt;();
    ///   if (anim != null) anim.SetTrigger("Appear");
    /// </summary>
    void ShowEnemyWithAppearance()
    {
        if (_enemyUnit == null) return;

        // 적 WorldSpace Canvas·Image 비활성화 — 흰 네모(HP바 배경) 방지
        foreach (Canvas c in _enemyUnit.GetComponentsInChildren<Canvas>(true))
            c.enabled = false;
        foreach (Image img in _enemyUnit.GetComponentsInChildren<Image>(true))
            img.enabled = false;

        _enemyUnit.gameObject.SetActive(true);

        if (!string.IsNullOrEmpty(enemyAppearTrigger))
        {
            Animator anim = _enemyUnit.GetComponent<Animator>();
            if (anim != null) anim.SetTrigger(enemyAppearTrigger);
        }
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

    // ── UI 헬퍼 ──

    /// <summary>패널의 첫 번째 버튼에 EventSystem 포커스를 설정합니다 (키보드 내비게이션용).</summary>
    void SelectFirstButton(GameObject panel)
    {
        if (panel == null) return;
        var btn = panel.GetComponentInChildren<Button>(false);
        if (btn != null)
            EventSystem.current?.SetSelectedGameObject(btn.gameObject);
    }

    /// <summary>패널에 Image 컴포넌트가 없거나 투명하면 기본 반투명 배경을 설정합니다.</summary>
    void EnsurePanelBackground(GameObject panel)
    {
        if (panel == null) return;
        var img = panel.GetComponent<Image>();
        if (img == null)
        {
            img = panel.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.75f);
        }
        else if (img.color.a < 0.05f)
        {
            Color c = img.color;
            c.a = 0.75f;
            img.color = c;
        }
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