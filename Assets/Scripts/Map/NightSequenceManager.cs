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

    [Header("목표 UI (S#2 / S#4 공용)")]
    public GameObject objectivePanel;
    public Text objectiveHeaderText;
    public Text objectiveBodyText;

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

    // ── 내부 상태 ─────────────────────────────────
    private bool _windowReached = false;
    private Coroutine _heartbeatCoroutine;

    /// <summary>WindowTrigger에서 호출 — S#2 창문 도달 신호.</summary>
    public void OnWindowReached() => _windowReached = true;

    // ─────────────────────────────────────────────
    void Start()
    {
        if (GameState.hasWatchedNightSequence) return;
        StartCoroutine(RunNightSequence());
    }

    IEnumerator RunNightSequence()
    {
        // TransitionManager 페이드인 완료 대기 (기본 0.3s + 여유 0.2s)
        yield return new WaitForSeconds(0.5f);

        yield return StartCoroutine(RunScene2());
        yield return StartCoroutine(RunScene3());
        yield return StartCoroutine(RunScene4());

        GameState.hasWatchedNightSequence = true;

        // 야간 → 주간 조명 전환 (암전 중 스왑)
        TransitionManager.Instance?.DoTransition(() =>
        {
            if (nightLightingRoot != null) nightLightingRoot.SetActive(false);
            if (dayLightingRoot   != null) dayLightingRoot.SetActive(true);
        });

        // 전환 완료 대기 후 S#5 아침 컷씬 바로 시작
        yield return new WaitForSeconds(1f);
        KitchenTriggerCutscene.Instance?.BeginCutscene();
    }

    // ─── S#2 ─────────────────────────────────────
    IEnumerator RunScene2()
    {
        LockPlayer();

        // 루 반응 대사: "...뭐야 이 소리."
        yield return PlayDialogueAndWait(lu_S2_Reaction);

        // 목표 UI 표시
        ShowObjective(
            "창밖에서 이상한 소리가 들립니다.",
            "목표: 창문으로 이동해서 밖을 확인하세요.");

        // 플레이어 이동 활성화
        UnlockPlayer();

        // 창문 도달 대기
        _windowReached = false;
        yield return new WaitUntil(() => _windowReached);

        HideObjective();
    }

    // ─── S#3 ─────────────────────────────────────
    IEnumerator RunScene3()
    {
        LockPlayer();

        // 드론 글리치 (치직 음성 표현)
        GlitchManager.Instance?.PlayGlitch(1.5f, GlitchManager.PresetMild);

        if (droneObject)         droneObject.SetActive(true);
        if (droneWreckageObject) droneWreckageObject.SetActive(false);

        yield return new WaitForSeconds(1f);

        // 루 내면 독백: "저게 뭐지..."
        yield return PlayDialogueAndWait(lu_S3_Monologue);

        // 세라 등장 (어둠 속에서 걸어나옴)
        if (seraAnimator) seraAnimator.SetTrigger(seraWalkInTrigger);
        yield return new WaitForSeconds(1.5f);

        // 세라 대사 1: "...시끄러워."
        yield return PlayDialogueAndWait(sera_S3_Line1);

        // 빛의 구속 이펙트 → 드론 파괴
        if (lightBindingEffect) lightBindingEffect.SetActive(true);
        yield return new WaitForSeconds(0.5f);

        if (droneObject)         droneObject.SetActive(false);
        if (droneWreckageObject) droneWreckageObject.SetActive(true);
        if (lightBindingEffect)  lightBindingEffect.SetActive(false);

        // 드론 파괴 순간: 0.5초 플래시 텍스트 + 글리치 동시
        StartCoroutine(ShowFlashText());
        GlitchManager.Instance?.PlayGlitch(0.5f, GlitchManager.PresetStrong);

        yield return new WaitForSeconds(0.8f);

        // 세라 대사 2: "더러운 게..."
        yield return PlayDialogueAndWait(sera_S3_Line2);

        // 세라 발로 잔해 차는 모션
        if (seraAnimator) seraAnimator.SetTrigger(seraKickTrigger);
        yield return new WaitForSeconds(1f);

        // 세라 대사 3: "...깨우면 안 되는데." (창문 올려다보며)
        if (seraAnimator) seraAnimator.SetTrigger(seraLookUpTrigger);
        yield return PlayDialogueAndWait(sera_S3_Line3);

        // 커튼 닫기 → 암전 전환
        TransitionManager.Instance?.DoTransition(null);
        yield return new WaitForSeconds(0.7f);
    }

    // ─── S#4 ─────────────────────────────────────
    IEnumerator RunScene4()
    {
        LockPlayer();

        // 심장 박동 시작
        _heartbeatCoroutine = StartCoroutine(HeartbeatPulse());

        // 루 내면 독백: "엄마 표정이... 왜 저래?..."
        yield return PlayDialogueAndWait(lu_S4_Monologue);

        // 도자기 손가락 클로즈업
        if (closeupImage != null && ceramicFingerSprite != null)
        {
            closeupImage.sprite = ceramicFingerSprite;
            closeupImage.gameObject.SetActive(true);
            yield return new WaitForSeconds(1.5f);
            closeupImage.gameObject.SetActive(false);
        }

        // 세라 차가운 눈빛 오버랩 (HallucinationManager)
        if (HallucinationManager.Instance != null && seraGazeSprite != null)
            HallucinationManager.Instance.TriggerHallucination(seraGazeSprite, 1.5f);
        yield return new WaitForSeconds(2.5f); // fadeIn(0.5) + 1.5s + fadeOut(0.5)

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

        // 최종 목표 UI: 튜토리얼 완료
        ShowObjective("튜토리얼 완료", "다음 목표: 날이 밝으면 엄마 몰래 단서를 찾으세요.");
        yield return new WaitForSeconds(3f);
        HideObjective();
    }

    // ─── 헬퍼 ────────────────────────────────────

    /// <summary>대사 재생 후 Space/클릭으로 진행. DialogueTrigger 없이 동작.</summary>
    IEnumerator PlayDialogueAndWait(DialogueData data)
    {
        if (data == null || DialogueManager.Instance == null) yield break;
        DialogueManager.Instance.StartDialogue(data);
        yield return null; // 1프레임 대기 — isTalking 활성화 보장
        while (DialogueManager.Instance != null && DialogueManager.Instance.isTalking)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
                DialogueManager.Instance.DisplayNextSentence();
            yield return null;
        }
    }

    IEnumerator ShowFlashText()
    {
        if (flashText == null) yield break;
        flashText.text = flashSentence_ko;
        flashText.gameObject.SetActive(true);
        yield return new WaitForSeconds(0.5f);
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

    void ShowObjective(string header, string body)
    {
        if (objectivePanel == null) return;
        if (objectiveHeaderText != null) objectiveHeaderText.text = header;
        if (objectiveBodyText   != null) objectiveBodyText.text   = body;
        objectivePanel.SetActive(true);
    }

    void HideObjective()
    {
        if (objectivePanel != null) objectivePanel.SetActive(false);
    }

    static void LockPlayer()
    {
        var ctrl = Object.FindAnyObjectByType<ClearSky.SimplePlayerController>();
        if (ctrl == null) return;
        ctrl.enabled = false;
        var rb = ctrl.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    static void UnlockPlayer()
    {
        var ctrl = Object.FindAnyObjectByType<ClearSky.SimplePlayerController>();
        if (ctrl != null) ctrl.enabled = true;
    }
}
