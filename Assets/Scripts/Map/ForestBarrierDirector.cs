using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// S#21 데모 종료 — 숲 끝 · 결계 (D-3 S#21A · S#21C).
///
/// <para>
/// <b>발동은 거리다.</b> 정본의 [TRIGGER] 는 「S#20 종료 후 이동 중 자동 발동」이고,
/// [CAM] 은 「걷는 둘을 뒤에서 잡는다. 결계에 도달할 때까지 컷을 바꾸지 않는다」이다.
/// 걸어가다 결계가 가까워지면 그대로 넘어가는 그림이므로, 밟는 순간 켜지는 콜라이더가 아니라
/// 결계로부터의 거리로 연다. <see cref="triggerDistance"/> 안으로 들어오면 1회 발동한다.
/// </para>
///
/// <para>
/// <b>구성은 2단이다.</b> S#21A(결계 도달 · 열쇠) → S#21C(강화 · 종료).
/// S#21B 는 폐기됐고 씬 번호를 재배열하지 않아 자리가 비어 있다(E-62-2).
/// 인형화 분기도 폐기됐으므로 대사는 한 벌이다(E-62-3) — 여기서 분기를 만들지 말 것.
/// </para>
///
/// <para>
/// <b>에셋이 비어 있어도 돈다.</b> 결계 스프라이트 3상태도 표정도 SFX 도 아직 없다.
/// 슬롯을 비워 두면 해당 연출만 조용히 건너뛰고 대사는 끝까지 진행한다
/// (<see cref="BadEndingDirector"/> · <c>KitchenTriggerCutscene</c> 과 같은 방식).
/// SFX 는 AudioManager 에 등록된 이름만 넣는다 — 이름을 지어내지 않는다(CLAUDE.md §0-4).
/// </para>
///
/// <para>
/// ⚠ <b>카메라 정사영 크기를 바꾸지 않는다.</b> 결계가 화면 안쪽으로 밀려오는 것은
/// 스프라이트 오프셋으로만 만든다. 줌으로 당기면 픽셀 정수배가 깨진다(F-6).
/// </para>
///
/// <para>
/// ⚠ <b>세라는 등장하지 않는다.</b> 목소리만 나오며 스프라이트를 만들지 않는다.
/// 화자 이름도 포트레이트도 띄우지 않는다 — 그 처리는 yarn 쪽 화자 ID <c>세라목소리</c> 가
/// 맡는다(E-62-4 · F-7-5). 여기서 세라 오브젝트를 세우지 말 것.
/// </para>
///
/// <para>
/// ⚠ <b>루가 저항하는 동작을 넣지 않는다.</b> 필터가 환상으로 덮이는 것을 그대로 둔다.
/// 저항을 넣으면 루가 상황을 파악한 것이 되어 은폐 축이 무너진다(정본 S#21C 주석).
/// </para>
///
/// 배치: 숲 끝의 결계 오브젝트에 붙이거나, 빈 오브젝트에 붙이고 <see cref="barrier"/> 를 꽂는다.
/// </summary>
public class ForestBarrierDirector : MonoBehaviour
{
    public static ForestBarrierDirector Instance { get; private set; }

    /// <summary>종료 연출이 도는 중. 다른 시스템이 끼어들면 안 된다.</summary>
    public static bool IsPlaying { get; private set; }

    // ── 발동 ────────────────────────────────────────────────────────────────
    [Header("발동 — 결계와의 거리")]
    [Tooltip("결계의 기준 위치. 비우면 이 오브젝트 자신을 쓴다.")]
    public Transform barrier;

    [Tooltip("이 거리(월드 유닛) 안으로 들어오면 발동한다.\n" +
             "⚠ 너무 짧게 주면 결계에 코를 박은 뒤에야 열려 「도착한다」가 아니라 「부딪힌다」가 된다.\n" +
             "   카메라가 둘을 뒤에서 잡고 있으므로 결계가 화면에 들어올 즈음이 적당하다.")]
    public float triggerDistance = 3.5f;

    [Tooltip("플레이어. 비우면 Player 태그로 찾는다.")]
    public Transform player;

    [Tooltip("거리 검사 간격(초). 매 프레임 잴 필요가 없다.")]
    public float checkInterval = 0.1f;

    // ── 결계 3상태 (F-6-2) ──────────────────────────────────────────────────
    [Header("결계 — 3상태. 없으면 건너뛴다")]
    [Tooltip("① 평상. 빛으로 된 얇은 판이며 황금색이 언뜻 비칠 뿐이다.\n" +
             "자세히 봐야 알 수 있는 정도이므로 걷는 동안에는 한 번만 스치게 두고 아무도 반응하지 않는다.")]
    public SpriteRenderer barrierIdle;

    [Tooltip("② 접촉 시 국소 가시화. 쿠루가 열쇠를 댄 자리 주변만 드러난다.\n" +
             "세라의 결계 점검(F-6-1)과 같은 원리다. 범위와 지속을 크게 쓰지 않는다 — ③과 구분돼야 한다.")]
    public SpriteRenderer barrierTouch;

    [Tooltip("③ 강화. 전면 가시화. 이 상태로 데모가 끝난다.")]
    public SpriteRenderer barrierReinforced;

    [Tooltip("③ 이 화면 안쪽으로 밀려오는 거리(월드 유닛). 스프라이트 오프셋으로만 만든다.\n" +
             "⚠ 카메라 정사영 크기를 바꾸지 않는다. 줌으로 당기면 픽셀 정수배가 깨진다(F-6).")]
    public float reinforcedPushIn = 0.5f;

    [Tooltip("밀려오는 데 걸리는 시간(초).")]
    public float reinforcedPushDuration = 1.2f;

    // ── 코드로 그리는 결계 빛 (BarrierLightFx) ───────────────────────────────
    [Header("결계 빛 — 아트 전까지 코드로 그린다")]
    [Tooltip("① 평상 판의 진하기. 「눈엔 안 보이겠지만」 — 거의 투명하게 둔다.\n" +
             "barrierIdle 이 비어 있으면 결계 오브젝트의 회색 상자 그림을 끄고 이 판을 쓴다.")]
    [Range(0f, 1f)] public float idlePlateAlpha = 0.05f;

    [Tooltip("① 걷는 중 황금색이 한 번 스치는 거리(월드 유닛). S#20 이 끝난 뒤 이 안에 들어오면 1회.\n" +
             "⚠ triggerDistance 보다 커야 도착 전에 스친다.")]
    public float glintDistance = 5.5f;

    [Tooltip("① 스치는 빛의 피크 진하기. 강조하지 않는다 — 자세히 봐야 알 정도(정본 1261).")]
    [Range(0f, 1f)] public float glintAlpha = 0.35f;

    [Tooltip("① 스치는 빛이 판을 가로지르는 시간(초).")]
    public float glintDuration = 1.1f;

    [Tooltip("③ 두꺼워지는 황금 막의 피크 높이(월드 유닛).")]
    public float curtainPeakHeight = 2.6f;

    [Tooltip("잔광에서 막의 높이(월드 유닛). 이 상태로 데모가 끝난다.")]
    public float curtainRestHeight = 1.5f;

