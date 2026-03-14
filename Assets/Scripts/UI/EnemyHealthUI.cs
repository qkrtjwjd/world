using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthUI : MonoBehaviour
{
    [Header("UI Reference")]
    public Slider enemyHPSlider;
    
    [Header("Targeting")]
    public EnemyHealth targetEnemy;
    public Vector3 offset = new Vector3(0, 2f, 0); // 머리 위 또는 발밑 오프셋

    private Camera mainCamera;
    private Canvas parentCanvas;

    void Start()
    {
        mainCamera = Camera.main;
        
        // 적이 생성될 때 자동으로 해당 적의 HP 데이터와 슬라이더를 연결(Binding)
        if (targetEnemy == null)
        {
            targetEnemy = GetComponentInParent<EnemyHealth>();
            if (targetEnemy == null)
            {
                targetEnemy = GetComponent<EnemyHealth>();
            }
        }

        if (enemyHPSlider == null)
        {
            enemyHPSlider = GetComponent<Slider>();
            if (enemyHPSlider == null)
            {
                enemyHPSlider = GetComponentInChildren<Slider>();
            }
        }

        if (enemyHPSlider != null)
        {
            parentCanvas = enemyHPSlider.GetComponentInParent<Canvas>();
        }

        // 시작 시점에 슬라이더 값을 즉시 초기화
        if (targetEnemy != null && enemyHPSlider != null && targetEnemy.maxHealth > 0)
        {
            enemyHPSlider.value = targetEnemy.currentHealth / targetEnemy.maxHealth;
        }
    }

    void Update()
    {
        if (targetEnemy == null || enemyHPSlider == null) return;

        // 1. HP 업데이트 로직
        // 조건: Slider.value = currentHP / maxHP 공식 사용
        if (targetEnemy.maxHealth > 0)
        {
            enemyHPSlider.value = targetEnemy.currentHealth / targetEnemy.maxHealth;
        }

        // 2. World-to-Screen: 적의 발밑 또는 머리 위 위치를 유지하도록 설정
        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.WorldSpace)
        {
            // Canvas가 Screen Space 인 경우
            if (mainCamera != null)
            {
                transform.position = mainCamera.WorldToScreenPoint(targetEnemy.transform.position + offset);
            }
        }
        else
        {
            // Canvas가 World Space 인 경우 카메라를 바라보게 설정 (빌보드 처리)
            if (mainCamera != null)
            {
                transform.rotation = mainCamera.transform.rotation;
            }
        }
    }
}