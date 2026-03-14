using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class RealitySystem : MonoBehaviour
{
    [Header("기본 설정")]
    public string nextSceneName = "DarkWorld";
    public float maxTime = 10f;
    public float fillSpeed = 1f;

    [Header("연출 설정")]
    public float flashDuration = 0.5f;

    [Header("연결할 것들")]
    public Image overlayImage;
    public Slider realityGauge;
    public GameObject player;

    public static RealitySystem Instance; // 싱글톤 추가
    
    [Header("씬 전체 설정")]
    [Tooltip("이 씬의 기본 상태입니다. RoomTransfer에서 'Use Scene Default'가 켜져 있으면 이 값을 따릅니다.")]
    public bool sceneDefaultActive = false; 

    [Header("현재 상태 (읽기 전용)")]
    public bool isSystemActive = false;   // 실제 작동 여부

    private float currentReality = 0f;
    private bool isTransitioning = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        // ★ 1. 전투에서 돌아온 경우: 저장된 게이지 불러오기!
        if (GameState.isComingFromBattle)
        {
            currentReality = GameState.savedGaugeValue;
            GameState.isComingFromBattle = false; // 확인했으니 끄기 (중요!)
        }
        else
        {
            // 전투가 아니라 DarkWorld나 처음 시작이면 0부터
            currentReality = 0f;
        }

        // 게이지 바 초기화
        if (realityGauge != null)
        {
            realityGauge.maxValue = maxTime;
            realityGauge.value = currentReality;
            
            // 시작하자마자 안 보이면 곤란하니까, 꺼져있으면 게이지 UI도 숨길지 결정 필요
            // 일단은 값만 멈추는 걸로
        }
        
        // 화면 연출 초기화 (게이지가 꽉 찬 상태로 돌아왔을 수도 있으니 체크)
        UpdateOverlay();

        // 위치 불러오기
        if (GameState.hasPositionSaved && player != null)
        {
            player.transform.position = GameState.lastPosition;
        }
    }

    void Update()
    {
        // 꺼져있으면 아예 멈춤
        if (!isSystemActive) return;

        // ★ 2. 실시간 저장: 전투 걸릴 때를 대비해 항상 기록
        GameState.savedGaugeValue = currentReality;

        if (isTransitioning) return;

        // ★ [추가됨] 멘탈 붕괴 상태라면 게이지가 차오르지 않음 (현실 진입 불가)
        if (GameState.mentalBreakdownTimer > 0)
        {
            // (선택사항) 게이지를 0으로 깎아버리거나, 멈추게 할 수 있음
            // 여기선 멈추는 것으로 처리
            return;
        }

        // 시간 흐름
        currentReality += Time.deltaTime * fillSpeed;

        if (realityGauge != null) realityGauge.value = currentReality;

        // 화면 연출 함수로 분리함
        UpdateOverlay();

        // 꽉 차면 이동
        if (currentReality >= maxTime)
        {
            isTransitioning = true;
            
            if (player != null)
            {
                GameState.lastPosition = player.transform.position;
                GameState.hasPositionSaved = true;
            }
            
            // ★ 중요: DarkWorld로 갈 때는 "전투 아님" 상태여야 함
            GameState.isComingFromBattle = false; 

            Debug.Log("현실 세계로 진입!");
            SceneManager.LoadScene(nextSceneName);
        }
    }

    void UpdateOverlay()
    {
        if (overlayImage != null)
        {
            float startFlashTime = maxTime - flashDuration;
            if (currentReality >= startFlashTime)
            {
                float progress = (currentReality - startFlashTime) / flashDuration;
                overlayImage.fillAmount = progress;
            }
            else
            {
                overlayImage.fillAmount = 0f;
            }
        }
    }
}