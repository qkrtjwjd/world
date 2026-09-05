using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 효과음 재생 싱글톤. DontDestroyOnLoad로 씬 전환 후에도 유지된다.
///
/// ── 사운드 추가 방법 ──────────────────────────────────────────────────
///   1. AudioManager 프리팹의 Sounds 배열에 항목 추가
///        name: "mySound"  /  clip: [클립 드래그]  /  loop: 체크 여부  /  category: 볼륨 카테고리
///   2-a) 일회성 효과음:
///        AudioManager.Instance?.Play("mySound");
///   2-b) 루프 사운드 시작/정지:
///        AudioManager.Instance?.PlayLoop("mySound");
///        AudioManager.Instance?.StopLoop("mySound");
///
/// ── 볼륨 카테고리 ─────────────────────────────────────────────────────
///   SFX / BGM / Voice / Ambient / Clicking / GlitchNoise
/// </summary>
public class AudioManager : PersistentSingleton<AudioManager>
{
    public enum VolumeCategory { SFX, BGM, Voice, Ambient, Clicking, GlitchNoise }

    [System.Serializable]
    public class SoundEntry
    {
        public string         name;
        public AudioClip      clip;
        public bool           loop;
        public VolumeCategory category = VolumeCategory.SFX;
    }

    [Header("효과음 목록")]
    [SerializeField] private SoundEntry[] sounds;

    [Header("재생용 AudioSource (PlayOneShot)")]
    [SerializeField] private AudioSource audioSource;

    private Dictionary<string, AudioClip>   _lookup;
    private Dictionary<string, SoundEntry>  _entryLookup;
    private readonly Dictionary<string, AudioSource> _loopSources = new();

    private System.Action<float> _onSFXVolume;
    private System.Action<float> _onVoiceVolume;
    private System.Action<float> _onAmbientVolume;
    private System.Action<float> _onClickingVolume;
    private System.Action<float> _onGlitchNoiseVolume;

    // ── AudioSource 볼륨 풀 ───────────────────────────────────────────────
    private static readonly List<AudioSource> _sfxSources         = new();
    private static readonly List<AudioSource> _bgmSources         = new();
    private static readonly List<AudioSource> _voiceSources       = new();
    private static readonly List<AudioSource> _ambientSources     = new();
    private static readonly List<AudioSource> _clickingSources    = new();
    private static readonly List<AudioSource> _glitchNoiseSources = new();

    // ── 소리 죽이기 (문틈 너머 엿듣기 등) ─────────────────────────────────
    // PlayOneShot 계열의 볼륨에 곱해지는 전역 계수. 1 = 평소, 0 = 완전 무음.
    // EavesdropAttenuator 가 플레이어와 문틈 사이 거리로 이 값을 흔든다.
    // ⚠ 씬을 벗어날 때 반드시 ResetMuffle()을 부를 것. 안 그러면 다음 씬까지 소리가 죽는다.
    public static float MuffleFactor { get; private set; } = 1f;

    public static void SetMuffle(float factor) => MuffleFactor = Mathf.Clamp01(factor);
    public static void ResetMuffle()           => MuffleFactor = 1f;

    /// <summary>
    /// BGM 감쇠 배율 0~1 (1 = 그대로). 다른 소리를 앞세워야 할 때 BGM 만 눌러 둔다.
    ///
    /// 집 구간 탈출 압박이 이걸 쓴다 — F-6 문단 732 「집 저음 레이어 … 4차에서 최대.
    /// <b>그 외 BGM은 낮춘다</b>」. 드론이 커지는 만큼 BGM 이 물러나야 저음이 들린다.
    ///
    /// ⚠ 설정 볼륨과는 별개의 축이다. 플레이어가 조절한 bgmVolume 에 이 값을 곱한다.
    /// ⚠ 전역 상태이므로 켠 쪽이 반드시 되돌려야 한다. 압박 해제·실패 처리에서 1 로 복원한다.
    /// </summary>
    public static float BgmDuck
    {
        get => _bgmDuck;
        set
        {
            float v = Mathf.Clamp01(value);
            if (Mathf.Approximately(v, _bgmDuck)) return;
            _bgmDuck = v;
            ApplyVolume(_bgmSources, (SettingsManager.Instance?.bgmVolume ?? 1f) * _bgmDuck);
        }
    }
    static float _bgmDuck = 1f;

