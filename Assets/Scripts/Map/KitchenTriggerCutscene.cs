using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;

/// <summary>
/// S#04A~H (부엌 아침 · 초인종 · 마당의 각설탕) 컷씬 트리거.
/// NightSequenceManager 종료 후 자동 호출되거나 플레이어가 트리거존에 진입하면 발동.
/// isBreakfastWatched 플래그로 한 번만 발동된다.
///
/// 2026-08-04 개편: 기존 S#04(팬케이크·마시멜로)를 S#04A~D로 전면 교체.
/// 2026-08-08 개편 (D 정본 2026-08-07): S#04E~H 추가, S#05 폐기.
///   S#04A 세 개의 접시 / S#04B 초인종 / S#04C 문틈 엿듣기 / S#04D 쪽지
///   S#04E 마시멜로   / S#04F 마당의 각설탕 / S#04G 마당인데요 / S#04H 한번만 더 와요
///
/// ⚠ S#05(세라 산책 · "다락방은 절대 가면 안 되고")는 폐기됐다.
///   정본은 세라가 금지하지 않는다. 루는 나가려다 막혀서(S#06 손잡이) 집을 뒤진다.
///   폐기 노드 원문은 Scenario/원본/폐기_드론버전_대사.md 참조.
///
/// 대사·연출 순서는 Assets/Dialogue/House_Opening.yarn 을 따른다.
/// </summary>
public class KitchenTriggerCutscene : MonoBehaviour
{
    public static KitchenTriggerCutscene Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── S#04A~D 대사 ─────────────────────────────
    [Header("S#04A — 세 개의 접시")]
    public string yarnNode_S4A_ThreePlates = "House_Kitchen_ThreePlates";

    [Header("S#04B — 초인종")]
    public string yarnNode_S4B_Ring  = "House_Doorbell_Ring";
    public string yarnNode_S4B_Tap   = "House_Doorbell_Tap";
    public string yarnNode_S4B_Sugar = "House_Doorbell_Sugar";

    [Header("S#04C — 문틈 엿듣기")]
    public string yarnNode_S4C_Tea     = "House_Eavesdrop_Tea";
    public string yarnNode_S4C_Silence = "House_Eavesdrop_Silence";
    public string yarnNode_S4C_Name    = "House_Eavesdrop_Name";
    public string yarnNode_S4C_Light   = "House_Eavesdrop_Light";

    [Header("S#04D — 쪽지")]
    public string yarnNode_S4D_Note     = "House_Note";
    public string yarnNode_S4D_Table    = "House_Note_Table";

    [Header("S#04E — 마시멜로")]
    public string yarnNode_S4E_Marshmallow = "House_Marshmallow_Eat";

    [Header("S#04F — 마당의 각설탕")]
    public string yarnNode_S4F_YardSugar = "House_Yard_Sugar";

    [Header("S#04G — 마당인데요")]
    public string yarnNode_S4G_Refuse  = "House_Yard_Refuse";
    public string yarnNode_S4G_Refuse2 = "House_Yard_Refuse2";

    [Header("S#04H — 한번만 더 와요")]
    public string yarnNode_S4H_Plea = "House_Window_Plea";

    // ── 세라 ─────────────────────────────────────
    // 2026-09-27: 옛 Freeze·ToDoor·Dishwash·LeaveHouse·TurnAround 트리거를 걷어냈다.
    //   Sera.controller 에는 dir·Speed 뿐이라 트리거가 있어도 세라가 움직이지 않았다.
    //   이제 SeraStageWalker 로 아래 자리까지 직접 걷게 한다(S#02 NightSequenceManager 와 공용).
    //   좌표는 Home.unity 실측(2026-09-27) — 싱크대 (-3.20, 2.29) · 부엌 창문 (-1.15, 2.73) ·
    //   현관문 (-0.02, -2.70) · 식탁 자리 = seraDiningSpawn(S_KitchenSpawn).
    //   yarn 노드 안에서는 <<sera_walk "자리">> · <<sera_face "방향">> 으로 부른다(아래 Yarn 커맨드).
    [Header("세라")]
    public Animator seraAnimator;
    [Tooltip("현관문 앞. 초인종에 문을 열러 가는 자리(S#04B), 외출할 때 사라지는 자리(S#04H).")]
    public Vector2 seraFrontDoorPoint = new Vector2(-0.02f, -2.1f);
    [Tooltip("싱크대 앞. 설거지하는 자리(S#04F·G). 등을 보이고 선다.")]
    public Vector2 seraSinkPoint      = new Vector2(-3.20f, 1.65f);
    [Tooltip("부엌 창문 앞. 꽃을 바라보고 잠금장치를 다시 잠그는 자리(S#04G).\n" +
             "창문(x -1.7~-0.6) 왼쪽 끝이다 — 가운데는 S#04F 에서 창밖을 본 루가 서 있어 겹친다.")]
    public Vector2 seraWindowPoint    = new Vector2(-1.6f, 2.1f);
    [Tooltip("루에게 다가갈 때 루 앞에서 멈추는 거리(월드 유닛). 머리를 쓰다듬는 거리.")]
    public float   seraNearLuDistance = 0.6f;
    [Tooltip("세라 걷는 속도(월드 유닛/초).")]
    public float   seraWalkSpeed      = 1.2f;

