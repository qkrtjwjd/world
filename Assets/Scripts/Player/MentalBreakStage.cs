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
/// 멘탈 수치를 4단계로 구분하고, 구간 변경 시 글리치 강도를 자동 조정합니다.
/// 불안/공황 구간에서는 랜덤 환각 루프(번쩍임, 귓속말, 강한 글리치, 조작 반전)를 실행합니다.
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
                GlitchManager.Instance?.SetGlitchLoop(false);
                StopHallucinationLoop();
                break;

            case MentalStage.Anxiety:
                GlitchManager.Instance?.SetGlitchLoop(true, 0.15f);
                RestartHallucinationLoop();
                break;

            case MentalStage.Panic:
                GlitchManager.Instance?.SetGlitchLoop(true, 0.35f);
                RestartHallucinationLoop();
                break;

            case MentalStage.Collapse:
                GlitchManager.Instance?.SetGlitchLoop(true, 0.5f);
                RestartHallucinationLoop();
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

        // 루프 중단 시 혹시 남아있는 상태 복원
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
            // 구간별 대기 시간
            float waitMin, waitMax;
            int   eventCount;

            switch (_currentStage)
            {
                case MentalStage.Anxiety:
                    waitMin    = 8f;
                    waitMax    = 18f;
                    eventCount = 1;
                    break;
                case MentalStage.Panic:
                    waitMin    = 4f;
                    waitMax    = 9f;
                    eventCount = Random.Range(1, 3); // 1 또는 2
                    break;
                case MentalStage.Collapse:
                    waitMin    = 2f;
                    waitMax    = 5f;
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
            case 0: yield return StartCoroutine(FlashReality());    break;
            case 1: yield return StartCoroutine(ShowWhisper());     break;
            case 2: yield return StartCoroutine(StrongGlitch());    break;
            case 3: yield return StartCoroutine(InvertControls());  break;
        }
    }

    // ── 환각 이펙트 ──

    IEnumerator FlashReality()
    {
        if (DaggerFilterController.Instance == null) yield break;
        DaggerFilterController.Instance.SwitchToRealityForced();
        yield return new WaitForSeconds(0.3f);
        DaggerFilterController.Instance.SwitchToFantasyForced();
    }

    IEnumerator ShowWhisper()
    {
        if (InteractionTextUI.Instance == null) yield break;
        string text = _whispers[Random.Range(0, _whispers.Length)];
        InteractionTextUI.Instance.Show(text);
        yield return new WaitForSeconds(2f);
        InteractionTextUI.Instance.Hide();
    }

    IEnumerator StrongGlitch()
    {
        GlitchManager.Instance?.PlayGlitch(0.6f, 0.9f);
        yield return new WaitForSeconds(0.6f);
    }

    IEnumerator InvertControls()
    {
        if (_playerController == null || _controlsInverted) yield break;

        _controlsInverted = true;
        _playerController.walkSpeed *= -1f;

        yield return new WaitForSeconds(1.5f);

        if (_playerController != null)
            _playerController.walkSpeed *= -1f;
        _controlsInverted = false;
    }
}
