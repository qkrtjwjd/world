using UnityEngine;

/// <summary>
/// 볼륨·언어 설정 관리 싱글톤 (DontDestroyOnLoad 자동 생성).
/// Inspector 배치 없이 SettingsManager.Instance 로 접근 가능.
/// </summary>
public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("SettingsManager [Auto]");
                _instance = go.AddComponent<SettingsManager>();
            }
            return _instance;
        }
    }
    static SettingsManager _instance;

    const string KEY_VOLUME     = "Settings_MasterVolume";
    const string KEY_BGM_VOLUME = "Settings_BGMVolume";
    const string KEY_SFX_VOLUME = "Settings_SFXVolume";
    const string KEY_LANGUAGE   = "Settings_Language";

    public float masterVolume = 1f;
    public float bgmVolume    = 1f;
    public float sfxVolume    = 1f;
    public LocalizationManager.Language language = LocalizationManager.Language.KO;

    public static System.Action<float> OnBGMVolumeChanged;
    public static System.Action<float> OnSFXVolumeChanged;

    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSettings();
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        LocalizationManager.Instance?.ChangeLanguage(language);
    }

    public void SetMasterVolume(float v)
    {
        masterVolume = Mathf.Clamp01(v);
        AudioListener.volume = masterVolume;
        PlayerPrefs.SetFloat(KEY_VOLUME, masterVolume);
    }

    public void SetBGMVolume(float v)
    {
        bgmVolume = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(KEY_BGM_VOLUME, bgmVolume);
        OnBGMVolumeChanged?.Invoke(bgmVolume);
    }

    public void SetSFXVolume(float v)
    {
        sfxVolume = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(KEY_SFX_VOLUME, sfxVolume);
        OnSFXVolumeChanged?.Invoke(sfxVolume);
    }

    public void SetLanguage(LocalizationManager.Language lang)
    {
        language = lang;
        LocalizationManager.Instance?.ChangeLanguage(lang);
        PlayerPrefs.SetInt(KEY_LANGUAGE, (int)lang);
    }

    void LoadSettings()
    {
        masterVolume = PlayerPrefs.GetFloat(KEY_VOLUME, 1f);
        bgmVolume    = PlayerPrefs.GetFloat(KEY_BGM_VOLUME, 1f);
        sfxVolume    = PlayerPrefs.GetFloat(KEY_SFX_VOLUME, 1f);
        AudioListener.volume = masterVolume;
        language = (LocalizationManager.Language)PlayerPrefs.GetInt(KEY_LANGUAGE, 0);
    }
}