    // ── 등록 / 해제 API ──────────────────────────────────────────────────
    public static void RegisterSFX(AudioSource src)         => Register(_sfxSources,         src, () => SettingsManager.Instance?.sfxVolume         ?? 1f);
    public static void UnregisterSFX(AudioSource src)       => _sfxSources.Remove(src);
    public static void RegisterBGM(AudioSource src)         => Register(_bgmSources,         src, () => (SettingsManager.Instance?.bgmVolume ?? 1f) * _bgmDuck);
    public static void UnregisterBGM(AudioSource src)       => _bgmSources.Remove(src);
    public static void RegisterVoice(AudioSource src)       => Register(_voiceSources,       src, () => SettingsManager.Instance?.voiceVolume       ?? 1f);
    public static void UnregisterVoice(AudioSource src)     => _voiceSources.Remove(src);
    public static void RegisterAmbient(AudioSource src)     => Register(_ambientSources,     src, () => SettingsManager.Instance?.ambientVolume     ?? 1f);
    public static void UnregisterAmbient(AudioSource src)   => _ambientSources.Remove(src);
    public static void RegisterClicking(AudioSource src)    => Register(_clickingSources,    src, () => SettingsManager.Instance?.clickingVolume    ?? 1f);
    public static void UnregisterClicking(AudioSource src)  => _clickingSources.Remove(src);
    public static void RegisterGlitchNoise(AudioSource src) => Register(_glitchNoiseSources, src, () => SettingsManager.Instance?.glitchNoiseVolume ?? 1f);
    public static void UnregisterGlitchNoise(AudioSource src) => _glitchNoiseSources.Remove(src);

    // ── 루프 재생 API ─────────────────────────────────────────────────────
    /// <summary>루프 사운드를 시작한다. 이미 재생 중이면 무시.</summary>
    public void PlayLoop(string soundName)
    {
        if (_loopSources.ContainsKey(soundName)) return;
        if (!TryGetClip(soundName, out var clip)) return;

        var src   = gameObject.AddComponent<AudioSource>();
        src.clip   = clip;
        src.loop   = true;
        src.volume = GetVolumeForCategory(GetCategory(soundName));
        src.Play();

        _loopSources[soundName] = src;
        RegisterToCategory(GetCategory(soundName), src);
    }

    /// <summary>루프 사운드를 정지하고 AudioSource를 제거한다.</summary>
    public void StopLoop(string soundName)
    {
        if (!_loopSources.TryGetValue(soundName, out var src)) return;
        if (src != null)
        {
            UnregisterFromCategory(GetCategory(soundName), src);
            src.Stop();
            Destroy(src);
        }
        _loopSources.Remove(soundName);
    }

    /// <summary>모든 루프 사운드를 정지한다.</summary>
    public void StopAllLoops()
    {
        foreach (var kv in _loopSources)
        {
            if (kv.Value == null) continue;
            UnregisterFromCategory(GetCategory(kv.Key), kv.Value);
            kv.Value.Stop();
            Destroy(kv.Value);
        }
        _loopSources.Clear();
    }

    // ── 볼륨별 PlayOneShot ─────────────────────────────────────────────────
    /// <summary>딱딱 소리 효과음 재생. 접근성 설정 비활성화 시 무음.</summary>
    public void PlayClicking(string soundName)
    {
        if (SettingsManager.Instance?.clickingSoundDisabled ?? false) return;
        if (!TryGetClip(soundName, out var clip)) return;
        float vol = (SettingsManager.Instance?.sfxVolume ?? 1f)
                  * (SettingsManager.Instance?.clickingVolume ?? 1f) * MuffleFactor;
        audioSource.PlayOneShot(clip, vol);
    }

