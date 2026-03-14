using UnityEngine;

public class MysteriousObject : MonoBehaviour
{
    [Header("■ 레벨 시스템")]
    [Tooltip("현재 레벨입니다.")]
    public int currentLevel = 1;

    [Tooltip("현재 먹은 경험치 양입니다.")]
    public float currentExp = 0;      
    
    [Tooltip("다음 레벨업에 필요한 경험치입니다.")]
    public float maxExp = 100;        
    
    [Header("■ 급체(Sick) 시스템 설정")]
    [Tooltip("현재 체한 상태인가요? 체크되면 아이템을 못 먹습니다.")]
    public bool isSick = false;       
    
    [Tooltip("체했을 때 아이템을 못 먹는 시간(초)입니다.")]
    public float sickDuration = 10f;  
    private float sickTimer = 0f;
    
    [Space(10)]
    [Tooltip("이번 턴에 고급 아이템을 먹은 횟수입니다.")]
    [SerializeField] private int highGradeEatCount = 0; 

    [Tooltip("이번 턴에 총 먹은 아이템 횟수입니다.")]
    [SerializeField] private int totalEatCount = 0;     

    // 마지막으로 먹이를 먹은 시간 (초기화 체크용)
    private float lastFedTime;
    
    [Space(10)]
    [Tooltip("고급 아이템을 이 횟수 이상 연속으로 먹으면 체합니다.")]
    public int maxHighGradeLimit = 3; 

    [Tooltip("종류 상관없이 한 번에 이 개수 이상 먹으면 체합니다.")]
    public int maxTotalLimit = 15;    

    [Tooltip("이 시간(초) 동안 먹이를 안 주면 먹은 횟수가 초기화됩니다. (기본 5분)")]
    public float resetEatCountTime = 300f;

    [Header("■ 시각 효과")]
    [Tooltip("먹이를 먹었을 때 머리 위에 뜰 하트 이펙트 프리팹")]
    public GameObject heartEffectPrefab;

    [Header("■ 보상 아이템 설정 (프리팹 연결)")]
    public GameObject[] commonRewards; // 일반
    public GameObject[] rareRewards;   // 레어
    public GameObject[] hiddenRewards; // 히든

    [Header("■ 스토리 제어")]
    [Tooltip("체크하면 다음 보상 타임에 무조건 히든 아이템을 뱉습니다.")]
    public bool forceHiddenReward = false; 

    void Start()
    {
        // 시작 시 최대 경험치 계산
        CalculateMaxExp();
        lastFedTime = Time.time;
    }

    void Update()
    {
        // 체했을 때 시간 줄이기
        if (isSick)
        {
            sickTimer -= Time.deltaTime;
            if (sickTimer <= 0)
            {
                RecoverFromSick();
            }
        }
    }

    // 경험치 요구량 계산 공식
    void CalculateMaxExp()
    {
        // 예: 레벨 * 100 (1Lv: 100, 2Lv: 200, 3Lv: 300...)
        maxExp = currentLevel * 100;
    }

