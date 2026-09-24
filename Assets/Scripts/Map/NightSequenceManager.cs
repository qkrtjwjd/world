using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

/// <summary>
/// S#01(루의 방/심야 인게임 · 부엉이) → S#02(세라의 개입 · 창문 잠금) → S#03(못 들은 척) 시퀀스 관리.
/// isNightSequenceWatched 플래그로 한 번만 발동된다.
/// IntroScene 종료 후 Home 씬이 로드되면 Start()에서 자동 실행되며,
/// 완료 후 KitchenTriggerCutscene(S#04A~D)으로 자연스럽게 이어진다.
///
/// 2026-08-04 개편: 드론 → 부엉이. 세라가 드론을 파괴하던 정원 씬이
/// 방 안에서 창문을 잠그고 이불을 여미는 '보호의 형태를 한 통제'로 대체됐다.
/// 대사·연출 순서는 Assets/Dialogue/House_Opening.yarn 을 따른다.
/// </summary>
public class NightSequenceManager : MonoBehaviour
{
    // ── 오프닝 독백 ───────────────────────────────
    [Header("오프닝 독백 — 기본값 비어 있음 (IntroScene 이 담당)")]
    [Tooltip("⚠ 2026-08-08: 기본적으로 비워 둡니다.\n" +
             "오프닝 독백은 IntroScene 의 IntroManager 가 검은 화면에 타이핑으로 띄웁니다.\n" +
             "여기에 Opening_Monologue 를 넣으면 플레이어가 같은 독백을 두 번 보게 되고,\n" +
             "Home 에서는 방이 다 보이는 상태 위에 떠서 연출이 무너집니다.")]
    public string yarnNode_opening = "";

    // ── S#01 — 부엉이 ─────────────────────────────
    [Header("S#01 — 루의 방 / 심야 · 부엉이")]
    public string yarnNode_S1_OwlWake = "House_Owl_Wake";

    [Header("S#01 — 부엉이 오브젝트")]
    [Tooltip("창틀에 앉은 부엉이. 시퀀스 시작 시 활성화되고 S#02 세라 등장 시 꺼진다.")]
    public GameObject owlObject;
    [Tooltip("부엉이 울음 반복 간격(초). 창문에 도달할 때까지 계속 운다.")]
    public float owlCallInterval = 3.5f;
    [Tooltip("AudioManager 에 등록한 부엉이 울음 이름. 비우면 무음으로 진행한다.")]
    public string owlCallSfxName = "";

    // ── S#02 — 세라의 개입 ────────────────────────
    [Header("S#02 — 세라의 개입 (Yarn 노드)")]
    public string yarnNode_S2_Enter  = "House_Sera_Lock_Enter";
    public string yarnNode_S2_Window = "House_Sera_Lock_Window";
    public string yarnNode_S2_Tuck   = "House_Sera_Lock_Tuck";

    [Header("S#02 — 세라 Animator")]
    public Animator seraAnimator;
    public string seraWalkInTrigger    = "WalkIn";
    public string seraToWindowTrigger  = "ToWindow";
    public string seraTuckTrigger      = "Tuck";
    public string seraExitTrigger      = "Exit";

    [Header("S#02 — 세라 조명 (문틈으로 새는 빛)")]
    public Light2D seraLight;
    public float   seraLightTarget = 1.2f;

    [Header("S#02 — 창문")]
    // 창문 잠금은 SFX 하나로만 표현한다. 시나리오가 "무서운 것은 창문을 잠그는 소리
    // 하나뿐이어야 한다"고 못박았으므로 창문 오브젝트를 조작할 일이 없다.
    [Tooltip("AudioManager 에 등록한 창문 잠금 딸깍 이름. 이 씬에서 가장 큰 소리여야 한다. 비우면 무음.")]
    public string windowLockSfxName = "";
    [Tooltip("복도 발소리 이름. 비우면 무음.")]
    public string footstepSfxName = "";

