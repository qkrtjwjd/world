using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public struct GlitchPreset
{
    public float intensity;
    public float colorDrift;
    public float scanLineJitter;
    public float staticNoise;
    public float blockDisplace;
}

public class GlitchManager : MonoBehaviour
{
    public static GlitchManager Instance;

    [Header("연결 필수")]
    [Tooltip("글리치 쉐이더가 적용된 UI 패널 (Image)")]
    public Image glitchPanel;

    [Header("기본 설정")]
    [Range(0, 1)] public float defaultIntensity = 0.5f;

    // ── 프리셋 ──
    public static readonly GlitchPreset PresetSubtle = new GlitchPreset
        { intensity = 0.12f, colorDrift = 0.01f, scanLineJitter = 0.02f, staticNoise = 0.0f,  blockDisplace = 0.0f  };
    public static readonly GlitchPreset PresetMild = new GlitchPreset
        { intensity = 0.25f, colorDrift = 0.02f, scanLineJitter = 0.05f, staticNoise = 0.03f, blockDisplace = 0.01f };
    public static readonly GlitchPreset PresetStrong = new GlitchPreset
        { intensity = 0.65f, colorDrift = 0.04f, scanLineJitter = 0.08f, staticNoise = 0.08f, blockDisplace = 0.05f };
    public static readonly GlitchPreset PresetCrash = new GlitchPreset
        { intensity = 0.9f,  colorDrift = 0.06f, scanLineJitter = 0.12f, staticNoise = 0.15f, blockDisplace = 0.10f };

    // 셰이더 프로퍼티 ID 캐싱 (string 룩업 제거)
    static readonly int PropIntensity      = Shader.PropertyToID("_Intensity");
    static readonly int PropColorDrift     = Shader.PropertyToID("_ColorDrift");
    static readonly int PropScanLineJitter = Shader.PropertyToID("_ScanLineJitter");
    static readonly int PropStaticNoise    = Shader.PropertyToID("_StaticNoise");
    static readonly int PropBlockDisplace  = Shader.PropertyToID("_BlockDisplace");

    private Material _glitchMat;
    private Coroutine _activeCoroutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoCreate()
    {
        if (Instance != null) return;
        var prefab = Resources.Load<GameObject>("Glitch");
        if (prefab != null) Instantiate(prefab);
    }

    void Awake()
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

    void Start()
    {
        if (glitchPanel != null)
        {
            _glitchMat = glitchPanel.material;
            // 새 프로퍼티 기본값 초기화
            _glitchMat.SetFloat(PropStaticNoise,   0f);
            _glitchMat.SetFloat(PropBlockDisplace, 0f);
            glitchPanel.gameObject.SetActive(false);
        }
    }

    // ── PlayGlitch ──

    /// <summary>duration 초 동안 프리셋으로 글리치 재생</summary>
    public void PlayGlitch(float duration, GlitchPreset preset)
    {
        if (glitchPanel == null) return;
        if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
        _activeCoroutine = StartCoroutine(GlitchRoutinePreset(duration, preset));
    }

    /// <summary>duration 초 동안 강도로 글리치 재생 (기존 호환)</summary>
    public void PlayGlitch(float duration, float intensity = -1f)
    {
        if (glitchPanel == null) return;
        if (intensity < 0) intensity = defaultIntensity;
        if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
        _activeCoroutine = StartCoroutine(GlitchRoutine(duration, intensity));
    }

    // ── SetGlitchLoop ──

    /// <summary>프리셋으로 글리치 루프 켜기/끄기</summary>
    public void SetGlitchLoop(bool isActive, GlitchPreset preset)
    {
        if (glitchPanel == null) return;
        if (isActive)
        {
            glitchPanel.gameObject.SetActive(true);
            ApplyPreset(preset);
        }
        else
        {
            TurnOffLoop();
        }
    }

    /// <summary>강도로 글리치 루프 켜기/끄기 (기존 호환)</summary>
    public void SetGlitchLoop(bool isActive, float intensity = -1f)
    {
        if (glitchPanel == null) return;
        if (isActive)
        {
            if (intensity < 0) intensity = defaultIntensity;
            glitchPanel.gameObject.SetActive(true);
            _glitchMat.SetFloat(PropIntensity, intensity);
        }
        else
        {
            TurnOffLoop();
        }
    }

    // ── 내부 ──

    void TurnOffLoop()
    {
        // PlayGlitch 코루틴이 실행 중이면 패널 끄지 않음 (스토리 글리치 보호)
        if (_activeCoroutine != null) return;
        glitchPanel.gameObject.SetActive(false);
    }

    void ApplyPreset(GlitchPreset p)
    {
        _glitchMat.SetFloat(PropIntensity,      p.intensity);
        _glitchMat.SetFloat(PropColorDrift,     p.colorDrift);
        _glitchMat.SetFloat(PropScanLineJitter, p.scanLineJitter);
        _glitchMat.SetFloat(PropStaticNoise,    p.staticNoise);
        _glitchMat.SetFloat(PropBlockDisplace,  p.blockDisplace);
    }

    IEnumerator GlitchRoutinePreset(float duration, GlitchPreset preset)
    {
        glitchPanel.gameObject.SetActive(true);

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float noise = Random.Range(0.5f, 1.5f);
            _glitchMat.SetFloat(PropIntensity,      preset.intensity      * noise);
            _glitchMat.SetFloat(PropColorDrift,     preset.colorDrift);
            _glitchMat.SetFloat(PropScanLineJitter, preset.scanLineJitter);
            _glitchMat.SetFloat(PropStaticNoise,    preset.staticNoise    * noise);
            _glitchMat.SetFloat(PropBlockDisplace,  preset.blockDisplace  * noise);
            yield return null;
        }

        glitchPanel.gameObject.SetActive(false);
        _activeCoroutine = null;
    }

    IEnumerator GlitchRoutine(float duration, float targetIntensity)
    {
        glitchPanel.gameObject.SetActive(true);

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float noise = Random.Range(0.5f, 1.5f);
            _glitchMat.SetFloat(PropIntensity, targetIntensity * noise);
            yield return null;
        }

        glitchPanel.gameObject.SetActive(false);
        _activeCoroutine = null;
    }
}
