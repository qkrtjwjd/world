using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class DarkRealitySystem : MonoBehaviour
{
    [Header("설정")]
    public string nextSceneName = "MapScene"; // 다시 돌아갈 2D 맵 이름
    public float maxTime = 10f;  // 버티는 시간 (환상 세계랑 똑같이 맞추세요)
    public float drainSpeed = 1f; // 줄어드는 속도

    [Header("연결할 것들")]
    public Image overlayImage;   // 어두운 필터 (여긴 처음부터 꽉 차있음)
    public Slider realityGauge;  // 게이지 바
    public GameObject player;    // 어두운 세계의 주인공 (위치 저장용)

    private float currentReality;
    private bool isTransitioning = false;

    void Start()
    {
        // 시작할 때 게이지 꽉 채워서 시작!
        currentReality = maxTime; 
        
        if (realityGauge != null) 
        {
            realityGauge.maxValue = maxTime;
            realityGauge.value = maxTime;
        }
    }

    void Update()
    {
        if (isTransitioning) return;

        // 1. 시간 빼기 (줄어들기!) 
        currentReality -= Time.deltaTime * drainSpeed;

        // 2. 연출 업데이트 (게이지가 줄어듦)
        if (overlayImage != null) overlayImage.fillAmount = currentReality / maxTime;
        if (realityGauge != null) realityGauge.value = currentReality;

        // 3. 시간이 0이 되면 -> 다시 환상 세계로 복귀
        if (currentReality <= 0)
        {
            isTransitioning = true;
            Debug.Log("환상 세계로 돌아갑니다...");

            // ★ 돌아갈 때도 위치 저장! (그래야 갔던 자리에서 다시 시작함)
            if (player != null)
            {
                GameState.lastPosition = player.transform.position;
                GameState.hasPositionSaved = true;
            }

            SceneManager.LoadScene(nextSceneName);
        }
    }
}