    // ── 효과음 ───────────────────────────────────
    [Header("효과음")]
    [SerializeField] private AudioClip sfxDoorClose;
    [Tooltip("AudioManager 에 등록한 초인종 이름. 비우면 무음으로 진행한다.")]
    public string doorbellSfxName = "";

    // ── 거실 식탁 시작 위치 ────────────────────────
    [Header("거실 식탁 시작 위치")]
    public Transform playerDiningSpawn;
    public Transform seraDiningSpawn;
    public RoomTransfer diningRoom;

    // ── S#04 연출 ─────────────────────────────────
    [Header("S#04 — BGM")]
    [Tooltip("AudioManager 프리팹 Sounds 배열에 동일 name으로 등록된 BGM 클립 이름 (category: BGM)")]
    public string bgmMusicBoxName = "music_box";

    [Header("S#04B — 각설탕 클로즈업")]
    [Tooltip("각설탕 클로즈업 Image (Canvas). 결계 밖 물건이라 채도·윤곽이 달라야 한다.")]
    public Image sugarCubeCloseupImage;

    [Header("S#04B·S#04C — 루 도자기 손 클로즈업")]
    public Image  ceramicHandCloseupImage;
    [Tooltip("AudioManager 등록명. 밤 씬과 달리 부엌 구간에서는 딱 소리가 난다.")]
    public string ceramicTapSfxName = "ceramic_tap";

    [Header("S#04C — 문틈 엿듣기")]
    [Tooltip("문틈 거리 감쇠. 비우면 씬에서 자동 탐색한다.")]
    public EavesdropAttenuator eavesdrop;
    [Tooltip("루의 방 문틈으로 들어오는 빛 오브젝트. 화이트아웃 대상.")]
    public Image doorGapLightImage;
    [Tooltip("들킬 뻔한 순간의 완전 무음 길이(초).")]
    public float heldBreathSilence = 1.5f;

    [Header("S#04D — 쪽지")]
    // 쪽지 표시는 House_Note 노드의 <<show_readable "kuru_note">> 가 맡는다 (ReadableOverlay · F-8-9).
    // 문구를 문자열로 띄우던 noteCloseupImage · noteBodyText · noteLines 는 폐지했다 —
    // 읽는 물건은 문구까지 그려 넣은 이미지 1장이며 텍스트 출력이 없다 (F-4-4 v1.25).
    [Tooltip("획득할 아이템. 비워두면 획득을 건너뛴다.")]
    public ItemData kuruNoteItem;
    public ItemData sugarCubeItem;

    [Header("S#04E — 마시멜로 씹기")]
    [Tooltip("Assets/Sound/ 에 추가할 마시멜로 씹기 클립을 여기에 드래그")]
    public AudioClip sfxMarshmallowChew;
    [Tooltip("전체 화면 흰 반투명 오버레이 Image (Canvas, 초기 alpha=0 비활성)")]
    public Image     fullScreenBlurImage;
    [Tooltip("마시멜로 섭취로 오르는 인형화 수치. ⚠ 정본에 수치 명시 없음 — " +
             "Resources/Items/Marshmallow.asset 의 fantasyEffect.puppetizationChange(+10)를 그대로 가져왔다.")]
    public float marshmallowCorruption = 10f;

