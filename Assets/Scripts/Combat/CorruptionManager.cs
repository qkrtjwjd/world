using UnityEngine;
using UnityEngine.SceneManagement; // 씬 이동용

public class CorruptionManager : MonoBehaviour
{
    public static CorruptionManager instance;

    [Header("인형화 수치 설정")]
    public float currentCorruption = 20f; // 시작 수치 20%
    public float maxCorruption = 100f;    // 최대치

    private void Awake()
    {
        // 싱글톤 (게임 내내 유지)
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ★ 외부에서 이 함수를 호출해서 수치를 올리세요!
    // 예: CorruptionManager.instance.AddCorruption(10);
    public void AddCorruption(float amount)
    {
        currentCorruption += amount;
        Debug.Log($"현재 인형화 수치: {currentCorruption}%");

        // 100% 이상이면 바로 베드 엔딩으로 납치
        CheckEnding();
    }

    private void CheckEnding()
    {
        if (currentCorruption >= maxCorruption)
        {
            Debug.Log("인형화 100% 도달! 베드 엔딩 실행!");
            // ★ 중요: 엔딩 씬 이름을 정확히 적으세요!
            SceneManager.LoadScene("BadEndingScene"); 
        }
    }
}