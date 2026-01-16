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

    private float currentReality = 0f;
    private bool isTransitioning = false;

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
        // ★ 2. 실시간 저장: 전투 걸릴 때를 대비해 항상 기록
        GameState.savedGaugeValue = currentReality;

        if (isTransitioning) return;

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