    [Tooltip("효과의 정렬 순서. 결계 오브젝트와 같은 정렬 레이어에 그린다.")]
    public int fxSortingOrder = 300;

    [Header("필터 — 번지는 전환 (정본 1287)")]
    [Tooltip("결계에서 번진 환상이 화면을 다 덮는 데 걸리는 시간(초). 컷이 아니다 — S#12 의 「끊긴 것」과 구분된다.")]
    public float spreadDuration = 1.8f;

    [Tooltip("번지는 물결 가장자리의 황금 테 세기(0~1).")]
    [Range(0f, 1f)] public float spreadRimStrength = 0.7f;

    [Header("부감 컷 — 결계가 숲 전체를 감쌌다 (사용자 결정 2026-09-30)")]
    [Tooltip("⑥ 루가 고개를 올려볼 때 띄우는 오버레이 컷(384×216) 프레임. 비우면 자리 표시 그림(ForestDomeOverlay)을 쓴다.\n" +
             "서 호수 · 북 고목 · 동 회전목마(기계숲)를 암시한다. 이름도 표시도 붙이지 않는다.")]
    public Sprite[] domeOverlayFrames;

    [Tooltip("부감 컷을 띄워 두는 시간(초). 표시와 닫힘은 즉시다 — 페이드 없음(F-3-9).")]
    public float domeOverlayDuration = 3.6f;

    [Tooltip("컷 안 3프레임 반복의 한 프레임 길이(초). 안개가 「천천히」 흐르는 속도다.")]
    public float domeFrameInterval = 0.3f;

    [Tooltip("컷의 위쪽 여백(내부 해상도 픽셀). 대사창과 겹치지 않게 화면 위쪽에 둔다(F-3-9).")]
    public float domeOverlayTopMargin = 8f;

    [Header("다시 걸음 → 끝")]
    [Tooltip("다시 걸어가는 방향. 옆모습 걷기만 그림이 있어 가로로 둔다(왼쪽 = −x).")]
    public Vector2 walkOffDirection = new Vector2(-1f, 0f);

    [Tooltip("쿠루가 먼저 걸음을 옮기고 루가 눈치를 보다 따라가기까지(초).")]
    public float walkOffLuDelay = 0.7f;

    [Tooltip("둘이 걷기 시작하고 암전이 시작되기까지(초). 암전 중에도 계속 걷는다.")]
    public float walkBeforeFade = 1.6f;

    [Tooltip("황금빛이 가라앉는 시간(초). 걷기 시작과 함께 시작한다.")]
    public float sinkDuration = 3f;

    [Tooltip("종료 화면의 한 줄이 떠오르는 시간(초).")]
    public float endCardTextFade = 1.2f;

    // ── 강화 연출 (정본 문단 1259) ──────────────────────────────────────────
    [Header("강화 — 황금 광막")]
    [Tooltip("황금빛의 기준 색. 피크에서는 흰빛 쪽으로 섞여 「눈이 부실 만큼」이 된다.")]
    public Color glowColor = new Color(0.98f, 0.85f, 0.45f);

    [Tooltip("① 결계가 눈에 들어온 직후의 밝기(0~1).")]
    [Range(0f, 1f)] public float revealAlpha = 0.22f;

    [Tooltip("② 푸른빛이 튕겨나가는 순간의 번쩍임(0~1).")]
    [Range(0f, 1f)] public float repelFlashAlpha = 0.6f;

    [Tooltip("④ 「눈이 부실 만큼 찬란한 광명」의 피크(0~1). " +
             "⚠ 1 에 가까우면 화면이 완전히 하얘진다. 순간이므로 높아도 되지만, " +
             "여기서 오래 머물면 안 된다 — 두꺼워지는 것은 결계이지 화면이 아니다.")]
    [Range(0f, 1f)] public float surgePeakAlpha = 0.92f;

    [Tooltip("피크 뒤에 남는 잔광(0~1). 데모가 끝날 때까지 이 값으로 유지된다. " +
             "⚠ 이후 대사가 이 위에 뜬다. 너무 높으면 글자가 묻힌다.")]
    [Range(0f, 1f)] public float afterglowAlpha = 0.18f;

    [Header("강화 — 박자와 흔들림")]
    [Tooltip("② 푸른빛을 튕겨내는 글리치 길이(초).")]
    public float repelGlitchDuration = 0.45f;

    [Tooltip("② 튕겨낼 때의 카메라 흔들림 세기.")]
    public float repelShake = 0.35f;

    [Tooltip("③ 쿠루가 열쇠를 회수할 때의 시간 배율. 1 이면 슬로모션 없음.")]
    [Range(0.1f, 1f)] public float recoverSlowmoScale = 0.45f;

    [Tooltip("③ 슬로모션이 이어지는 시간(초).")]
    public float recoverSlowmoDuration = 0.5f;

    [Tooltip("④ 광명이 두꺼워지는 데 걸리는 시간(초). 흔들림도 같은 길이로 간다.")]
    public float surgeDuration = 1.6f;

    [Tooltip("④ 강화 순간의 카메라 흔들림 세기. " +
             "⚠ 흔들기만 쓴다. 줌은 정사영 크기를 바꿔 픽셀 정수배를 깨뜨린다(F-6 · §11).")]
    public float surgeShake = 0.6f;

    [Header("도착 — 쿠루가 루 앞으로 나선다")]
    [Tooltip("도착하면 쿠루가 루보다 이만큼 결계 쪽으로 나서서 선다(월드 유닛). 「팔을 벌려 루를 막는다」(정본 1247)의 자리다. " +
             "그래서 「…뭐?」에서 고개를 돌리면 뒤의 루를 돌아보는 그림이 된다(정본 1263 · 2026-09-30 사용자 결정).")]
    public float kuruBlockAhead = 1.0f;

    [Tooltip("쿠루가 결계 판에서 떨어져 서는 최소 거리(월드 유닛). 루가 결계에 너무 붙어 도착했을 때 판을 넘어가지 않게 한다.")]
    public float kuruBarrierGap = 0.6f;

    [Tooltip("쿠루가 루를 비켜 지나갈 옆 간격(월드 유닛). 루를 뚫고 걷지 않게 옆으로 돌아 나선다.")]
    public float kuruSidestep = 0.8f;

    [Tooltip("쿠루가 나서는 걸음 속도(월드 유닛/초).")]
    public float kuruStepInSpeed = 2.5f;

    [Header("강화 — 물러섬과 시선")]
    [Tooltip("③ 쿠루가 열쇠를 회수하며 물러나는 거리(월드 유닛).")]
    public float kuruStepBack = 0.35f;

    [Tooltip("⑤ 둘이 「몇 발자국」 물러나는 거리(월드 유닛).")]
    public float pairStepBack = 0.6f;

    [Tooltip("⑥ 루가 고개를 올리기까지의 사이(초). 「천천히」가 이 값이다.")]
    public float lookUpDelay = 0.7f;


