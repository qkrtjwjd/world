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

    // 2026-09-27 개정 D BE#02 대조 — 좌표는 배치 실측. 루의 방 문은 방 아래 변(-2.42, 5.1~5.4)이다.
    [Header("BE#02-b — 루의 방 (월드 좌표)")]
    [Tooltip("루가 방 안에서 서서 문을 바라보는 자리(D 665). luRoomSpawn 은 문 바로 앞이라 세라와 겹친다.")]
    public Vector2 luLockedRoomPoint = new Vector2(-2.40f, 7.40f);
    [Tooltip("세라가 문을 열고 서는 문간(D 674).")]
    public Vector2 seraDoorwayPoint  = new Vector2(-2.42f, 5.45f);
    [Tooltip("세라가 밀어넣은 저녁밥 쟁반(D 677 · 705). 비우면 없음 — 그림 대기.")]
    public GameObject dinnerTray;
    [Tooltip("BE#02-b. 방문이 밖에서 잠기는 소리.")]
    public string sfxRoomDoorLockName = "";
    [Tooltip("BE#02-b. 방문이 열리는 / 닫히는 소리.")]
    public string sfxRoomDoorOpenName = "";
    public string sfxRoomDoorCloseName = "";
    [Tooltip("BE#02-c. 의자 끄는 소리(D 687).")]
    public string sfxChairDragName = "";

    [Header("세라")]
    [Tooltip("컷씬에 등장시킬 세라. 비우면 세라 없이 대사만 진행한다.")]
    public GameObject seraObject;
    [Tooltip("BE#01-c 에서 세라가 거실로 들어와 서는 자리.")]
    public Transform seraLivingSpawn;
    [Tooltip("BE#02-c 에서 세라가 식탁에 앉는 자리.")]
    public Transform seraDiningSpawn;

    // 2026-09-27 개정 D BE#01 대조 — 세라가 뿅 나타나고 사라지던 것을 걸어 들고 나게 했다. 좌표는 배치 실측(거실·부엌 가구 경계)으로 잡았다.
    //   ⚠ seraLivingSpawn(2.25,-2.25)은 소파 안쪽이라 세라가 가구에 가려졌다 — BE#01-c 는 아래 지점을 쓴다.
    [Header("BE#01-c · d — 동선 (월드 좌표)")]
    [Tooltip("세라가 현관문을 열고 들어서는 자리(D 499). 현관문 바로 안쪽.")]
    public Vector2 seraEnterPoint   = new Vector2(-0.02f, -2.1f);
    [Tooltip("세라가 루에게 말을 거는 자리. 소파(x 2.09~) 왼쪽 옆.")]
    public Vector2 seraGreetPoint   = new Vector2(1.45f, -2.15f);
    [Tooltip("세라가 들어가는 부엌 — 싱크대 앞(S#04 seraSinkPoint 와 같은 자리).")]
    public Vector2 seraKitchenPoint = new Vector2(-3.20f, 1.65f);
    [Tooltip("BE#01-d 첫머리 — 루가 소파에서 일어나 방 쪽으로 걷는 목표(D 515). 여기서 암전한다.")]
    public Vector2 luLeaveSofaPoint = new Vector2(1.6f, -1.1f);
    [Tooltip("식탁의 유의 자리. 루(왼쪽 의자) · 세라(오른쪽 의자) 사이 식탁 위쪽 변 — 의자 오브젝트는 없다(F-3-2 빈 의자는 그림 대기).")]
    public Vector2 yuSeatPoint      = new Vector2(-4.0f, 0.1f);   // 루 자리에서 위쪽이 우세 → 고개를 드는 것으로 보인다(옆이면 식탁을 보는 것과 구분이 안 됐다 — 실측)

    // ── 컷 ──────────────────────────────────────────────────────────────────
    // ⛔ 2026-09-27: BE#01-a 의 두 컷은 개정 D 문단 474(「컷을 잇지 않는다」)로 쓰지 않는다. 직렬화 값 때문에 필드만 남긴다.
    [Header("BE#01-a 컷 — 폐기됨 (쓰지 않는다)")]
    [Tooltip("열쇠 구멍 클로즈업.")]
    public Image keyholeCloseup;
    [Tooltip("손잡이를 쥔 손 클로즈업.")]
    public Image handOnKnobCloseup;

    // ⛔ 2026-09-27: 개정 D 674 로 BE#02-b 는 클로즈업이 아니라 「문간에 선 역광 실루엣 스프라이트」다. 쓰지 않는다 — 직렬화 값 때문에 필드만 남긴다.
    [Header("BE#02-b 컷 — 폐기됨 (쓰지 않는다)")]
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

    [Tooltip("같은 박자로 함께 낮출 전체 조명(Global Light 2D). 비우면 빛 조각만 움직인다.\n" +
             "⚠ 여기는 아주 조금만 내린다. 많이 내리면 「조명만 이동」이 아니라 페이드가 되어 " +
             "정본 문단 474 가 금지한 것이 된다.")]
    public Light2D ambientLight;

    [Tooltip("전체 조명의 단계별 밝기. livingLightIntensities 와 같은 박자로 간다.\n" +
             "비워 두면 전체 조명은 건드리지 않는다.")]
    public float[] ambientIntensities = { 1f, 0.95f, 0.9f, 0.85f };

    // ── 카메라 ──────────────────────────────────────────────────────────────
    // ⛔ 2026-09-27: BE#01-a 「문이 커지는 컷」(doorZoomStages · doorZoomDuration)을 폐기했다.
    //   개정 D 문단 474 가 카메라를 고정하고 압박은 화면 효과에 맡겼다(E-64 줌 폐기).

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
    [Tooltip("BE#02-b · BE#01-d. 문 너머 저녁을 만드는 소리(멀게).")]
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
    [Tooltip("BE#01-a — 시간이 끝나는 순간의 암전(초). D 465 「즉시 암전」.")]
    public float instantBlackout = 0.15f;
    [Tooltip("세라 · 루가 걷는 속도(월드 유닛/초).")]
    public float walkSpeed = 1.6f;

    // ── 내부 상태 ───────────────────────────────────────────────────────────
    ClearSky.SimplePlayerController _lockedCtrl;
    bool      _origSeraActive;
    Vector3   _origSeraPos;
    Transform _origCameraTarget;
    float     _origLightIntensity;
    float     _origAmbientIntensity;
    Quaternion _origLightRotation;
    Collider2D[]    _luColliders;
    bool[]          _luColliderWasEnabled;
    SeraStageWalker _seraWalker;
    bool            _diningCutDone;

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

        // 마지막 컷(식탁)에서 곧바로 암전한다. 먼저 정리하면 카메라가 루에게 되돌아간 화면이 엔딩 직전에 비친다(2026-09-27 실측).
        yield return FadeOut();
        EndPlayback();
        EndingManager.TriggerBadEnding(BadEndingType.HouseSealed);
    }

    /// <remarks>
    /// 개정 D 465: 「시간이 끝나는 순간 루가 어디에 있든 즉시 암전한 뒤 BE#01-a 를 현관 앞에서 연다.」
    /// 472 · 476: 열쇠가 헛돌고, 저음이 사방에서 조여들고, 손잡이만 뜨거웠던 것이 문 전체로 퍼진다.
    /// 477: 진행되던 가장자리 어두워짐 · 복도 축소 · 문틀 좁아짐이 <b>끝까지 갔다가 암전으로 닫힌다</b> — 새 연출을 만들지 않는다.
    /// 479: 루는 문을 두드리거나 소리치지 않는다. 한 번 더 돌려보고 손을 뗀다.
    /// 2026-09-27: 전에는 암전 속에서 조임을 다 걷어 현관 장면이 밋밋했고, 루가 움직이지 않았다(배치 실측).
    /// </remarks>
    IEnumerator RunBE01a_SealedDoor()
    {
        yield return FadeOut(instantBlackout);

        // 암전 중에 정리한다. 정본 문단 470 — 단검을 파지 중이었다면 발동과 동시에 해제한다.
        // ⚠ 조임(가장자리 · 공간 · 저음)은 걷지 않는다 — 현관에서 이어져 끝까지 간다.
        DaggerFilterController.Instance?.SwitchToFantasyForced();
        FilterManager.Instance?.SetFilter(FilterType.Fantasy);
        TeleportPlayer(frontDoorSpawn);
        FaceLu(Vector2.down);                       // 현관문은 루의 아래쪽(남쪽 벽)이다
        CameraDirector.Instance?.Hold();            // [CAM] 현관 전경에서 고정. 컷을 잇지 않는다(474)

        yield return FadeIn();

        // 루가 아무리 열쇠를 돌려도 문은 열리지 않는다(475). 헛도는 소리 + 같은 흔들림 두 번.
        for (int i = 0; i < 2; i++)
        {
            PlaySfxIfNamed(sfxKeySlipName);
            yield return KeyTurnMotion();
            yield return new WaitForSecondsRealtime(0.45f);
        }

        yield return YarnDialogue.PlayIfExists(yarnNode_BE01a, false);

        // 한 번 더 돌려보고 손을 뗀다(479).
        PlaySfxIfNamed(sfxKeySlipName);
        yield return KeyTurnMotion();
        yield return new WaitForSecondsRealtime(0.3f);
        yield return StepLu(Vector2.up * 0.25f, 0.12f);
        yield return new WaitForSecondsRealtime(0.5f);

        // 조임이 끝까지 간다 → 그대로 암전(477). 가장자리 효과가 접근성 설정으로 꺼져 있어도 암전은 온다.
        yield return HouseEscapePressureController.CloseIn();
        yield return FadeOut(cutFadeDuration * 0.5f);
        HouseEscapePressureController.FinishFailPressure();   // 암전 속에서 조임·저음을 걷는다. BE#01-b 는 무음이다(487)
    }

    /// <remarks>
    /// 정본 문단 488 · 491: 거실 전경 고정, 소파에 앉은 루. 「빛의 각도 변화 외에 화면에서 아무것도 움직이지 않는다.
    /// 루의 자세도 바뀌지 않는다. 페이드나 디졸브를 쓰지 않고 같은 컷 안에서 조명만 이동시킨다.」
    /// 그래서 이 씬 <b>안에서는</b> 컷을 바꾸지 않는다. BE#01-a 가 암전으로 끝났으므로 여기서는 밝아지기만 한다.
    /// 493: '축적 없이 흐른다' 가 이 씬의 전부다.
    /// 2026-09-27: 루가 소파 콜라이더에 밀려 0.57 위에 섰다 — 연출 동안 루의 충돌을 끈다(BeginPlayback).
    /// </remarks>
    IEnumerator RunBE01b_LivingWait()
    {
        TeleportPlayer(livingRoomSpawn);
        FaceLu(Vector2.down);                       // 정면을 응시한다(489)
        CameraDirector.Instance?.Hold();
        yield return FadeIn();
        yield return WaitBeat();

        // 단계 수는 빛 조각 쪽이 정한다. 빛 조각이 없으면 전체 조명 배열이 대신 정한다.
        int stages = (livingLightIntensities != null) ? livingLightIntensities.Length : 0;
        if (stages == 0 && ambientIntensities != null) stages = ambientIntensities.Length;

        if ((livingLight == null && ambientLight == null) || stages == 0)
        {
            // 조명이 배선돼 있지 않아도 시간의 경과는 흘러야 한다.
            yield return new WaitForSecondsRealtime(livingLightStageSeconds * 3f);
            yield break;
        }

        // 기준 각도는 씬에서 맞춰 둔 값이다. 디렉터는 거기에 더하기만 한다 —
        // 빛이 어느 쪽에서 드는지는 씬이 정하고, 코드는 「움직인다」만 맡는다.
        float baseAngle = (livingLight != null) ? livingLight.transform.eulerAngles.z : 0f;

        for (int i = 0; i < stages; i++)
        {
            if (livingLight != null)
            {
                if (livingLightIntensities != null && i < livingLightIntensities.Length)
                    livingLight.intensity = livingLightIntensities[i];

                livingLight.transform.rotation =
                    Quaternion.Euler(0f, 0f, baseAngle + livingLightAngleStep * i);
            }

            // 전체 조명은 같은 박자로 아주 조금만 내린다(정본 문단 491 — 페이드가 되면 안 된다).
            if (ambientLight != null && ambientIntensities != null && i < ambientIntensities.Length)
                ambientLight.intensity = ambientIntensities[i];

            yield return new WaitForSecondsRealtime(livingLightStageSeconds);
        }
    }

    /// <remarks>
    /// 정본 문단 499: BE#01-b 의 고정 구도를 그대로 유지한다. <b>세라가 화면 안으로 걸어 들어온다.</b> 세라를 따로 잡지 않는다.
    /// 502: 루는 고개를 돌려 세라를 바라본다. 여전히 멍한 표정이다. 505: 세라는 루를 한번 바라보며 웃고 부엌으로 들어간다.
    /// 506: 세라는 코트를 언급하지 않는다. <b>시선이 코트에 잠깐도 머물지 않아야 한다</b> — 세라는 루 쪽(옆)만 본다.
    /// 508: 세라는 화내지 않는다.
    /// 2026-09-27: 전에는 세라가 그 자리에 켜졌다 꺼졌고, 렌더러가 꺼져 있어(외출 뒤 상태) 아예 보이지 않았다(배치 실측).
    /// </remarks>
    IEnumerator RunBE01c_SeraReturn()
    {
        PlaySfxIfNamed(sfxDoorOpenName);
        yield return new WaitForSecondsRealtime(0.5f);

        ShowSera(seraEnterPoint);
        _seraWalker?.Face(Vector2.up);
        yield return new WaitForSecondsRealtime(0.3f);
        PlaySfxIfNamed(sfxBasketDownName);
        yield return new WaitForSecondsRealtime(0.4f);

        if (_seraWalker != null) yield return _seraWalker.WalkTo(seraGreetPoint, walkSpeed);
        _seraWalker?.FaceToward(LuPosition);

        // 루는 세라가 들어와 설 때까지 정면을 보고 있다가, 고개만 돌린다.
        yield return WaitBeat();
        FaceLu(seraGreetPoint - LuPosition);
        yield return new WaitForSecondsRealtime(0.4f);

        yield return YarnDialogue.PlayIfExists(yarnNode_BE01c, false);

        // 한번 바라보며 웃고 — 부엌으로 들어간다.
        yield return new WaitForSecondsRealtime(0.5f);
        if (_seraWalker != null) yield return _seraWalker.WalkTo(seraKitchenPoint, walkSpeed);
        _seraWalker?.Face(Vector2.up);              // 싱크대를 향한다 — 저녁을 준비한다
        yield return WaitBeat();
    }

    /// <remarks>
    /// 정본 문단 515~522: 루도 일어나서 방으로 향한다 → (루의 방) 멀리 저녁을 만드는 소리 → 정적 → 「저녁 먹게 나오렴」
    /// → 루가 부엌으로 가자 3명분의 밥 → 유의 자리를 한 박자 보고 → 「잘 먹겠습니다」.
    /// 부름과 「잘 먹겠습니다」 사이의 장소 전환은 yarn <c>&lt;&lt;be_cut "dining"&gt;&gt;</c> 이 여기로 넘긴다(2026-09-27 사용자 결정).
    /// 514: S#04A 와 같은 부엌 전경 고정 구도. 아침과 저녁의 빛만 다르다 — 거실에서 내린 전체 조명을 그대로 둔다.
    /// 528: 코트를 벗는 장면은 두지 않는다.
    /// </remarks>
    IEnumerator RunBE01d_ThreePlates()
    {
        // 루도 일어나서 방으로 향한다.
        yield return new WaitForSecondsRealtime(0.4f);
        yield return WalkLu(luLeaveSofaPoint);
        yield return FadeOut();

        // 루의 방. 부엌에서 저녁을 만드는 소리(멀리) → 정적.
        HideSera();
        TeleportPlayer(luRoomSpawn);
        BindRoomBound(luRoomSpawn);
        FaceLu(Vector2.down);
        CameraDirector.Instance?.Hold();
        yield return FadeIn();

        PlayLoopIfNamed(sfxDistantCookingName);
        yield return new WaitForSecondsRealtime(beatSeconds * 3f);
        StopLoopIfNamed(sfxDistantCookingName);
        yield return new WaitForSecondsRealtime(beatSeconds * 1.5f);   // 정적

        // 세라가 부른다 → be_cut "dining" 이 식탁으로 넘긴다 → 잘 먹겠습니다.
        _diningCutDone = false;
        yield return YarnDialogue.PlayIfExists(yarnNode_BE01d, false);
        if (!_diningCutDone) yield return CutToDining();   // 노드에 커맨드가 없어도 식탁은 보여준다
        yield return WaitBeat();
    }

    /// <summary>BE#01-d 식탁 컷. 암전 → 식탁(S#04A 구도) → 식기 소리 → 유의 자리를 한 박자.</summary>
    IEnumerator CutToDining()
    {
        _diningCutDone = true;
        yield return FadeOut();

        CameraFollow.Instance?.SetBound(null);      // 방에서 나온다
        SetPlates(three: true);
        TeleportPlayer(diningSpawn);
        ShowSera(seraDiningSpawn != null ? (Vector2)seraDiningSpawn.position : seraKitchenPoint);
        _seraWalker?.FaceToward(LuPosition);
        FaceLu(Vector2.right);                      // 식탁 쪽(세라 맞은편)
        CameraDirector.Instance?.Hold();

        yield return FadeIn();
        PlaySfxIfNamed(sfxTablewareName);
        yield return WaitBeat();

        // 유의 자리를 말없이 본다 — 한 박자면 된다. 그리고 바로 숟가락을 든다(522).
        FaceLu(yuSeatPoint - LuPosition);
        yield return WaitBeat();
        FaceLu(Vector2.right);
    }

    // <<be_cut "dining">> — BE#01-d. 세라의 부름(루의 방)과 「잘 먹겠습니다」(식탁) 사이. 컷이 끝날 때까지 대사를 멈춘다.
    // Yarn Spinner 3.x: 인스턴스 [YarnCommand] 는 첫 인자를 GameObject 이름으로 해석하므로 static + Instance 패턴(KitchenTriggerCutscene 과 같다).
    [Yarn.Unity.YarnCommand("be_cut")]
    public static IEnumerator YarnBeCut(string where)
    {
        var d = Instance;
        if (d == null || !IsPlaying) yield break;
        if (where == "dining") { yield return d.StartCoroutine(d.CutToDining()); yield break; }
        Debug.LogWarning($"[BadEndingDirector] be_cut \"{where}\" — 알 수 없는 자리. 무시한다.");
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

        // 마지막 컷(식탁)에서 곧바로 암전한다. 먼저 정리하면 카메라가 루에게 되돌아간 화면이 엔딩 직전에 비친다(2026-09-27 실측).
        yield return FadeOut();
        EndPlayback();
        EndingManager.TriggerBadEnding(BadEndingType.Captured);
    }

    /// <remarks>
    /// 개정 D 664~665 (BE#02-a 의 끝): 집에 도착하자 세라는 루를 방에 넣고 밖에서 문을 잠근다. 루는 허망하게 문을 바라본다.
    /// 674: [CAM] 고정. 루의 방 전경. 문이 열리며 들어오는 빛이 바닥에 떨어지고, 세라는 문간에 역광 실루엣으로 선다.
    /// 675~679: 시간이 흘렀다(멀리 저녁 만드는 소리). 문이 열린다 — 문틈이 아니라 문을 연다. 루의 얼굴을 본다.
    /// <b>세라는 한 마디도 하지 않는다. 문을 열고, 웃고, 놓고, 닫는다. 네 동작뿐이다.</b>
    /// 2026-09-27: 전에는 방에 2초 서 있다가 끝났다 — 잠김 · 문 바라보기 · 네 동작이 전부 없었다(배치 실측).
    /// </remarks>
    IEnumerator RunBE02b_LockedRoom()
    {
        // ⚠ 여기서 페이드 인을 하지 않는다. 마을에서 넘어올 때 TransitionManager 의
        //    씬 전환이 이미 페이드 인을 맡고 있고, 겹치면 두 코루틴이 같은 오버레이를 다툰다.
        //    이동은 코루틴의 첫 동기 구간에서 끝나므로 페이드가 걷힐 때 이미 방 안이다.
        SetLuPosition(luLockedRoomPoint);
        BindRoomBound(luRoomSpawn);
        FaceLu(seraDoorwayPoint - luLockedRoomPoint);   // 문을 바라본다
        CameraDirector.Instance?.Hold();
        if (dinnerTray != null) dinnerTray.SetActive(false);

        // 밖에서 문을 잠근다. 루는 허망하게 문을 바라본다.
        yield return new WaitForSecondsRealtime(0.6f);
        PlaySfxIfNamed(sfxRoomDoorLockName);
        yield return new WaitForSecondsRealtime(beatSeconds * 2f);

        // 시간이 흘렀다. 문 너머에서 저녁 만드는 소리 — 아주 멀게(673).
        PlayLoopIfNamed(sfxDistantCookingName);
        yield return new WaitForSecondsRealtime(beatSeconds * 4f);
        StopLoopIfNamed(sfxDistantCookingName);
        yield return new WaitForSecondsRealtime(beatSeconds);

        // ① 문을 연다 — 문간에 선다. 역광 실루엣 스프라이트(703)와 바닥에 떨어지는 빛은 그림 대기라 지금은 세라의 모습만 선다.
        PlaySfxIfNamed(sfxRoomDoorOpenName);
        ShowSera(seraDoorwayPoint);
        _seraWalker?.FaceToward(LuPosition);             // 루의 얼굴을 본다(679)
        yield return WaitBeat();

        // ② 웃는다 — 말하지 않는다. 한 박자 그대로 둔다.
        yield return WaitBeat();

        // ③ 놓는다 — 밥을 방문 앞에 밀어넣는다(677).
        if (dinnerTray != null) dinnerTray.SetActive(true);
        yield return WaitBeat();

        // ④ 닫는다.
        HideSera();
        PlaySfxIfNamed(sfxRoomDoorCloseName);
        yield return new WaitForSecondsRealtime(beatSeconds * 1.5f);
    }

    /// <remarks>
    /// 개정 D 688: S#04A · BE#01-d 와 같은 부엌 전경 고정 구도. 접시 수만 다르다.
    /// 689~693: 세라는 부엌으로 돌아가 식탁에 앉는다. 루는 나오지 못한다 — <b>루를 옮기지 않고 카메라만</b> 부엌으로 넘긴다.
    /// 접시는 2개(세라 · 유). 빈자리에 접시를 놓지 않는다. 그녀는 잠시 루의 빈자리를 보다가 자신의 밥을 먹기 시작한다.
    /// 2026-09-27: 전에는 루의 방 경계가 카메라를 붙잡아 부엌이 한 번도 보이지 않았다(배치 실측 cam 이 방 중앙에 고정).
    /// </remarks>
    IEnumerator RunBE02c_TwoPlates()
    {
        yield return FadeOut();

        CameraFollow.Instance?.SetBound(null);            // 방 경계를 풀어야 카메라가 부엌으로 간다
        SetPlates(three: false);
        ShowSera(seraDiningSpawn);
        MoveCameraTo(diningSpawn);                        // BE#01-d 와 같은 자리 = 같은 구도

        yield return FadeIn();

        PlaySfxIfNamed(sfxChairDragName);                 // 식탁에 앉는다
        yield return WaitBeat();

        // 잠시 루의 빈자리를 본다 → 자기 밥을 먹기 시작한다.
        if (diningSpawn != null) _seraWalker?.FaceToward(diningSpawn.position);
        yield return WaitBeat();
        _seraWalker?.Face(Vector2.down);
        PlaySfxIfNamed(sfxTablewareName);
        yield return new WaitForSecondsRealtime(beatSeconds * 2.5f);
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
            _origCameraTarget = cam.target;
        }

        // 세라는 씬에 하나뿐이라 컷씬이 끝나면 원래 자리로 돌려놓는다.
        // (엔딩 뒤에는 씬을 새로 불러오지만, 도중에 중단돼도 씬이 망가지지 않게 한다.)
        if (seraObject != null)
        {
            _origSeraActive = seraObject.activeSelf;
            _origSeraPos    = seraObject.transform.position;
            // 걷기 · 방향 · 모습 표시는 S#02 · S#04 와 같은 공용 도구로 한다. 방향·렌더러 상태도 끝나면 되돌린다.
            _seraWalker = SeraStageWalker.On(seraObject.GetComponent<Animator>());
            _seraWalker?.Capture();
        }

        // 루를 가구 자리(소파 · 식탁 의자)에 정확히 앉히려면 충돌을 꺼야 한다 — 켜 두면 콜라이더가 밀어낸다(실측 0.38~0.57).
        if (_lockedCtrl != null)
        {
            _luColliders = _lockedCtrl.GetComponents<Collider2D>();
            _luColliderWasEnabled = new bool[_luColliders.Length];
            for (int i = 0; i < _luColliders.Length; i++)
            {
                _luColliderWasEnabled[i] = _luColliders[i].enabled;
                _luColliders[i].enabled = false;
            }
        }

        if (livingLight != null)
        {
            _origLightIntensity = livingLight.intensity;
            _origLightRotation  = livingLight.transform.rotation;
        }

        if (ambientLight != null)
            _origAmbientIntensity = ambientLight.intensity;
    }

    void EndPlayback()
    {
        // ⚠ 남아 있는 대사를 먼저 끊는다. 배드 엔딩은 화면을 통째로 가져가는 자리라
        //    다른 대사가 떠 있으면 안 되고, 무엇보다 줄이 페이드 중인 채로 씬을 넘기면
        //    Yarn 의 LinePresenter 가 파괴된 CanvasGroup 을 만져 예외를 던진다(2026-08-23 실측).
        if (YarnDialogue.IsRunning) YarnDialogue.Runner.Stop();

        // 씬을 넘기기 전에 되돌려 둔다. 되감기 복귀 후 같은 씬을 다시 쓰기 때문이다.
        RestoreCamera();
        CameraDirector.Instance?.Track();
        CameraFollow.Instance?.SetBound(null);
        StopLoopIfNamed(sfxDistantCookingName);

        if (seraObject != null)
        {
            _seraWalker?.Restore();
            _seraWalker = null;
            seraObject.transform.position = _origSeraPos;
            seraObject.SetActive(_origSeraActive);
        }

        if (_luColliders != null)
        {
            for (int i = 0; i < _luColliders.Length; i++)
                if (_luColliders[i] != null) _luColliders[i].enabled = _luColliderWasEnabled[i];
            _luColliders = null;
        }

        if (livingLight != null)
        {
            livingLight.intensity          = _origLightIntensity;
            livingLight.transform.rotation = _origLightRotation;
        }

        if (ambientLight != null)
            ambientLight.intensity = _origAmbientIntensity;

        YarnDialogue.UnlockPlayer(_lockedCtrl);
        _lockedCtrl = null;

        ObjectiveManager.Instance?.ResetCutscene();
        IsPlaying = false;
    }

    // ─── 도구 ───────────────────────────────────────────────────────────────
    IEnumerator FadeOut() => FadeOut(cutFadeDuration);

    IEnumerator FadeOut(float duration)
    {
        var tm = TransitionManager.Instance;
        if (tm == null) yield break;
        yield return tm.FadeToBlack(duration);
    }

    IEnumerator FadeIn()
    {
        var tm = TransitionManager.Instance;
        if (tm == null) yield break;
        yield return tm.FadeFromBlack(cutFadeDuration);
    }

    WaitForSecondsRealtime WaitBeat() => new WaitForSecondsRealtime(beatSeconds);

    ClearSky.SimplePlayerController Lu => _lockedCtrl != null
        ? _lockedCtrl
        : FindAnyObjectByType<ClearSky.SimplePlayerController>();

    Vector2 LuPosition => Lu != null ? (Vector2)Lu.transform.position : Vector2.zero;

    void TeleportPlayer(Transform spawn)
    {
        if (spawn == null) return;
        SetLuPosition(spawn.position);
        // 고정이 걸려 있으면 푼 뒤 스냅한다 — 새 자리에서 다시 고정한다(호출부).
        CameraDirector.Instance?.Track();
        // 스냅하지 않으면 카메라가 이전 자리에서 새 자리까지 부드럽게 따라오는 것이 그대로 보인다.
        CameraFollow.Instance?.SnapCameraToFollow();
    }

    void SetLuPosition(Vector2 p)
    {
        var lu = Lu;
        if (lu == null) return;
        var rb = lu.GetComponent<Rigidbody2D>();
        if (rb != null) { rb.position = p; rb.linearVelocity = Vector2.zero; }
        lu.transform.position = new Vector3(p.x, p.y, lu.transform.position.z);
    }

    /// <summary>루가 delta 방향을 보게 한다. 0=아래 1=옆 2=위, 옆은 localScale.x 부호만 뒤집는다(양수가 왼쪽 — CLAUDE.md §11).</summary>
    void FaceLu(Vector2 delta)
    {
        var lu = Lu;
        if (lu == null || delta.sqrMagnitude < 0.0001f) return;
        var anim = lu.GetComponent<Animator>();
        int dir = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) ? 1 : (delta.y > 0f ? 2 : 0);
        Vector3 s = lu.transform.localScale;
        s.x = dir == 1 ? Mathf.Abs(s.x) * (delta.x > 0f ? -1f : 1f) : Mathf.Abs(s.x);
        lu.transform.localScale = s;
        if (anim != null) { anim.SetInteger("dir", dir); anim.SetBool("isRun", false); }
    }

    /// <summary>열쇠를 돌리는 흔들림 — S#06 손잡이 임시 동작과 같은 궤적(1픽셀 좌우). 그림이 오면 애니메이션으로 바꾼다.</summary>
    IEnumerator KeyTurnMotion()
    {
        var lu = Lu;
        if (lu == null) yield break;
        Vector2 home = lu.transform.position;
        const float px = 1f / 32f;
        foreach (float k in new[] { 1f, 0f, -1f, 0f })
        {
            SetLuPosition(home + Vector2.right * (k * px));
            yield return new WaitForSecondsRealtime(0.075f);
        }
        SetLuPosition(home);
    }

    /// <summary>짧게 한 걸음 — 손을 떼며 물러나는 동작.</summary>
    IEnumerator StepLu(Vector2 delta, float duration)
    {
        Vector2 from = LuPosition, to = from + delta;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            SetLuPosition(Vector2.Lerp(from, to, t / duration));
            yield return null;
        }
        SetLuPosition(to);
    }

    /// <summary>잠긴 루를 목표까지 걷게 한다. 컨트롤러가 잠겨 있어 위치로 옮긴다.</summary>
    IEnumerator WalkLu(Vector2 target)
    {
        var lu = Lu;
        if (lu == null) yield break;
        var anim = lu.GetComponent<Animator>();
        FaceLu(target - LuPosition);
        if (anim != null) anim.SetBool("isRun", true);
        while ((LuPosition - target).sqrMagnitude > 0.0001f)
        {
            SetLuPosition(Vector2.MoveTowards(LuPosition, target, walkSpeed * Time.unscaledDeltaTime));
            yield return null;
        }
        if (anim != null) anim.SetBool("isRun", false);
    }

    /// <summary>방 안 지점이면 그 방 경계로 카메라를 묶는다(RoomTransfer 가 지점에 붙어 있다).</summary>
    void BindRoomBound(Transform spawn)
    {
        if (spawn == null) return;
        var room = spawn.GetComponentInParent<RoomTransfer>();
        CameraFollow.Instance?.SetBound(room != null ? room.roomBound : null, snap: true);
    }

    /// <summary>클로즈업 Image 를 잠깐 띄웠다 끈다. 비어 있으면 조용히 건너뛴다.</summary>
    IEnumerator FlashCloseup(Image image)
    {
        if (!CloseupArt.Has(image)) yield break;   // 그림이 없으면 흰 화면만 뜬다
        image.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(closeupHoldSeconds);
        image.gameObject.SetActive(false);
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
    }

    void ShowSera(Transform spawn)
    {
        if (seraObject == null) return;
        ShowSera(spawn != null ? (Vector2)spawn.position : (Vector2)seraObject.transform.position);
    }

    /// <remarks>
    /// ⚠ 오브젝트만 켜면 안 된다. S#04H 외출 뒤 세라의 SpriteRenderer 가 꺼져 있어(2026-09-27 실측 en=False)
    /// 켜진 채로 보이지 않았다. 렌더러까지 켠다 — 원래 상태는 EndPlayback 의 walker.Restore 가 되돌린다.
    /// </remarks>
    void ShowSera(Vector2 at)
    {
        if (seraObject == null) return;
        seraObject.transform.position = new Vector3(at.x, at.y, seraObject.transform.position.z);
        seraObject.SetActive(true);
        foreach (var r in seraObject.GetComponentsInChildren<SpriteRenderer>(true)) r.enabled = true;
    }

    void HideSera()
    {
        if (seraObject != null) seraObject.SetActive(false);
    }

    void PlayLoopIfNamed(string soundName)
    {
        if (string.IsNullOrEmpty(soundName)) return;
        AudioManager.Instance?.PlayLoop(soundName);
    }

    void StopLoopIfNamed(string soundName)
    {
        if (string.IsNullOrEmpty(soundName)) return;
        AudioManager.Instance?.StopLoop(soundName);
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
