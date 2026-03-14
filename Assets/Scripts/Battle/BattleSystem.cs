using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public enum BattleState { START, PLAYERTURN, ENEMYTURN, WON, LOST }

public class BattleSystem : MonoBehaviour
{
    public BattleState state;

    [Header("유닛 및 위치 설정")]
    public GameObject playerPrefab;
    public GameObject companionPrefab; // 동료 프리팹
    public GameObject enemyPrefab;

    public Transform playerBattleStation;
    public Transform companionBattleStation; // 동료 위치
    public Transform enemyBattleStation;

    List<Unit> playerParty = new List<Unit>(); // 아군 파티 리스트
    Unit enemyUnit;

    int currentUnitIndex = 0; // 현재 턴인 아군 유닛의 인덱스

    [Header("■ 탄막/환상 모드 설정")]
    [Tooltip("현재 재미 게이지 (특수 행동으로 증가)")]
    public float funGauge = 0f;
    [Tooltip("현재 피로도 게이지 (지루한 행동으로 증가)")]
    public float fatigueGauge = 0f;
    [Tooltip("게이지 최대치 (이 값에 도달하면 승리)")]
    public float maxGauge = 100f;
    
    [Tooltip("플레이어 공격 데미지 배율 (0.1 = 10% 데미지). 환상 모드에선 살상이 어렵습니다.")]
    public float playerDamageMultiplier = 0.1f; 

    [Tooltip("적의 탄막 패턴이 지속되는 시간 (초)")]
    public float danmakuDuration = 5.0f;

    [Header("UI 연결")]
    public Text dialogueText;
    
    [Header("메뉴 패널")]
    public GameObject mainMenuPanel;   // 행동/아이템/도망 선택 패널
    public GameObject actionMenuPanel; // 공격/방어/특수 선택 패널 (기존 actionPanel 대체)
    public GameObject itemMenuPanel;   // 아이템 목록 패널 (지금은 힐 버튼만)

    // ■ 최적화: WaitForSeconds 캐싱 (메모리 할당 최소화)
    WaitForSeconds wait1sec;
    WaitForSeconds wait1_5sec;
    WaitForSeconds wait2sec;
    WaitForSeconds waitDanmaku; // 탄막 시간 대기용

    void Start()
    {
        // 캐싱해두고 계속 재사용
        wait1sec = new WaitForSeconds(1f);
        wait1_5sec = new WaitForSeconds(1.5f);
        wait2sec = new WaitForSeconds(2f);
        waitDanmaku = new WaitForSeconds(danmakuDuration);

        state = BattleState.START;
        StartCoroutine(SetupBattle());
    }

    // 전투 시작 전/후에 기존 유닛 정리
    void ClearBattle()
    {
        // 아군 파티 삭제
        foreach (Unit unit in playerParty)
        {
            if (unit != null) Destroy(unit.gameObject);
        }
        playerParty.Clear();

        // 적군 삭제
        if (enemyUnit != null)
        {
            Destroy(enemyUnit.gameObject);
        }
        
        funGauge = 0f;
        fatigueGauge = 0f;
    }

