using System.Collections;
using UnityEngine;

/// <summary>
/// 멘탈 구간을 관리하고 구간별 환각 효과를 실행합니다.
/// 정상(70~100) / 불안(40~70) / 공황(15~40) / 붕괴(0~15)
/// </summary>
public class MentalBreakStage : MonoBehaviour
{
    public static MentalBreakStage Instance { get; private set; }

    private enum Stage { Normal, Anxious, Panic, Collapse }
    private Stage _currentStage = Stage.Normal;
    private Coroutine _hallucinationLoop;

    private static readonly string[] WhisperMessages =
        { "뒤를 봐", "도망쳐", "넌 혼자야", "믿지 마", "여긴 아니야" };

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

    void Update()
    {
        if (PlayerStats.Instance == null) return;

        float ratio = PlayerStats.Instance.currentMental / PlayerStats.Instance.maxMental;
        CheckStage(ratio);
    }

    void CheckStage(float mentalRatio)
    {
        Stage newStage;
        if      (mentalRatio > 0.70f) newStage = Stage.Normal;
        else if (mentalRatio > 0.40f) newStage = Stage.Anxious;
        else if (mentalRatio > 0.15f) newStage = Stage.Panic;
        else                          newStage = Stage.Collapse;

        if (newStage != _currentStage)
        {
            _currentStage = newStage;
            OnStageChanged(newStage);
        }
    }

    void OnStageChanged(Stage newStage)
    {
        // 기존 환각 루프 중단
        if (_hallucinationLoop != null)
        {
            StopCoroutine(_hallucinationLoop);
            _hallucinationLoop = null;
        }

        switch (newStage)
        {
            case Stage.Normal:
                GlitchManager.Instance?.SetGlitchLoop(false);
                break;

            case Stage.Anxious:
                GlitchManager.Instance?.SetGlitchLoop(true, 0.05f);
                _hallucinationLoop = StartCoroutine(HallucinationLoop(30f, 60f));
                break;

            case Stage.Panic:
                GlitchManager.Instance?.SetGlitchLoop(true, 0.15f);
                _hallucinationLoop = StartCoroutine(HallucinationLoop(15f, 30f));
                break;

            case Stage.Collapse:
                GlitchManager.Instance?.SetGlitchLoop(true, 0.3f);
                _hallucinationLoop = StartCoroutine(HallucinationLoop(8f, 15f));
                break;
        }
    }

    IEnumerator HallucinationLoop(float minInterval, float maxInterval)
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));

            int effect = Random.Range(0, 4);
            switch (effect)
            {
                case 0: yield return StartCoroutine(EffectFlash());        break;
                case 1: yield return StartCoroutine(EffectWhisper());      break;
                case 2: EffectGlitch();                                     break;
                case 3: yield return StartCoroutine(EffectControlInvert()); break;
            }
        }
    }

    // ─── 환각 효과 ────────────────────────────────────────────────

    IEnumerator EffectFlash()
    {
        DaggerFilterController.Instance?.SwitchToRealityForced();
        yield return new WaitForSeconds(0.3f);
        DaggerFilterController.Instance?.SwitchToFantasyForced();
    }

    IEnumerator EffectWhisper()
    {
        string msg = WhisperMessages[Random.Range(0, WhisperMessages.Length)];
        InteractionTextUI.Instance?.Show(msg);
        yield return new WaitForSeconds(2f);
        InteractionTextUI.Instance?.Hide();
    }

    void EffectGlitch()
    {
        GlitchManager.Instance?.PlayGlitch(0.5f, 0.9f);
    }

    IEnumerator EffectControlInvert()
    {
        var controller = FindFirstObjectByType<ClearSky.SimplePlayerController>();
        if (controller == null) yield break;

        controller.walkSpeed *= -1f;
        yield return new WaitForSeconds(1.5f);
        controller.walkSpeed *= -1f;
    }
}