    [Header("S#02 — 루가 침대로 돌아간다")]
    [Tooltip("루가 돌아갈 침대 위치. 비우면 S#01 시작 때 루가 있던 자리(잠에서 깬 자리)로 돌아간다.")]
    public Transform luBedPoint;
    [Tooltip("침대까지 걸리는 시간(초). 정본 D-S#02: 「반사적으로 … 망설임이 없다. 몸에 밴 동작」 — 짧게 둔다.")]
    public float returnToBedDuration = 0.4f;

    // ── S#03 — 못 들은 척 ──────────────────────────
    [Header("S#03 — 못 들은 척 (Yarn 노드)")]
    public string yarnNode_S3_Owl   = "House_Unheard_Owl";
    public string yarnNode_S3_Close = "House_Unheard_Close";

    [Header("S#03 — 도자기 손가락 클로즈업 (초기 비활성)")]
    public Image  closeupImage;
    public Sprite ceramicFingerSprite;
    // ⚠ 밤 씬은 딱딱 무음 확정(루 캐릭터 설정서 10-3: 집 = 없음).
    //    도자기 손은 소리 없는 시각 연출로만 노출한다. 여기서 SFX를 재생하지 말 것.

    [Header("S#03 — 창밖 날개 그림자 (루는 못 보고 플레이어만 본다)")]
    [Tooltip("창밖을 스쳐 지나가는 날개 그림자 오브젝트. 비워도 무해하다.")]
    public GameObject wingShadowObject;
    public float      wingShadowDuration = 0.8f;

    [Header("S#03 — 배경 어두워짐")]
    public Image darkOverlay;
    public float darkOverlayAlpha = 0.5f;

    // ── 폐기된 드론 씬 잔재 ────────────────────────
    [Header("정리 대상 (구 드론 씬 잔재)")]
    [Tooltip("구 S#3 정원 컷씬용 루트. 씬에서 활성 상태로 시작하므로 시퀀스 끝에 꺼 준다. " +
             "부엉이 버전에는 정원 장면이 없다. 씬에서 오브젝트를 지우면 이 필드도 비워도 된다.")]
    public GameObject gardenViewRoot;

    // ── 조명 전환 ─────────────────────────────────
    [Header("조명 전환 (야간 → 주간, 시퀀스 종료 시)")]
    public GameObject nightLightingRoot;
    public GameObject dayLightingRoot;

    // ── 캐싱된 WaitForSeconds (GC 방지) ───────────────
    private static readonly WaitForSeconds _wait05s = new WaitForSeconds(0.5f);
    private static readonly WaitForSeconds _wait07s = new WaitForSeconds(0.7f);
    private static readonly WaitForSeconds _wait1s  = new WaitForSeconds(1f);
    private static readonly WaitForSeconds _wait15s = new WaitForSeconds(1.5f);
    private static readonly WaitForSeconds _wait3s  = new WaitForSeconds(3f);

    // ── 내부 상태 ─────────────────────────────────
    private bool      _windowReached = false;
    private Coroutine _owlCallLoop;
    private Vector3   _luWakePosition;
    private bool      _hasWakePosition;

    [Header("── 테스트 전용 (빌드 전 해제) ──")]
    [SerializeField] private bool _skipForTesting = false;

    public void OnWindowReached() => _windowReached = true;

    // ─────────────────────────────────────────────
    void OnDisable()
    {
        if (_owlCallLoop != null)
        {
            StopCoroutine(_owlCallLoop);
            _owlCallLoop = null;
        }
    }

    void Start()
    {
        if (GameState.isNightSequenceWatched) return;

        if (_skipForTesting)
        {
            GameState.isNightSequenceWatched = true;
            if (nightLightingRoot != null) nightLightingRoot.SetActive(false);
            if (dayLightingRoot   != null) dayLightingRoot.SetActive(true);
            return;
        }

        StartCoroutine(RunNightSequence());
    }

