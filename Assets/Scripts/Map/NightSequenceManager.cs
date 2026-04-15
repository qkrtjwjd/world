using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using HallucinationSystem;

/// <summary>
/// S#2(루의 방/심야 인게임) → S#3(정원 컷씬) → S#4(루의 방 컷씬) 시퀀스 관리.
/// hasWatchedNightSequence 플래그로 한 번만 발동된다.
/// IntroScene 종료 후 Home 씬이 로드되면 Start()에서 자동 실행되며,
/// 완료 후 기존 S#5 KitchenTriggerCutscene으로 자연스럽게 이어진다.
/// </summary>
public class NightSequenceManager : MonoBehaviour
{
    // ── S#2 ──────────────────────────────────────
    [Header("S#2 — 루의 방 / 심야")]
    public DialogueData lu_S2_Reaction;          // "...뭐야 이 소리."

    // ── S#3 ──────────────────────────────────────
    [Header("S#3 — 정원 컷씬 대사")]
    public DialogueData lu_S3_Monologue;         // "저게 뭐지. 새가 아닌 것 같은데... 기계?"
    public DialogueData sera_S3_Line1;           // "...시끄러워."
    public DialogueData sera_S3_Line2;           // "더러운 게... 감히 내 집에 기어들어 와?"
    public DialogueData sera_S3_Line3;           // "...깨우면 안 되는데."

    [Header("S#3 — 오브젝트")]
    public GameObject droneObject;              // 드론 GameObject (초기 비활성)
    public GameObject droneWreckageObject;      // 파괴된 잔해 GameObject (초기 비활성)
    public GameObject lightBindingEffect;       // 빛의 구속 이펙트 (초기 비활성)
    public Animator seraAnimator;               // 세라 Animator

    [Header("S#3 — 세라 Animator 트리거명 (Inspector에서 설정)")]
    public string seraWalkInTrigger = "WalkIn";
    public string seraKickTrigger   = "Kick";
    public string seraLookUpTrigger = "LookUp";

    [Header("S#3 — 플래시 텍스트 (0.5초, 초기 비활성)")]
    public Text flashText;
    public string flashSentence_ko = "저건 어떤 원리로 작동하는 거지?";

    // ── S#4 ──────────────────────────────────────
    [Header("S#4 — 루의 방 / 심야 컷씬 대사")]
    public DialogueData lu_S4_Monologue;         // "엄마 표정이... 왜 저래?..."

    [Header("S#4 — 심장박동 이펙트 (붉은 Image, 초기 alpha=0)")]
    public Image heartbeatOverlay;
    public AudioSource heartbeatAudio;

    [Header("S#4 — 세라 눈빛 오버랩 (HallucinationManager에 전달)")]
    public Sprite seraGazeSprite;

    [Header("S#4 — 도자기 손가락 클로즈업 (초기 비활성)")]
    public Image closeupImage;
    public Sprite ceramicFingerSprite;

    // ── 조명 전환 ─────────────────────────────────
    [Header("조명 전환 (야간 → 주간, 시퀀스 종료 시)")]
    public GameObject nightLightingRoot;         // 야간 조명 부모 오브젝트
    public GameObject dayLightingRoot;           // 주간 조명 부모 오브젝트

    // ── 캐싱된 WaitForSeconds (GC 방지) ───────────────
    private static readonly WaitForSeconds _wait05s = new WaitForSeconds(0.5f);
    private static readonly WaitForSeconds _wait07s = new WaitForSeconds(0.7f);
    private static readonly WaitForSeconds _wait08s = new WaitForSeconds(0.8f);
    private static readonly WaitForSeconds _wait1s  = new WaitForSeconds(1f);
    private static readonly WaitForSeconds _wait15s = new WaitForSeconds(1.5f);
    private static readonly WaitForSeconds _wait25s = new WaitForSeconds(2.5f);

    // ── 내부 상태 ─────────────────────────────────
    private bool _windowReached = false;
    private Coroutine _heartbeatCoroutine;

    [Header("── 테스트 전용 (빌드 전 해제) ──")]
    [SerializeField] private bool _skipForTesting = false;

    /// <summary>WindowTrigger에서 호출 — S#2 창문 도달 신호.</summary>
    public void OnWindowReached() => _windowReached = true;

    // ─────────────────────────────────────────────
    void Start()
    {
        if (GameState.hasWatchedNightSequence) return;

        if (_skipForTesting)
        {
            GameState.hasWatchedNightSequence = true;
            if (nightLightingRoot != null) nightLightingRoot.SetActive(false);
            if (dayLightingRoot   != null) dayLightingRoot.SetActive(true);
            return;
        }

        StartCoroutine(RunNightSequence());
    }

    IEnumerator RunNightSequence()
    {
        // TransitionManager 페이드인 완료 대기 (기본 0.3s + 여유 0.2s)
        yield return _wait05s;

        yield return StartCoroutine(RunScene2());
        yield return StartCoroutine(RunScene3());
        yield return StartCoroutine(RunScene4());

        GameState.hasWatchedNightSequence = true;

        // 야간 → 주간 조명 전환 (암전 중 스왑) + 부엌 스폰
        TransitionManager.Instance?.DoTransition(() =>
        {
            if (nightLightingRoot != null) nightLightingRoot.SetActive(false);
            if (dayLightingRoot   != null) dayLightingRoot.SetActive(true);
            KitchenTriggerCutscene.Instance?.TeleportToKitchen(); // 암전 중 스폰
        });

        // 전환 완료 대기 후 S#5 아침 컷씬 바로 시작
        yield return _wait07s; // DoTransition 완료(0.6s) + 여유
        KitchenTriggerCutscene.Instance?.BeginCutscene();
    }

