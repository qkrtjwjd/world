using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    public static PlayerStats Instance;

    [Header("■ 기본 스탯 설정")]
    [Tooltip("플레이어의 최대 체력")]
    public float maxHealth = 100f;
    [Tooltip("현재 체력")]
    public float currentHealth;

    [Header("■ 동료 스탯 설정")]
    [Tooltip("동료의 최대 체력")]
    public float allyMaxHP = 100f;
    [Tooltip("동료의 현재 체력")]
    public float allyCurrentHP = 100f;

    [Tooltip("최대 멘탈 (0이 되면 멘탈 붕괴)")]
    public float maxMental = 100f;
    [Tooltip("현재 멘탈 (현실에서 스트레스를 받으면 감소)")]
    public float currentMental;

    [Tooltip("최대 배고픔 수치")]
    public float maxHunger = 100f;
    [Tooltip("현재 배고픔 수치 (시간이 지나면 감소)")]
    public float currentHunger;

    [Tooltip("최대 인형화 수치 (환상에 동화된 정도)")]
    public float maxPuppetization = 100f;
    [Tooltip("현재 인형화 수치 (높을수록 환상에 가까워짐)")]
    public float currentPuppetization;

    [Header("■ 자연 감소/회복 설정")]
    [Tooltip("초당 배고픔 감소량 (0으로 설정하면 줄어들지 않음)")]
    public float hungerDecreaseRate = 1.0f; // 다시 1.0으로 복구 (자동 감소)

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    // 최적화: 이전 프레임의 값 저장 (UI 갱신 최소화용)
    private float _lastHealth;
    private float _lastMental;
    private float _lastHunger;

    void Start()
    {
        // 1. GameState에 저장된 정보가 있다면 불러오기 (씬 이동 후 유지)
        if (GameState.playerHealth >= 0) currentHealth = GameState.playerHealth;
        else currentHealth = maxHealth;

        if (GameState.playerMental >= 0) currentMental = GameState.playerMental;
        else currentMental = maxMental;

        if (GameState.playerHunger >= 0) currentHunger = GameState.playerHunger;
        else currentHunger = maxHunger;

        // Trauma 로드 로직 제거됨

        if (GameState.playerPuppetization >= 0) currentPuppetization = GameState.playerPuppetization;
        else currentPuppetization = 0f;
        
        // 초기화 시엔 강제 갱신
        UpdateUI(true);
    }

    void Update()
    {
        // 배고픔 자연 감소
        if (currentHunger > 0)
        {
            currentHunger -= hungerDecreaseRate * Time.deltaTime;
            if (currentHunger < 0) currentHunger = 0;
        }

        // ★ [추가됨] 멘탈 붕괴 체크
        if (currentMental <= 0)
        {
            // 아직 붕괴 상태가 아니라면 발동
            if (GameState.mentalBreakdownTimer <= 0)
            {
                TriggerMentalBreakdown();
            }
        }

        // ★ [추가됨] 멘탈 붕괴 타이머 감소
        if (GameState.mentalBreakdownTimer > 0)
        {
            GameState.mentalBreakdownTimer -= Time.deltaTime;

            // 타이머가 방금 끝났다면
            if (GameState.mentalBreakdownTimer <= 0)
            {
                GameState.mentalBreakdownTimer = 0;
                // 글리치 루프 끄기
                if (GlitchManager.Instance != null)
                {
                    GlitchManager.Instance.SetGlitchLoop(false);
                    // 해방되는 느낌으로 살짝 지지직하고 끝내기
                    GlitchManager.Instance.PlayGlitch(0.3f, 0.3f);
                }
                Debug.Log("멘탈 붕괴 상태 해제!");
            }
        }

        // ★ 실시간으로 GameState에 동기화
        GameState.playerHealth = currentHealth;
        GameState.playerMental = currentMental;
        GameState.playerHunger = currentHunger;
        GameState.playerPuppetization = currentPuppetization;

        // UI 갱신 (값이 변했을 때만)
        UpdateUI(false);
    }

    // 멘탈 0이 됐을 때 강제 이동
    void TriggerMentalBreakdown()
    {
        Debug.Log("멘탈 붕괴! 환상 세계(안전지대?)로 강제 이동합니다. (1분간 현실 진입 불가)");
        
        // 1. 1분(60초) 타이머 설정
        GameState.mentalBreakdownTimer = 60f;

        // ★ [추가됨] 글리치 효과 시작
        if (GlitchManager.Instance != null)
        {
            // 처음에 강하게 한번 지지직!
            GlitchManager.Instance.PlayGlitch(1.0f, 0.8f);
            // 그 뒤로 1분 동안 미세하게 계속 지지직 (루프)
            GlitchManager.Instance.SetGlitchLoop(true, 0.2f);
        }

        // 2. 현재 씬이 '현실(DarkWorld)'이라면 -> '환상'으로 강제 추방
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        if (GameState.IsRealityScene(currentScene))
        {
            // 위치 저장 (돌아올 때를 위해)
            GameState.lastPosition = transform.position;
            GameState.hasPositionSaved = true;
            GameState.isComingFromBattle = false;

            // 짝꿍 씬(환상)으로 이동
            string targetScene = GameState.GetFantasyScene(currentScene);
            UnityEngine.SceneManagement.SceneManager.LoadScene(targetScene);
        }
    }

    // UI 갱신 요청 (force = true면 무조건 갱신)
    public void UpdateUI(bool force = false)
    {
        if (PlayerStatusUI.Instance == null) return;

        // 체력 변경 체크
        if (force || Mathf.Abs(currentHealth - _lastHealth) > 0.01f)
        {
            PlayerStatusUI.Instance.UpdateHP(currentHealth, maxHealth);
            _lastHealth = currentHealth;
        }

        // 멘탈 변경 체크
        if (force || Mathf.Abs(currentMental - _lastMental) > 0.01f)
        {
            PlayerStatusUI.Instance.UpdateMental(currentMental, maxMental);
            _lastMental = currentMental;
        }

        // 배고픔 변경 체크
        if (force || Mathf.Abs(currentHunger - _lastHunger) > 0.01f)
        {
            PlayerStatusUI.Instance.UpdateHunger(currentHunger, maxHunger);
            _lastHunger = currentHunger;
        }
    }

    // 데미지 받는 함수 (테스트용)
    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        if (currentHealth < 0) currentHealth = 0;
        if (PlayerStatusUI.Instance != null) PlayerStatusUI.Instance.UpdateHP(currentHealth, maxHealth);
    }

    // 밥 먹는 함수
    public void EatFood(float amount)
    {
        currentHunger += amount;
        if (currentHunger > maxHunger) currentHunger = maxHunger;
        
        // 인형화 수치 증가 (음식을 먹으면 인형화 진행)
        AddPuppetization(5f); 
        
        // ★ 음식 먹으면 멘탈도 약간 회복? (선택사항, 일단 추가)
        RecoverMental(5f);

        if (PlayerStatusUI.Instance != null) PlayerStatusUI.Instance.UpdateHunger(currentHunger, maxHunger);
    }

    // ★ 트라우마 추가 = 멘탈 감소로 통합
    public void AddTrauma(float amount)
    {
        currentMental -= amount;
        if (currentMental < 0) currentMental = 0;
        
        // 멘탈 깎였으니 UI 갱신
        if (PlayerStatusUI.Instance != null) PlayerStatusUI.Instance.UpdateMental(currentMental, maxMental);

        // 트라우마(스트레스)가 오르면 인형화 수치는 조금 떨어짐 (기존 로직 유지)
        ReducePuppetization(amount * 0.5f);
    }

    public void RecoverMental(float amount)
    {
        currentMental += amount;
        if (currentMental > maxMental) currentMental = maxMental;
        if (PlayerStatusUI.Instance != null) PlayerStatusUI.Instance.UpdateMental(currentMental, maxMental);
    }

    public void AddPuppetization(float amount)
    {
        currentPuppetization += amount;
        if (currentPuppetization > maxPuppetization) currentPuppetization = maxPuppetization;
    }

    public void ReducePuppetization(float amount)
    {
        currentPuppetization -= amount;
        if (currentPuppetization < 0) currentPuppetization = 0;
    }
}