using UnityEngine;
using UnityEngine.SceneManagement;

public class CorruptionManager : MonoBehaviour
{
    public static CorruptionManager instance;

    [Header("인형화 수치 설정")]
    public float currentCorruption = 20f;
    public float maxCorruption     = 100f;

    private bool _isEnding = false;

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
        if (_isEnding) return;
        currentCorruption = Mathf.Clamp(currentCorruption + amount, 0f, maxCorruption);
        Debug.Log($"현재 인형화 수치: {currentCorruption}%");
        CheckEnding();
    }

    private void CheckEnding()
    {
        if (currentCorruption >= maxCorruption)
        {
            _isEnding = true;
            SceneManager.LoadScene(SceneNames.BadEnding);
        }
    }
}