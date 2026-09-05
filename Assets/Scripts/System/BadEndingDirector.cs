using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// 배드 엔딩 컷씬 — 정본 D 의 BE#01-a~d(집 구간)와 BE#02-b~c(마을 구간의 집 파트)를 재생합니다.
/// Home 씬에 하나 배치합니다.
///
/// <para>
/// <b>왜 BadEndingScene 이 아니라 Home 씬인가.</b> 정본이 BE#01-d 와 BE#02-c 의 식탁 컷을
/// 「S#04A 와 완전히 같은 구도, 접시 수만 다름」으로 못박았습니다(정본 문단 500 · 668 · 679).
/// 빈 씬에 새로 그리면 그 대칭이 성립하지 않으므로 실제 부엌·식탁을 그대로 씁니다.
/// BadEndingScene 은 연출이 끝난 뒤의 「다시 시도해 보시겠습니까?」 화면만 맡습니다.
/// </para>
///
/// <para>
/// <b>클로즈업 컷은 비어 있어도 됩니다.</b> 아트가 아직 없으므로 Image 슬롯을 비워 두면
/// <see cref="FlashCloseup"/> 가 조용히 건너뜁니다(KitchenTriggerCutscene 과 같은 방식).
/// SFX 이름도 AudioManager 에 등록된 것만 넣고, 없으면 빈 문자열로 두어 무음으로 갑니다.
/// </para>
///
/// <para>
/// ⚠ 인형화 페널티를 붙이지 않습니다(CLAUDE.md §2 · 정본 문단 452 · 631).
/// 복귀 지점 계산은 <see cref="BadEndingSequence"/> 소관이며 여기서 손대지 않습니다.
/// </para>
/// </summary>
public class BadEndingDirector : MonoBehaviour
{
    public static BadEndingDirector Instance { get; private set; }

    /// <summary>
    /// 배드 엔딩 연출이 도는 중. 이 동안에는 집의 다른 시스템이 끼어들면 안 됩니다.
    /// <see cref="HouseEscapePressureController"/> 가 이 값을 보고 압박 재개를 건너뜁니다.
    /// </summary>
    public static bool IsPlaying { get; private set; }

    // 마을에서 발각돼 집으로 넘어오는 중. Home 이 로드되면 BE#02-b 부터 이어서 재생한다.
    static bool _pendingCaptured;

    // ── 위치 ────────────────────────────────────────────────────────────────
    [Header("BE#01 — 이동 지점")]
    [Tooltip("BE#01-a. 현관문 앞. 비우면 현재 위치에서 그대로 진행한다.")]
    public Transform frontDoorSpawn;
    [Tooltip("BE#01-b · c. 거실 소파에 앉은 자리.")]
    public Transform livingRoomSpawn;
    [Tooltip("BE#01-d · BE#02-c. 식탁. ⚠ KitchenTriggerCutscene 의 playerDiningSpawn 과 같은 지점이어야 한다 — 정본이 S#04A 와 같은 구도를 요구한다.")]
    public Transform diningSpawn;

    [Header("BE#02 — 이동 지점")]
    [Tooltip("BE#02-b. 밖에서 잠긴 루의 방.")]
    public Transform luRoomSpawn;

    [Header("세라")]
    [Tooltip("컷씬에 등장시킬 세라. 비우면 세라 없이 대사만 진행한다.")]
    public GameObject seraObject;
    [Tooltip("BE#01-c 에서 세라가 거실로 들어와 서는 자리.")]
    public Transform seraLivingSpawn;
    [Tooltip("BE#02-c 에서 세라가 식탁에 앉는 자리.")]
    public Transform seraDiningSpawn;

    // ── 컷 ──────────────────────────────────────────────────────────────────
    [Header("BE#01-a 컷 — 비워 두면 건너뛴다")]
    [Tooltip("열쇠 구멍 클로즈업.")]
    public Image keyholeCloseup;
    [Tooltip("손잡이를 쥔 손 클로즈업.")]
    public Image handOnKnobCloseup;

    [Header("BE#02-b 컷 — 비워 두면 건너뛴다")]
    [Tooltip("문이 열리며 들어오는 빛과 세라의 역광 실루엣.")]
    public Image backlitSeraCloseup;

    [Tooltip("클로즈업 한 컷을 띄워 두는 시간(초).")]
    public float closeupHoldSeconds = 1.4f;