    [Header("강화 — 가장자리 보조 채널")]
    [Tooltip("잔광이 가장자리에 남는 진하기(0~1). " +
             "⚠ 접근성 설정으로 꺼질 수 있는 채널이다. 강화 자체는 광막이 전달하므로 " +
             "여기에만 정보를 싣지 않는다.")]
    [Range(0f, 1f)] public float afterglowEdgeAlpha = 0.3f;

    [Tooltip("가장자리가 안쪽으로 파고드는 비율(0~1). F-6 의 18% / 30% / 44% 눈금을 따른다.")]
    [Range(0f, 1f)] public float afterglowEdgeRatio = 0.3f;

    // ── 박자 ────────────────────────────────────────────────────────────────
    [Header("박자")]
    [Tooltip("S#21A 가 끝나고 결계가 강화되기까지의 사이(초).\n" +
             "쿠루가 고개를 돌리는 순간에 맞춘다 — 「…뭐?」 직후다.")]
    public float delayBeforeReinforce = 0.6f;

    [Tooltip("「아, 그 생각을 못 했어요」 뒤의 무음(초).\n" +
             "정본: 「잠깐 아무 소리도 나지 않는다.」 목소리가 오기 전의 공백이다.")]
    public float silenceBeforeVoice = 1.6f;

    [Tooltip("목소리가 끝나고 둘이 돌아보기까지(초).")]
    public float delayBeforeLookBack = 0.5f;

    [Tooltip("돌아본 뒤 아무것도 없는 채로 두는 시간(초).\n" +
             "⚠ 이 공백이 끝난 다음에 부엉이가 운다. 순서를 바꾸지 말 것 — 공백이 먼저다.")]
    public float emptyBeatDuration = 1.2f;

    [Tooltip("부엉이가 울고 다시 걷기 시작하기까지(초).")]
    public float delayAfterOwl = 0.9f;

    // ── 음향 ────────────────────────────────────────────────────────────────
    [Header("음향 — AudioManager 에 등록된 이름만. 비우면 무음")]
    [Tooltip("부엉이 울음 「후우」. 대사창을 쓰지 않고 SFX 로 처리한다(F-4-4). 표기는 고정이다.\n" +
             "S#01 의 부엉이 SFX 를 재활용할 수 있는지는 F 에서 확인 중이다.")]
    public string sfxOwl = "";

    [Tooltip("결계가 강화되는 소리.")]
    public string sfxReinforce = "";

    [Tooltip("딱딱. 두 번 — 다시 걷기 시작하는 순간에 맞춘다(C-7-2 숲 후반).\n" +
             "「분노 이후의 당혹감」 곡선과 같은 강도이며 새 강도를 만들지 않는다.")]
    public string sfxClicking = "ceramic_tap";

    [Tooltip("딱딱 두 번 사이의 간격(초).")]
    public float clickingGap = 0.22f;

    // ── 종료 화면 ───────────────────────────────────────────────────────────
    [Header("종료 화면")]
    [Tooltip("암전 시간(초).")]
    public float fadeOutDuration = 2f;

    [Tooltip("「인형화 00%」 를 띄워 두는 시간(초).")]
    public float endCardDuration = 4f;

    [Tooltip("종료 화면 뒤에 넘어갈 씬. 비우면 화면을 띄운 채 멈춘다.\n" +
             "⚠ 크레딧을 올리지 않는다(정본 [UI]). CreditsScene 을 넣지 말 것 —\n" +
             "   그쪽은 크레딧을 스크롤한 뒤 결과를 띄우는 화면이라 정본과 어긋난다.")]
    public string nextScene = SceneNames.Title;

    // ── 내부 ────────────────────────────────────────────────────────────────
    bool _fired;
    float _nextCheck;
    ClearSky.SimplePlayerController _lockedCtrl;
    Vector3 _reinforcedOrigin;
    GameObject _glowRoot;
    BarrierLightFx _fx;
    bool _glinted;
    Vector2 _contact;
    CompanionFollow _kuruFollow;
    Vector2 _lastPlayerPos;

    void Awake()
    {
        Instance = this;
        if (barrier == null) barrier = transform;

        // 시작 상태 — 평상만 켜 둔다. 나머지는 각 단계에서 켠다.
        SetVisible(barrierIdle, true);
        SetVisible(barrierTouch, false);
        SetVisible(barrierReinforced, false);
    }

    void Start()
    {
        // 결계 빛. 평상 판은 거의 투명하다 — 「눈엔 안 보이겠지만 이게 결계야」.
        // ⚠ 평상 그림이 없으면 결계 오브젝트의 회색 상자(자리 표시용 사각형)가 불투명한 흰 막대로
        //   처음부터 보인다(2026-09-30 실측). 그 그림만 끄고 판은 이쪽이 그린다. 콜라이더는 그대로다.
        SpriteRenderer own = barrier.GetComponent<SpriteRenderer>();
        if (barrierIdle == null && own != null) own.enabled = false;

        var col = barrier.GetComponent<Collider2D>();
        Bounds b = col != null ? col.bounds : (own != null ? own.bounds : new Bounds(barrier.position, new Vector3(4f, 0.5f, 0f)));
        _fx = BarrierLightFx.Create(barrier, b,
                                    own != null ? own.sortingLayerID : 0, fxSortingOrder);
        _fx.gold              = glowColor;
        _fx.idleAlpha         = idlePlateAlpha;
        _fx.glintAlpha        = glintAlpha;
        _fx.curtainPeakHeight = curtainPeakHeight;
        _fx.curtainRestHeight = curtainRestHeight;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        RealityGradeRenderFeature.Instance?.ClearSpread();
    }

    void Update()
    {
        if (_fired || IsPlaying) return;
        if (Time.time < _nextCheck) return;
        _nextCheck = Time.time + Mathf.Max(0.02f, checkInterval);

        if (player == null)
        {
            var go = GameObject.FindWithTag("Player");
            if (go == null) return;
            player = go.transform;
        }

        // 2D 탑다운이라 z 는 보지 않는다. 카메라·정렬 때문에 z 가 서로 다를 수 있다.
        float d = Vector2.Distance(player.position, barrier.position);

        // ① 걷는 동안 황금색이 한 번 언뜻 비친다. 루도 쿠루도 반응하지 않으며 카메라도 돌리지 않는다(정본 1243 · 1256).
        //    S#20 이 끝난 뒤(솔 재조우 이후)의 이동 중에만 — 그 전에 스치면 복선이 아니라 그냥 배경이 된다.
        //    ⚠ 솔의 자리가 이미 이 거리 안이다 — 걷고 있을 때만 센다. 거래창이 떠 있는 동안 스치면 아무도 못 본다.
        bool walking = ((Vector2)player.position - _lastPlayerPos).sqrMagnitude > 0.0004f;
        _lastPlayerPos = player.position;
        if (!_glinted && _fx != null && walking && GameState.isForestSolMet && !YarnDialogue.IsRunning
            && d <= glintDistance)
        {
            _glinted = true;
            Dbg.Log("[S#21] 걷는 중 결계의 황금색이 한 번 스친다");
            _fx.StartCoroutine(_fx.Glint(glintDuration));
        }

        if (d > triggerDistance) return;

        _fired = true;
        StartCoroutine(PlayDemoEnding());
    }

