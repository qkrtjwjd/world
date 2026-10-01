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
    [Tooltip("Sera.controller 에는 dir(0=아래 1=옆 2=위) 과 Speed 뿐이다. 걷기는 이 둘로 건다.")]
    public Animator seraAnimator;

    // 2026-09-26: 옛 WalkIn·ToWindow·Tuck·Exit 트리거를 걷어냈다. 컨트롤러에 그 파라미터가 없어
    //   세라가 문 밖에 선 채였다. 이제 코드가 아래 동선을 따라 세라를 직접 걷게 한다(SeraPatrol 과 같은 방식).
    //   좌표는 Home.unity 실측(2026-09-26 배치 조사) — 방은 x -6.26~0.03 · y 5.26~13.17,
    //   문은 아래 벽 x -2.42, 창문은 위 벽 x -4.30. 선택하면 씬 뷰에 기즈모로 보인다.
    [Header("S#02 — 세라 동선 (월드 좌표)")]
    [Tooltip("발소리가 시작되는 복도 자리. 여기서 문 밖까지는 모습을 숨기고 빛만 다가온다.")]
    public Vector2 seraHallPoint       = new Vector2(-5.2f, 4.6f);
    [Tooltip("문 바로 밖. 문틈 대사(House_Sera_Lock_Enter)가 여기서 나온다.")]
    public Vector2 seraDoorOutsidePoint = new Vector2(-2.42f, 4.6f);
    [Tooltip("문 바로 안. 여기서 모습이 드러나고, 나갈 때는 여기서 문 뒤로 사라진다.")]
    public Vector2 seraDoorInsidePoint  = new Vector2(-2.42f, 5.95f);
    [Tooltip("창가. 루 쪽에서는 세라의 뒷모습만 보인다(D S#02 [CAM]).")]
    public Vector2 seraWindowPoint      = new Vector2(-4.30f, 12.3f);
    [Tooltip("이불을 여밀 때 세라가 서는 자리 = 루 위치 + 이 값. 루가 침대 자리를 옮겨도 따라간다.")]
    public Vector2 seraTuckOffset       = new Vector2(-0.75f, 0f);
    [Tooltip("퇴장 후 발소리가 멀어지는 복도 끝. 모습은 숨긴 채 빛만 멀어진다.")]
    public Vector2 seraHallExitPoint    = new Vector2(0.8f, 4.6f);
    [Tooltip("세라 걷는 속도(월드 유닛/초). 정본 「느리고 규칙적」.")]
    public float seraWalkSpeed = 1.2f;

    [Header("S#02 — 이불 여미는 손 오버레이 (D 문단 45)")]
    [Tooltip("⛔ 폐기됨(2026-10-01) — 오버레이 공용 시스템으로 옮겼다. 그림은 Resources/Overlays/house_overlay_sera_tuck_hand.png. 직렬화 값 때문에 필드만 남긴다.")]
    public Sprite tuckHandSprite;
    [Tooltip("이불 여미는 손 오버레이를 띄워 두는 시간(초).")]
    public float  tuckCloseupDuration = 1.2f;

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

    [Header("S#03 — 도자기 손가락 오버레이")]
    [Tooltip("⛔ 폐기됨(2026-10-01) — 오버레이 공용 시스템(OverlayCut)으로 옮겼다. 직렬화 값 때문에 필드만 남긴다.")]
    public Image  closeupImage;
    [Tooltip("⛔ 폐기됨(2026-10-01) — 그림은 Resources/Overlays/house_overlay_lu_porcelain_hand_2.png(두 마디). 직렬화 값 때문에 필드만 남긴다.")]
    public Sprite ceramicFingerSprite;
    // ⚠ 밤 씬은 딱딱 무음 확정(루 캐릭터 설정서 10-3: 집 = 없음).
    //    도자기 손은 소리 없는 시각 연출로만 노출한다. 여기서 SFX를 재생하지 말 것.

    [Header("S#03 — 창밖 날개 그림자 (루는 못 보고 플레이어만 본다)")]
    [Tooltip("창밖을 스쳐 지나가는 날개 그림자 오브젝트. 아래 경로를 따라 움직인다.\n" +
             "비우면 임시 실루엣(어두운 반투명 새 모양)을 런타임에 만들어 쓴다 — 에셋이 오면 여기에 꽂는다.")]
    public GameObject wingShadowObject;
    public float      wingShadowDuration = 0.8f;
    // 2026-09-26 좌표: S#03 은 camera_closeup "루 침대" (ortho 2.5 → 픽셀퍼펙트 2.8125) 상태다.
    //   그 화면에 걸리는 창밖(정원 ▸ 배경) 구간이 대략 x -5.4~0.1 · y 13.4~15.2 라서 그 안을 가로지른다.
    [Tooltip("그림자가 들어오는 자리(월드). 창밖 왼쪽.")]
    public Vector2 wingShadowFrom = new Vector2(-5.9f, 14.7f);
    [Tooltip("그림자가 빠져나가는 자리(월드). 창밖 오른쪽.")]
    public Vector2 wingShadowTo   = new Vector2(0.6f, 14.2f);
    [Tooltip("궤적이 위로 휘는 정도(월드 유닛).")]
    public float   wingShadowArc  = 0.35f;

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
            RestoreSeraState();
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

        // D S#01 에는 세라가 없다. 세라의 평소 자리(방 왼쪽 밖)가 방 카메라에 걸리므로
        // 밤 시퀀스 동안 숨기고, 아침으로 넘어갈 때 원래 자리·상태로 되돌린다.
        CaptureSeraState();
        SetSeraVisible(false);

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

        // 세라를 복도로 옮긴다. 모습은 S#01 부터 숨겨져 있다. 원래 자리·상태로는
        // 아침 전환 때 되돌린다(BadEndingDirector 가 같은 세라를 쓴다).
        Transform sera = seraAnimator ? seraAnimator.transform : null;
        if (sera) sera.position = new Vector3(seraHallPoint.x, seraHallPoint.y, sera.position.z);
        SetSeraVisible(false);

        // 발소리 — 세라가 복도에서 다가온다. 모습 없이 빛만 문 쪽으로 온다.
        PlaySfxIfNamed(footstepSfxName);
        SetSeraLightPresence(0f);
        FadeSeraLight(1f, 1f);

        // 발소리에 루가 반사적으로 침대로 돌아간다(D-S#02 ▶ 연출). 세라가 걷는 동안 같이 일어난다.
        StartCoroutine(ReturnLuToBed());
        yield return SeraWalkTo(seraDoorOutsidePoint);
        SeraFace(Vector2.up);

        // 문틈 — 「잘못 들은 거니…?」 까지. 문틈 눈 오버레이 컷은 노드 안의 show_overlay · hide_overlay 가 맡는다.
        yield return YarnDialogue.PlayAndWait(yarnNode_S2_Enter, false);

        // 방 안으로 들어와 창가로 걸어간다. 루 쪽에서는 뒷모습만 보인다.
        yield return SeraWalkTo(seraDoorInsidePoint);
        SetSeraVisible(true);
        yield return SeraWalkTo(seraWindowPoint);

        // 창틀이 비어 있다. 사라지는 연출을 넣지 않는다 — 세라가 창가에 선 그 프레임에서 이미 없다.
        if (owlObject) owlObject.SetActive(false);
        SeraFace(Vector2.up);

        // 창밖을 내다보는 한 박자. 굳은 표정은 플레이어에게도 보여주지 않는다(2회차 복선).
        yield return _wait1s;

        // 창문 잠금 — 이 씬에서 가장 큰 소리. D 순서대로 「위험해」 앞에 온다(문단 41→43).
        PlaySfxIfNamed(windowLockSfxName);
        yield return _wait07s;

        yield return YarnDialogue.PlayAndWait(yarnNode_S2_Window, false);

        // 루의 머리맡으로 간다.
        var lu = FindLu();
        if (lu != null && sera != null)
        {
            Vector2 luPos = lu.transform.position;
            yield return SeraWalkTo(luPos + seraTuckOffset);
            SeraFace(luPos - (Vector2)sera.position);
        }
        yield return _wait05s;

        // 이불 여미기 — 다정함과 구속이 같은 그림이 되어야 한다(문단 45). 오버레이 컷(F-3-9).
        yield return OverlayCut.Instance.ShowForSeconds(OverlayIds.HouseSeraTuckHand, tuckCloseupDuration);

        yield return YarnDialogue.PlayAndWait(yarnNode_S2_Tuck, false);

        // 퇴장. 문 안쪽까지 걸어가 문 뒤로 사라지고, 문이 닫힌 뒤 발소리가 멀어진다.
        yield return SeraWalkTo(seraDoorInsidePoint);
        SetSeraVisible(false);
        AudioManager.Instance?.Play("doorClose");
        FadeSeraLight(0f, 1f);
        var leaving = StartCoroutine(SeraLeaveThroughHall());

        // 완전히 사라질 때까지 카메라 고정 — 3초 정도 아무 일도 일어나지 않게 둔다.
        yield return _wait3s;

        StopCoroutine(leaving);
        { var w = SeraWalker; if (w != null) w.Stop(); }
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
        // 마디 수(인형화 단계)는 OverlayCut 이 고른다 — 여기서는 20 이라 두 마디다.
        yield return OverlayCut.Instance.ShowForSeconds(OverlayIds.HouseLuPorcelainHand, 1.5f);

        yield return YarnDialogue.PlayAndWait(yarnNode_S3_Close, false);

        // 창밖 날개 그림자 — 루는 눈을 감아 못 보고, 플레이어만 본다.
        // 이 비대칭이 데모 내내 유지되는 시점 규칙이다.
        // 창밖 멀리서 한 번 스치고 사라진다(D 문단 64). 소리는 붙이지 않는다.
        yield return StartCoroutine(PlayWingShadow());
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

    // ─── S#02 세라 이동 ───────────────────────────
    // 걷기·방향·숨기기는 SeraStageWalker 가 맡는다(S#04 KitchenTriggerCutscene 과 공용).
    // 여기 있는 것은 그 얇은 창구뿐이다.

    SeraStageWalker SeraWalker => SeraStageWalker.On(seraAnimator);

    void CaptureSeraState()          { var w = SeraWalker; if (w != null) w.Capture(); }
    void RestoreSeraState()          { var w = SeraWalker; if (w != null) w.Restore(); }
    void SetSeraVisible(bool visible){ var w = SeraWalker; if (w != null) w.SetVisible(visible); }
    void SeraFace(Vector2 delta)     { var w = SeraWalker; if (w != null) w.Face(delta); }

    IEnumerator SeraWalkTo(Vector2 target)
    {
        var w = SeraWalker;
        if (w != null) yield return w.WalkTo(target, seraWalkSpeed);
    }

    /// <summary>문 밖으로 나간 뒤 복도를 따라 멀어진다. 모습은 숨긴 채 빛만 움직인다 — 벽을 가로지르지 않게 문을 거친다.</summary>
    IEnumerator SeraLeaveThroughHall()
    {
        yield return SeraWalkTo(seraDoorOutsidePoint);
        yield return SeraWalkTo(seraHallExitPoint);
    }

    void OnDrawGizmosSelected()
    {
        Vector2[] route = { seraHallPoint, seraDoorOutsidePoint, seraDoorInsidePoint, seraWindowPoint };
        Gizmos.color = new Color(1f, 0.85f, 0.4f);
        for (int i = 0; i < route.Length; i++)
        {
            Gizmos.DrawWireSphere(route[i], 0.15f);
            if (i > 0) Gizmos.DrawLine(route[i - 1], route[i]);
        }
        Gizmos.DrawWireSphere(seraHallExitPoint, 0.15f);
        Gizmos.DrawLine(seraDoorOutsidePoint, seraHallExitPoint);

        Gizmos.color = new Color(0.4f, 0.4f, 0.5f);
        Gizmos.DrawLine(wingShadowFrom, wingShadowTo);
    }

    // ─── S#03 날개 그림자 ─────────────────────────

    IEnumerator PlayWingShadow()
    {
        GameObject shadow = wingShadowObject != null ? wingShadowObject : BuildPlaceholderWingShadow();
        if (shadow == null) yield break;

        var t  = shadow.transform;
        var sr = shadow.GetComponentInChildren<SpriteRenderer>();
        Color baseColor = sr != null ? sr.color : Color.black;
        Vector3 baseScale = t.localScale;
        float z = t.position.z;

        shadow.SetActive(true);
        float duration = Mathf.Max(0.05f, wingShadowDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / duration);

            Vector2 p = Vector2.Lerp(wingShadowFrom, wingShadowTo, u);
            p.y += Mathf.Sin(u * Mathf.PI) * wingShadowArc;
            t.position = new Vector3(p.x, p.y, z);

            // 날갯짓 — 세로로 두 번 접혔다 펴진다
            Vector3 s = baseScale;
            s.y *= 1f - 0.4f * Mathf.Abs(Mathf.Sin(u * Mathf.PI * 2f));
            t.localScale = s;

            // 가장자리에서 흐려진다 — 「스치고 사라진다」
            if (sr != null)
            {
                float a = Mathf.Min(1f, Mathf.Min(u, 1f - u) / 0.2f);
                Color c = baseColor; c.a = baseColor.a * a;
                sr.color = c;
            }
            yield return null;
        }

        shadow.SetActive(false);
        t.localScale = baseScale;
        if (sr != null) sr.color = baseColor;
        if (wingShadowObject == null)
        {
            if (sr != null && sr.sprite != null) { Destroy(sr.sprite.texture); Destroy(sr.sprite); }
            Destroy(shadow);
        }
    }

    /// <summary>
    /// 에셋이 오기 전의 임시 날개 실루엣. 날개를 펼친 새를 멀리서 본 V 자 모양을 도트로 찍는다.
    /// 정원 배경(sortingOrder 60) 바로 위에 그려 창밖에만 보이게 하고, 정원 밑에 둬서 정원과 함께 꺼진다.
    /// </summary>
    GameObject BuildPlaceholderWingShadow()
    {
        const int W = 24, H = 10;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var px = new Color32[W * H];
        for (int x = 0; x < W; x++)
        {
            float d = Mathf.Abs(x - (W - 1) * 0.5f) / ((W - 1) * 0.5f);   // 몸통 0 → 날개 끝 1
            float centerY = 3f + 4f * d * d;                               // 날개 끝이 위로 휜다
            float half = Mathf.Lerp(1.6f, 0.5f, d);                        // 몸통 쪽이 두껍다
            for (int y = 0; y < H; y++)
                px[y * W + x] = Mathf.Abs(y - centerY) <= half ? new Color32(0, 0, 0, 255) : new Color32(0, 0, 0, 0);
        }
        tex.SetPixels32(px);
        tex.Apply();

        var go = new GameObject("WingShadow (임시)");
        if (gardenViewRoot != null) go.transform.SetParent(gardenViewRoot.transform, false);
        go.transform.position = new Vector3(wingShadowFrom.x, wingShadowFrom.y, 0f);
        go.transform.localScale = Vector3.one;
        go.transform.localScale = new Vector3(1f / go.transform.lossyScale.x, 1f / go.transform.lossyScale.y, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 32f);
        sr.color = new Color(0f, 0f, 0f, 0.55f);
        sr.sortingOrder = 61;
        go.SetActive(false);
        return go;
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