    IEnumerator RunNightSequence()
    {
        yield return _wait05s;

        if (!string.IsNullOrEmpty(yarnNode_opening))
            yield return YarnDialogue.PlayAndWait(yarnNode_opening, false);

        yield return StartCoroutine(RunScene1());
        yield return StartCoroutine(RunScene2());
        yield return StartCoroutine(RunScene3());

        GameState.isNightSequenceWatched = true;

        // 각 씬 코루틴의 LockPlayer/UnlockPlayer 쌍이 맞으면 이 시점에서 lockCount=0.
        // 예상치 못한 경로로 Lock이 누적됐을 경우를 대비한 최후 안전장치.
        PlayerInputLock.Instance.ForceUnlock();

        TransitionManager.Instance?.DoTransition(() =>
        {
            if (nightLightingRoot != null) nightLightingRoot.SetActive(false);
            if (dayLightingRoot   != null) dayLightingRoot.SetActive(true);
            KitchenTriggerCutscene.Instance?.TeleportToDiningTable();
        });

        yield return _wait07s;
        KitchenTriggerCutscene.Instance?.BeginCutscene();
    }

    // ─── S#01 — 부엉이 ────────────────────────────
    // 루가 깨어나 창틀의 부엉이를 본다. 플레이어가 창문에 도달하면 S#02로.
    // 2026-09-17: 목표 UI(「창문으로 이동해서 밖을 확인하세요.」)는 띄우지 않는다.
    // 유도는 부엉이 울음 반복만 맡는다.
    IEnumerator RunScene1()
    {
        LockPlayer();

        // 잠에서 깬 자리 = 침대. S#02 에서 루가 여기로 돌아간다.
        var lu = FindLu();
        _hasWakePosition = lu != null;
        if (lu != null) _luWakePosition = lu.transform.position;

        if (owlObject) owlObject.SetActive(true);

        yield return YarnDialogue.PlayAndWait(yarnNode_S1_OwlWake, false);

        UnlockPlayer();

        // 부엉이가 일정 간격으로 울며 창문 방향으로 유도한다.
        _owlCallLoop = StartCoroutine(OwlCallLoop());

        _windowReached = false;
        yield return new WaitUntil(() => _windowReached);

        if (_owlCallLoop != null)
        {
            StopCoroutine(_owlCallLoop);
            _owlCallLoop = null;
        }

        ObjectiveManager.Instance?.HideObjective();
    }

    // ─── S#02 — 세라의 개입 ───────────────────────
    // 발소리 → 문 열림 → 창가 → 창문 잠금 → 이불 여미기 → 퇴장.
    // 세라는 단 한 번도 화를 내지 않는다. 무서운 것은 창문을 잠그는 소리 하나뿐이다.
    IEnumerator RunScene2()
    {
        LockPlayer();

        // 발소리 — 세라가 복도에서 다가온다
        PlaySfxIfNamed(footstepSfxName);
        SetSeraLightPresence(0f);
        if (seraAnimator) seraAnimator.SetTrigger(seraWalkInTrigger);
        FadeSeraLight(1f, 1f);

        // 발소리에 루가 반사적으로 침대로 돌아간다(D-S#02 ▶ 연출).
        yield return StartCoroutine(ReturnLuToBed());

        yield return YarnDialogue.PlayAndWait(yarnNode_S2_Enter, false);

        // 세라가 창가로 걸어간다. 창틀의 부엉이는 이미 없다.
        // 사라지는 연출을 넣지 않는다 — 세라가 창가에 선 그 프레임에서 이미 없어야 한다.
        if (owlObject) owlObject.SetActive(false);
        if (seraAnimator) seraAnimator.SetTrigger(seraToWindowTrigger);
        yield return _wait1s;

        yield return YarnDialogue.PlayAndWait(yarnNode_S2_Window, false);

        // 창문 잠금 — 이 씬에서 가장 큰 소리
        PlaySfxIfNamed(windowLockSfxName);
        yield return _wait05s;

        // 이불 여미기 — 다정함과 구속이 같은 그림이 되어야 한다
        if (seraAnimator) seraAnimator.SetTrigger(seraTuckTrigger);
        yield return _wait05s;

        yield return YarnDialogue.PlayAndWait(yarnNode_S2_Tuck, false);

        // 퇴장. 발소리가 멀어질 때까지 아무 일도 일어나지 않게 둔다.
        if (seraAnimator) seraAnimator.SetTrigger(seraExitTrigger);
        AudioManager.Instance?.Play("doorClose");
        FadeSeraLight(0f, 1f);
        yield return _wait3s;

        UnlockPlayer();
    }

