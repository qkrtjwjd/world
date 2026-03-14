using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class DarkRealitySystem : MonoBehaviour
{
    [Header("기본 설정")]
    public string nextSceneName = "MapScene";
    public float maxTime = 10f; 
    public float drainSpeed = 1f;

    [Header("연출 설정")]
    public float clearDuration = 0.5f; 

    [Header("연결할 것들")]
    public Image overlayImage;
    public Slider realityGauge;
    public GameObject player;

    private float currentReality;
    private bool isTransitioning = false;
    private float timeSinceStart = 0f; 

    void Start()
    {
        // ★ 1. 어디서 왔는지 확인
        if (GameState.isComingFromBattle)
        {
            // 전투 끝나고 왔으면? 아까 쓰던 게이지 이어서
            currentReality = GameState.savedGaugeValue;
            GameState.isComingFromBattle = false; // 확인했으니 끄기
        }
        else
        {
            // MapScene에서 넘어왔으면? 무조건 풀충전!
            currentReality = maxTime;
        }

        timeSinceStart = 0f; 

        if (realityGauge != null)
        {
            realityGauge.maxValue = maxTime;
            realityGauge.value = currentReality;
        }
        
        if (GameState.hasPositionSaved && player != null)
        {
            player.transform.position = GameState.lastPosition;
        }
    }

    void Update()
    {
        // ★ 2. 실시간 저장 (전투 대비)
        GameState.savedGaugeValue = currentReality;

        if (isTransitioning) return;

        // 게이지 줄어듦
        currentReality -= Time.deltaTime * drainSpeed;
        
        if (realityGauge != null) realityGauge.value = currentReality;

        // 화면 연출 (시작할 때 걷히는 효과)
        timeSinceStart += Time.deltaTime;
        if (overlayImage != null)
        {
            if (timeSinceStart < clearDuration)
            {
                float progress = 1f - (timeSinceStart / clearDuration);
                overlayImage.fillAmount = progress;
            }
            else
            {
                overlayImage.fillAmount = 0f;
            }
        }

        // 3. 시간이 0이 되면 복귀 (여기서 튕기는 문제 해결됨)
        if (currentReality <= 0)
        {
            isTransitioning = true;
            Debug.Log("환상 세계로 돌아갑니다...");

            if (player != null)
            {
                GameState.lastPosition = player.transform.position;
                GameState.hasPositionSaved = true;
            }
            
            // MapScene으로 갈 때도 "전투 아님"이니까 MapScene은 0부터 시작함
            GameState.isComingFromBattle = false; 
            
            SceneManager.LoadScene(nextSceneName);
        }
    }
}