    IEnumerator SetupBattle()
    {
        // 1. 소환 (부모 설정 및 위치 초기화)
        if (playerPrefab != null)
        {
            GameObject playerGO = Instantiate(playerPrefab, playerBattleStation);
            playerGO.transform.localPosition = Vector3.zero;
            playerGO.transform.localRotation = Quaternion.identity;
            Unit playerUnit = playerGO.GetComponent<Unit>();
            if (playerUnit != null) playerParty.Add(playerUnit);
        }

        // 동료 소환 및 위치 초기화 (선택 사항)
        if (companionPrefab != null)
        {
            GameObject companionGO = Instantiate(companionPrefab, companionBattleStation);
            companionGO.transform.localPosition = Vector3.zero;
            companionGO.transform.localRotation = Quaternion.identity;
            Unit companionUnit = companionGO.GetComponent<Unit>();
            if (companionUnit != null) playerParty.Add(companionUnit);
        }

        // 적 소환 및 위치 초기화 (EncounterManager로부터 적 데이터 가져오기)
        GameObject enemyToSpawn = EncounterManager.Instance != null && EncounterManager.Instance.enemyPrefabToSpawn != null 
            ? EncounterManager.Instance.enemyPrefabToSpawn : enemyPrefab;

        if (enemyToSpawn != null)
        {
            GameObject enemyGO = Instantiate(enemyToSpawn, enemyBattleStation);
            enemyGO.transform.localPosition = Vector3.zero;
            enemyGO.transform.localRotation = Quaternion.identity;
            enemyUnit = enemyGO.GetComponent<Unit>();
        }

        // 다음 전투를 위해 초기화
        BattleData.nextEnemyPrefab = null;

        // 2. 초기화
        string eName = (enemyUnit != null) ? enemyUnit.unitName : "적";
        
        if (dialogueText != null)
        {
            if (LocalizationManager.Instance != null)
            {
                dialogueText.text = LocalizationManager.Instance.GetText("battle.wild_enemy_appear", eName);
            }
            else
            {
                dialogueText.text = $"야생의 {eName}이(가) 나타났다!";
            }
        }
        else
        {
            Debug.LogError("dialogueText가 인스펙터에 할당되지 않았습니다!");
        }
        
        // 메뉴들 숨기기
        mainMenuPanel.SetActive(false);
        actionMenuPanel.SetActive(false);
        if(itemMenuPanel != null) itemMenuPanel.SetActive(false);

        yield return wait2sec; // 최적화된 대기

        // 3. 전투 시작 (플레이어 파티 턴)
        state = BattleState.PLAYERTURN;
        currentUnitIndex = 0;
        ProcessPartyTurn();
    }

    // 파티원들의 턴을 순서대로 처리
    void ProcessPartyTurn()
    {
        if (currentUnitIndex < playerParty.Count)
        {
            Unit currentUnit = playerParty[currentUnitIndex];
            if (currentUnit.currentHP > 0)
            {
                currentUnit.ResetState(); // 방어 상태 등 리셋
                dialogueText.text = LocalizationManager.Instance.GetText("battle.player_turn_prompt", currentUnit.unitName);
                ShowMainMenu();
            }
            else
            {
                // 기절한 유닛은 패스
                currentUnitIndex++;
                ProcessPartyTurn();
            }
        }
        else
        {
            // 모든 아군 행동 종료 -> 적 턴으로 (탄막 패턴 시작)
            state = BattleState.ENEMYTURN;
            StartCoroutine(EnemyTurn());
        }
    }

    #region UI Control
    void ShowMainMenu()
    {
        mainMenuPanel.SetActive(true);
        actionMenuPanel.SetActive(false);
        if(itemMenuPanel != null) itemMenuPanel.SetActive(false);
    }

    void ShowActionMenu()
    {
        mainMenuPanel.SetActive(false);
        actionMenuPanel.SetActive(true);
    }

    void ShowItemMenu()
    {
        mainMenuPanel.SetActive(false);
        if(itemMenuPanel != null) itemMenuPanel.SetActive(true);
    }
    #endregion

    #region Main Menu Buttons
    // [행동] 버튼
    public void OnActionMenuButton()
    {
        if (state != BattleState.PLAYERTURN) return;
        ShowActionMenu();
    }

    // [아이템] 버튼
    public void OnItemMenuButton()
    {
        if (state != BattleState.PLAYERTURN) return;
        // 지금은 바로 아이템 메뉴(또는 힐) 보여주기
        ShowItemMenu(); 
    }

    // [도망] 버튼
    public void OnEscapeButton()
    {
        if (state != BattleState.PLAYERTURN) return;
        StartCoroutine(TryEscape());
    }
    #endregion

    #region Action Menu Buttons
    // [공격] 버튼
    public void OnAttackButton()
    {
        if (state != BattleState.PLAYERTURN || currentUnitIndex >= playerParty.Count) return;
        actionMenuPanel.SetActive(false);
        StartCoroutine(PlayerAttack());
    }