    // ─── S#03 — 못 들은 척 ────────────────────────
    // 루가 아무것도 하지 않는다는 것이 이 씬의 핵심이다.
    IEnumerator RunScene3()
    {
        LockPlayer();

        if (darkOverlay != null)
            StartCoroutine(FadeInImage(darkOverlay, darkOverlayAlpha, 0.3f));

        yield return YarnDialogue.PlayAndWait(yarnNode_S3_Owl, false);

        // 도자기 손가락 클로즈업 — 소리 없이 시각으로만.
        // 루는 자기 손을 이상하게 여기지 않는다. 그 무반응이 인형화 20%의 표현이다.
        if (closeupImage != null && ceramicFingerSprite != null)
        {
            closeupImage.sprite = ceramicFingerSprite;
            closeupImage.gameObject.SetActive(true);
            yield return _wait15s;
            closeupImage.gameObject.SetActive(false);
        }

        yield return YarnDialogue.PlayAndWait(yarnNode_S3_Close, false);

        // 창밖 날개 그림자 — 루는 눈을 감아 못 보고, 플레이어만 본다.
        // 이 비대칭이 데모 내내 유지되는 시점 규칙이다.
        if (wingShadowObject != null)
        {
            wingShadowObject.SetActive(true);
            yield return new WaitForSeconds(wingShadowDuration);
            wingShadowObject.SetActive(false);
        }
        yield return _wait05s;

        if (darkOverlay != null) darkOverlay.gameObject.SetActive(false);

        // 구 드론 씬의 정원 뷰를 끈다. 기존 RunScene3 이 같은 시점에 하던 일이라
        // 이걸 빼면 정원이 계속 화면에 남는다.
        if (gardenViewRoot != null) gardenViewRoot.SetActive(false);

        UnlockPlayer();
    }

    // ─── 헬퍼 ────────────────────────────────────

    /// <summary>이름이 비어 있으면 조용히 건너뛴다. 미등록 SFX로 경고가 도배되는 것을 막는다.</summary>
    void PlaySfxIfNamed(string soundName)
    {
        if (string.IsNullOrEmpty(soundName)) return;
        AudioManager.Instance?.Play(soundName);
    }

    static ClearSky.SimplePlayerController FindLu() =>
        PlayerStats.Instance != null
            ? PlayerStats.Instance.GetComponent<ClearSky.SimplePlayerController>()
            : Object.FindAnyObjectByType<ClearSky.SimplePlayerController>();

