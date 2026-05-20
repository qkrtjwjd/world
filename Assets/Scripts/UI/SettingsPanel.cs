using UnityEngine;
using UnityEngine.UI;

public class SettingsPanel : MonoBehaviour
{
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;

    void OnEnable()
    {
        var sm = SettingsManager.Instance;
        if (sm == null) return;
        if (volumeSlider != null) volumeSlider.value = sm.masterVolume;
        if (bgmSlider    != null) bgmSlider.value    = sm.bgmVolume;
        if (sfxSlider    != null) sfxSlider.value    = sm.sfxVolume;
    }

    public void OnVolumeChanged(float value)    => SettingsManager.Instance?.SetMasterVolume(value);
    public void OnBGMVolumeChanged(float value) => SettingsManager.Instance?.SetBGMVolume(value);
    public void OnSFXVolumeChanged(float value) => SettingsManager.Instance?.SetSFXVolume(value);
    public void OnLanguageKO() => SettingsManager.Instance?.SetLanguage(LocalizationManager.Language.KO);
    public void OnLanguageEN() => SettingsManager.Instance?.SetLanguage(LocalizationManager.Language.EN);
    public void OnLanguageJP() => SettingsManager.Instance?.SetLanguage(LocalizationManager.Language.JP);
}
