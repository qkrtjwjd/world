using System.Collections;
using UnityEngine;
using UnityEngine.UI;

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

    // ── 세라 Animator ─────────────────────────────
    [Header("세라 Animator")]
    public Animator seraAnimator;
    [Tooltip("초인종에 반응해 손을 멈추는 트리거.")]
    public string seraFreezeTrigger      = "Freeze";
    [Tooltip("현관으로 걸어가는 트리거.")]
    public string seraToDoorTrigger      = "ToDoor";
    [Tooltip("설거지를 시작하는 트리거.")]
    public string seraDishwashTrigger    = "Dishwash";
    [Tooltip("집을 나서는 트리거. S#04H 끝에서 발동.")]
    public string seraLeaveHouseTrigger  = "LeaveHouse";

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
    public Image noteCloseupImage;
    [Tooltip("쪽지 본문 텍스트. 손글씨 폰트를 지정한 TMP_Text (noteCloseupImage 자식). " +
             "비우면 본문이 화면에 뜨지 않는다.")]
    public TMPro.TMP_Text noteBodyText;
    [Tooltip("쪽지 본문. 대화창이 아니라 이 UI 로 출력한다(정본 S#04D). " +
             "출처는 Scenario/output_v3/notes.json. 줄당 한 항목.")]
    [TextArea] public string[] noteLines;
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
    [Tooltip("세라가 뒤돌아보는 트리거.")]
    public string seraTurnAroundTrigger = "TurnAround";
    [Tooltip("창문 잠금장치 딸깍 (AudioManager 등록 이름). 비우면 무음.")]
    public string sfxWindowLockName = "";

    [Header("S#04H — 창유리")]
    public Animator luAnimator;
    [Tooltip("다리가 떨리는 동안 적용할 이동 속도 배율. 1이면 평소와 같다.")]
    [Range(0.1f, 1f)] public float tremblingSpeedMultiplier = 0.55f;
    [Tooltip("'…아무도 오지 않는다' 뒤에 아무 일도 일어나지 않게 두는 시간(초). 정본 지정 3초.")]
    public float noAnswerSilence = 3f;

    // ── 캐싱된 WaitForSeconds ─────────────────────
    private static readonly WaitForSeconds _wait03s = new WaitForSeconds(0.3f);
    private static readonly WaitForSeconds _wait05s = new WaitForSeconds(0.5f);
    private static readonly WaitForSeconds _wait08s = new WaitForSeconds(0.8f);
    private static readonly WaitForSeconds _wait1s  = new WaitForSeconds(1f);

    // ─────────────────────────────────────────────

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

        // ⚠ 목표를 띄우지 않는다. 정본은 여기서 아무 지시도 주지 않는다.
        //   첫 목표는 S#06(현관 손잡이 4회 실패) 뒤에 "나갈 방법을 찾으세요"로 처음 뜬다.
        //   FrontDoorInteraction 소관.
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
        if (seraAnimator != null && !string.IsNullOrEmpty(seraFreezeTrigger))
            seraAnimator.SetTrigger(seraFreezeTrigger);

        yield return YarnDialogue.PlayAndWait(yarnNode_S4B_Ring, false);

        // 루의 도자기 손가락이 저절로 딱 — 부엌 구간은 소리가 난다
        yield return StartCoroutine(ShowCeramicHand(1));

        if (seraAnimator != null && !string.IsNullOrEmpty(seraToDoorTrigger))
            seraAnimator.SetTrigger(seraToDoorTrigger);

        yield return YarnDialogue.PlayAndWait(yarnNode_S4B_Tap, false);

        // 각설탕 — 결계 밖에서 들어온 물건이라 채도·윤곽이 다르다
        if (sugarCubeCloseupImage != null)
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
        // 쪽지 본문은 대화창이 아니라 전용 UI 로 나간다. 결계 안 UI 와 다른 폰트여야
        // '밖에서 들어온 물건' 이라는 것이 전달된다 (정본 S#04D ▶ 스프라이트).
        if (noteBodyText != null)
            noteBodyText.text = noteLines != null ? string.Join("\n", noteLines) : string.Empty;

        if (noteCloseupImage != null)
        {
            noteCloseupImage.gameObject.SetActive(true);
            yield return _wait08s;
        }

        yield return YarnDialogue.PlayAndWait(yarnNode_S4D_Note, false);

        if (noteCloseupImage != null) noteCloseupImage.gameObject.SetActive(false);
        if (noteBodyText != null) noteBodyText.text = string.Empty;

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
        if (seraAnimator != null && !string.IsNullOrEmpty(seraDishwashTrigger))
            seraAnimator.SetTrigger(seraDishwashTrigger);

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

        if (seraAnimator != null && !string.IsNullOrEmpty(seraTurnAroundTrigger))
            seraAnimator.SetTrigger(seraTurnAroundTrigger);
        yield return _wait05s;

        yield return YarnDialogue.PlayAndWait(yarnNode_S4G_Refuse, false);

        // 잠금장치를 다시 잠근다. 딸깍.
        PlaySfxIfNamed(sfxWindowLockName);
        yield return _wait03s;

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

        // 세라 외출. 루는 보지 못하고 소리로만 안다.
        if (seraAnimator != null && !string.IsNullOrEmpty(seraLeaveHouseTrigger))
            seraAnimator.SetTrigger(seraLeaveHouseTrigger);
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
        }

        if (diningRoom != null)
        {
            diningRoom.EnterRoom();
            CameraFollow.Instance?.SetBound(diningRoom.roomBound, snap: true);
        }
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
        if (ceramicHandCloseupImage != null)
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
        if (image == null) yield break;

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