    // ─── S#2 ─────────────────────────────────────
    IEnumerator RunScene2()
    {
        LockPlayer();

        // 루 반응 대사: "...뭐야 이 소리."
        yield return DialogueRunner.PlayAndWait(lu_S2_Reaction);

        // 목표 UI 표시
        ObjectiveManager.Instance?.ShowObjective(
            "창밖에서 이상한 소리가 들립니다.",
            "목표: 창문으로 이동해서 밖을 확인하세요.");

        // 플레이어 이동 활성화
        UnlockPlayer();

        // 창문 도달 대기
        _windowReached = false;
        yield return new WaitUntil(() => _windowReached);

        ObjectiveManager.Instance?.HideObjective();
    }

    // ─── S#3 ─────────────────────────────────────
    IEnumerator RunScene3()
    {
        LockPlayer();

        // 드론 글리치 (치직 음성 표현)
        GlitchManager.Instance?.PlayGlitch(1.5f, GlitchManager.PresetMild);

        if (droneObject)         droneObject.SetActive(true);
        if (droneWreckageObject) droneWreckageObject.SetActive(false);

        yield return _wait1s;

        // 루 내면 독백: "저게 뭐지..."
        yield return DialogueRunner.PlayAndWait(lu_S3_Monologue);

        // 세라 등장 (어둠 속에서 걸어나옴)
        if (seraAnimator) seraAnimator.SetTrigger(seraWalkInTrigger);
        yield return _wait15s;

        // 세라 대사 1: "...시끄러워."
        yield return DialogueRunner.PlayAndWait(sera_S3_Line1);

        // 빛의 구속 이펙트 → 드론 파괴
        if (lightBindingEffect) lightBindingEffect.SetActive(true);
        yield return _wait05s;

        if (droneObject)         droneObject.SetActive(false);
        if (droneWreckageObject) droneWreckageObject.SetActive(true);
        if (lightBindingEffect)  lightBindingEffect.SetActive(false);

        // 드론 파괴 순간: 0.5초 플래시 텍스트 + 글리치 동시
        StartCoroutine(ShowFlashText());
        GlitchManager.Instance?.PlayGlitch(0.5f, GlitchManager.PresetStrong);

        yield return _wait08s;

        // 세라 대사 2: "더러운 게..."
        yield return DialogueRunner.PlayAndWait(sera_S3_Line2);

        // 세라 발로 잔해 차는 모션
        if (seraAnimator) seraAnimator.SetTrigger(seraKickTrigger);
        yield return _wait1s;

        // 세라 대사 3: "...깨우면 안 되는데." (창문 올려다보며)
        if (seraAnimator) seraAnimator.SetTrigger(seraLookUpTrigger);
        yield return DialogueRunner.PlayAndWait(sera_S3_Line3);

        // 커튼 닫기 → 암전 전환
        TransitionManager.Instance?.DoTransition(null);
        yield return _wait07s;
    }

    // ─── S#4 ─────────────────────────────────────
    IEnumerator RunScene4()
    {
        LockPlayer();

        // 심장 박동 시작
        _heartbeatCoroutine = StartCoroutine(HeartbeatPulse());

        // 루 내면 독백: "엄마 표정이... 왜 저래?..."
        yield return DialogueRunner.PlayAndWait(lu_S4_Monologue);

        // 도자기 손가락 클로즈업
        if (closeupImage != null && ceramicFingerSprite != null)
        {
            closeupImage.sprite = ceramicFingerSprite;
            closeupImage.gameObject.SetActive(true);
            yield return _wait15s;
            closeupImage.gameObject.SetActive(false);
        }

        // 세라 차가운 눈빛 오버랩 (HallucinationManager)
        if (HallucinationManager.Instance != null && seraGazeSprite != null)
            HallucinationManager.Instance.TriggerHallucination(seraGazeSprite, 1.5f);
        yield return _wait25s; // fadeIn(0.5) + 1.5s + fadeOut(0.5)

        // 심장 박동 종료
        if (_heartbeatCoroutine != null)
        {
            StopCoroutine(_heartbeatCoroutine);
            _heartbeatCoroutine = null;
        }
        if (heartbeatOverlay != null)
        {
            Color c = heartbeatOverlay.color;
            c.a = 0f;
            heartbeatOverlay.color = c;
        }
        if (heartbeatAudio != null) heartbeatAudio.Stop();

    }

    // ─── 헬퍼 ────────────────────────────────────

    IEnumerator ShowFlashText()
    {
        if (flashText == null) yield break;
        flashText.text = flashSentence_ko;
        flashText.gameObject.SetActive(true);
        yield return _wait05s;
        flashText.gameObject.SetActive(false);
    }

    IEnumerator HeartbeatPulse()
    {
        if (heartbeatOverlay == null) yield break;
        if (heartbeatAudio != null) heartbeatAudio.Play();
        float speed    = 1.8f;
        float maxAlpha = 0.38f;
        while (true)
        {
            float alpha = (Mathf.Sin(Time.time * speed * Mathf.PI) * 0.5f + 0.5f) * maxAlpha;
            Color c = heartbeatOverlay.color;
            c.a = alpha;
            heartbeatOverlay.color = c;
            yield return null;
        }
    }

    static void LockPlayer()   => DialogueRunner.LockPlayer();
    static void UnlockPlayer() => DialogueRunner.UnlockPlayer(
        PlayerStats.Instance != null
            ? PlayerStats.Instance.GetComponent<ClearSky.SimplePlayerController>()
            : Object.FindAnyObjectByType<ClearSky.SimplePlayerController>());
}
