using System.Collections;
using UnityEngine;

/// <summary>
/// 시나리오 연출용 BGM 페이드 + 딱 소리 전담 매니저.
/// 실제 오디오 재생은 AudioManager에 위임한다.
/// </summary>
public class SFXManager : PersistentSingleton<SFXManager>
{
    [Header("딱 소리")]
    [SerializeField] private AudioClip snapClip;
    [SerializeField] private float     snapInterval = 0.25f;

    private AudioSource _bgmSource;
    private Coroutine   _bgmCoroutine;
    private Coroutine   _snapCoroutine;

    // ── 라이프사이클 ──────────────────────────────────────────────────────
    protected override void OnAwake()
    {
        _bgmSource             = gameObject.AddComponent<AudioSource>();
        _bgmSource.playOnAwake = false;
        _bgmSource.loop        = true;
        _bgmSource.volume      = 0f;
        AudioManager.RegisterBGM(_bgmSource);
    }

    protected override void OnDestroy()
    {
        AudioManager.UnregisterBGM(_bgmSource);
        base.OnDestroy();
    }

    // ── Snap API ─────────────────────────────────────────────────────────
    /// <summary>인형화 수치에 따라 딱 소리를 재생한다.</summary>
    public void PlaySnap(float dollification)
    {
        if (snapClip == null) return;

        StopSnap();

        switch (CorruptionManager.GetStage(dollification))
        {
            case CorruptionStage.Autonomy:  PlaySnapOnce(); break;
            case CorruptionStage.Crack:     _snapCoroutine = StartCoroutine(PlaySnapTimes(2)); break;
            case CorruptionStage.Backfire:  _snapCoroutine = StartCoroutine(PlaySnapTimes(3)); break;
            default:                        _snapCoroutine = StartCoroutine(PlaySnapLoop());   break;
        }
    }

    public void StopSnap()
    {
        if (_snapCoroutine != null)
        {
            StopCoroutine(_snapCoroutine);
            _snapCoroutine = null;
        }
    }

    // ── BGM API ───────────────────────────────────────────────────────────
    /// <summary>새 BGM을 fadeInTime 초에 걸쳐 페이드 인한다.</summary>
    public void PlayBGM(AudioClip clip, float fadeInTime)
    {
        if (clip == null) return;
        if (_bgmCoroutine != null) StopCoroutine(_bgmCoroutine);
        _bgmCoroutine = StartCoroutine(PlayBGMRoutine(clip, fadeInTime));
    }

    /// <summary>현재 BGM을 fadeOutTime 초에 걸쳐 페이드 아웃 후 정지한다.</summary>
    public void StopBGM(float fadeOutTime)
    {
        if (_bgmCoroutine != null) StopCoroutine(_bgmCoroutine);
        if (!_bgmSource.isPlaying) return;
        _bgmCoroutine = StartCoroutine(StopBGMRoutine(fadeOutTime));
    }

    /// <summary>현재 BGM을 즉시 정지한다.</summary>
    public void StopBGMImmediate()
    {
        if (_bgmCoroutine != null)
        {
            StopCoroutine(_bgmCoroutine);
            _bgmCoroutine = null;
        }
        _bgmSource.Stop();
        _bgmSource.volume = 0f;
    }

    // ── 내부 코루틴 ──────────────────────────────────────────────────────
    void PlaySnapOnce()
    {
        AudioManager.Instance?.Play(snapClip);
    }

    IEnumerator PlaySnapTimes(int count)
    {
        for (int i = 0; i < count; i++)
        {
            AudioManager.Instance?.Play(snapClip);
            yield return new WaitForSeconds(snapInterval);
        }
        _snapCoroutine = null;
    }

    IEnumerator PlaySnapLoop()
    {
        while (true)
        {
            AudioManager.Instance?.Play(snapClip);
            yield return new WaitForSeconds(snapInterval);
        }
    }

    IEnumerator PlayBGMRoutine(AudioClip clip, float fadeInTime)
    {
        if (_bgmSource.isPlaying && fadeInTime > 0f)
        {
            float crossFade = Mathf.Min(fadeInTime * 0.5f, 0.5f);
            float startVol  = _bgmSource.volume;
            for (float t = 0f; t < crossFade; t += Time.deltaTime)
            {
                _bgmSource.volume = Mathf.Lerp(startVol, 0f, t / crossFade);
                yield return null;
            }
        }

        _bgmSource.Stop();
        _bgmSource.clip   = clip;
        _bgmSource.volume = 0f;
        _bgmSource.Play();

        float targetVol = SettingsManager.Instance?.bgmVolume ?? 1f;

        if (fadeInTime <= 0f)
        {
            _bgmSource.volume = targetVol;
        }
        else
        {
            for (float t = 0f; t < fadeInTime; t += Time.deltaTime)
            {
                _bgmSource.volume = Mathf.Lerp(0f, targetVol, t / fadeInTime);
                yield return null;
            }
            _bgmSource.volume = targetVol;
        }

        _bgmCoroutine = null;
    }

    IEnumerator StopBGMRoutine(float fadeOutTime)
    {
        float startVol = _bgmSource.volume;

        if (fadeOutTime <= 0f)
        {
            _bgmSource.Stop();
            _bgmSource.volume = 0f;
            _bgmCoroutine = null;
            yield break;
        }

        for (float t = 0f; t < fadeOutTime; t += Time.deltaTime)
        {
            _bgmSource.volume = Mathf.Lerp(startVol, 0f, t / fadeOutTime);
            yield return null;
        }

        _bgmSource.Stop();
        _bgmSource.volume = 0f;
        _bgmCoroutine = null;
    }
}