    /// <summary>
    /// 디버그·에디터에서 거리와 무관하게 강제로 재생한다.
    ///
    /// <para>⚠ Update 를 거치지 않으므로 <see cref="player"/> 를 여기서 채운다.
    /// 비워 두면 물러섬·고개 올리기·돌아보기가 통째로 빠져 확인이 되지 않는다.</para>
    /// </summary>
    public void ForcePlay()
    {
        if (_fired || IsPlaying) return;
        if (player == null)
        {
            var go = GameObject.FindWithTag("Player");
            if (go != null) player = go.transform;
        }
        _fired = true;
        StartCoroutine(PlayDemoEnding());
    }

    // ─── 본 시퀀스 ──────────────────────────────────────────────────────────
    IEnumerator PlayDemoEnding()
    {
        IsPlaying = true;
        Time.timeScale = 1f;
        _lockedCtrl = YarnDialogue.LockPlayer();

        // 종료 씬에는 [목표] 가 없다. 떠 있을 수 있는 목표 패널을 먼저 지운다.
        ObjectiveManager.Instance?.ResetCutscene();
        ObjectiveManager.Instance?.HideHUD();

        Dbg.Log("[S#21] 결계 도달 — 데모 종료 연출 시작");

        // 끝까지 쿠루를 직접 세운다. 동행 추적이 켜져 있으면 매 프레임 방향·속도를 덮어써서
        // 열쇠를 대는 자세·돌아보기·먼저 걸음을 옮기는 것이 전부 지워진다.
        Transform kuru = FindCompanion();
        _kuruFollow = kuru != null ? kuru.GetComponent<CompanionFollow>() : null;
        if (_kuruFollow != null)
        {
            _kuruFollow.enabled = false;
            var krb = kuru.GetComponent<Rigidbody2D>();
            if (krb != null) krb.linearVelocity = Vector2.zero;
            kuru.GetComponentInChildren<Animator>()?.SetBool("isRun", false);
        }

        // ── S#21A 1/2 — 결계 도달 ───────────────────────────────────────────
        // [FILTER] 직전 씬의 필터를 유지한다. 강제하지 않으며 어떤 값도 건드리지 않는다(F 문단 813).
        //   여기서 set_filter 를 부르지 않는 것이 그 지시다.
        //
        // ⚠ 결계는 아직 보이지 않는다. 루가 「아무것도 안 보이는데요」라고 말하는 자리다.
        //
        // 쿠루가 먼저 루 앞(결계 쪽)으로 나서서 결계를 본다 — 「쿠루가 멈춰 선 채」(정본 1243) ·
        // 「팔을 벌려 루를 막는다」(1247). 루는 쿠루가 보는 곳을 본다(1245).
        // 이 자리 덕에 「…뭐?」에서 고개를 돌리는 것이 뒤의 루를 돌아보는 그림이 된다(1263).
        yield return KuruStepInFront(kuru);
        if (kuru != null) FaceVector(kuru, ContactPoint(kuru) - (Vector2)kuru.position);
        if (player != null) FaceVector(player, ContactPoint(player) - (Vector2)player.position);
        yield return YarnDialogue.PlayAndWait(YarnNodes.Forest_Barrier_Arrival, false);

        // ── 열쇠를 댄다 ─────────────────────────────────────────────────────
        // 정본 문단 1238 — 「쿠루가 은색 열쇠를 허공에서 꺼내 결계에 가져다 댄다.」
        // 접촉면을 중심으로 그 자리만 드러난다. 세라의 결계 점검(F-6-1)과 같은 원리이며
        // 새 규칙이 아니다. 범위와 지속을 크게 쓰지 않는다 — 강화(③)의 전면 가시화와
        // 구분돼야 한다(F-6-2).
        //
        // ⚠ 이 자리가 노드를 둘로 나눈 이유다. 앞 노드에서 켜면 대사와 화면이 어긋난다.
        SetVisible(barrierTouch, true);
        _contact = ContactPoint(kuru != null ? kuru : player);
        if (kuru != null) FaceVector(kuru, _contact - (Vector2)kuru.position);   // 결계를 본다 — 창을 보는 중이며 루를 보지 않는다(정본 1263)
        if (_fx != null)
        {
            _fx.ShowTouch(_contact);
            _fx.OpenKeyWindows(_contact);                   // 푸른 창 — 내용은 판독되지 않는다(정본 1255)
        }

        // ── S#21A 2/2 — 열쇠를 댄 뒤 ────────────────────────────────────────
        yield return YarnDialogue.PlayAndWait(YarnNodes.Forest_Barrier_KeyCheck, false);

        // ── 결계 강화 ───────────────────────────────────────────────────────
        // 쿠루가 고개를 돌리는 순간이다. 「…뭐?」 바로 뒤. 처음으로 루 쪽으로 고개가 돌아간다(정본 1263).
        FaceToward(kuru, player);
        yield return new WaitForSeconds(delayBeforeReinforce);
        yield return ReinforceBarrier();

        // ── S#21C 1/3 ───────────────────────────────────────────────────────
        yield return YarnDialogue.PlayAndWait(YarnNodes.Forest_Barrier_Reinforced, false);

        // 잠깐 아무 소리도 나지 않는다.
        yield return new WaitForSeconds(silenceBeforeVoice);

        // ── S#21C 2/3 — 세라의 목소리 ───────────────────────────────────────
        // 사방에서 온다. 방향을 특정하지 않는다 — 특정하면 플레이어가 세라의 위치를
        // 계산하게 되고 「어디까지 왔나」가 이 씬의 주제로 바뀐다.
        yield return YarnDialogue.PlayAndWait(YarnNodes.Forest_Sera_Voice, false);

        // ── 돌아보기 ────────────────────────────────────────────────────────
        yield return new WaitForSeconds(delayBeforeLookBack);
        LookBack();

        // 아무것도 없다. 공백을 먼저 둔다.
        yield return new WaitForSeconds(emptyBeatDuration);

        // 공백이 끝난 다음에 부엉이가 운다.
        PlaySfxIfNamed(sfxOwl);
        yield return new WaitForSeconds(delayAfterOwl);

        // ── S#21C 3/3 — 다시 걸음 ───────────────────────────────────────────
        yield return YarnDialogue.PlayAndWait(YarnNodes.Forest_Demo_End, false);

        // 쿠루가 먼저 걸음을 옮기고, 루가 눈치를 보다가 따라간다(정본 1280 · 1282).
        // 종결이 아니라 정지 — 갇힌 것을 알면서 앞으로 가는 그림이다(정본 1306).
        if (_fx != null) _fx.StartCoroutine(_fx.Sink(sinkDuration));   // 황금빛이 서서히 가라앉는다
        StartCoroutine(WalkOff(kuru));
        FaceToward(player, kuru);
        yield return new WaitForSeconds(walkOffLuDelay);
        StartCoroutine(WalkOff(player));

        // 딱딱. 두 번 — 다시 걷기 시작하는 순간에 맞춘다(루가 걷기 시작하는 순간).
        yield return PlayClickingTwice();
        yield return new WaitForSeconds(walkBeforeFade);

        // ── 종료 ────────────────────────────────────────────────────────────
        yield return EndDemo();
    }

