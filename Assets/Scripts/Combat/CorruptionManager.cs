using UnityEngine;
using UnityEngine.SceneManagement;

public class CorruptionManager : MonoBehaviour
{
    public static CorruptionManager instance;

    [Header("인형화 수치 설정")]
    public float currentCorruption = 20f;
    public float maxCorruption     = 100f;

    private void Awake()
    {
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

    public void AddCorruption(float amount)
    {
        currentCorruption += amount;
        Debug.Log($"현재 인형화 수치: {currentCorruption}%");
        CheckEnding();
    }

    private void CheckEnding()
    {
        if (currentCorruption >= maxCorruption)
            SceneManager.LoadScene(SceneNames.BadEnding);
    }
}