    /// <inheritdoc cref="PlayClicking(string)"/>
    public void PlayClicking(AudioClip clip)
    {
        if (SettingsManager.Instance?.clickingSoundDisabled ?? false) return;
        if (clip == null) return;
        float vol = (SettingsManager.Instance?.sfxVolume ?? 1f)
                  * (SettingsManager.Instance?.clickingVolume ?? 1f) * MuffleFactor;
        audioSource.PlayOneShot(clip, vol);
    }

    /// <summary>글리치 노이즈 재생.</summary>
    public void PlayGlitchNoise(string soundName)
    {
        if (!TryGetClip(soundName, out var clip)) return;
        float vol = (SettingsManager.Instance?.sfxVolume ?? 1f)
                  * (SettingsManager.Instance?.glitchNoiseVolume ?? 1f);
        audioSource.PlayOneShot(clip, vol);
    }

    /// <inheritdoc cref="PlayGlitchNoise(string)"/>
    public void PlayGlitchNoise(AudioClip clip)
    {
        if (clip == null) return;
        float vol = (SettingsManager.Instance?.sfxVolume ?? 1f)
                  * (SettingsManager.Instance?.glitchNoiseVolume ?? 1f);
        audioSource.PlayOneShot(clip, vol);
    }

    // ── 라이프사이클 ─────────────────────────────────────────────────────
    protected override void OnAwake()
    {
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        BuildLookup();

        _onSFXVolume         = v => ApplyVolume(_sfxSources,         v);
        _onVoiceVolume       = v => ApplyVolume(_voiceSources,       v);
        _onAmbientVolume     = v => ApplyVolume(_ambientSources,     v);
        _onClickingVolume    = v => ApplyVolume(_clickingSources,    v);
        _onGlitchNoiseVolume = v => ApplyVolume(_glitchNoiseSources, v);
    }

    void OnEnable()
    {
        SettingsManager.OnBGMVolumeChanged         += ApplyBGMVolume;
        SettingsManager.OnSFXVolumeChanged         += _onSFXVolume;
        SettingsManager.OnVoiceVolumeChanged       += _onVoiceVolume;
        SettingsManager.OnAmbientVolumeChanged     += _onAmbientVolume;
        SettingsManager.OnClickingVolumeChanged    += _onClickingVolume;
        SettingsManager.OnGlitchNoiseVolumeChanged += _onGlitchNoiseVolume;
    }

    void OnDisable()
    {
        SettingsManager.OnBGMVolumeChanged         -= ApplyBGMVolume;
        SettingsManager.OnSFXVolumeChanged         -= _onSFXVolume;
        SettingsManager.OnVoiceVolumeChanged       -= _onVoiceVolume;
        SettingsManager.OnAmbientVolumeChanged     -= _onAmbientVolume;
        SettingsManager.OnClickingVolumeChanged    -= _onClickingVolume;
        SettingsManager.OnGlitchNoiseVolumeChanged -= _onGlitchNoiseVolume;
    }

    // ── 정지 ─────────────────────────────────────────────────────────────
    public void StopAllBGM()
    {
        _bgmSources.RemoveAll(s => s == null);
        foreach (var src in _bgmSources) src.Stop();
    }

    // ── 재생 ─────────────────────────────────────────────────────────────
    /// <summary>등록된 이름의 효과음을 PlayOneShot으로 재생한다.</summary>
    public void Play(string soundName)
    {
        if (!TryGetClip(soundName, out var clip)) return;
        float vol = (SettingsManager.Instance?.sfxVolume ?? 1f) * MuffleFactor;
        audioSource.PlayOneShot(clip, vol);
    }

    /// <summary>AudioClip을 직접 전달해 재생한다.</summary>
    public void Play(AudioClip clip)
    {
        if (clip == null) return;
        float vol = (SettingsManager.Instance?.sfxVolume ?? 1f) * MuffleFactor;
        audioSource.PlayOneShot(clip, vol);
    }

    // ── 내부 ─────────────────────────────────────────────────────────────
    // 설정에서 BGM 볼륨을 바꿔도 감쇠(BgmDuck)는 유지된다.
    void ApplyBGMVolume(float vol) => ApplyVolume(_bgmSources, vol * _bgmDuck);