    /// <summary>
    /// 걸어서 떠난다. 잠긴 컨트롤러가 속도를 0 으로 되돌리므로 위치로 옮긴다(FrontDoorInteraction 과 같은 방식).
    /// 암전이 끝나 씬이 넘어갈 때까지 계속 걷는다.
    /// </summary>
    IEnumerator WalkOff(Transform who)
    {
        if (who == null || walkOffDirection.sqrMagnitude < 0.0001f) yield break;
        Vector2 dir = walkOffDirection.normalized;

        var anim = who.GetComponentInChildren<Animator>();
        var rb   = who.GetComponent<Rigidbody2D>();
        FaceVector(who, dir);
        if (anim != null) anim.SetBool("isRun", true);

        var lu = who.GetComponent<ClearSky.SimplePlayerController>();
        float speed = lu != null ? Mathf.Max(0.1f, lu.walkSpeed) : 2f;

        while (who != null && IsPlaying)
        {
            Vector2 p  = rb != null ? rb.position : (Vector2)who.position;
            Vector2 np = p + dir * speed * Time.fixedDeltaTime;
            if (rb != null) rb.MovePosition(np); else who.position = new Vector3(np.x, np.y, who.position.z);
            yield return new WaitForFixedUpdate();
        }
    }

    /// <summary>
    /// 쿠루가 루 앞(결계 쪽)으로 나선다. 루에서 결계로 가는 선 위, 루보다 <see cref="kuruBlockAhead"/> 앞이다.
    /// 쿠루가 루 뒤에 있으면 곧게 가면 루를 뚫으므로 루 옆(<see cref="kuruSidestep"/>)을 한 번 거친다.
    /// 잠긴 컨트롤러가 속도를 0 으로 되돌리므로 위치로 옮긴다(<see cref="WalkOff"/> 와 같은 방식).
    /// </summary>
    IEnumerator KuruStepInFront(Transform kuru)
    {
        if (kuru == null || player == null) yield break;

        Vector2 lu      = player.position;
        Vector2 contact = ContactPoint(player);
        Vector2 toBar   = contact - lu;
        float   dist    = toBar.magnitude;
        if (dist < 0.0001f) yield break;
        Vector2 fwd = toBar / dist;

        float   ahead  = Mathf.Min(kuruBlockAhead, Mathf.Max(0f, dist - kuruBarrierGap));
        Vector2 target = lu + fwd * ahead;

        // 쿠루가 루보다 뒤(결계 반대쪽)에 있으면 루 옆으로 비켜 돈다. 원래 서 있던 쪽 옆을 쓴다.
        Vector2 k     = kuru.position;
        Vector2 side  = new Vector2(-fwd.y, fwd.x);
        if (Vector2.Dot(k - lu, side) < 0f) side = -side;
        var route = new System.Collections.Generic.List<Vector2>();
        if (Vector2.Dot(k - lu, fwd) < ahead * 0.5f) route.Add(lu + side * kuruSidestep + fwd * (ahead * 0.5f));
        route.Add(target);

        var anim = kuru.GetComponentInChildren<Animator>();
        var rb   = kuru.GetComponent<Rigidbody2D>();
        if (anim != null) anim.SetBool("isRun", true);

        foreach (Vector2 point in route)
        {
            Vector2 p = rb != null ? rb.position : (Vector2)kuru.position;
            FaceVector(kuru, point - p);
            while (kuru != null && (point - p).sqrMagnitude > 0.0004f)
            {
                p = Vector2.MoveTowards(p, point, Mathf.Max(0.1f, kuruStepInSpeed) * Time.fixedDeltaTime);
                if (rb != null) rb.MovePosition(p); else kuru.position = new Vector3(p.x, p.y, kuru.position.z);
                yield return new WaitForFixedUpdate();
            }
        }

        if (anim != null) anim.SetBool("isRun", false);
        Dbg.Log($"[S#21] 쿠루가 루 앞으로 나섰다 — 쿠루 {(Vector2)kuru.position} · 루 {lu} · 접촉 {contact}");
    }

    /// <summary>열쇠가 결계에 닿는 자리 — 결계 판 위에서 그 사람과 가장 가까운 점.</summary>
    Vector2 ContactPoint(Transform who)
    {
        var col = barrier.GetComponent<Collider2D>();
        Vector2 from = who != null ? (Vector2)who.position : (Vector2)barrier.position;
        return col != null ? col.ClosestPoint(from) : (Vector2)barrier.position;
    }

    /// <summary>상대 쪽을 본다. 옆을 볼 때는 localScale.x 부호만 뒤집는다(양수가 왼쪽 — CLAUDE.md §11).</summary>
    static void FaceToward(Transform who, Transform target)
    {
        if (who == null || target == null) return;
        FaceVector(who, (Vector2)(target.position - who.position));
    }

    static void FaceVector(Transform who, Vector2 d)
    {
        if (who == null || d.sqrMagnitude < 0.0001f) return;
        Vector3 s = who.localScale;
        if (Mathf.Abs(d.x) >= Mathf.Abs(d.y))
        {
            FaceDirection(who, 1);
            s.x = Mathf.Abs(s.x) * (d.x > 0f ? -1f : 1f);
        }
        else
        {
            FaceDirection(who, d.y > 0f ? 2 : 0);
            s.x = Mathf.Abs(s.x);
        }
        who.localScale = s;
    }