    // ── 식탁 ────────────────────────────────────────────────────────────────
    [Header("식탁 접시 — 비워 두면 건드리지 않는다")]
    [Tooltip("BE#01-d. 접시 3개(루 · 세라 · 유) 상태.")]
    public GameObject platesThree;
    [Tooltip("BE#02-c. 접시 2개(세라 · 유). 루의 자리는 처음부터 없었던 것처럼 차린다(정본 문단 673).")]
    public GameObject platesTwo;

    // ── BE#01-b 조명 ────────────────────────────────────────────────────────
    [Header("BE#01-b — 거실의 시간 경과")]
    [Tooltip("각도만 움직일 조명. 비우면 대기만 한다.")]
    public Light2D livingLight;
    [Tooltip("단계별 밝기. 정본은 '3~4단'을 요구한다(문단 516).")]
    public float[] livingLightIntensities = { 1f, 0.72f, 0.48f, 0.3f };
    [Tooltip("한 단계마다 조명이 도는 각도(도).")]
    public float livingLightAngleStep = 9f;
    [Tooltip("한 단계를 유지하는 시간(초).")]
    public float livingLightStageSeconds = 2.2f;

    // ── 카메라 ──────────────────────────────────────────────────────────────
    [Header("BE#01-a — 문이 커지는 컷")]
    [Tooltip("컷이 바뀔 때마다 줄어드는 orthoSize 단계(정본 문단 460). 비우면 줌을 쓰지 않는다.")]
    public float[] doorZoomStages = { 4.2f, 3.4f, 2.6f };
    [Tooltip("한 줌 단계에 걸리는 시간(초).")]
    public float doorZoomDuration = 0.9f;

    // ── SFX ─────────────────────────────────────────────────────────────────
    // ⚠ AudioManager 에 등록된 이름만 넣는다. 없는 이름을 지어내면 조용히 무음이 되는 것이 아니라
    //   경고만 남고 연출 의도가 사라진다. 미등록이면 빈 문자열로 두는 것이 정답이다.
    [Header("SFX — AudioManager 에 등록된 이름만. 비우면 무음")]
    [Tooltip("BE#01-a. 열쇠가 헛도는 소리(정본 문단 458).")]
    public string sfxKeySlipName = "";
    [Tooltip("BE#01-c. 현관문이 열리는 소리(정본 문단 484).")]
    public string sfxDoorOpenName = "";
    [Tooltip("BE#01-c. 장바구니를 내려놓는 소리.")]
    public string sfxBasketDownName = "";
    [Tooltip("BE#01-d · BE#02-c. 식기 소리.")]
    public string sfxTablewareName = "";
    [Tooltip("BE#02-b. 문 너머 저녁을 만드는 소리(멀게).")]
    public string sfxDistantCookingName = "";

    // ── Yarn 노드 ───────────────────────────────────────────────────────────
    // 이름은 Scenario/node_map.json 의 BE 씬 등재와 같다. 바꾸면 게이트가 막는다.
    // BE#01-b · BE#02-b · BE#02-c 에는 노드가 없다. 정본상 대사가 0줄이고,
    // Yarn 이 본문 없는 노드를 컴파일에서 떨어뜨리기 때문이다(House_BadEnding.yarn 헤더 참조).
    // 그 구간의 길이는 아래 조명 단계와 beat 가 정한다.
    [Header("Yarn 노드")]
    public string yarnNode_BE01a = "House_BadEnd_Sealed_Door";
    public string yarnNode_BE01c = "House_BadEnd_Sera_Return";
    public string yarnNode_BE01d = "House_BadEnd_ThreePlates";

    [Header("연출 간격")]
    [Tooltip("컷 사이 암전 페이드 시간(초).")]
    public float cutFadeDuration = 0.5f;
    [Tooltip("컷이 열린 뒤 한 박자 두는 시간(초).")]
    public float beatSeconds = 0.9f;

    // ── 내부 상태 ───────────────────────────────────────────────────────────
    ClearSky.SimplePlayerController _lockedCtrl;
    float     _origOrthoSize;
    bool      _origSeraActive;
    Vector3   _origSeraPos;
    Transform _origCameraTarget;
    float     _origLightIntensity;
    Quaternion _origLightRotation;