    [Header("S#04F — 마당의 각설탕")]
    [Tooltip("부엌 창문 트리거. 플레이어가 다가가면 창밖을 본 것으로 처리한다. 비우면 대기 없이 진행.")]
    public WindowTrigger kitchenWindowTrigger;
    [Tooltip("창밖 보기를 기다리는 최대 시간(초). 지나면 자동 진행해 소프트락을 막는다.")]
    public float yardLookTimeout = 25f;
    [Tooltip("마당에 떨어진 각설탕 클로즈업 Image. 결계 밖 물건이라 채도·윤곽이 달라야 한다.")]
    public Image yardSugarCloseupImage;
    [Tooltip("풀린 상태의 창문 잠금장치 클로즈업 Image. S#02의 잠긴 상태 에셋을 풀린 상태로 재사용.")]
    public Image windowLockCloseupImage;
    [Tooltip("설거지 물소리 (AudioManager 등록 이름, 루프). 비우면 무음.")]
    public string sfxDishwashingLoopName = "";
    [Tooltip("각설탕이 유리에 부딪히는 '툭' 소리 (AudioManager 등록 이름). 비우면 무음.")]
    public string sfxGlassTapName = "";

    [Header("S#04G — 세라 대치")]
    [Tooltip("창문 잠금장치 딸깍 (AudioManager 등록 이름). 비우면 무음. yarn 의 <<kitchen_window_lock>> 이 울린다.")]
    public string sfxWindowLockName = "";

    [Header("S#04H — 창유리")]
    public Animator luAnimator;
    [Tooltip("다리가 떨리는 동안 적용할 이동 속도 배율. 1이면 평소와 같다.")]
    [Range(0.1f, 1f)] public float tremblingSpeedMultiplier = 0.55f;
    [Tooltip("'…아무도 오지 않는다' 뒤에 아무 일도 일어나지 않게 두는 시간(초). 정본 지정 3초.")]
    public float noAnswerSilence = 3f;

    [Header("S#06 — 조작권이 넘어올 때의 첫 목표 (D 문단 291)")]
    public string s6ObjectiveHeader = "[목표]";
    public string s6ObjectiveBody   = "마당으로 나가세요";

    // ── 캐싱된 WaitForSeconds ─────────────────────
    private static readonly WaitForSeconds _wait03s = new WaitForSeconds(0.3f);
    private static readonly WaitForSeconds _wait05s = new WaitForSeconds(0.5f);
    private static readonly WaitForSeconds _wait1s  = new WaitForSeconds(1f);

    // ─────────────────────────────────────────────

    void Start()
    {
        // 세라가 이미 외출한 뒤의 세이브를 불러오면 세라는 집에 없다(S#04H 이후).
        if (GameState.isSeraOut && seraAnimator != null)
            seraAnimator.gameObject.SetActive(false);
    }

    /// <summary>NightSequenceManager 종료 후 자동 호출.</summary>
    public void BeginCutscene()
    {
        if (GameState.isBreakfastWatched) return;
        GameState.isBreakfastWatched = true;
        StartCoroutine(PlayCutscene());
    }