    /// <summary>
    /// 결계 강화 — 정본 문단 1259 의 여섯 단계를 순서대로 만든다.
    ///
    /// <para>
    /// 「쿠루가 고개를 돌리는 순간 결계가 황금색으로 확실하게 눈에 들어온다.
    /// 쿠루의 푸른빛을 전부 튕겨내며 열쇠까지 집어삼키려고 한다. 쿠루가 열쇠를 회수하며
    /// 뒤로 물러난다. <b>황금빛은 눈이 부실 만큼 찬란한 광명이 되어 두꺼워지기 시작한다.</b>
    /// 쿠루와 루가 뒤로 몇 발자국 물러난다. 결계가 강화된다. 루가 천천히 고개를 위로 올려본다.」
    /// </para>
    ///
    /// <para>
    /// <b>여기가 이 씬에서 유일하게 화려해도 되는 자리다.</b> 정본이 직접 「눈이 부실 만큼
    /// 찬란한 광명」이라고 쓴다. 나머지는 전부 절제를 요구한다 — 걷는 중의 황금색 복선은
    /// 강조하지 않고, 돌아보기는 시선을 모으지 않으며, 종료 화면은 한 줄이다.
    /// </para>
    ///
    /// <para>
    /// ⚠ 광막은 <b>자체 오버레이</b>로 그린다. <see cref="ScreenEdgeEffectController"/> 는
    /// 접근성 설정이 꺼지면 아무것도 하지 않으므로 거기에만 실으면 설정을 끈 플레이어에게
    /// 강화가 전혀 전달되지 않는다. 가장자리는 보조 채널로만 함께 쓴다.
    /// </para>
    ///
    /// <para>⚠ 필터는 환상으로 덮은 <b>뒤에</b> 봉인한다. 봉인이 먼저면 전환 자체가 막힌다.</para>
    /// <para>⚠ 카메라 정사영 크기를 바꾸지 않는다. 흔들기는 되고 줌은 안 된다(F-6 · CLAUDE.md §11).</para>
    /// </summary>
    IEnumerator ReinforceBarrier()
    {
        GameObject glow = BuildGlow();
        var glowImage = glow != null ? glow.GetComponentInChildren<Image>() : null;

        // ── ① 결계가 황금색으로 확실하게 눈에 들어온다 ──────────────────────
        Dbg.Log("[S#21] (1) 결계가 눈에 들어온다");
        SetVisible(barrierTouch, false);
        SetVisible(barrierIdle, false);
        SetVisible(barrierReinforced, true);
        PlaySfxIfNamed(sfxReinforce);
        if (_fx != null) _fx.StartCoroutine(_fx.RevealPlate(0.55f, 0.3f));   // 접촉 자리만이 아니라 판 전체가 드러난다
        yield return FadeGlow(glowImage, 0f, revealAlpha, 0.25f, glowColor);

        // ── ② 쿠루의 푸른빛을 전부 튕겨낸다 · 열쇠까지 집어삼키려 한다 ──────
        Dbg.Log("[S#21] (2) 푸른빛을 튕겨낸다");
        _fx?.RepelKeyLight(_contact);                        // 푸른 창이 부서져 흩어진다
        YarnCommandBridge.PlaySnap();
        YarnCommandBridge.PlayGlitch(repelGlitchDuration);
        CameraDirector.YarnCamShake(repelShake, repelGlitchDuration);
        // 튕겨나가는 순간의 번쩍임. 흰빛에 가까울수록 「부시다」에 가깝다.
        yield return FadeGlow(glowImage, revealAlpha, repelFlashAlpha, 0.12f,
                              Color.Lerp(glowColor, Color.white, 0.6f));
        yield return FadeGlow(glowImage, repelFlashAlpha, revealAlpha, 0.22f, glowColor);

        // ── ③ 쿠루가 열쇠를 회수하며 뒤로 물러난다 ──────────────────────────
        Dbg.Log("[S#21] (3) 쿠루가 열쇠를 회수하며 물러난다");
        CameraDirector.YarnCamSlowmo(recoverSlowmoScale, recoverSlowmoDuration);
        Transform kuru = FindCompanion();
        yield return PushAway(kuru, kuruStepBack, 0.35f);

        // ── ④ 눈이 부실 만큼 찬란한 광명이 되어 두꺼워지기 시작한다 ─────────
        //    결계가 강화되는 지점이다. [FILTER] 가 「강화 발동과 동시에」라고 못박는다.
        Dbg.Log("[S#21] (4) 광명이 두꺼워진다 — 필터 환상 강제 · 토글 봉인");

        CameraDirector.YarnCamShake(surgeShake, surgeDuration);
        StartCoroutine(PushBarrierInward());          // 스프라이트 오프셋으로만(F-6)
        if (_fx != null) _fx.StartCoroutine(_fx.Surge(surgeDuration));  // 막이 솟고 빛기둥이 서고 빛 가루가 오른다

        // 순간 번쩍 — 피크에 머무르지 않는다. 두꺼워지는 것은 결계이지 화면이 아니다.
        yield return FadeGlow(glowImage, revealAlpha, surgePeakAlpha, 0.18f,
                              Color.Lerp(glowColor, Color.white, 0.75f));

        // 빛이 가장 밝은 순간에 필터가 넘어간다. 미는 주체는 세라다 — 마시멜로가 게이지를 환상 극값으로
        // 옮기는 것과 같은 계통이며(C-3-2 · S#04E) 새 규칙이 아니라 기존 강제 전환의 세 번째 발생원이다.
        // 루가 단검을 파지한 상태여도 덮인다.
        // ⚠ 보이는 필터는 게이지다. DaggerFilterController · FilterManager 만 바꾸면 화면이 현실 그대로
        //   남는다(2026-09-30 실측 — S#19 뒤 현실로 온 플레이어가 끝까지 현실 화면으로 데모를 마쳤다).
        StartCoroutine(SpreadFantasy());
        ScreenEdgeEffectController.SetSustainedLevel(glowColor, afterglowEdgeAlpha, afterglowEdgeRatio);
        yield return FadeGlow(glowImage, surgePeakAlpha, afterglowAlpha, surgeDuration, glowColor);
        if (_fx != null) _fx.StartCoroutine(_fx.Settle(1.4f));          // 잔광으로 내려앉아 끝까지 반짝인다

        // ── ⑤ 쿠루와 루가 뒤로 몇 발자국 물러난다 ───────────────────────────
        Dbg.Log("[S#21] (5) 둘이 몇 발자국 물러난다");
        StartCoroutine(PushAway(kuru, pairStepBack, 0.5f));
        yield return PushAway(player, pairStepBack, 0.5f);

        // ── ⑥ 루가 천천히 고개를 위로 올려본다 ─────────────────────────────
        Dbg.Log("[S#21] (6) 루가 천천히 고개를 올려본다 — 부감 컷");
        yield return new WaitForSeconds(lookUpDelay);
        // 탑다운에서는 「올려다본다」를 방향으로 그릴 수 없다 — 올려다본 것을 컷으로 보여 준다.
        yield return ShowDomeOverlay();

        // 루가 당황해서 쿠루를 쳐다본다(정본 1271) — 곧이어 「이거 어떡해요..」
        FaceToward(player, kuru);
    }

    /// <summary>
    /// 부감 오버레이 — 강화된 결계가 숲 전체를 감싼 한 장. 본편에서 돌 세 곳(서 호수 · 북 고목 · 동 회전목마)이
    /// 흐르는 황금 안개 틈으로 비친다. 사용자 결정(2026-09-30) · F-3-9 오버레이 규칙을 따른다 —
    /// 즉시 표시 · 즉시 닫힘 · 배경을 어둡게 덮지 않음 · 표시 중 맵 캐릭터 애니메이션 정지 · 그림 안 움직임은 3프레임 반복.
    /// </summary>
    IEnumerator ShowDomeOverlay()
    {
        Sprite[] frames = domeOverlayFrames != null && domeOverlayFrames.Length > 0
            ? domeOverlayFrames : ForestDomeOverlay.Build(3);

        var root = new GameObject("S21 DomeOverlay [Auto]");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 97;                       // 황금 광막(96) 위, 암전(999) 아래
        UiCanvasScale.Add(root);

        var imgGo = new GameObject("Cut");
        imgGo.transform.SetParent(root.transform, false);
        var img = imgGo.AddComponent<Image>();
        img.raycastTarget = false;
        img.sprite = frames[0];
        var rt = imgGo.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(ForestDomeOverlay.W, ForestDomeOverlay.H);
        rt.anchoredPosition = new Vector2(0f, -domeOverlayTopMargin);

        // 표시 중 맵 위 캐릭터의 애니메이션을 멈춘다
        var anims = new System.Collections.Generic.List<Animator>();
        foreach (var t in new[] { player, FindCompanion() })
        {
            var a = t != null ? t.GetComponentInChildren<Animator>() : null;
            if (a != null) { a.speed = 0f; anims.Add(a); }
        }

        float elapsed = 0f;
        int fi = 0;
        float nextFrame = domeFrameInterval;
        while (elapsed < domeOverlayDuration)
        {
            elapsed += Time.deltaTime;
            if (elapsed >= nextFrame && frames.Length > 1)
            {
                fi = (fi + 1) % frames.Length;
                img.sprite = frames[fi];
                nextFrame += Mathf.Max(0.05f, domeFrameInterval);
            }
            yield return null;
        }

        foreach (var a in anims) if (a != null) a.speed = 1f;
        Destroy(root);                                  // 즉시 닫힘. ⚠ 씬 전환과 같은 프레임이 아니다(EndDemo 주석 참고)
    }

