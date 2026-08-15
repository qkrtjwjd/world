using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 전투 모드 전환 연출의 사운드를 제어합니다.
/// BattleTransitionManager의 전환 코루틴에서 호출하세요.
///
/// [에디터 설정]
/// - _mixer           : AudioMixer 연결. "LowPassCutoff" 파라미터가 Expose 되어 있어야 합니다.
/// - _sfxSource       : PlayOneShot 용 AudioSource. 비우면 자동 생성합니다.
/// - _glassBreakClip  : 유리 깨짐 효과음
/// - _metalScratchClip: 금속 긁힘 효과음
/// - _sweetChimeClip  : 달콤한 차임 효과음
/// - _fullCutoff      : LowPass 제거 시 복구할 최대 차단 주파수 (기본 22000 Hz)
/// </summary>
public class TransitionSFXController : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  싱글톤
    // ─────────────────────────────────────────────
    public static TransitionSFXController Instance
    {
        get
        {
            if (!_instance)
            {
                // 상주 프리팹(VFX와 동일 루트) 우선 로드 — Awake에서 _instance 등록됨
                var prefab = Resources.Load<GameObject>("TransitionFX");
                if (prefab != null)
                    Instantiate(prefab).name = "TransitionFX";

                if (!_instance)
                {
                    var go = new GameObject("TransitionSFXController [Auto]");
                    _instance = go.AddComponent<TransitionSFXController>();
                }
            }
            return _instance;
        }
    }
    private static TransitionSFXController _instance;

    // ─────────────────────────────────────────────
    //  Inspector 설정
    // ─────────────────────────────────────────────
    [Header("AudioMixer")]
    [Tooltip("LowPassCutoff 파라미터가 Expose 된 AudioMixer.")]
    [SerializeField] private AudioMixer _mixer;

    [Header("효과음 AudioSource")]
    [Tooltip("PlayOneShot 전용 AudioSource. 비워두면 자동 생성합니다.")]
    [SerializeField] private AudioSource _sfxSource;

    [Header("효과음 클립")]
    [SerializeField] private AudioClip _glassBreakClip;
    [SerializeField] private AudioClip _metalScratchClip;
    [SerializeField] private AudioClip _sweetChimeClip;

    [Header("LowPass 설정")]
    [Tooltip("RemoveLowPassFilter 시 복구할 최대 차단 주파수 (Hz). Unity 기본값 22000.")]
    [SerializeField] private float _fullCutoff = 22000f;

    // ─────────────────────────────────────────────
    //  AudioMixer 파라미터 이름 (Expose 된 이름과 일치해야 함)
    // ─────────────────────────────────────────────
    private const string LowPassParam = "LowPassCutoff";

    // ─────────────────────────────────────────────
    //  코루틴 핸들 (중복 방지)
    // ─────────────────────────────────────────────
    private Coroutine _crossfadeCoroutine;
    private Coroutine _lowPassCoroutine;

    // ─────────────────────────────────────────────
    //  라이프사이클
    // ─────────────────────────────────────────────
    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            // 컴포넌트만 파괴 — 매니저 루트 오브젝트에 함께 붙은 다른 컴포넌트 보호
            Destroy(this);
            return;
        }

        if (_sfxSource == null)
            _sfxSource = gameObject.AddComponent<AudioSource>();
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // ─────────────────────────────────────────────
    //  공개 API — PlayOneShot 계열
    // ─────────────────────────────────────────────

    /// <summary>유리 깨짐 효과음을 재생합니다.</summary>
    public void PlayGlassBreak()
    {
        PlayClip(_glassBreakClip, "PlayGlassBreak");
    }

    /// <summary>금속 긁힘 효과음을 재생합니다.</summary>
    public void PlayMetalScratch()
    {
        PlayClip(_metalScratchClip, "PlayMetalScratch");
    }

    /// <summary>달콤한 차임 효과음을 재생합니다.</summary>
    public void PlaySweetChime()
    {
        PlayClip(_sweetChimeClip, "PlaySweetChime");
    }

    // ─────────────────────────────────────────────
    //  공개 API — BGM 크로스페이드
    // ─────────────────────────────────────────────

    /// <summary>
    /// from BGM을 페이드 아웃하며 to BGM을 페이드 인합니다.
    /// from / to 가 null 이면 해당 방향의 페이드를 건너뜁니다.
    /// </summary>
    public void CrossfadeBGM(AudioSource from, AudioSource to, float duration)
    {
        if (from == null && to == null)
        {
            Debug.LogWarning("[TransitionSFXController] CrossfadeBGM: from 과 to 가 모두 null 입니다.");
            return;
        }
        if (_crossfadeCoroutine != null) StopCoroutine(_crossfadeCoroutine);
        _crossfadeCoroutine = StartCoroutine(CrossfadeRoutine(from, to, duration));
    }

    // ─────────────────────────────────────────────
    //  공개 API — AudioMixer LowPass 필터
    // ─────────────────────────────────────────────

    /// <summary>
    /// AudioMixer의 LowPassCutoff 파라미터를 현재 값에서 cutoff 까지
    /// duration 초 동안 Lerp합니다.
    /// </summary>
    public void ApplyLowPassFilter(float cutoff, float duration)
    {
        if (!ValidateMixer("ApplyLowPassFilter")) return;
        if (_lowPassCoroutine != null) StopCoroutine(_lowPassCoroutine);
        _lowPassCoroutine = StartCoroutine(LowPassRoutine(cutoff, duration));
    }

    /// <summary>
    /// AudioMixer의 LowPassCutoff 파라미터를 현재 값에서 _fullCutoff(22000 Hz) 로
    /// duration 초 동안 Lerp합니다.
    /// </summary>
    public void RemoveLowPassFilter(float duration)
    {
        if (!ValidateMixer("RemoveLowPassFilter")) return;
        if (_lowPassCoroutine != null) StopCoroutine(_lowPassCoroutine);
        _lowPassCoroutine = StartCoroutine(LowPassRoutine(_fullCutoff, duration));
    }

    // ─────────────────────────────────────────────
    //  코루틴
    // ─────────────────────────────────────────────

    /// <summary>BGM 크로스페이드 — from 페이드 아웃과 to 페이드 인을 동시에 진행.</summary>
    IEnumerator CrossfadeRoutine(AudioSource from, AudioSource to, float duration)
    {
        // to 재생 준비: 볼륨 0으로 시작
        if (to != null)
        {
            to.volume = 0f;
            if (!to.isPlaying) to.Play();
        }

        float fromStartVol = (from != null) ? from.volume : 0f;
        float elapsed      = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t       = Mathf.Clamp01(elapsed / duration);
            float tSmooth = Mathf.SmoothStep(0f, 1f, t);

            if (from != null) from.volume = Mathf.Lerp(fromStartVol, 0f, tSmooth);
            if (to   != null) to.volume   = Mathf.Lerp(0f, 1f, tSmooth);

            yield return null;
        }

        // 최종값 확정
        if (from != null) { from.volume = 0f; from.Stop(); }
        if (to   != null)   to.volume   = 1f;

        _crossfadeCoroutine = null;
    }

    /// <summary>LowPassCutoff 파라미터를 현재 값에서 targetCutoff 까지 Lerp.</summary>
    IEnumerator LowPassRoutine(float targetCutoff, float duration)
    {
        // 현재 값 읽기
        if (!_mixer.GetFloat(LowPassParam, out float startCutoff))
        {
            Debug.LogWarning($"[TransitionSFXController] AudioMixer에서 '{LowPassParam}' 파라미터를 읽을 수 없습니다. " +
                             "Expose 이름이 정확한지 확인하세요.");
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float value = Mathf.Lerp(startCutoff, targetCutoff, Mathf.Clamp01(elapsed / duration));
            _mixer.SetFloat(LowPassParam, value);
            yield return null;
        }

        _mixer.SetFloat(LowPassParam, targetCutoff);
        _lowPassCoroutine = null;
    }

    // ─────────────────────────────────────────────
    //  내부 헬퍼
    // ─────────────────────────────────────────────

    /// <summary>AudioClip null 체크 후 PlayOneShot 재생.</summary>
    void PlayClip(AudioClip clip, string callerName)
    {
        if (clip == null)
        {
            Debug.LogWarning($"[TransitionSFXController] {callerName}: AudioClip 이 연결되지 않았습니다.");
            return;
        }
        _sfxSource.PlayOneShot(clip);
    }

    /// <summary>_mixer null 체크. null 이면 LogWarning 후 false 반환.</summary>
    bool ValidateMixer(string callerName)
    {
        if (_mixer == null)
        {
            Debug.LogWarning($"[TransitionSFXController] {callerName}: AudioMixer 가 연결되지 않았습니다.");
            return false;
        }
        return true;
    }
}