    // 외부(UI)에서 아이템을 먹일 때 부르는 함수
    public void EatItem(ItemData item)
    {
        // 1. 체했으면 못 먹음
        if (isSick)
        {
            Debug.Log("우욱... (체해서 더 이상 못 먹습니다. 남은 시간: " + (int)sickTimer + "초)");
            return;
        }

        // 2. 시간 경과 체크: 5분(300초) 이상 지났으면 먹은 횟수 초기화
        if (Time.time - lastFedTime >= resetEatCountTime)
        {
            Debug.Log("오랜만에 먹어서 소화가 다 됐어! (횟수 초기화)");
            highGradeEatCount = 0;
            totalEatCount = 0;
        }
        lastFedTime = Time.time; // 시간 갱신

        // 3. 급체 체크 로직
        // 고급 아이템 체크
        if (item.isHighGrade)
        {
            if (highGradeEatCount >= maxHighGradeLimit)
            {
                GetSick("으악! 너무 기름진 걸 많이 먹었어!");
                return;
            }
            highGradeEatCount++;
        }

        // 과식 체크 (아이템 개수 너무 많이)
        if (totalEatCount >= maxTotalLimit)
        {
            GetSick("끄윽... 너무 많이 먹었어... 배 터져...");
            return;
        }
        totalEatCount++;


        // 4. 아이템 먹기 성공
        currentExp += item.feedValue;
        Debug.Log($"{item.itemName} 냠냠! (Lv.{currentLevel} | Exp: {currentExp}/{maxExp})");

        // 하트 이펙트 띄우기
        // Debug.Log($"[MysteriousObject] 하트 프리팹 확인: {heartEffectPrefab}");
        if (heartEffectPrefab != null)
        {
            // Z값을 -5로 당겨서 다른 물체보다 앞에 그려지게 함 (2D 기준)
            Vector3 spawnPos = transform.position + Vector3.up * 1.5f;
            spawnPos.z = -5f; 
            
            GameObject heartInstance = Instantiate(heartEffectPrefab, spawnPos, Quaternion.identity);
            Destroy(heartInstance, 0.7f); // 0.7초 뒤에 자동 삭제
        }
        else
        {
            Debug.LogWarning("[MysteriousObject] 하트 이펙트 프리팹이 연결되지 않았습니다! 인스펙터를 확인하세요.");
        }

        // 5. 레벨업 체크
        if (currentExp >= maxExp)
        {
            LevelUp();
        }
    }

    // 체하는 함수
    void GetSick(string message)
    {
        isSick = true;
        sickTimer = sickDuration;
        Debug.Log($"[급체 발생] {message} ({sickDuration}초 동안 아이템 거부)");
        // 시각적 효과 추가 가능
    }

    // 회복 함수
    void RecoverFromSick()
    {
        isSick = false;
        sickTimer = 0;
        Debug.Log("꺼억~ 소화 다 됐다! 다시 먹을 수 있어.");
    }

    // 레벨업 및 보상
    void LevelUp()
    {
        Debug.Log($"🎉 레벨 업! {currentLevel} -> {currentLevel + 1}");
        
        // 보상 뱉기
        SpitReward();

        // 경험치 이월 및 레벨 증가
        currentExp -= maxExp; // 남은 경험치는 다음 레벨로
        currentLevel++;
        CalculateMaxExp(); // 다음 레벨 필요 경험치 계산

        // 만약 이월된 경험치가 너무 많아서 바로 또 레벨업 할 정도라면?
        if (currentExp >= maxExp)
        {
            LevelUp(); // 재귀 호출 (연속 레벨업)
        }
    }

    // 보상 뱉기
    void SpitReward()
    {
        Debug.Log("끄억! 보상을 뱉습니다.");

        GameObject rewardToSpawn = null;
        int randomValue = Random.Range(0, 100); 

        // 레벨이 오를수록 레어 확률 증가 (기본 30% + 레벨당 2%)
        int rareChance = 30 + (currentLevel * 2);
        if (rareChance > 90) rareChance = 90; // 최대 90% 제한

        // 1. 스토리상 히든
        if (forceHiddenReward) 
        {
            if (hiddenRewards.Length > 0)
            {
                rewardToSpawn = hiddenRewards[0]; 
                forceHiddenReward = false; 
            }
        }
        // 2. 레어
        else if (randomValue < rareChance)
        {
            if (rareRewards.Length > 0)
            {
                rewardToSpawn = rareRewards[Random.Range(0, rareRewards.Length)];
            }
        }
        // 3. 일반
        else
        {
            if (commonRewards.Length > 0)
            {
                rewardToSpawn = commonRewards[Random.Range(0, commonRewards.Length)];
            }
        }

        // 아이템 생성 (레벨에 따라 여러 개 뱉을 수도 있음)
        // 여기서는 레벨 5당 1개씩 추가로 뱉게 설정
        int spawnCount = 1 + (currentLevel / 5);
        
        for(int i=0; i < spawnCount; i++)
        {
            if (rewardToSpawn != null)
            {
                // 약간씩 흩뿌리기
                Vector3 offset = new Vector3(Random.Range(-0.5f, 0.5f), 1f, 0);
                Instantiate(rewardToSpawn, transform.position + offset, Quaternion.identity);
            }
        }

        // ★ 중요: 보상을 뱉어도 먹은 횟수(급체 스택)는 초기화하지 않음!
    }
}