    /// <summary>
    /// 필터 강제 전환 — <b>컷이 아니라 번지는 형태</b>(정본 1287). S#12 의 「끊긴 것」과 구분된다.
    ///
    /// <para>게이지는 한 번에 환상 극값으로 옮기고, 화면은 결계의 접촉 자리에서 시작한 물결이 지나간
    /// 자리부터 환상 색이 된다. 물결 가장자리에 황금 테가 선다. 물결 바깥은 전환 전 색 그대로다.</para>
    ///
    /// <para>⚠ 환상으로 덮은 <b>뒤에</b> 토글을 봉인한다. 봉인이 먼저면 전환 자체가 막힌다.
    /// 잠겼다는 UI 표시를 두지 않는다(정본 1288).</para>
    /// </summary>
    IEnumerator SpreadFantasy()
    {
        var driver  = FindAnyObjectByType<RealityGaugeDriver>();
        var feature = RealityGradeRenderFeature.Instance;
        float outer = driver != null ? driver.CurrentNormalized
                    : (GaugeManager.Instance != null ? GaugeManager.Instance.fantasyRealityGauge / 100f : 1f);

        DaggerFilterController.Instance?.SwitchToFantasyForced();
        FilterManager.Instance?.SetFilter(FilterType.Fantasy);
        // 마시멜로의 「퐁」은 울리지 않는다 — 정본 S#21C 의 소리는 부엉이와 딱딱뿐이고, 같은 소리로 계통을 들려주면
        // 설명이 된다(사용자 결정 2026-09-30). 강화음은 sfxReinforce 슬롯이 맡는다.
        GaugeManager.Instance?.ForceFantasyMax(playSfx: false);
        DaggerFilterController.SealToggle();
        driver?.SnapToGauge();                          // 화면 전체가 같이 서서히 바뀌면 물결이 안 보인다

        var cam = Camera.main;
        if (feature == null || cam == null) yield break;

        Vector2 c = cam.WorldToViewportPoint(_contact);
        float aspect = cam.aspect;
        float maxR = 0f;
        foreach (var corner in new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one })
            maxR = Mathf.Max(maxR, Vector2.Scale(corner - c, new Vector2(aspect, 1f)).magnitude);
        maxR += 0.15f;                                  // 가장자리의 울퉁불퉁한 물결까지 다 지나가게

        Color rim = Color.Lerp(glowColor, Color.white, 0.3f);
        rim.a = spreadRimStrength;

