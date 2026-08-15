using System.Collections;
using UnityEngine;

public enum MentalStage
{
    Normal,   // 70 ~ 100
    Anxiety,  // 40 ~ 70
    Panic,    // 15 ~ 40
    Collapse  //  0 ~ 15
}

/// <summary>
/// 멘탈 수치를 4단계로 구분하고, 구간 변경 시 환각 루프를 관리합니다.
/// 불안/공황 구간에서는 랜덤 환각 이벤트(번쩍임, 귓속말, 강한 글리치, 조작 반전)를 실행합니다.
/// 플레이어 오브젝트 또는 GameManager 오브젝트에 추가하세요.
/// </summary>
public class MentalBreakStage : MonoBehaviour
{
    public static MentalBreakStage Instance { get; private set; }

    // ── 내부 상태 ──
    private MentalStage _currentStage = MentalStage.Normal;
    private Coroutine   _hallucinationLoop;
    private ClearSky.SimplePlayerController _playerController;
    private bool _controlsInverted = false;

    // WaitForSeconds 캐시
    private static readonly WaitForSeconds WaitFlashReality   = new WaitForSeconds(0.3f);
    private static readonly WaitForSeconds WaitWhisper        = new WaitForSeconds(2f);
    private static readonly WaitForSeconds WaitInvertControls = new WaitForSeconds(1.5f);
    private static readonly WaitForSeconds WaitStrongGlitch   = new WaitForSeconds(2f);

    private readonly string[] _whispers =
        { "뒤를 봐", "도망쳐", "눈을 감아", "여기야", "도망가", "멈춰", "조심해" };

    // ── 구간 경계 ──
    private const float ThresholdAnxiety  = 70f;
    private const float ThresholdPanic    = 40f;
    private const float ThresholdCollapse = 15f;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        _playerController = FindAnyObjectByType<ClearSky.SimplePlayerController>();

        if (PlayerStats.Instance != null)
            _currentStage = GetStage(PlayerStats.Instance.currentMental);
    }

    void Update()
    {
        if (PlayerStats.Instance == null) return;

        MentalStage newStage = GetStage(PlayerStats.Instance.currentMental);
        if (newStage != _currentStage)
            OnStageChanged(newStage);
    }

    // ── 구간 판정 ──
    MentalStage GetStage(float mental)
    {
        if (mental > ThresholdAnxiety)  return MentalStage.Normal;
        if (mental > ThresholdPanic)    return MentalStage.Anxiety;
        if (mental > ThresholdCollapse) return MentalStage.Panic;
        return MentalStage.Collapse;
    }

    // ── 구간 변경 처리 ──
    void OnStageChanged(MentalStage newStage)
    {
        _currentStage = newStage;

        switch (newStage)
        {
            case MentalStage.Normal:
                StopHallucinationLoop();
                break;

            case MentalStage.Anxiety:
            case MentalStage.Panic:
            case MentalStage.Collapse:
                RestartHallucinationLoop();
                // 불안/공황 구간 진입 시 붉은 심박 테두리 펄스
                // (screenEdgeEffectEnabled=false면 내부에서 자동 무시됨)
                ScreenEdgeEffectController.ShowHeartbeat();
                break;
        }
    }

    // ── 환각 루프 관리 ──
    void RestartHallucinationLoop()
    {
        StopHallucinationLoop();
        _hallucinationLoop = StartCoroutine(HallucinationLoop());
    }

    void StopHallucinationLoop()
    {
        if (_hallucinationLoop != null)
        {
            StopCoroutine(_hallucinationLoop);
            _hallucinationLoop = null;
        }

        // 루프 중단 시 반전된 조작 복원
        if (_controlsInverted && _playerController != null)
        {
            _playerController.walkSpeed *= -1f;
            _controlsInverted = false;
        }
    }

    IEnumerator HallucinationLoop()
    {
        while (true)
        {
            float waitMin, waitMax;
            int   eventCount;

            switch (_currentStage)
            {
                case MentalStage.Anxiety:
                    waitMin    = 20f;
                    waitMax    = 30f;
                    eventCount = 1;
                    break;
                case MentalStage.Panic:
                    waitMin    = 13f;
                    waitMax    = 20f;
                    eventCount = Random.Range(1, 3);
                    break;
                case MentalStage.Collapse:
                    waitMin    = 8f;
                    waitMax    = 13f;
                    eventCount = Random.Range(1, 3);
                    break;
                default:
                    yield break;
            }

            yield return new WaitForSeconds(Random.Range(waitMin, waitMax));

            for (int i = 0; i < eventCount; i++)
                yield return StartCoroutine(RunRandomHallucination());
        }
    }

    IEnumerator RunRandomHallucination()
    {
        int pick = Random.Range(0, 4);
        switch (pick)
        {
            case 0: yield return StartCoroutine(FlashReality());   break;
            case 1: yield return StartCoroutine(ShowWhisper());    break;
            case 2: yield return StartCoroutine(InvertControls()); break;
            case 3: yield return StartCoroutine(StrongGlitch());   break;
        }
    }

    // ── 환각 이펙트 ──

    IEnumerator FlashReality()
    {
        if (DaggerFilterController.Instance == null) yield break;
        DaggerFilterController.Instance.SwitchToRealityForced();
        yield return WaitFlashReality;
        DaggerFilterController.Instance.SwitchToFantasyForced();
    }

    IEnumerator ShowWhisper()
    {
        if (InteractionTextUI.Instance == null) yield break;
        string text = _whispers[Random.Range(0, _whispers.Length)];
        InteractionTextUI.Instance.Show(text);
        yield return WaitWhisper;
        InteractionTextUI.Instance.Hide();
    }

    IEnumerator InvertControls()
    {
        if (_playerController == null || _controlsInverted) yield break;

        _controlsInverted = true;
        _playerController.walkSpeed *= -1f;

        // 조작 반전 알림 (설정으로 끌 수 있음)
        bool showAlert = SettingsManager.Instance?.showInputReverseAlert ?? true;
        if (showAlert && InteractionTextUI.Instance != null)
            InteractionTextUI.Instance.Show("조작이 뒤틀린다...");

        yield return WaitInvertControls;

        // StopHallucinationLoop이 먼저 복원한 경우(_controlsInverted=false) 이중 반전 방지
        if (_controlsInverted)
        {
            if (_playerController != null)
                _playerController.walkSpeed *= -1f;
            _controlsInverted = false;
        }

        if (showAlert && InteractionTextUI.Instance != null)
            InteractionTextUI.Instance.Hide();
    }

    IEnumerator StrongGlitch()
    {
        if (GlitchManager.Instance == null) yield break;
        GlitchManager.Instance.PlayGlitch(2f, GlitchManager.PresetStrong);
        yield return WaitStrongGlitch;
    }
}