    /// <summary>직접 트리거존에 진입했을 때 fallback.</summary>
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        BeginCutscene();
    }

    IEnumerator PlayCutscene()
    {
        TeleportToDiningTable();
        ObjectiveManager.Instance?.HideHUD();
        var ctrl = YarnDialogue.LockPlayer();

        yield return StartCoroutine(RunS4A_ThreePlates());
        yield return StartCoroutine(RunS4B_Doorbell());
        yield return StartCoroutine(RunS4C_Eavesdrop());
        yield return StartCoroutine(RunS4D_Note());
        yield return StartCoroutine(RunS4E_Marshmallow());
        yield return StartCoroutine(RunS4F_YardSugar());
        yield return StartCoroutine(RunS4G_Refuse());
        yield return StartCoroutine(RunS4H_Window());

        // ⚠ 정본: "암전 없이 그대로 조작권을 넘긴다. 집 안 전체가 개방된다."
        //   페이드를 넣지 말 것. 세라가 나간 직후의 정적이 그대로 이어져야 한다.
        YarnDialogue.UnlockPlayer(ctrl);

        GameState.isSeraOut = true;
        ObjectiveManager.Instance?.ResetCutscene();

        // 조작권이 넘어오는 순간 첫 목표를 띄운다 — D S#06 문단 291 「[UI][목표] 마당으로 나가세요」.
        //   마당의 각설탕을 본 직후라 현관이 가장 강한 유인이지만 강제하지 않는다(문단 293).
        //   2026-09-27 사용자 결정. 예전 주석은 「정본은 여기서 아무 지시도 주지 않는다」였으나 D 와 어긋났다.
        //   손잡이 4회 뒤의 「나갈 방법을 찾으세요」 갱신은 FrontDoorInteraction 소관.
        ObjectiveManager.Instance?.ShowObjective(s6ObjectiveHeader, s6ObjectiveBody);
    }

    // ─── S#04A — 세 개의 접시 ──────────────────────
    // 결계 안의 아침은 늘 똑같다. 유의 접시는 아무도 언급하지 않는다.
    IEnumerator RunS4A_ThreePlates()
    {
        AudioManager.Instance?.PlayLoop(bgmMusicBoxName);
        yield return YarnDialogue.PlayAndWait(yarnNode_S4A_ThreePlates, false);
    }

    // ─── S#04B — 초인종 ───────────────────────────
    // 세라는 쿠루를 죽일 수 있는데 죽이지 않는다. 자기 규칙에 스스로 묶여 있다.
    IEnumerator RunS4B_Doorbell()
    {
        // 오르골이 뚝 끊기고 초인종이 울린다 — 결계 안에서 한 번도 난 적 없는 소리
        AudioManager.Instance?.StopLoop(bgmMusicBoxName);
        PlaySfxIfNamed(doorbellSfxName);
        // 세라의 손이 멈춘다 — 그 자리에 선 채로 둔다(동작 그림 없음).

        yield return YarnDialogue.PlayAndWait(yarnNode_S4B_Ring, false);

        // 루의 도자기 손가락이 저절로 딱 — 부엌 구간은 소리가 난다
        yield return StartCoroutine(ShowCeramicHand(1));

        // 세라가 현관으로 가 문을 살짝 연다(D 문단 105). 다음 노드 첫 줄이 현관문 클로즈업이다.
        yield return SeraGo("door");

        yield return YarnDialogue.PlayAndWait(yarnNode_S4B_Tap, false);

        // 각설탕 — 결계 밖에서 들어온 물건이라 채도·윤곽이 다르다
        if (CloseupArt.Has(sugarCubeCloseupImage))
        {
            sugarCubeCloseupImage.gameObject.SetActive(true);
            yield return _wait1s;
            sugarCubeCloseupImage.gameObject.SetActive(false);
        }

        yield return YarnDialogue.PlayAndWait(yarnNode_S4B_Sugar, false);

        // 문이 열린다. 딱. 딱. 딱.
        yield return StartCoroutine(ShowCeramicHand(3));
    }

    // ─── S#04C — 문틈 엿듣기 ──────────────────────
    // 문틈에서 멀어지면 소리가 줄고 자막이 흐려진다. 강제하지 않고 유도한다.
    IEnumerator RunS4C_Eavesdrop()
    {
        if (eavesdrop == null)
            eavesdrop = Object.FindAnyObjectByType<EavesdropAttenuator>();

        // 손님을 들이고 식탁으로 돌아간다. 루가 방으로 가는 동안 같이 일어난다 — 이후 부엌은 소리로만 나온다.
        StartCoroutine(SeraGo("table"));

        // 이 구간만 조작을 풀어 준다 — 플레이어가 문틈에 붙는 행위 자체가 연출이다
        PlayerInputLock.Instance?.Unlock();
        eavesdrop?.Begin();

        yield return YarnDialogue.PlayAndWait(yarnNode_S4C_Tea, false);

        // 루가 자신도 모르게 딱딱 — 부엌의 소리가 멈춘다
        yield return StartCoroutine(ShowCeramicHand(2));

        // 완전한 무음. 조작도 함께 잠근다.
        // ⚠ 감쇠 컴포넌트를 잠시 꺼야 한다. 켜둔 채로 두면 LateUpdate 가 매 프레임
        //    거리 기반 값으로 덮어써서 무음이 0.1초도 유지되지 않는다.
        PlayerInputLock.Instance?.Lock();
        if (eavesdrop != null) eavesdrop.enabled = false;
        AudioManager.SetMuffle(0f);
        yield return new WaitForSeconds(heldBreathSilence);
        if (eavesdrop != null) eavesdrop.enabled = true;

        yield return YarnDialogue.PlayAndWait(yarnNode_S4C_Silence, false);

        PlayerInputLock.Instance?.Unlock();
        yield return YarnDialogue.PlayAndWait(yarnNode_S4C_Name, false);

        // '뿌리가 없어요' — 세라가 터진다. 대사로 설명하지 않고 빛 하나로 처리한다.
        PlayerInputLock.Instance?.Lock();
        eavesdrop?.End();
        yield return StartCoroutine(DoorGapWhiteout());

        yield return YarnDialogue.PlayAndWait(yarnNode_S4C_Light, false);
    }

    // ─── S#04D — 쪽지 ─────────────────────────────
    // 루의 독백과 손의 동작이 어긋나는 것이 이 컷의 전부다.
    IEnumerator RunS4D_Note()
    {
        // 쪽지는 노드 안에서 원고 순서대로 뜬다 — 세라의 첫 부름 뒤, [자막] 앞.
        //   <<show_readable "kuru_note">> 가 전체화면 이미지를 띄우고 플레이어가 닫을 때까지 멈춘다.
        yield return YarnDialogue.PlayAndWait(yarnNode_S4D_Note, false);

        // 못 본 척해야 한다. 그런데 손은 주머니 속에 챙긴다.
        GiveItem(kuruNoteItem);
        GiveItem(sugarCubeItem);

        // 루가 식탁에 앉으면 오르골이 돌아온다
        AudioManager.Instance?.PlayLoop(bgmMusicBoxName);

        yield return YarnDialogue.PlayAndWait(yarnNode_S4D_Table, false);
    }

    // ─── S#04E — 마시멜로 ─────────────────────────
    // 초인종 사건으로 흔들린 루를 세라가 마시멜로로 진정시킨다.
    // 환상 필터 튜토리얼이 이 지점에서 성립한다(C-3-2, C-5-1).
    IEnumerator RunS4E_Marshmallow()
    {
        SeraFaceMark("lu");
        yield return YarnDialogue.PlayAndWait(yarnNode_S4E_Marshmallow, false);

        // 씹을 때마다 화면 가장자리가 뽀얗게 번지고, 네 번째에 화면 전체가 흐려진다.
        yield return StartCoroutine(PlayMarshmallowChewing());

        // 인형화 상승 / 환상 게이지 강제 100
        CorruptionManager.Instance?.AddCorruption(marshmallowCorruption);
        GaugeManager.Instance?.ForceFantasyMax();
        Dbg.Log($"[KitchenTriggerCutscene] S#04E 마시멜로 — 인형화 +{marshmallowCorruption}, 환상 강제 100");
    }

    // ─── S#04F — 마당의 각설탕 ────────────────────
    // 쿠루가 남긴 것은 쪽지만이 아니었다. 각설탕을 던져 창을 두드리고 잠금장치를 풀어놓았다.
    // 루가 나오기만 하면 되는 상태를 만들어놓고 간 것이다.
    IEnumerator RunS4F_YardSugar()
    {
        // 마시멜로가 남긴 흐릿함 정리
        if (fullScreenBlurImage != null && fullScreenBlurImage.gameObject.activeSelf)
            yield return StartCoroutine(FadeOutImage(fullScreenBlurImage, 0.3f));

        AudioManager.Instance?.StopLoop(bgmMusicBoxName);

        // 다 먹고 세라는 설거지를 시작한다(D 문단 224). 싱크대 앞에서 등을 보인다.
        yield return SeraGo("sink");

        // 설거지 물소리 — 이 소리 때문에 세라는 '툭'도 딱 소리도 듣지 못한다
        if (!string.IsNullOrEmpty(sfxDishwashingLoopName))
            AudioManager.Instance?.PlayLoop(sfxDishwashingLoopName);

        // 앞부분 대사(툭 소리까지)를 먼저 재생한 뒤 창밖 보기를 기다린다.
        PlaySfxIfNamed(sfxGlassTapName);

        // 창밖 보기 — 강제하지 않고 유도한다. 트리거가 없거나 시간이 지나면 자동 진행.
        PlayerInputLock.Instance?.Unlock();
        yield return StartCoroutine(WaitForWindowLook());
        PlayerInputLock.Instance?.Lock();

        GameState.isYardSugarSeen = true;

        // 각설탕 → 잠금장치 순서로 클로즈업. 각설탕만 채도·윤곽이 다르다.
        yield return StartCoroutine(FlashCloseup(yardSugarCloseupImage, 1.2f));
        yield return StartCoroutine(FlashCloseup(windowLockCloseupImage, 1f));

        yield return YarnDialogue.PlayAndWait(yarnNode_S4F_YardSugar, false);
    }

    // ─── S#04G — 마당인데요 ───────────────────────
    // 세라의 시선이 각설탕 위를 멈추지 않고 통과한다. 플레이어만 그것을 안다.
    IEnumerator RunS4G_Refuse()
    {
        if (!string.IsNullOrEmpty(sfxDishwashingLoopName))
            AudioManager.Instance?.StopLoop(sfxDishwashingLoopName);

        // 물소리가 멈추고 한 박자 뒤, 세라가 천천히 뒤돌아본다(D 문단 241).
        yield return _wait05s;
        SeraFaceMark("lu");
        yield return _wait05s;

        // 창가 → 딸깍 → 루 앞 → 싱크대 는 대사 사이에 끼므로 노드 안의 <<sera_walk>> 가 맡는다.
        // 딸깍도 D 순서(문단 247→248→249)대로 노드 안의 <<kitchen_window_lock>> 이 울린다.
        yield return YarnDialogue.PlayAndWait(yarnNode_S4G_Refuse, false);

        yield return YarnDialogue.PlayAndWait(yarnNode_S4G_Refuse2, false);
    }

    // ─── S#04H — 한번만 더 와요 ───────────────────
    // 루가 처음으로 먼저 손을 뻗었는데 아무 반응이 없다. 그 직후 세라가 나가고 루가 혼자 남는다.
    IEnumerator RunS4H_Window()
    {
        // 다리가 떨린다 — 이동 속도를 낮춰 조작감으로 전달한다.
        var player = Object.FindAnyObjectByType<ClearSky.SimplePlayerController>();
        float originalSpeed = player != null ? player.walkSpeed : 0f;
        if (player != null) player.walkSpeed = originalSpeed * tremblingSpeedMultiplier;

        // 이 구간은 플레이어가 직접 방으로 걸어간다.
        // ⚠ 잠금 카운트 균형: 여기서 Unlock 한 만큼 이 코루틴 끝에서 다시 Lock 한다.
        //   PlayCutscene 이 마지막에 UnlockPlayer(ctrl) 로 0을 만든다. RunS4C_Eavesdrop 과 같은 패턴.
        PlayerInputLock.Instance?.Unlock();

        yield return YarnDialogue.PlayAndWait(yarnNode_S4H_Plea, false);

        // '…아무도 오지 않는다' — 아무것도 주지 않고 그냥 둔다.
        yield return new WaitForSeconds(noAnswerSilence);

        // 세라 외출. 루는 방에 있어 보지 못하고 소리로만 안다.
        // 현관까지 걸어가서 사라진다 — 누가 부엌에 나와 있으면 나가는 모습을 본다.
        yield return SeraGo("door");
        if (seraAnimator != null) seraAnimator.gameObject.SetActive(false);
        AudioManager.Instance?.Play(sfxDoorClose);

        // 속도 복원 — 다리 떨림이 풀린다(정본 S#07: "이동 속도를 S#04H보다 빠르게 되돌린다").
        if (player != null) player.walkSpeed = originalSpeed;

        yield return _wait05s;

        PlayerInputLock.Instance?.Lock();
    }

    public void TeleportToDiningTable()
    {
        var ctrl = Object.FindAnyObjectByType<ClearSky.SimplePlayerController>();
        if (ctrl != null && playerDiningSpawn != null)
            ctrl.transform.position = playerDiningSpawn.position;

        if (seraAnimator != null && seraDiningSpawn != null)
        {
            var companion = seraAnimator.GetComponent<CompanionFollow>();
            if (companion != null)
                companion.TeleportTo(seraDiningSpawn.position);
            else
                seraAnimator.transform.position = seraDiningSpawn.position;

            // 루가 부엌으로 나오자 세라가 환하게 웃는다(D 문단 88) — 루 쪽을 보고 선다.
            SeraFaceMark("lu");
        }

        if (diningRoom != null)
        {
            diningRoom.EnterRoom();
            CameraFollow.Instance?.SetBound(diningRoom.roomBound, snap: true);
        }
        else
        {
            // 2026-09-27: 밤은 루의 방 안에서 시작해 방 경계가 걸려 있다(RoomTransfer.Start). 부엌으로 순간이동하면서
            //   방을 나간 것으로 처리한다 — 안 하면 카메라가 루의 방 중앙에 묶인 채 아침 장면이 진행된다.
            RoomTransfer.CurrentRoom?.ExitRoom();
            CameraFollow.Instance?.SetBound(null, snap: true);
        }
    }

    // ── 세라 무대 ─────────────────────────────────

    SeraStageWalker SeraWalker => SeraStageWalker.On(seraAnimator);

    static Vector2 LuPosition()
    {
        var lu = Object.FindAnyObjectByType<ClearSky.SimplePlayerController>();
        return lu != null ? (Vector2)lu.transform.position : Vector2.zero;
    }

    /// <summary>
    /// 이름 붙은 자리로 세라를 걷게 한다. 도착하면 그 자리에 맞는 쪽을 본다.
    /// door=현관(문을 본다) · sink=싱크대(등) · window=창문(등) · table=식탁 자리(루를 본다) · lu=루 앞(루를 본다).
    /// 모르는 이름은 경고를 남기고 건너뛴다(CLAUDE.md §0-7 — 조용히 버리지 않는다).
    /// </summary>
    IEnumerator SeraGo(string mark)
    {
        var w = SeraWalker;
        if (w == null || !w.isActiveAndEnabled) yield break;

        switch (mark)
        {
            case "door":
                yield return w.WalkTo(seraFrontDoorPoint, seraWalkSpeed);
                w.Face(Vector2.down);
                break;
            case "sink":
                yield return w.WalkTo(seraSinkPoint, seraWalkSpeed);
                w.Face(Vector2.up);
                break;
            case "window":
                yield return w.WalkTo(seraWindowPoint, seraWalkSpeed);
                w.Face(Vector2.up);
                break;
            case "table":
                if (seraDiningSpawn != null) yield return w.WalkTo(seraDiningSpawn.position, seraWalkSpeed);
                w.FaceToward(LuPosition());
                break;
            case "lu":
                yield return w.WalkNear(LuPosition(), seraNearLuDistance, seraWalkSpeed);
                break;
            default:
                Debug.LogWarning($"[KitchenTriggerCutscene] sera_walk: 모르는 자리 '{mark}' — door·sink·window·table·lu 중 하나");
                break;
        }
    }

    /// <summary>세라를 제자리에서 돌려 세운다. lu=루 쪽 · up · down · left · right.</summary>
    void SeraFaceMark(string mark)
    {
        var w = SeraWalker;
        if (w == null) return;
        switch (mark)
        {
            case "lu":    w.FaceToward(LuPosition()); break;
            case "up":    w.Face(Vector2.up);    break;
            case "down":  w.Face(Vector2.down);  break;
            case "left":  w.Face(Vector2.left);  break;
            case "right": w.Face(Vector2.right); break;
            default:
                Debug.LogWarning($"[KitchenTriggerCutscene] sera_face: 모르는 방향 '{mark}' — lu·up·down·left·right 중 하나");
                break;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.4f);
        Gizmos.DrawWireSphere(seraFrontDoorPoint, 0.15f);
        Gizmos.DrawWireSphere(seraSinkPoint,      0.15f);
        Gizmos.DrawWireSphere(seraWindowPoint,    0.15f);
        if (seraDiningSpawn != null) Gizmos.DrawWireSphere(seraDiningSpawn.position, 0.15f);
    }

    // ── Yarn 커맨드 ─────────────────────────────
    // Yarn Spinner 3.x: 인스턴스 [YarnCommand] 는 첫 인자를 GameObject 이름으로 해석하므로
    // static + Instance 패턴을 쓴다 (CameraDirector · YarnCommandBridge 와 같은 규약).

    // <<sera_walk "door|sink|window|table|lu">> — 도착할 때까지 대사를 멈춘다
    [YarnCommand("sera_walk")]
    public static IEnumerator YarnSeraWalk(string mark)
    {
        if (Instance == null) yield break;
        yield return Instance.StartCoroutine(Instance.SeraGo(mark));
    }

    // <<sera_face "lu|up|down|left|right">>
    [YarnCommand("sera_face")]
    public static void YarnSeraFace(string mark)
    {
        if (Instance != null) Instance.SeraFaceMark(mark);
    }

    // <<kitchen_window_lock>> — S#04G 세라가 부엌 창문 잠금장치를 다시 잠근다. 딸깍.
    [YarnCommand("kitchen_window_lock")]
    public static void YarnKitchenWindowLock()
    {
        if (Instance != null) Instance.PlaySfxIfNamed(Instance.sfxWindowLockName);
    }

    // ── 헬퍼 ────────────────────────────────────

    /// <summary>이름이 비어 있으면 조용히 건너뛴다. 미등록 SFX 경고 도배를 막는다.</summary>
    void PlaySfxIfNamed(string soundName)
    {
        if (string.IsNullOrEmpty(soundName)) return;
        AudioManager.Instance?.Play(soundName);
    }

    void GiveItem(ItemData item)
    {
        if (item == null) return;
        if (InventoryManager.Instance == null)
        {
            Debug.LogWarning($"[KitchenTriggerCutscene] InventoryManager가 없어 '{item.itemName}' 획득을 건너뜁니다.");
            return;
        }
        InventoryManager.Instance.AddItem(item);
    }

    /// <summary>도자기 손가락 클로즈업 + 딱 소리 count회.</summary>
    IEnumerator ShowCeramicHand(int count)
    {
        if (CloseupArt.Has(ceramicHandCloseupImage))
            ceramicHandCloseupImage.gameObject.SetActive(true);

        for (int i = 0; i < count; i++)
        {
            PlaySfxIfNamed(ceramicTapSfxName);
            yield return _wait03s;
        }
        yield return _wait05s;

        if (ceramicHandCloseupImage != null)
            ceramicHandCloseupImage.gameObject.SetActive(false);
    }

    /// <summary>문틈의 가는 빛이 화면 전체를 삼킬 만큼 확 밝아진다.</summary>
    IEnumerator DoorGapWhiteout()
    {
        if (doorGapLightImage == null)
        {
            yield return _wait05s;
            yield break;
        }

        yield return StartCoroutine(FadeInImage(doorGapLightImage, 1f, 0.15f));
        yield return _wait05s;
        yield return StartCoroutine(FadeOutImage(doorGapLightImage, 0.6f));
    }

    IEnumerator PlayMarshmallowChewing()
    {
        for (int i = 1; i <= 4; i++)
        {
            AudioManager.Instance?.Play(sfxMarshmallowChew);
            ScreenEdgeEffectController.ShowMarshmallow(0.8f);

            if (i == 4 && fullScreenBlurImage != null)
                yield return StartCoroutine(FadeInImage(fullScreenBlurImage, 0.7f, 0.5f));
            else
                yield return new WaitForSeconds(0.9f);
        }
    }

    /// <summary>
    /// 플레이어가 부엌 창문에 다가가기를 기다린다. 강제하지 않고 유도한다(정본).
    /// 트리거가 없거나 yardLookTimeout 을 넘기면 자동으로 진행해 소프트락을 막는다.
    /// </summary>
    IEnumerator WaitForWindowLook()
    {
        if (kitchenWindowTrigger == null)
        {
            yield return _wait1s;
            yield break;
        }

        kitchenWindowTrigger.Arm();

        float elapsed = 0f;
        while (!kitchenWindowTrigger.HasReached && elapsed < yardLookTimeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!kitchenWindowTrigger.HasReached)
            Dbg.Log("[KitchenTriggerCutscene] S#04F 창밖 보기 타임아웃 — 자동 진행");
    }

    /// <summary>클로즈업 Image 를 잠깐 띄웠다 끈다. 비어 있으면 조용히 건너뛴다.</summary>
    IEnumerator FlashCloseup(Image image, float holdSeconds)
    {
        if (!CloseupArt.Has(image)) yield break;   // 그림이 없으면 흰 화면만 뜬다

        image.gameObject.SetActive(true);
        yield return new WaitForSeconds(holdSeconds);
        image.gameObject.SetActive(false);
    }

    IEnumerator FadeInImage(Image image, float targetAlpha, float duration)
    {
        Color c = image.color;
        c.a = 0f;
        image.color = c;
        image.gameObject.SetActive(true);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(0f, targetAlpha, elapsed / duration);
            image.color = c;
            yield return null;
        }
        c.a = targetAlpha;
        image.color = c;
    }

    IEnumerator FadeOutImage(Image image, float duration)
    {
        Color c     = image.color;
        float start = c.a;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(start, 0f, elapsed / duration);
            image.color = c;
            yield return null;
        }
        c.a = 0f;
        image.color = c;
        image.gameObject.SetActive(false);
    }
}
