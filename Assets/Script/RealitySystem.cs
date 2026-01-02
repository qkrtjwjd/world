using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement; // ★ 씬 이동하려면 이거 필수!

public class RealitySystem : MonoBehaviour
{
    [Header("설정")]
    public GameObject player; // 주인공을 드래그해서 넣어줘야 함!
    public string nextSceneName = "DarkReality"; // 이동할 씬 이름 (정확해야 함!)
    public float maxTime = 10f;
    public float fillSpeed = 1f;

    [Header("연결할 것들")]
    public Image overlayImage;
    public Slider realityGauge;

    private float currentReality = 0f;
    private bool isTransitioning = false; // 중복 이동 방지용

    void Start()
    {
        currentReality = 0f;
        if (realityGauge != null) realityGauge.maxValue = maxTime;
    }

    void Update()
    {
        // 이미 이동 중이면 업데이트 멈춤
        if (isTransitioning) return;

        // 1. 시간 흐름
        currentReality += Time.deltaTime * fillSpeed;

        // 2. 연출 업데이트
        if (overlayImage != null) overlayImage.fillAmount = currentReality / maxTime;
        if (realityGauge != null) realityGauge.value = currentReality;

        // 3. ★ 꽉 찼을 때 씬 이동!
        if (currentReality >= maxTime)
        {
            isTransitioning = true; // "나 이동한다!" 표시 (중복 실행 방지)
            Debug.Log("현실 세계로 진입합니다...");

            // ★ 추가: 떠나기 전에 위치 저장!
            if (player != null)
            {
                GameState.lastPosition = player.transform.position;
                GameState.hasPositionSaved = true;
            }
            
            // 씬 이동 명령
            SceneManager.LoadScene(DarkReality);
        }
    }
}