        float t = 0f;
        while (t < spreadDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / spreadDuration);
            float r = maxR * (1f - Mathf.Pow(1f - k, 2.2f));   // 빨리 터져 나가다 느려진다
            feature.SetSpread(c, r, outer, rim);
            yield return null;
        }
        feature.ClearSpread();
    }

    /// <summary>강화된 결계가 화면 안쪽으로 밀려온다. 스프라이트 오프셋으로만 만든다(F-6).</summary>
    IEnumerator PushBarrierInward()
    {
        if (barrierReinforced == null || reinforcedPushIn <= 0f) yield break;

        Transform t = barrierReinforced.transform;
        _reinforcedOrigin = t.localPosition;

        Vector3 dir = player != null
            ? (Vector3)((Vector2)(player.position - barrier.position)).normalized
            : Vector3.down;
        Vector3 to = _reinforcedOrigin + dir * reinforcedPushIn;

        float elapsed = 0f;
        while (elapsed < reinforcedPushDuration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / reinforcedPushDuration));
            t.localPosition = Vector3.Lerp(_reinforcedOrigin, to, k);
            yield return null;
        }
        t.localPosition = to;
    }

    /// <summary>결계 반대쪽으로 물러난다. 상해가 아니라 물러서는 정도다.</summary>
    IEnumerator PushAway(Transform who, float distance, float duration)
    {
        if (who == null || distance <= 0f) yield break;

        Vector2 away = (Vector2)(who.position - barrier.position);
        if (away.sqrMagnitude < 0.0001f) yield break;
        away.Normalize();

        Vector3 from = who.position;
        Vector3 to   = from + (Vector3)(away * distance);
        var rb = who.GetComponent<Rigidbody2D>();

        float t = 0f;
        while (t < duration && who != null)
        {
            t += Time.deltaTime;
            Vector3 pos = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration)));
            if (rb != null) rb.MovePosition(pos); else who.position = pos;
            yield return null;
        }
    }

    /// <summary>
    /// 둘이 동시에 뒤를 돌아본다.
    ///
    /// <para>소리가 난 방향을 본 것이 아니라 어디서 나는지 몰라서 돌아본 것이므로
    /// 시선을 한 지점으로 모으지 않는다. 그리고 아무것도 없다 — S#16A 의 회수다.</para>
    ///
    /// <para>⚠ 좌우 반전으로 만들지 않는다. 탑다운에서 그건 옆을 보는 것이지 뒤가 아니다.
    /// 애니메이터의 <c>dir</c> 로 정면(카메라 쪽)을 향하게 한다.</para>
    /// </summary>
    void LookBack()
    {
        // 결계를 등지고 온 길 쪽을 본다 — S#16A 「뒤를 돌아보면 안 된다」의 회수다(정본 1303).
        // ⚠ 방향 번호를 고정하지 않는다. 9-17 판은 결계가 북쪽이라고 가정해 정면(남)을 보게 했는데
        //   실제 결계는 남쪽이라 결계를 정면으로 보는 그림이 됐다(2026-09-30 실측). 결계 반대쪽을 좌표로 잰다.
        FaceAwayFromBarrier(player);
        FaceAwayFromBarrier(FindCompanion());
        Dbg.Log("[S#21] 둘이 돌아본다 — 아무것도 없다");
    }

    void FaceAwayFromBarrier(Transform who)
    {
        if (who == null) return;
        var col = barrier.GetComponent<Collider2D>();
        Vector2 near = col != null ? col.ClosestPoint(who.position) : (Vector2)barrier.position;
        FaceVector(who, (Vector2)who.position - near);
    }

    Transform FindCompanion()
    {
        var c = FindAnyObjectByType<CompanionFollow>();
        return c != null ? c.transform : null;
    }

    /// <summary>애니메이터의 dir 을 바꾼다. 파라미터가 없으면 아무 일도 하지 않는다.</summary>
    static void FaceDirection(Transform who, int dir)
    {
        if (who == null) return;
        var anim = who.GetComponentInChildren<Animator>();
        if (anim == null || !anim.isActiveAndEnabled) return;
        foreach (var p in anim.parameters)
        {
            if (p.name != "dir") continue;
            if (p.type == AnimatorControllerParameterType.Int)        anim.SetInteger("dir", dir);
            else if (p.type == AnimatorControllerParameterType.Float) anim.SetFloat("dir", dir);
            return;
        }
    }

    /// <summary>황금 광막. 결계 스프라이트가 없어도 강화가 화면에 전달되게 하는 주 채널이다.</summary>
    GameObject BuildGlow()
    {
        var root = new GameObject("S21 BarrierGlow [Auto]");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 96;            // 가장자리 효과(95) 위, 페이드(999) 아래
        UiCanvasScale.Add(root);

        var imgGo = new GameObject("Glow");
        imgGo.transform.SetParent(root.transform, false);
        var img = imgGo.AddComponent<Image>();
        img.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
        img.raycastTarget = false;
        var rt = imgGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _glowRoot = root;
        return root;
    }

    static IEnumerator FadeGlow(Image img, float from, float to, float duration, Color color)
    {
        if (img == null) yield break;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;     // 슬로모션 중에도 빛은 제 속도로 간다
            float a = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            img.color = new Color(color.r, color.g, color.b, a);
            yield return null;
        }
        img.color = new Color(color.r, color.g, color.b, to);
    }

    IEnumerator PlayClickingTwice()
    {
        if (string.IsNullOrEmpty(sfxClicking)) yield break;
        PlaySfxIfNamed(sfxClicking);
        yield return new WaitForSeconds(clickingGap);
        PlaySfxIfNamed(sfxClicking);
    }

    /// <summary>
    /// 암전 → 「인형화 00%」 한 줄 → 데모 종료.
    ///
    /// <para>⚠ 크레딧을 올리지 않는다(정본 [UI]). 값만 바뀌는 한 줄이다.</para>
    /// <para>⚠ % 는 화면 표시 전용 표기다. 문서와 내부 값은 숫자를 쓰며, 두 표기가 다른 것은
    /// 오류가 아니다(C-11).</para>
    /// </summary>
    IEnumerator EndDemo()
    {
        if (TransitionManager.Instance != null)
            yield return TransitionManager.Instance.FadeToBlack(fadeOutDuration);

        // 남아 있는 대사를 먼저 끊는다. 줄이 페이드 중인 채로 씬을 넘기면 Yarn 의
        // LinePresenter 가 파괴된 CanvasGroup 을 만져 예외를 던진다(BadEndingDirector 실측).
        if (YarnDialogue.IsRunning) YarnDialogue.Runner.Stop();

        GameObject card = BuildEndCard(CurrentPuppetization());

        // 한 줄이 검은 화면 위로 천천히 떠오른다. 문구는 그대로다 — 크레딧을 올리지 않는다.
        var line = card != null ? card.GetComponentInChildren<TextMeshProUGUI>() : null;
        if (line != null)
        {
            float t = 0f;
            while (t < endCardTextFade)
            {
                t += Time.unscaledDeltaTime;
                line.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(0.01f, endCardTextFade)));
                yield return null;
            }
            line.alpha = 1f;
        }
        yield return new WaitForSecondsRealtime(endCardDuration);

        IsPlaying = false;
        Time.timeScale = 1f;               // 슬로모션이 남아 있을 자리는 아니지만 방어적으로 되돌린다
        YarnDialogue.UnlockPlayer(_lockedCtrl);
        _lockedCtrl = null;

        if (string.IsNullOrEmpty(nextScene))
        {
            // 화면을 띄운 채로 둔다. 「종결이 아니라 정지」를 그대로 두고 싶을 때의 선택지다.
            yield break;
        }

        // ── 데모 밖으로 나가기 전에 되돌린다 ────────────────────────────────
        // 셋 다 씬을 넘어 살아남는 것들이라, 여기서 정리하지 않으면 타이틀 화면까지 따라간다.

        // ① 가장자리 잔광 — ScreenEdgeEffectController 는 DontDestroyOnLoad 다.
        ScreenEdgeEffectController.ClearSustained();

        // ② 황금 광막 — 씬 오브젝트라 전환에 파괴되지만, 전환 전 한 프레임을 남기지 않는다.
        if (_glowRoot != null) { Destroy(_glowRoot); _glowRoot = null; }
        // ⚠ 결계 빛(_fx)은 여기서 Destroy 하지 않는다. 직접 파괴한 같은 프레임에 LoadScene 하면 Unity 가
        //   씬을 내리다 멈췄다(2026-09-30 배치 실측 — 재현 2회, 파괴를 빼면 정상). 씬 오브젝트라 씬과 함께 사라지고,
        //   이 시점 화면은 이미 종료 화면(검정)이라 남아도 보이지 않는다.

        // ②-2 번지는 전환의 값 — 컬러 그레이드 머티리얼은 공유 에셋이라 끄지 않으면 다음 화면까지 남는다.
        RealityGradeRenderFeature.Instance?.ClearSpread();

        // ③ 필터 토글 봉인 — static 이라 씬을 넘어 유지된다.
        //    ⚠ GameState 의 초기화는 [RuntimeInitializeOnLoadMethod] 라 에디터 플레이를
        //      다시 시작할 때만 돈다. 게임 안에서 타이틀로 갔다 새로 시작하는 경로에서는
        //      불리지 않으므로, 여기서 풀지 않으면 다음 회차에 F 키가 영영 듣지 않는다.
        //      봉인의 목적은 「종료 화면까지 환상으로 고정」이고 그 뒤는 데모 밖이다.
        DaggerFilterController.UnsealToggle();

        if (card != null) Destroy(card);
        UnityEngine.SceneManagement.SceneManager.LoadScene(nextScene);
    }

    static float CurrentPuppetization()
    {
        if (CorruptionManager.Instance != null) return CorruptionManager.Instance.currentCorruption;
        return GameState.player.IsInitialized ? GameState.player.puppetization : 0f;
    }

    /// <summary>검은 화면에 한 줄. 페이드(999) 위에 얹는다. 틀은 사망 화면(F-9)과 공용이다 — <see cref="BlackLineCard"/>.</summary>
    static GameObject BuildEndCard(float puppetization)
        => BlackLineCard.Build("S21 EndCard [Auto]", $"인형화 {Mathf.RoundToInt(puppetization):00}%", alpha: 0f);   // EndDemo 가 떠오르게 한다

    static void SetVisible(SpriteRenderer sr, bool on)
    {
        if (sr != null) sr.enabled = on;
    }

    static void PlaySfxIfNamed(string soundName)
    {
        if (!string.IsNullOrEmpty(soundName)) AudioManager.Instance?.Play(soundName);
    }

    void OnDrawGizmosSelected()
    {
        Transform origin = barrier != null ? barrier : transform;
        Gizmos.color = new Color(0.95f, 0.78f, 0.30f, 0.9f);
        Gizmos.DrawWireSphere(origin.position, triggerDistance);
    }
}
