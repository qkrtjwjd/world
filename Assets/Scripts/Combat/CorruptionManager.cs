using UnityEngine;
using UnityEngine.SceneManagement;

public class CorruptionManager : MonoBehaviour
{
    public static CorruptionManager Instance;

    [Header("인형화 수치 설정")]
    [SerializeField] private float _currentCorruption = 20f;
    public float maxCorruption = 100f;

    public float currentCorruption
    {
        get => _currentCorruption;
        private set => _currentCorruption = value;
    }

    public event System.Action<float> OnCorruptionChanged;
    public event System.Action        OnCorruptionMaxReached;

    private const float DefaultCorruption = 20f;
    private const float MaxCorruptionCap  = 100f;

    private bool _isEnding = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        OnCorruptionChanged    = null;
        OnCorruptionMaxReached = null;
    }

    public void AddCorruption(float amount)
    {
        if (_isEnding) return;
        float prev = currentCorruption;
        currentCorruption = Mathf.Clamp(currentCorruption + amount, 0f, maxCorruption);
        float delta = currentCorruption - prev;
        if (!Mathf.Approximately(delta, 0f))
            OnCorruptionChanged?.Invoke(delta);
        Dbg.Log($"현재 인형화 수치: {currentCorruption}%");
        CheckEnding();
    }

    /// <summary>세이브 로드 전용. 이벤트를 발생시키지 않고 수치를 직접 복원합니다.</summary>
    public void LoadCorruption(float value)
    {
        _isEnding = false;
        currentCorruption = Mathf.Clamp(value, 0f, maxCorruption);
    }

    private void CheckEnding()
    {
        if (currentCorruption >= maxCorruption && !_isEnding)
        {
            _isEnding = true;
            OnCorruptionMaxReached?.Invoke();
            SceneManager.LoadScene(SceneNames.BadEnding);
        }
    }
}