    /// <summary>
    /// 루를 침대 자리로 짧게 옮긴다. 조작이 잠겨 있어 SimplePlayerController 가 Animator 를
    /// 덮어쓰지 않으므로 여기서 걷기·방향을 직접 건다. 눕는 자세 스프라이트는 아직 없어
    /// 도착하면 아래(0)를 보고 멈춘다.
    /// </summary>
    IEnumerator ReturnLuToBed()
    {
        var lu = FindLu();
        if (lu == null) yield break;

        Vector3 target;
        if (luBedPoint != null)      target = luBedPoint.position;
        else if (_hasWakePosition)   target = _luWakePosition;
        else yield break;

        var rb   = lu.GetComponent<Rigidbody2D>();
        var anim = lu.GetComponent<Animator>();
        Vector3 start = lu.transform.position;
        Vector3 delta = target - start;
        if (delta.sqrMagnitude < 0.0001f) yield break;

        if (anim != null)
        {
            // 0=아래 · 1=옆 · 2=위. 옆이면 SimplePlayerController 와 같은 규칙으로 부호만 뒤집는다.
            int dir = Mathf.Abs(delta.x) > Mathf.Abs(delta.y) ? 1 : (delta.y > 0f ? 2 : 0);
            Vector3 s = lu.transform.localScale;
            s.x = dir == 1 ? Mathf.Abs(s.x) * (delta.x > 0f ? -1f : 1f) : Mathf.Abs(s.x);
            lu.transform.localScale = s;
            anim.SetInteger("dir", dir);
            anim.SetBool("isRun", true);
        }

        float t = 0f;
        float duration = Mathf.Max(0.01f, returnToBedDuration);
        while (t < duration)
        {
            t += Time.deltaTime;
            Vector3 p = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / duration));
            if (rb != null) rb.position = p; else lu.transform.position = p;
            yield return null;
        }
        if (rb != null) { rb.position = target; rb.linearVelocity = Vector2.zero; }
        else lu.transform.position = target;

        if (anim != null)
        {
            anim.SetBool("isRun", false);
            anim.SetInteger("dir", 0);
        }
    }

    IEnumerator OwlCallLoop()
    {
        var wait = new WaitForSeconds(owlCallInterval);
        while (true)
        {
            yield return wait;
            PlaySfxIfNamed(owlCallSfxName);
        }
    }

    IEnumerator FadeInImage(Image image, float targetAlpha, float duration)
    {
        Color c = image.color;
        c.a = 0f;
        image.color = c;
        image.gameObject.SetActive(true);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(0f, targetAlpha, t / duration);
            image.color = c;
            yield return null;
        }
        c.a = targetAlpha;
        image.color = c;
    }

    /// <summary>
    /// 세라의 빛을 「등장 정도」로 다룹니다.
    ///
    /// <see cref="SeraLightDirector"/> 가 붙어 있으면 세기를 직접 쓰지 않고 presence 축만 밉니다 —
    /// 세기는 세라의 기분이 정하고 연출은 「얼마나 와 있는가」만 정하기 때문입니다.
    /// 둘 다 세기를 쓰면 매 프레임 서로 밀어냅니다.
    ///
    /// 디렉터가 없으면 예전처럼 <see cref="seraLightTarget"/> 까지 세기를 페이드합니다.
    /// </summary>
    SeraLightDirector SeraLightDir =>
        seraLight != null ? seraLight.GetComponent<SeraLightDirector>() : null;

    void SetSeraLightPresence(float value)
    {
        if (seraLight == null) return;

        var dir = SeraLightDir;
        if (dir != null) { dir.SetPresence(value); return; }

        seraLight.intensity = value <= 0f ? 0f : seraLightTarget;
    }

    void FadeSeraLight(float target, float duration)
    {
        if (seraLight == null) return;

        var dir = SeraLightDir;
        if (dir != null) { dir.FadePresenceTo(target, duration); return; }

        if (target <= 0f) StartCoroutine(FadeOutLight(seraLight, duration));
        else              StartCoroutine(FadeInLight(seraLight, seraLightTarget, duration));
    }

    IEnumerator FadeInLight(Light2D light, float target, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            light.intensity = Mathf.Lerp(0f, target, t / duration);
            yield return null;
        }
        light.intensity = target;
    }

    IEnumerator FadeOutLight(Light2D light, float duration)
    {
        float start = light.intensity;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            light.intensity = Mathf.Lerp(start, 0f, t / duration);
            yield return null;
        }
        light.intensity = 0f;
    }

    static void LockPlayer()   => YarnDialogue.LockPlayer();
    static void UnlockPlayer() => YarnDialogue.UnlockPlayer(
        PlayerStats.Instance != null
            ? PlayerStats.Instance.GetComponent<ClearSky.SimplePlayerController>()
            : Object.FindAnyObjectByType<ClearSky.SimplePlayerController>());
}