    // [방어] 버튼
    public void OnDefendButton()
    {
        if (state != BattleState.PLAYERTURN || currentUnitIndex >= playerParty.Count) return;
        actionMenuPanel.SetActive(false);
        StartCoroutine(PlayerDefend());
    }

    // [특수] - 재미/피로도 공략 (비살상)
    public void OnSpecialButton()
    {
        if (state != BattleState.PLAYERTURN || currentUnitIndex >= playerParty.Count) return;
        actionMenuPanel.SetActive(false);
        StartCoroutine(PlayerSpecialAction());
    }
    #endregion

    #region Item Menu Buttons
    // 기존 OnHealButton 재활용 (아이템 사용 예시)
    public void OnUseItemButton()
    {
        if (state != BattleState.PLAYERTURN || currentUnitIndex >= playerParty.Count) return;
        mainMenuPanel.SetActive(false); // 잘못 연결되었을 때를 대비해 메인메뉴도 끕니다.
        if(itemMenuPanel != null) itemMenuPanel.SetActive(false);
        StartCoroutine(PlayerHeal());
    }
    #endregion


    IEnumerator TryEscape()
    {
        mainMenuPanel.SetActive(false);
        dialogueText.text = LocalizationManager.Instance.GetText("battle.escape") + "...";
        
        yield return wait1_5sec;

        // 50% 확률 예시
        if (Random.Range(0, 2) == 0)
        {
            dialogueText.text = LocalizationManager.Instance.GetText("battle.escape_success");
            yield return wait1sec;
            state = BattleState.WON; // 도망 성공도 일단 승리 처리(전투 종료)
            EndBattle();
        }
        else
        {
            dialogueText.text = LocalizationManager.Instance.GetText("battle.escape_fail");
            yield return wait1sec;
            NextPartyMember();
        }
    }

    IEnumerator PlayerAttack()
    {
        Unit currentUnit = playerParty[currentUnitIndex];
        
        // ■ 살상 공격 로직 (데미지 낮게)
        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(currentUnit.damage * playerDamageMultiplier));
        bool isDead = enemyUnit.TakeDamage(finalDamage);

        dialogueText.text = $"{currentUnit.unitName}의 공격! {finalDamage}의 데미지를 입혔다. (살상 모드)";

        yield return wait2sec;

