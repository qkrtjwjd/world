using UnityEngine;
using UnityEngine.SceneManagement;

public enum CorruptionStage { Autonomy, Crack, Backfire, Loss, Doll }

public class CorruptionManager : PersistentSingleton<CorruptionManager>
{
    // ── 구간 경계 — 이 파일 외부에 숫자로 적지 말 것 ────────────────────────
    private const float StageCrack    = 31f;
    private const float StageBackfire = 61f;
    private const float StageLoss     = 81f;
    private const float StageDoll     = 100f;

    public static CorruptionStage GetStage(float value)
    {
        if (value >= StageDoll)     return CorruptionStage.Doll;
        if (value >= StageLoss)     return CorruptionStage.Loss;
        if (value >= StageBackfire) return CorruptionStage.Backfire;
        if (value >= StageCrack)    return CorruptionStage.Crack;
        return CorruptionStage.Autonomy;
    }


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

    protected override void OnDestroy()
    {
        OnCorruptionChanged    = null;
        OnCorruptionMaxReached = null;
        base.OnDestroy();
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
        if (GetStage(currentCorruption) == CorruptionStage.Doll && !_isEnding)
        {
            _isEnding = true;
            OnCorruptionMaxReached?.Invoke();
            Time.timeScale = 1f; // 턴제 전투(timeScale 0) 중 도달해도 배드엔딩이 정지되지 않도록
            SceneManager.LoadScene(SceneNames.BadEnding);
        }
    }
}