    // ─────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        // ⚠ Destroy(gameObject) 를 쓰지 않는다. 이 프로젝트의 다른 매니저들이 그 함정으로
        //   같은 GameObject 에 붙은 컴포넌트를 통째로 날린 전례가 있다. 컴포넌트만 지운다.
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (!_pendingCaptured) return;
        _pendingCaptured = false;
        StartCoroutine(CapturedHouseRoutine());
    }

    // ─── 진입점 ─────────────────────────────────────────────────────────────
    /// <summary>
    /// BE#01 (집 구간 · 90초 초과). 호출한 코루틴은 연출이 끝날 때까지 기다립니다.
    /// 끝나면 <see cref="EndingManager.TriggerBadEnding"/> 까지 이 안에서 처리합니다.
    /// </summary>
    public static IEnumerator PlayHouseSealed()
    {
        var d = Instance;
        if (d == null)
        {
            // 씬에 배치돼 있지 않아도 엔딩 자체는 성립해야 한다. 컷씬만 건너뛴다.
            // 압박 연출을 지우는 것은 원래 BE#01-a 의 암전 안에서 하므로 여기서 대신 지운다.
            Debug.LogWarning("[BadEndingDirector] Home 씬에 배치돼 있지 않습니다. BE#01 컷씬을 건너뜁니다.");
            ScreenEdgeEffectController.ClearSustained();
            EndingManager.TriggerBadEnding(BadEndingType.HouseSealed);
            yield break;
        }
        yield return d.StartCoroutine(d.HouseSealedRoutine());
    }

    /// <summary>
    /// BE#02 의 집 파트를 예약합니다. 마을(MapScene)에서 발각 컷을 재생한 뒤 호출하고,
    /// 이어서 Home 씬으로 전환하면 <see cref="Start"/> 가 BE#02-b 부터 이어 재생합니다.
    /// </summary>
    /// <remarks>
    /// <see cref="IsPlaying"/> 을 <b>여기서 미리</b> 세웁니다. Home 의 sceneLoaded 핸들러가
    /// 이 오브젝트의 Start() 보다 먼저 돌기 때문에, 재생 시점에 세우면 늦습니다 —
    /// 그 사이에 <see cref="HouseEscapePressureController"/> 가 90초 압박을 다시 걸어 버립니다.
    /// </remarks>
    public static void QueueCapturedHousePart()
    {
        _pendingCaptured = true;
        IsPlaying        = true;
    }

    // ─── BE#01 — 집 구간 ────────────────────────────────────────────────────
    IEnumerator HouseSealedRoutine()
    {
        BeginPlayback();

        Dbg.Log("[배드엔딩] BE#01-a 시작");
        yield return RunBE01a_SealedDoor();
        Dbg.Log("[배드엔딩] BE#01-b 시작");
        yield return RunBE01b_LivingWait();
        Dbg.Log("[배드엔딩] BE#01-c 시작");
        yield return RunBE01c_SeraReturn();
        Dbg.Log("[배드엔딩] BE#01-d 시작");
        yield return RunBE01d_ThreePlates();
        Dbg.Log("[배드엔딩] BE#01 종료 — 엔딩 화면으로");

        EndPlayback();
        EndingManager.TriggerBadEnding(BadEndingType.HouseSealed);
    }

    /// <remarks>
    /// 정본 문단 451: 「시간이 끝나는 순간 루가 어디에 있든 즉시 암전한 뒤 BE#01-a 를 현관 앞에서 연다.
    /// 루가 현관까지 내려가는 과정은 보여주지 않는다.」 그래서 이동은 암전 안에서 끝낸다.
    /// 문단 463: 압박 연출의 마지막 단계가 그대로 암전으로 닫힌다 — 새 연출을 만들지 않는다.
    /// </remarks>
    IEnumerator RunBE01a_SealedDoor()
    {
        yield return FadeOut();

        // 암전 중에 정리한다. 정본 문단 456 — 단검을 파지 중이었다면 발동과 동시에 해제한다.
        DaggerFilterController.Instance?.SwitchToFantasyForced();
        FilterManager.Instance?.SetFilter(FilterType.Fantasy);
        ScreenEdgeEffectController.ClearSustained();
        TeleportPlayer(frontDoorSpawn);

        yield return FadeIn();

        PlaySfxIfNamed(sfxKeySlipName);

        // [CAM] 열쇠 구멍 → 손잡이를 쥔 손 → 문 전체 와이드. 컷마다 문이 조금씩 커진다.
        yield return FlashCloseup(keyholeCloseup);
        yield return FlashCloseup(handOnKnobCloseup);
        yield return ZoomThroughStages();

        yield return YarnDialogue.PlayIfExists(yarnNode_BE01a, false);
        yield return WaitBeat();
    }

    /// <remarks>
    /// 정본 문단 477: 「페이드나 디졸브를 쓰지 않고 같은 컷 안에서 조명만 이동시킨다.」
    /// 그래서 이 씬 <b>안에서는</b> 컷을 바꾸지 않는다. 거실로 옮기는 것은 앞 컷과의 전환이다.
    /// 문단 479: '축적 없이 흐른다' 가 이 씬의 전부다 — 루의 자세도 바뀌지 않는다.
    /// </remarks>
    IEnumerator RunBE01b_LivingWait()
    {
        yield return CutTo(livingRoomSpawn);

        if (livingLight == null || livingLightIntensities == null || livingLightIntensities.Length == 0)
        {
            // 조명이 배선돼 있지 않아도 시간의 경과는 흘러야 한다.
            yield return new WaitForSecondsRealtime(livingLightStageSeconds * 3f);
            yield break;
        }

        float baseAngle = livingLight.transform.eulerAngles.z;
        for (int i = 0; i < livingLightIntensities.Length; i++)
        {
            livingLight.intensity = livingLightIntensities[i];
            livingLight.transform.rotation =
                Quaternion.Euler(0f, 0f, baseAngle + livingLightAngleStep * i);
            yield return new WaitForSecondsRealtime(livingLightStageSeconds);
        }
    }

    /// <remarks>
    /// 정본 문단 485: BE#01-b 의 루 정면 컷을 그대로 유지한다. 세라를 따로 잡지 않는다 — 카메라를 옮기지 않는다.
    /// 문단 492: 세라는 코트를 언급하지 않는다. <b>시선이 코트에 잠깐도 머물지 않아야 한다.</b>
    /// 문단 494: 세라는 화내지 않는다. 두 배드 엔딩 모두 같은 규칙이다.
    /// </remarks>
    IEnumerator RunBE01c_SeraReturn()
    {
        PlaySfxIfNamed(sfxDoorOpenName);
        yield return new WaitForSecondsRealtime(0.6f);
        PlaySfxIfNamed(sfxBasketDownName);

        ShowSera(seraLivingSpawn);
        yield return WaitBeat();

        yield return YarnDialogue.PlayIfExists(yarnNode_BE01c, false);

        HideSera();
        yield return WaitBeat();
    }

    /// <remarks>
    /// 정본 문단 500: 세 개의 접시를 <b>S#04A 와 완전히 같은 구도</b>로 잡는다. 아침과 저녁의 빛만 다르다.
    /// 문단 508: 루가 유의 자리를 보는 시간을 길게 두지 않는다. 한 박자면 된다.
    /// 문단 514: 코트를 벗는 장면은 두지 않는다.
    /// </remarks>
    IEnumerator RunBE01d_ThreePlates()
    {
        yield return CutTo(diningSpawn);

        SetPlates(three: true);
        // 정본 문단 504 — 세라가 "저녁 먹게 나오렴" 하고 부른 자리다. 부엌에 있다.
        ShowSera(seraDiningSpawn);
        PlaySfxIfNamed(sfxTablewareName);
        yield return WaitBeat();

        yield return YarnDialogue.PlayIfExists(yarnNode_BE01d, false);
        yield return WaitBeat();
    }

    // ─── BE#02 — 마을 구간의 집 파트 ────────────────────────────────────────
    IEnumerator CapturedHouseRoutine()
    {
        BeginPlayback();

        Dbg.Log("[배드엔딩] BE#02-b 시작");
        yield return RunBE02b_LockedRoom();
        Dbg.Log("[배드엔딩] BE#02-c 시작");
        yield return RunBE02c_TwoPlates();
        Dbg.Log("[배드엔딩] BE#02 종료 — 엔딩 화면으로");

        EndPlayback();
        EndingManager.TriggerBadEnding(BadEndingType.Captured);
    }

    /// <remarks>
    /// 정본 문단 658: 세라는 한 마디도 하지 않는다. 문을 열고, 웃고, 놓고, 닫는다. 네 동작뿐이다.
    /// 문단 661: BE#01 에서 계속 말을 걸던 목소리가 전부 사라지고 웃음만 남는다 — 그래서 대사가 0줄이다.
    /// </remarks>
    IEnumerator RunBE02b_LockedRoom()
    {
        // ⚠ 여기서 페이드 인을 하지 않는다. 마을에서 넘어올 때 TransitionManager 의
        //    씬 전환이 이미 페이드 인을 맡고 있고, 겹치면 두 코루틴이 같은 오버레이를 다툰다.
        //    이동은 코루틴의 첫 동기 구간에서 끝나므로 페이드가 걷힐 때 이미 방 안이다.
        TeleportPlayer(luRoomSpawn);

        PlaySfxIfNamed(sfxDistantCookingName);
        yield return new WaitForSecondsRealtime(beatSeconds * 2f);

        yield return FlashCloseup(backlitSeraCloseup);
        yield return WaitBeat();
    }

    /// <remarks>
    /// 정본 문단 670: 루는 나오지 못한다. 세라가 문을 잠갔으니까.
    /// 그래서 <b>루를 옮기지 않고 카메라만</b> 부엌으로 넘긴다. BE#01-d 와 같은 구도, 접시 수만 다르다.
    /// 문단 673: 빈자리에 접시를 놓지 않는다. 치운 것이 아니라 처음부터 없었던 것처럼 차린다.
    /// </remarks>
    IEnumerator RunBE02c_TwoPlates()
    {
        yield return FadeOut();

        SetPlates(three: false);
        ShowSera(seraDiningSpawn);
        MoveCameraTo(diningSpawn);

        yield return FadeIn();

        PlaySfxIfNamed(sfxTablewareName);
        yield return new WaitForSecondsRealtime(beatSeconds * 3f);
    }

    // ─── 재생 전후 ──────────────────────────────────────────────────────────
    void BeginPlayback()
    {
        IsPlaying   = true;

        // ⚠ 배드 엔딩은 어떤 상태에서 불려도 끝까지 재생돼야 한다.
        //    턴제 전투가 걸려 있으면 Time.timeScale 이 0 이라(EncounterManager.StartTurnBased)
        //    스케일 시간 대기가 영영 안 끝난다 — 2026-08-23 에 BE#02 가 실제로 여기서 멈췄다.
        //    EndingManager.TriggerBadEnding 도 같은 이유로 timeScale 을 되돌린다.
        //    아래 대기는 전부 Realtime 이지만, 화면(플레이어·애니메이션)도 멈춰 있으면
        //    컷씬이 정지 화면이 되므로 여기서 함께 풀어 준다.
        Time.timeScale = 1f;

        _lockedCtrl = YarnDialogue.LockPlayer();

        // 정본 문단 459 · 637 — [UI] 없음. HideHUD 는 HUD 줄만 감추므로,
        // 떠 있을 수 있는 목표 패널은 ResetCutscene 으로 먼저 지운다.
        ObjectiveManager.Instance?.ResetCutscene();
        ObjectiveManager.Instance?.HideHUD();

        var cam = CameraFollow.Instance;
        if (cam != null)
        {
            _origOrthoSize    = cam.currentOrthoSize;
            _origCameraTarget = cam.target;
        }

        // 세라는 씬에 하나뿐이라 컷씬이 끝나면 원래 자리로 돌려놓는다.
        // (엔딩 뒤에는 씬을 새로 불러오지만, 도중에 중단돼도 씬이 망가지지 않게 한다.)
        if (seraObject != null)
        {
            _origSeraActive = seraObject.activeSelf;
            _origSeraPos    = seraObject.transform.position;
        }

        if (livingLight != null)
        {
            _origLightIntensity = livingLight.intensity;
            _origLightRotation  = livingLight.transform.rotation;
        }
    }

    void EndPlayback()
    {
        // ⚠ 남아 있는 대사를 먼저 끊는다. 배드 엔딩은 화면을 통째로 가져가는 자리라
        //    다른 대사가 떠 있으면 안 되고, 무엇보다 줄이 페이드 중인 채로 씬을 넘기면
        //    Yarn 의 LinePresenter 가 파괴된 CanvasGroup 을 만져 예외를 던진다(2026-08-23 실측).
        if (YarnDialogue.IsRunning) YarnDialogue.Runner.Stop();

        // 씬을 넘기기 전에 되돌려 둔다. 되감기 복귀 후 같은 씬을 다시 쓰기 때문이다.
        RestoreCamera();

        if (seraObject != null)
        {
            seraObject.transform.position = _origSeraPos;
            seraObject.SetActive(_origSeraActive);
        }

        if (livingLight != null)
        {
            livingLight.intensity          = _origLightIntensity;
            livingLight.transform.rotation = _origLightRotation;
        }

        YarnDialogue.UnlockPlayer(_lockedCtrl);
        _lockedCtrl = null;

        ObjectiveManager.Instance?.ResetCutscene();
        IsPlaying = false;
    }

    // ─── 도구 ───────────────────────────────────────────────────────────────
    IEnumerator FadeOut()
    {
        var tm = TransitionManager.Instance;
        if (tm == null) yield break;
        yield return tm.FadeToBlack(cutFadeDuration);
    }

    IEnumerator FadeIn()
    {
        var tm = TransitionManager.Instance;
        if (tm == null) yield break;
        yield return tm.FadeFromBlack(cutFadeDuration);
    }

    /// <summary>암전 → 이동 → 밝아짐. 컷이 바뀌는 자리에 쓴다.</summary>
    /// <remarks>
    /// ⚠ 줌을 여기서 되돌린다. BE#01-a 가 문을 키우려고 orthoSize 를 좁혀 놓기 때문에,
    /// 그대로 두면 BE#01-d 의 식탁이 바짝 당겨진 화면으로 잡힌다 —
    /// 정본이 요구하는 「S#04A 와 완전히 같은 구도」(문단 500)가 깨진다.
    /// 되돌리는 것은 암전 안에서 하므로 줌이 풀리는 과정이 보이지 않는다.
    /// </remarks>
    IEnumerator CutTo(Transform spawn)
    {
        yield return FadeOut();
        TeleportPlayer(spawn);
        RestoreZoom();
        yield return FadeIn();
        yield return WaitBeat();
    }

    void RestoreZoom()
    {
        if (_origOrthoSize <= 0f) return;
        CameraFollow.Instance?.ZoomTo(_origOrthoSize, 0.05f);
    }

    WaitForSecondsRealtime WaitBeat() => new WaitForSecondsRealtime(beatSeconds);

    void TeleportPlayer(Transform spawn)
    {
        if (spawn == null) return;
        var ctrl = _lockedCtrl != null
            ? _lockedCtrl
            : FindAnyObjectByType<ClearSky.SimplePlayerController>();
        if (ctrl == null) return;

        ctrl.transform.position = spawn.position;
        // 스냅하지 않으면 카메라가 이전 자리에서 새 자리까지 부드럽게 따라오는 것이 그대로 보인다.
        CameraFollow.Instance?.SnapCameraToFollow();
    }

    /// <summary>클로즈업 Image 를 잠깐 띄웠다 끈다. 비어 있으면 조용히 건너뛴다.</summary>
    IEnumerator FlashCloseup(Image image)
    {
        if (image == null) yield break;
        image.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(closeupHoldSeconds);
        image.gameObject.SetActive(false);
    }

    /// <remarks>
    /// ⚠ 카메라 줌은 RoomTransfer(방 이동)·CameraDirector 와 서로 덮어쓴 전례가 있다.
    /// 그래서 <b>입력이 잠긴 컷씬 구간에서만</b> 쓰고 <see cref="RestoreCamera"/> 로 반드시 되돌린다.
    /// </remarks>
    IEnumerator ZoomThroughStages()
    {
        var cam = CameraFollow.Instance;
        if (cam == null || doorZoomStages == null || doorZoomStages.Length == 0)
        {
            yield return WaitBeat();
            yield break;
        }

        foreach (float size in doorZoomStages)
        {
            cam.ZoomTo(size, doorZoomDuration);
            yield return new WaitForSecondsRealtime(doorZoomDuration);
        }
    }

    void MoveCameraTo(Transform target)
    {
        if (target == null) return;
        var cam = CameraFollow.Instance;
        if (cam == null) return;
        cam.SetTarget(target);
        cam.SnapToTarget();
    }

    void RestoreCamera()
    {
        var cam = CameraFollow.Instance;
        if (cam == null) return;
        if (_origCameraTarget != null)
        {
            cam.SetTarget(_origCameraTarget);
            cam.SnapToTarget();
        }
        if (_origOrthoSize > 0f) cam.ZoomTo(_origOrthoSize, 0.3f);
    }

    void ShowSera(Transform spawn)
    {
        if (seraObject == null) return;
        if (spawn != null) seraObject.transform.position = spawn.position;
        seraObject.SetActive(true);
    }

    void HideSera()
    {
        if (seraObject != null) seraObject.SetActive(false);
    }

    void SetPlates(bool three)
    {
        if (platesThree != null) platesThree.SetActive(three);
        if (platesTwo   != null) platesTwo.SetActive(!three);
    }

    void PlaySfxIfNamed(string soundName)
    {
        if (string.IsNullOrEmpty(soundName)) return;
        AudioManager.Instance?.Play(soundName);
    }
}