        if (isDead)
        {
            state = BattleState.WON;
            EndBattle();
        }
        else
        {
            NextPartyMember();
        }
    }

    // ■ 비살상 특수 행동 (재미/피로도 증가)
    IEnumerator PlayerSpecialAction()
    {
        Unit currentUnit = playerParty[currentUnitIndex];
        
        // 예시: 랜덤하게 재미나 피로도를 올림
        float increaseAmount = 20f;
        
        // 재미를 올릴지 피로도를 올릴지는 스킬에 따라 다르겠지만 여기선 랜덤
        if (Random.value > 0.5f)
        {
            funGauge += increaseAmount;
            dialogueText.text = $"{currentUnit.unitName}의 재롱! 적의 재미가 {increaseAmount}만큼 올랐다.";
        }
        else
        {
            fatigueGauge += increaseAmount;
            dialogueText.text = $"{currentUnit.unitName}의 지루한 설교... 적의 피로도가 {increaseAmount}만큼 올랐다.";
        }

        yield return wait2sec;

        // 게이지 꽉 찼는지 확인
        if (funGauge >= maxGauge || fatigueGauge >= maxGauge)
        {
            state = BattleState.WON;
            EndBattle();
        }
        else
        {
            NextPartyMember();
        }
    }

    IEnumerator PlayerDefend()
    {
        Unit currentUnit = playerParty[currentUnitIndex];
        currentUnit.isDefending = true;

        dialogueText.text = LocalizationManager.Instance.GetText("battle.defend_action", currentUnit.unitName);
        yield return wait1_5sec;

        NextPartyMember();
    }

    IEnumerator PlayerHeal()
    {
        Unit currentUnit = playerParty[currentUnitIndex];
        currentUnit.Heal(5); // 회복량 예시
        dialogueText.text = LocalizationManager.Instance.GetText("battle.heal", currentUnit.unitName);

        yield return wait2sec;

        NextPartyMember();
    }

    void NextPartyMember()
    {
        currentUnitIndex++;
        ProcessPartyTurn();
    }

    IEnumerator EnemyTurn()
    {
        // ★ 적의 공격 턴 (턴제 전투)
        dialogueText.text = $"{enemyUnit.unitName}의 턴!";
        yield return wait1sec;

        // 살아있는 아군 중 무작위로 공격 대상 선택 (최적화: List 재할당 방지)
        Unit target = GetRandomAlivePartyMember();

        if (target != null)
        {
            // 데미지 계산 (Unit 스크립트 내부에서 방어 시 20% 경감)
            int damage = enemyUnit.damage;

            bool isDead = target.TakeDamage(damage);
            
            // UI 표시를 위해 실제로 입은 데미지를 다시 계산해서 텍스트로 띄워줍니다
            int actualDamage = target.isDefending ? Mathf.RoundToInt(damage * 0.8f) : damage;
            dialogueText.text = $"{enemyUnit.unitName}가 {target.unitName}를 공격하여 {actualDamage}의 데미지를 주었다!";
            
            yield return wait2sec;
        }

        // 아군 전멸 확인 (최적화: TrueForAll 람다식 할당 방지)
        if (IsPartyDead())
        {
            state = BattleState.LOST;
            EndBattle();
        }
        else
        {
            state = BattleState.PLAYERTURN;
            currentUnitIndex = 0;
            ProcessPartyTurn();
        }
    }

    // 최적화: 가비지 컬렉션(GC)을 유발하는 FindAll 대신 for문 사용
    Unit GetRandomAlivePartyMember()
    {
        int aliveCount = 0;
        for (int i = 0; i < playerParty.Count; i++)
        {
            if (playerParty[i].currentHP > 0) aliveCount++;
        }

        if (aliveCount == 0) return null;

        int targetIndex = Random.Range(0, aliveCount);
        int currentAliveIndex = 0;

        for (int i = 0; i < playerParty.Count; i++)
        {
            if (playerParty[i].currentHP > 0)
            {
                if (currentAliveIndex == targetIndex) return playerParty[i];
                currentAliveIndex++;
            }
        }
        return null;
    }

    // 최적화: 가비지 컬렉션(GC)을 유발하는 TrueForAll 대신 for문 사용
    bool IsPartyDead()
    {
        for (int i = 0; i < playerParty.Count; i++)
        {
            if (playerParty[i].currentHP > 0) return false; // 한 명이라도 살아있으면 false
        }
        return true;
    }

    void EndBattle()
    {
        StartCoroutine(EndBattleCoroutine());
    }

    IEnumerator EndBattleCoroutine()
    {
        if (state == BattleState.WON)
        {
            dialogueText.text = "승리했다! (인형화 수치 증가)";
            
            // ★ 처치된 적의 ID(이름)를 기록합니다 ★
            if (!string.IsNullOrEmpty(EncounterManager.currentEnemyID))
            {
                if (!GameState.defeatedEnemyIDs.Contains(EncounterManager.currentEnemyID))
                {
                    GameState.defeatedEnemyIDs.Add(EncounterManager.currentEnemyID);
                }
            }

            PlayerStats pStats = PlayerStats.Instance;
            if (pStats != null) pStats.AddPuppetization(10f);
        }
        else if (state == BattleState.LOST)
        {
            dialogueText.text = LocalizationManager.Instance.GetText("battle.lose");
        }

        yield return new WaitForSeconds(3f);

        // ★ 전투 직후 맵에 돌아갈 때 쿨타임 확실히 활성화 ★
        GameState.isComingFromBattle = true;

        // 원래 있던 씬으로 돌아가기
        if (!string.IsNullOrEmpty(GameState.returnSceneName))
        {
            SceneManager.LoadScene(GameState.returnSceneName);
        }
        else
        {
            // 기본값으로 돌아가기 (안전장치)
            SceneManager.LoadScene("DarkReality");
        }
    }
}