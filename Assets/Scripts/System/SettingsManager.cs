using UnityEngine;

/// <summary>
/// 게임 전체 설정 관리 싱글톤 (DontDestroyOnLoad 자동 생성).
/// Inspector 배치 없이 SettingsManager.Instance 로 접근 가능.
/// 모든 설정은 PlayerPrefs에 저장되며 게임 시작 시 자동 복원된다.
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

    // ─── PlayerPrefs 키 ────────────────────────────────────────────────────

    // 사운드
    const string KEY_MASTER_VOLUME      = "Settings_MasterVolume";
    const string KEY_BGM_VOLUME         = "Settings_BGMVolume";
    const string KEY_SFX_VOLUME         = "Settings_SFXVolume";
    const string KEY_VOICE_VOLUME       = "Settings_VoiceVolume";
    const string KEY_AMBIENT_VOLUME     = "Settings_AmbientVolume";
    const string KEY_CLICKING_VOLUME    = "Settings_ClickingVolume";
    const string KEY_GLITCH_NOISE_VOL   = "Settings_GlitchNoiseVolume";
    const string KEY_MUTE_UNFOCUSED     = "Settings_MuteWhenUnfocused";

    // 화면
    const string KEY_FULLSCREEN         = "Settings_Fullscreen";
    const string KEY_DISPLAY_MODE       = "Settings_DisplayMode";
    const string KEY_FRAMERATE_CAP      = "Settings_FrameRateCap";
    const string KEY_RESOLUTION_W       = "Settings_ResolutionWidth";
    const string KEY_RESOLUTION_H       = "Settings_ResolutionHeight";
    const string KEY_VSYNC              = "Settings_VSync";
    const string KEY_BRIGHTNESS         = "Settings_Brightness";
    const string KEY_SATURATION         = "Settings_Saturation";
    const string KEY_GLITCH_INTENSITY   = "Settings_GlitchIntensity";
    const string KEY_CAMERA_SHAKE       = "Settings_CameraShake";
    const string KEY_SCREEN_EDGE        = "Settings_ScreenEdgeEffect";

    // 조작
    const string KEY_KEY_INTERACT       = "Settings_Key_Interact";
    const string KEY_KEY_INVENTORY      = "Settings_Key_Inventory";
    const string KEY_KEY_DAGGER         = "Settings_Key_Dagger";
    const string KEY_KEY_PAUSE          = "Settings_Key_Pause";
    const string KEY_KEY_SKIP           = "Settings_Key_DialogueSkip";
    const string KEY_KEY_LOG            = "Settings_Key_DialogueLog";
    const string KEY_KEY_QUICKSAVE      = "Settings_Key_QuickSave";

    // 접근성
    const string KEY_COLORBLIND_MODE    = "Settings_ColorblindMode";
    const string KEY_GLITCH_DISABLED    = "Settings_GlitchDisabled";
    const string KEY_FLASH_DISABLED     = "Settings_FlashDisabled";
    const string KEY_CLICKING_DISABLED  = "Settings_ClickingSoundDisabled";
    const string KEY_DIALOGUE_SPEED     = "Settings_DialogueSpeed";
    const string KEY_AUTO_DIALOGUE      = "Settings_AutoDialogue";
    const string KEY_TEXT_SIZE          = "Settings_TextSize";
    const string KEY_TEXT_BG_OPACITY    = "Settings_TextBgOpacity";
    const string KEY_INPUT_REVERSE_ALERT= "Settings_InputReverseAlert";

    // 저장
    const string KEY_AUTO_SAVE          = "Settings_AutoSave";

    // 언어
    const string KEY_LANGUAGE           = "Settings_Language";
    const string KEY_DIALOGUE_LANGUAGE  = "Settings_DialogueLanguage";

    // 게임플레이
    const string KEY_TUTORIAL_HINTS     = "Settings_TutorialHints";
    const string KEY_OBJECTIVE_UI       = "Settings_ObjectiveUI";
    const string KEY_DOLL_GAUGE         = "Settings_DollGauge";
    const string KEY_FANTASY_GAUGE      = "Settings_FantasyGauge";
    const string KEY_COMBAT_MODE_AUTO   = "Settings_CombatModeAuto";
    const string KEY_DIALOGUE_LOG       = "Settings_DialogueLog";

    // ─── 필드 ──────────────────────────────────────────────────────────────

    // 🔊 사운드
    public float masterVolume      = 1f;
    public float bgmVolume         = 1f;
    public float sfxVolume         = 1f;
    public float voiceVolume       = 1f;
    public float ambientVolume     = 1f;
    public float clickingVolume    = 1f;
    public float glitchNoiseVolume = 1f;
    public bool  muteWhenUnfocused = false;

    // 🖥️ 화면
    public bool  fullscreen              = true;
    public int   displayMode             = 1;   // 0=전체화면(Exclusive), 1=테두리 없는 창(Borderless), 2=창모드(Windowed)
    public int   frameRateCap            = 0;   // 0=무제한, 그 외 목표 FPS
    public int   resolutionWidth         = 0;   // 0=미저장(현재 해상도 유지)
    public int   resolutionHeight        = 0;
    public bool  vsync                   = true;
    public float brightness              = 0.5f;
    public float saturation              = 1f;
    public float glitchEffectIntensity   = 1f;
    public bool  cameraShakeEnabled      = true;
    public bool  screenEdgeEffectEnabled = true;

    // ⌨️ 조작
    public KeyCode keyInteract     = KeyCode.E;
    public KeyCode keyInventory    = KeyCode.I;
    public KeyCode keyDagger       = KeyCode.F;
    public KeyCode keyPause        = KeyCode.Escape;
    public KeyCode keyDialogueSkip = KeyCode.Space;
    public KeyCode keyDialogueLog  = KeyCode.L;
    public KeyCode keyQuickSave    = KeyCode.F5;

    // ♿ 접근성
    public int   colorblindMode          = 0;  // 0=없음, 1=적록(1형), 2=적록(2형), 3=청황
    public bool  glitchEffectDisabled    = false;
    public bool  flashEffectDisabled     = false;
    public bool  clickingSoundDisabled   = false;
    public float dialogueSpeed           = 1f;
    public bool  autoDialogue            = false;
    public int   textSize                = 1;  // 0=소, 1=중, 2=대
    public float textBgOpacity           = 0.5f;
    public bool  showInputReverseAlert   = true;

    // 💾 저장
    public bool autoSaveEnabled = true;

    // 🌐 언어
    public LocalizationManager.Language language         = LocalizationManager.Language.KO;
    public LocalizationManager.Language dialogueLanguage = LocalizationManager.Language.KO;

    // 📋 게임플레이
    public bool showTutorialHints      = true;
    public bool showObjectiveUI        = true;
    public bool showDollificationGauge = true;
    public bool showFantasyRealityGauge= true;
    public bool combatModeAuto         = false;
    public bool showDialogueLog        = true;

    // ─── 이벤트 ────────────────────────────────────────────────────────────

    // 사운드
    public static System.Action<float> OnBGMVolumeChanged;
    public static System.Action<float> OnSFXVolumeChanged;
    public static System.Action<float> OnVoiceVolumeChanged;
    public static System.Action<float> OnAmbientVolumeChanged;
    public static System.Action<float> OnClickingVolumeChanged;
    public static System.Action<float> OnGlitchNoiseVolumeChanged;

    // 화면
    public static System.Action<bool>  OnFullscreenChanged;
    public static System.Action<int>   OnDisplayModeChanged;
    public static System.Action<float> OnBrightnessChanged;
    public static System.Action<float> OnSaturationChanged;
    public static System.Action<float> OnGlitchIntensityChanged;
    public static System.Action<bool>  OnCameraShakeChanged;
    public static System.Action<bool>  OnScreenEdgeEffectChanged;

    // 조작
    public static System.Action        OnKeyBindingsChanged;

    // 접근성
    public static System.Action<int>   OnColorblindModeChanged;
    public static System.Action<bool>  OnGlitchDisabledChanged;
    public static System.Action<bool>  OnFlashDisabledChanged;
    public static System.Action<bool>  OnClickingSoundDisabledChanged;
    public static System.Action<float> OnDialogueSpeedChanged;
    public static System.Action<bool>  OnAutoDialogueChanged;
    public static System.Action<int>   OnTextSizeChanged;
    public static System.Action<float> OnTextBgOpacityChanged;
    public static System.Action<bool>  OnShowInputReverseAlertChanged;

    // 저장
    public static System.Action<bool>  OnAutoSaveChanged;

    // 언어
    public static System.Action<LocalizationManager.Language> OnDialogueLanguageChanged;

    // 게임플레이
    public static System.Action<bool>  OnShowObjectiveUIChanged;
    public static System.Action<bool>  OnShowDollificationGaugeChanged;
    public static System.Action<bool>  OnShowFantasyRealityGaugeChanged;
    public static System.Action<bool>  OnCombatModeAutoChanged;
    public static System.Action<bool>  OnShowTutorialHintsChanged;
    public static System.Action<bool>  OnShowDialogueLogChanged;

    // ─── 라이프사이클 ──────────────────────────────────────────────────────

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
        var lm = LocalizationManager.Instance;
        if (lm != null && lm.currentLanguage != language)
            lm.ChangeLanguage(language);
        ApplyScreenSettings();
    }

    /// <summary>비활성 창 음소거 옵션 처리. 포커스 상실 시 무음, 복귀 시 마스터 볼륨으로 복원.</summary>
    void OnApplicationFocus(bool hasFocus)
    {
        if (!muteWhenUnfocused) return;
        AudioListener.volume = hasFocus ? masterVolume : 0f;
    }

    // ─── 🔊 사운드 세터 ────────────────────────────────────────────────────

    public void SetMasterVolume(float v)
    {
        masterVolume = Mathf.Clamp01(v);
        AudioListener.volume = masterVolume;
        PlayerPrefs.SetFloat(KEY_MASTER_VOLUME, masterVolume);
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

    public void SetVoiceVolume(float v)
    {
        voiceVolume = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(KEY_VOICE_VOLUME, voiceVolume);
        OnVoiceVolumeChanged?.Invoke(voiceVolume);
    }

    public void SetAmbientVolume(float v)
    {
        ambientVolume = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(KEY_AMBIENT_VOLUME, ambientVolume);
        OnAmbientVolumeChanged?.Invoke(ambientVolume);
    }

    public void SetClickingVolume(float v)
    {
        clickingVolume = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(KEY_CLICKING_VOLUME, clickingVolume);
        OnClickingVolumeChanged?.Invoke(clickingVolume);
    }

    public void SetGlitchNoiseVolume(float v)
    {
        glitchNoiseVolume = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(KEY_GLITCH_NOISE_VOL, glitchNoiseVolume);
        OnGlitchNoiseVolumeChanged?.Invoke(glitchNoiseVolume);
    }

    public void SetMuteWhenUnfocused(bool v)
    {
        muteWhenUnfocused = v;
        PlayerPrefs.SetInt(KEY_MUTE_UNFOCUSED, v ? 1 : 0);
        // 옵션을 끄면 즉시 마스터 볼륨 복원 (포커스 없는 상태에서 껐을 때 대비)
        if (!v) AudioListener.volume = masterVolume;
    }

    // ─── 🖥️ 화면 세터 ──────────────────────────────────────────────────────

    public void SetFullscreen(bool v)
    {
        fullscreen = v;
        Screen.fullScreen = v;
        PlayerPrefs.SetInt(KEY_FULLSCREEN, v ? 1 : 0);
        OnFullscreenChanged?.Invoke(v);
    }

    /// <summary>0=전체화면, 1=테두리 없는 창, 2=창모드. fullscreen bool 도 동기화 유지(하위호환).</summary>
    public void SetDisplayMode(int mode)
    {
        displayMode = Mathf.Clamp(mode, 0, 2);
        Screen.fullScreenMode = displayMode switch
        {
            0 => FullScreenMode.ExclusiveFullScreen,
            1 => FullScreenMode.FullScreenWindow,
            _ => FullScreenMode.Windowed
        };
        fullscreen = displayMode != 2;
        PlayerPrefs.SetInt(KEY_DISPLAY_MODE, displayMode);
        PlayerPrefs.SetInt(KEY_FULLSCREEN, fullscreen ? 1 : 0);
        OnDisplayModeChanged?.Invoke(displayMode);
        OnFullscreenChanged?.Invoke(fullscreen);
    }

    /// <summary>0=무제한, 그 외 목표 FPS. VSync ON 시 실제로는 무시된다.</summary>
    public void SetFrameRateCap(int fps)
    {
        frameRateCap = Mathf.Max(0, fps);
        Application.targetFrameRate = frameRateCap == 0 ? -1 : frameRateCap;
        PlayerPrefs.SetInt(KEY_FRAMERATE_CAP, frameRateCap);
    }

    public void SetResolution(int w, int h)
    {
        if (w <= 0 || h <= 0) return;
        resolutionWidth  = w;
        resolutionHeight = h;
        Screen.SetResolution(w, h, Screen.fullScreenMode);
        PlayerPrefs.SetInt(KEY_RESOLUTION_W, w);
        PlayerPrefs.SetInt(KEY_RESOLUTION_H, h);
    }

    public void SetVSync(bool v)
    {
        vsync = v;
        QualitySettings.vSyncCount = v ? 1 : 0;
        PlayerPrefs.SetInt(KEY_VSYNC, v ? 1 : 0);
    }

    public void SetBrightness(float v)
    {
        brightness = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(KEY_BRIGHTNESS, brightness);
        OnBrightnessChanged?.Invoke(brightness);
    }

    public void SetSaturation(float v)
    {
        saturation = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(KEY_SATURATION, saturation);
        OnSaturationChanged?.Invoke(saturation);
    }

    public void SetGlitchEffectIntensity(float v)
    {
        glitchEffectIntensity = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(KEY_GLITCH_INTENSITY, glitchEffectIntensity);
        OnGlitchIntensityChanged?.Invoke(glitchEffectIntensity);
    }

    public void SetCameraShake(bool v)
    {
        cameraShakeEnabled = v;
        PlayerPrefs.SetInt(KEY_CAMERA_SHAKE, v ? 1 : 0);
        OnCameraShakeChanged?.Invoke(v);
    }

    public void SetScreenEdgeEffect(bool v)
    {
        screenEdgeEffectEnabled = v;
        PlayerPrefs.SetInt(KEY_SCREEN_EDGE, v ? 1 : 0);
        OnScreenEdgeEffectChanged?.Invoke(v);
    }

    // ─── ⌨️ 조작 세터 ──────────────────────────────────────────────────────

    public void SetKeyInteract(KeyCode k)     { keyInteract     = k; PlayerPrefs.SetInt(KEY_KEY_INTERACT,  (int)k); OnKeyBindingsChanged?.Invoke(); }
    public void SetKeyInventory(KeyCode k)    { keyInventory    = k; PlayerPrefs.SetInt(KEY_KEY_INVENTORY, (int)k); OnKeyBindingsChanged?.Invoke(); }
    public void SetKeyDagger(KeyCode k)       { keyDagger       = k; PlayerPrefs.SetInt(KEY_KEY_DAGGER,    (int)k); OnKeyBindingsChanged?.Invoke(); }
    public void SetKeyPause(KeyCode k)        { keyPause        = k; PlayerPrefs.SetInt(KEY_KEY_PAUSE,     (int)k); OnKeyBindingsChanged?.Invoke(); }
    public void SetKeyDialogueSkip(KeyCode k) { keyDialogueSkip = k; PlayerPrefs.SetInt(KEY_KEY_SKIP,      (int)k); OnKeyBindingsChanged?.Invoke(); }
    public void SetKeyDialogueLog(KeyCode k)  { keyDialogueLog  = k; PlayerPrefs.SetInt(KEY_KEY_LOG,       (int)k); OnKeyBindingsChanged?.Invoke(); }
    public void SetKeyQuickSave(KeyCode k)    { keyQuickSave    = k; PlayerPrefs.SetInt(KEY_KEY_QUICKSAVE, (int)k); OnKeyBindingsChanged?.Invoke(); }

    /// <summary>모든 키 바인딩을 기본값으로 되돌린다. (조작 탭 '기본값 복원')</summary>
    public void ResetKeyBindings()
    {
        keyInteract     = KeyCode.E;
        keyInventory    = KeyCode.I;
        keyDagger       = KeyCode.F;
        keyPause        = KeyCode.Escape;
        keyDialogueSkip = KeyCode.Space;
        keyDialogueLog  = KeyCode.L;
        keyQuickSave    = KeyCode.F5;

        PlayerPrefs.SetInt(KEY_KEY_INTERACT,  (int)keyInteract);
        PlayerPrefs.SetInt(KEY_KEY_INVENTORY, (int)keyInventory);
        PlayerPrefs.SetInt(KEY_KEY_DAGGER,    (int)keyDagger);
        PlayerPrefs.SetInt(KEY_KEY_PAUSE,     (int)keyPause);
        PlayerPrefs.SetInt(KEY_KEY_SKIP,      (int)keyDialogueSkip);
        PlayerPrefs.SetInt(KEY_KEY_LOG,       (int)keyDialogueLog);
        PlayerPrefs.SetInt(KEY_KEY_QUICKSAVE, (int)keyQuickSave);

        OnKeyBindingsChanged?.Invoke();
    }

    // ─── ♿ 접근성 세터 ─────────────────────────────────────────────────────

    /// <summary>0=없음, 1=적록(1형/Protanopia), 2=적록(2형/Deuteranopia), 3=청황(Tritanopia).</summary>
    public void SetColorblindMode(int mode)
    {
        colorblindMode = Mathf.Clamp(mode, 0, 3);
        PlayerPrefs.SetInt(KEY_COLORBLIND_MODE, colorblindMode);
        OnColorblindModeChanged?.Invoke(colorblindMode);
    }

    public void SetGlitchEffectDisabled(bool v)
    {
        glitchEffectDisabled = v;
        PlayerPrefs.SetInt(KEY_GLITCH_DISABLED, v ? 1 : 0);
        OnGlitchDisabledChanged?.Invoke(v);
    }

    public void SetFlashEffectDisabled(bool v)
    {
        flashEffectDisabled = v;
        PlayerPrefs.SetInt(KEY_FLASH_DISABLED, v ? 1 : 0);
        OnFlashDisabledChanged?.Invoke(v);
    }

    public void SetClickingSoundDisabled(bool v)
    {
        clickingSoundDisabled = v;
        PlayerPrefs.SetInt(KEY_CLICKING_DISABLED, v ? 1 : 0);
        OnClickingSoundDisabledChanged?.Invoke(v);
    }

    public void SetDialogueSpeed(float v)
    {
        dialogueSpeed = Mathf.Clamp(v, 0.25f, 3f);
        PlayerPrefs.SetFloat(KEY_DIALOGUE_SPEED, dialogueSpeed);
        OnDialogueSpeedChanged?.Invoke(dialogueSpeed);
    }

    public void SetAutoDialogue(bool v)
    {
        autoDialogue = v;
        PlayerPrefs.SetInt(KEY_AUTO_DIALOGUE, v ? 1 : 0);
        OnAutoDialogueChanged?.Invoke(v);
    }

    public void SetTextSize(int v)
    {
        textSize = Mathf.Clamp(v, 0, 2);
        PlayerPrefs.SetInt(KEY_TEXT_SIZE, textSize);
        OnTextSizeChanged?.Invoke(textSize);
    }

    public void SetTextBgOpacity(float v)
    {
        textBgOpacity = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(KEY_TEXT_BG_OPACITY, textBgOpacity);
        OnTextBgOpacityChanged?.Invoke(textBgOpacity);
    }

    public void SetShowInputReverseAlert(bool v)
    {
        showInputReverseAlert = v;
        PlayerPrefs.SetInt(KEY_INPUT_REVERSE_ALERT, v ? 1 : 0);
        OnShowInputReverseAlertChanged?.Invoke(v);
    }

    // ─── 💾 저장 세터 ──────────────────────────────────────────────────────

    public void SetAutoSave(bool v)
    {
        autoSaveEnabled = v;
        PlayerPrefs.SetInt(KEY_AUTO_SAVE, v ? 1 : 0);
        OnAutoSaveChanged?.Invoke(v);
    }

    // ─── 🌐 언어 세터 ──────────────────────────────────────────────────────

    public void SetLanguage(LocalizationManager.Language lang)
    {
        language = lang;
        LocalizationManager.Instance?.ChangeLanguage(lang);
        PlayerPrefs.SetInt(KEY_LANGUAGE, (int)lang);
    }

    public void SetDialogueLanguage(LocalizationManager.Language lang)
    {
        dialogueLanguage = lang;
        PlayerPrefs.SetInt(KEY_DIALOGUE_LANGUAGE, (int)lang);
        OnDialogueLanguageChanged?.Invoke(lang);
    }

    // ─── 📋 게임플레이 세터 ────────────────────────────────────────────────

    public void SetShowTutorialHints(bool v)
    {
        showTutorialHints = v;
        PlayerPrefs.SetInt(KEY_TUTORIAL_HINTS, v ? 1 : 0);
        OnShowTutorialHintsChanged?.Invoke(v);
    }

    public void SetShowObjectiveUI(bool v)
    {
        showObjectiveUI = v;
        PlayerPrefs.SetInt(KEY_OBJECTIVE_UI, v ? 1 : 0);
        OnShowObjectiveUIChanged?.Invoke(v);
    }

    public void SetShowDollificationGauge(bool v)
    {
        showDollificationGauge = v;
        PlayerPrefs.SetInt(KEY_DOLL_GAUGE, v ? 1 : 0);
        OnShowDollificationGaugeChanged?.Invoke(v);
    }

    public void SetShowFantasyRealityGauge(bool v)
    {
        showFantasyRealityGauge = v;
        PlayerPrefs.SetInt(KEY_FANTASY_GAUGE, v ? 1 : 0);
        OnShowFantasyRealityGaugeChanged?.Invoke(v);
    }

    public void SetCombatModeAuto(bool v)
    {
        combatModeAuto = v;
        PlayerPrefs.SetInt(KEY_COMBAT_MODE_AUTO, v ? 1 : 0);
        OnCombatModeAutoChanged?.Invoke(v);
    }

    public void SetShowDialogueLog(bool v)
    {
        showDialogueLog = v;
        PlayerPrefs.SetInt(KEY_DIALOGUE_LOG, v ? 1 : 0);
        OnShowDialogueLogChanged?.Invoke(v);
    }

    // ─── 데이터 초기화 ─────────────────────────────────────────────────────

    /// <summary>모든 설정을 기본값으로 되돌리고 PlayerPrefs에서 삭제.</summary>
    public void ResetAllSettings()
    {
        PlayerPrefs.DeleteAll();
        LoadSettings();
        ApplyScreenSettings();
        LocalizationManager.Instance?.ChangeLanguage(language);
        RaiseAllEvents();
    }

    /// <summary>현재 필드 값 기준으로 모든 변경 이벤트를 발행한다. 초기화 직후 라이브 시스템 동기화용.</summary>
    void RaiseAllEvents()
    {
        // 사운드
        OnBGMVolumeChanged?.Invoke(bgmVolume);
        OnSFXVolumeChanged?.Invoke(sfxVolume);
        OnVoiceVolumeChanged?.Invoke(voiceVolume);
        OnAmbientVolumeChanged?.Invoke(ambientVolume);
        OnClickingVolumeChanged?.Invoke(clickingVolume);
        OnGlitchNoiseVolumeChanged?.Invoke(glitchNoiseVolume);

        // 화면
        OnFullscreenChanged?.Invoke(fullscreen);
        OnDisplayModeChanged?.Invoke(displayMode);
        OnBrightnessChanged?.Invoke(brightness);
        OnSaturationChanged?.Invoke(saturation);
        OnGlitchIntensityChanged?.Invoke(glitchEffectIntensity);
        OnCameraShakeChanged?.Invoke(cameraShakeEnabled);
        OnScreenEdgeEffectChanged?.Invoke(screenEdgeEffectEnabled);

        // 조작
        OnKeyBindingsChanged?.Invoke();

        // 접근성
        OnColorblindModeChanged?.Invoke(colorblindMode);
        OnGlitchDisabledChanged?.Invoke(glitchEffectDisabled);
        OnFlashDisabledChanged?.Invoke(flashEffectDisabled);
        OnClickingSoundDisabledChanged?.Invoke(clickingSoundDisabled);
        OnDialogueSpeedChanged?.Invoke(dialogueSpeed);
        OnAutoDialogueChanged?.Invoke(autoDialogue);
        OnTextSizeChanged?.Invoke(textSize);
        OnTextBgOpacityChanged?.Invoke(textBgOpacity);
        OnShowInputReverseAlertChanged?.Invoke(showInputReverseAlert);

        // 저장
        OnAutoSaveChanged?.Invoke(autoSaveEnabled);

        // 언어
        OnDialogueLanguageChanged?.Invoke(dialogueLanguage);

        // 게임플레이
        OnShowTutorialHintsChanged?.Invoke(showTutorialHints);
        OnShowObjectiveUIChanged?.Invoke(showObjectiveUI);
        OnShowDollificationGaugeChanged?.Invoke(showDollificationGauge);
        OnShowFantasyRealityGaugeChanged?.Invoke(showFantasyRealityGauge);
        OnCombatModeAutoChanged?.Invoke(combatModeAuto);
        OnShowDialogueLogChanged?.Invoke(showDialogueLog);
    }

    // ─── 내부 ──────────────────────────────────────────────────────────────

    void LoadSettings()
    {
        // 사운드
        masterVolume      = PlayerPrefs.GetFloat(KEY_MASTER_VOLUME,    1f);
        bgmVolume         = PlayerPrefs.GetFloat(KEY_BGM_VOLUME,       1f);
        sfxVolume         = PlayerPrefs.GetFloat(KEY_SFX_VOLUME,       1f);
        voiceVolume       = PlayerPrefs.GetFloat(KEY_VOICE_VOLUME,     1f);
        ambientVolume     = PlayerPrefs.GetFloat(KEY_AMBIENT_VOLUME,   1f);
        clickingVolume    = PlayerPrefs.GetFloat(KEY_CLICKING_VOLUME,  1f);
        glitchNoiseVolume = PlayerPrefs.GetFloat(KEY_GLITCH_NOISE_VOL, 1f);
        muteWhenUnfocused = PlayerPrefs.GetInt(KEY_MUTE_UNFOCUSED, 0) == 1;
        AudioListener.volume = masterVolume;

        // 화면
        fullscreen              = PlayerPrefs.GetInt(KEY_FULLSCREEN,       1) == 1;
        displayMode             = PlayerPrefs.GetInt(KEY_DISPLAY_MODE,     fullscreen ? 1 : 2);
        frameRateCap            = PlayerPrefs.GetInt(KEY_FRAMERATE_CAP,    0);
        resolutionWidth         = PlayerPrefs.GetInt(KEY_RESOLUTION_W,     0);
        resolutionHeight        = PlayerPrefs.GetInt(KEY_RESOLUTION_H,     0);
        vsync                   = PlayerPrefs.GetInt(KEY_VSYNC,            1) == 1;
        brightness              = PlayerPrefs.GetFloat(KEY_BRIGHTNESS,     0.5f);
        saturation              = PlayerPrefs.GetFloat(KEY_SATURATION,     1f);
        glitchEffectIntensity   = PlayerPrefs.GetFloat(KEY_GLITCH_INTENSITY, 1f);
        cameraShakeEnabled      = PlayerPrefs.GetInt(KEY_CAMERA_SHAKE,     1) == 1;
        screenEdgeEffectEnabled = PlayerPrefs.GetInt(KEY_SCREEN_EDGE,      1) == 1;

        // 조작
        keyInteract     = (KeyCode)PlayerPrefs.GetInt(KEY_KEY_INTERACT,  (int)KeyCode.E);
        keyInventory    = (KeyCode)PlayerPrefs.GetInt(KEY_KEY_INVENTORY, (int)KeyCode.I);
        keyDagger       = (KeyCode)PlayerPrefs.GetInt(KEY_KEY_DAGGER,    (int)KeyCode.F);
        keyPause        = (KeyCode)PlayerPrefs.GetInt(KEY_KEY_PAUSE,     (int)KeyCode.Escape);
        keyDialogueSkip = (KeyCode)PlayerPrefs.GetInt(KEY_KEY_SKIP,      (int)KeyCode.Space);
        keyDialogueLog  = (KeyCode)PlayerPrefs.GetInt(KEY_KEY_LOG,       (int)KeyCode.L);
        keyQuickSave    = (KeyCode)PlayerPrefs.GetInt(KEY_KEY_QUICKSAVE, (int)KeyCode.F5);

        // 접근성
        colorblindMode        = PlayerPrefs.GetInt(KEY_COLORBLIND_MODE,   0);
        glitchEffectDisabled  = PlayerPrefs.GetInt(KEY_GLITCH_DISABLED,   0) == 1;
        flashEffectDisabled   = PlayerPrefs.GetInt(KEY_FLASH_DISABLED,    0) == 1;
        clickingSoundDisabled = PlayerPrefs.GetInt(KEY_CLICKING_DISABLED, 0) == 1;
        dialogueSpeed         = PlayerPrefs.GetFloat(KEY_DIALOGUE_SPEED,  1f);
        autoDialogue          = PlayerPrefs.GetInt(KEY_AUTO_DIALOGUE,     0) == 1;
        textSize              = PlayerPrefs.GetInt(KEY_TEXT_SIZE,         1);
        textBgOpacity         = PlayerPrefs.GetFloat(KEY_TEXT_BG_OPACITY, 0.5f);
        showInputReverseAlert = PlayerPrefs.GetInt(KEY_INPUT_REVERSE_ALERT, 1) == 1;

        // 저장
        autoSaveEnabled = PlayerPrefs.GetInt(KEY_AUTO_SAVE, 1) == 1;

        // 언어
        language         = (LocalizationManager.Language)PlayerPrefs.GetInt(KEY_LANGUAGE,          0);
        dialogueLanguage = (LocalizationManager.Language)PlayerPrefs.GetInt(KEY_DIALOGUE_LANGUAGE, 0);

        // 게임플레이
        showTutorialHints       = PlayerPrefs.GetInt(KEY_TUTORIAL_HINTS, 1) == 1;
        showObjectiveUI         = PlayerPrefs.GetInt(KEY_OBJECTIVE_UI,   1) == 1;
        showDollificationGauge  = PlayerPrefs.GetInt(KEY_DOLL_GAUGE,     1) == 1;
        showFantasyRealityGauge = PlayerPrefs.GetInt(KEY_FANTASY_GAUGE,  1) == 1;
        combatModeAuto          = PlayerPrefs.GetInt(KEY_COMBAT_MODE_AUTO, 0) == 1;
        showDialogueLog         = PlayerPrefs.GetInt(KEY_DIALOGUE_LOG,   1) == 1;
    }

    void ApplyScreenSettings()
    {
        Screen.fullScreenMode = displayMode switch
        {
            0 => FullScreenMode.ExclusiveFullScreen,
            1 => FullScreenMode.FullScreenWindow,
            _ => FullScreenMode.Windowed
        };

        // 저장된 해상도가 있으면 복원 (없으면 현재 해상도 유지)
        if (resolutionWidth > 0 && resolutionHeight > 0)
            Screen.SetResolution(resolutionWidth, resolutionHeight, Screen.fullScreenMode);

        QualitySettings.vSyncCount  = vsync ? 1 : 0;
        Application.targetFrameRate = frameRateCap == 0 ? -1 : frameRateCap;
    }
}