    static void ApplyVolume(List<AudioSource> list, float vol)
    {
        list.RemoveAll(s => s == null);
        foreach (var src in list) src.volume = vol;
    }

    static void Register(List<AudioSource> list, AudioSource src, System.Func<float> getVol)
    {
        if (src == null || list.Contains(src)) return;
        list.Add(src);
        src.volume = getVol();
    }

    private VolumeCategory GetCategory(string soundName)
    {
        if (_entryLookup != null && _entryLookup.TryGetValue(soundName, out var entry))
            return entry.category;
        return VolumeCategory.SFX;
    }

    private float GetVolumeForCategory(VolumeCategory cat) => cat switch
    {
        VolumeCategory.BGM         => SettingsManager.Instance?.bgmVolume         ?? 1f,
        VolumeCategory.Voice       => SettingsManager.Instance?.voiceVolume       ?? 1f,
        VolumeCategory.Ambient     => SettingsManager.Instance?.ambientVolume     ?? 1f,
        VolumeCategory.Clicking    => SettingsManager.Instance?.clickingVolume    ?? 1f,
        VolumeCategory.GlitchNoise => SettingsManager.Instance?.glitchNoiseVolume ?? 1f,
        _                          => SettingsManager.Instance?.sfxVolume         ?? 1f,
    };

    private void RegisterToCategory(VolumeCategory cat, AudioSource src)
    {
        switch (cat)
        {
            case VolumeCategory.SFX:         RegisterSFX(src);         break;
            case VolumeCategory.BGM:         RegisterBGM(src);         break;
            case VolumeCategory.Voice:       RegisterVoice(src);       break;
            case VolumeCategory.Ambient:     RegisterAmbient(src);     break;
            case VolumeCategory.Clicking:    RegisterClicking(src);    break;
            case VolumeCategory.GlitchNoise: RegisterGlitchNoise(src); break;
        }
    }

    private void UnregisterFromCategory(VolumeCategory cat, AudioSource src)
    {
        switch (cat)
        {
            case VolumeCategory.SFX:         UnregisterSFX(src);         break;
            case VolumeCategory.BGM:         UnregisterBGM(src);         break;
            case VolumeCategory.Voice:       UnregisterVoice(src);       break;
            case VolumeCategory.Ambient:     UnregisterAmbient(src);     break;
            case VolumeCategory.Clicking:    UnregisterClicking(src);    break;
            case VolumeCategory.GlitchNoise: UnregisterGlitchNoise(src); break;
        }
    }

    /// <summary>
    /// 그 이름이 등록되어 있는지. 경고를 찍지 않고 조용히 확인한다.
    ///
    /// 에셋이 아직 없는 소리에 절차 생성 폴백을 붙일 때 쓴다 — 등록되면 자동으로 그쪽을 타고,
    /// 없는 동안에도 채널이 비지 않는다. 이름을 지어내는 것과는 다르다(CLAUDE.md §0-4).
    /// </summary>
    public bool HasSound(string soundName)
        => !string.IsNullOrEmpty(soundName) && _lookup != null && _lookup.ContainsKey(soundName);

    bool TryGetClip(string soundName, out AudioClip clip)
    {
        if (_lookup != null && _lookup.TryGetValue(soundName, out clip)) return true;
        Debug.LogWarning($"[AudioManager] 등록되지 않은 사운드: '{soundName}'");
        clip = null;
        return false;
    }

    void BuildLookup()
    {
        _lookup      = new Dictionary<string, AudioClip>();
        _entryLookup = new Dictionary<string, SoundEntry>();
        if (sounds == null) return;
        foreach (var entry in sounds)
        {
            if (string.IsNullOrEmpty(entry.name) || entry.clip == null) continue;
            if (!_lookup.ContainsKey(entry.name))
            {
                _lookup[entry.name]      = entry.clip;
                _entryLookup[entry.name] = entry;
            }
            else
                Debug.LogWarning($"[AudioManager] 중복 이름 무시됨: '{entry.name}'");
        }
    }
}
