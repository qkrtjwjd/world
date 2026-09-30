using System.Collections;
using UnityEngine;

/// <summary>
/// S#09~S#12 다락방 상자 — 코트 · 라디오 · 단검.
///
/// [설정 방법]
/// 1. 이 컴포넌트를 다락방 상자 GameObject 에 추가
/// 2. InteractionTrigger.onInteract 에 OnBoxInteract() 연결
/// 3. ItemPickup 컴포넌트는 제거
///
/// 2026-08-08 개편 (D 정본 2026-08-07)
///   이전 구현은 아이템 3개를 한 번에 주고 글리치 뒤 단검을 바로 쥐여준 다음
///   플레이어 스프라이트까지 교체하고 끝났다. 정본은 4단계다.
///
///   S#09 상자   — 코트·라디오·단검을 **한 화면에 동시** 노출. 아이템 지급 없음.
///                 순서대로 비추지 않는다. 셋이 함께 놓여 있었다는 사실 자체가 정보다.
///   S#10 코트   — 주머니에서 현관문 열쇠. 유의 코트 + 현관문 열쇠 획득.
///   S#11 라디오 — 다이얼이 저 혼자 떨리며 아빠 목소리. 라디오 획득 + 시스템 활성화.
///   S#12 단검   — 단검 획득 → 0.5초 현실 컷 → 필터 토글 개방. DaggerPickupCutscene 소관.
///
///   ⚠ 코트는 **S#11 끝에 여기서 입는다**(D 467 · 2026-09-27 사용자 결정). 전에는 S#13 현관에서 입혔다(D 420 쪽).
///      S#11 의 「코트를 끌어안는」 동작(D 382)은 입기 전이다.
///   ⚠ 루가 둘러보는 모션(좌→우→원위치)은 정본에 없어 삭제했다.
/// </summary>
[RequireComponent(typeof(InteractionTrigger))]
public class AtticBoxInteraction : MonoBehaviour
{
    // ── S#09 ─────────────────────────────────────────────────────────────
    [Header("S#09 — 상자 개방")]
    [Tooltip("⛔ 폐기됨(2026-10-01) — 오버레이 공용 시스템(OverlayCut · house_overlay_attic_box)으로 옮겼다. 세 물건이 함께 놓인 한 장이며 순서대로 비추지 않는다. 직렬화 값 때문에 필드만 남긴다.")]
    public UnityEngine.UI.Image boxContentsImage;
    public string yarnNode_S9_Box = "House_Attic_Box";
    [Tooltip("상자 뚜껑이 열리는 소리.")]
    public AudioClip sfxBoxOpen;
    [Tooltip("상자를 여는 순간부터 깔리는 아주 낮은 지속음 (AudioManager 등록 이름, 루프). 비우면 무음.\n" +
             "D 문단 333 에서 시작해 S#10 동안 유지(346), S#11 라디오에서 끊는다(362).")]
    public string droneLoopName = "";

    // 2026-09-27: House_Attic_Box 의 camera_closeup "상자" 를 걷어냈다(6배로 파고들었다). 같은 날 대신 넣은
    //   카메라 부감도 줌 폐기(E-64 · F-3-9)로 걷어냈다. 개정 D 문단 348: 방 전경 고정 + 열린 상자 오버레이 컷.

    // ── S#10 ─────────────────────────────────────────────────────────────
    [Header("S#10 — 코트 주머니")]
    public string yarnNode_S10_Coat = "House_Coat_Key";
    [Tooltip("Resources/Items/Coat.asset")]
    public ItemData coatItem;
    [Tooltip("Resources/Items/FrontDoorKey.asset — S#13 현관문을 여는 열쇠")]
    public ItemData frontDoorKeyItem;
    [Tooltip("⛔ 폐기됨(2026-10-01) — 오버레이 공용 시스템(OverlayCut · house_overlay_front_door_key)으로 옮겼다. 「주머니를 더듬는 손」 컷은 E-64 에서 내렸다. 직렬화 값 때문에 필드만 남긴다.")]
    public UnityEngine.UI.Image coatPocketCloseupImage;
    public AudioClip sfxClothRustle;

    // ── S#11 ─────────────────────────────────────────────────────────────
    [Header("S#11 — 라디오")]
    [Tooltip("Resources/Items/radio.asset")]
    public ItemData radioItem;
    [Tooltip("비우면 씬에서 자동 탐색한다. 없으면 S#11을 건너뛴다.")]
    public AtticRadioCutscene radioCutscene;
    [Tooltip("코트를 입은 루의 이동 애니메이터(4방향 · 소매가 손을 덮는 상태, D 456). 비우면 교체하지 않는다.\n" +
             "스프라이트 한 장이 아니라 컨트롤러를 바꾼다 — SpriteRenderer.sprite 는 Animator 가 매 프레임 덮어쓴다.")]
    public RuntimeAnimatorController coatedLuAnimator;

    // ── S#12 ─────────────────────────────────────────────────────────────
    [Header("S#12 — 단검")]
    [Tooltip("비우면 씬에서 자동 탐색한다. 없으면 단검을 직접 지급한다.")]
    public DaggerPickupCutscene daggerCutscene;
    [Tooltip("daggerCutscene 이 없을 때만 쓰는 폴백. Resources/Items/dagger.asset")]
    public ItemData daggerItem;

    // ── 공통 ─────────────────────────────────────────────────────────────
    // 2026-09-27: 시퀀스 끝의 목표 「아빠를 찾으러 가세요.」를 뺐다(사용자 결정). D 에 없는 문구이고,
    //   S#12 [튜토리얼] 을 즉시 덮어써 보이지 않게 했다. D 는 S#12 뒤 튜토리얼 하나만 둔다(문단 403).

    [Tooltip("아빠의 유품을 발견했을 때의 인형화 변동. 정본 미명시 — 기존 값 유지.")]
    public float corruptionOnFindingKeepsakes = -3f;

    private bool _used;

    void Start()
    {
        // 세이브 로드 후 재개봉 방지
        if (GameState.isAtticBoxOpened)
        {
            _used = true;
            GetComponent<InteractionTrigger>().enabled = false;
        }

        // 세이브 로드 · 되감기로 S#11 이후에 다시 들어오면 코트 차림으로 시작한다. S#11 끝(WearCoat)과 같은 기준이다.
        if (GameState.isAtticRadioPlayed) WearCoat();
    }

    /// <summary>InteractionTrigger.onInteract 에 연결.</summary>
    public void OnBoxInteract()
    {
        if (_used || GameState.isAtticBoxOpened) return;
        _used = true;
        GameState.isAtticBoxOpened = true;

        GetComponent<InteractionTrigger>().enabled = false;

        StartCoroutine(BoxRoutine());
    }

    IEnumerator BoxRoutine()
    {
        var ctrl = YarnDialogue.LockPlayer();

        yield return StartCoroutine(RunS9_Box());
        yield return StartCoroutine(RunS10_CoatPocket());
        yield return StartCoroutine(RunS11_Radio());
        yield return StartCoroutine(RunS12_Dagger());

        // 다락방 진입 목표 「상자를 살펴보세요.」(AtticDoorCutscene)를 내린다. 예전엔 뒤이은 목표가 덮어써 가렸다.
        ObjectiveManager.Instance?.HideObjective();

        YarnDialogue.UnlockPlayer(ctrl);
    }

    // ─── S#09 — 상자 ─────────────────────────────────────────────────────
    // 유가 남기고 간 것이 아니라, 누군가 치워둔 배치다.
    // 세 물건이 한 상자에 있는 이유는 데모에서 설명하지 않는다. 2회차 정보다.
    IEnumerator RunS9_Box()
    {
        AudioManager.Instance?.Play(sfxBoxOpen);

        // 상자를 여는 순간부터 아주 낮은 지속음 하나(D 문단 333). S#11 라디오에서 끊는다.
        if (!string.IsNullOrEmpty(droneLoopName))
            AudioManager.Instance?.PlayLoop(droneLoopName);

        // 열린 상자 오버레이 컷(개정 D 문단 348 · F-3-9). 카메라로 대신하지 않는다(E-64 줌 폐기).
        OverlayCut.Instance.Show(OverlayIds.HouseAtticBox);
        yield return new WaitForSeconds(0.8f);

        if (!string.IsNullOrEmpty(yarnNode_S9_Box))
            yield return YarnDialogue.PlayIfExists(yarnNode_S9_Box, false);

        OverlayCut.Instance.Hide();
    }

    // ─── S#10 — 코트 주머니 ──────────────────────────────────────────────
    // 코트가 여기 있다는 것은 유가 코트를 입고 나가지 않았다는 뜻이다.
    // 루는 이 사실이 무엇을 의미하는지 끝까지 생각하지 않는다.
    IEnumerator RunS10_CoatPocket()
    {
        AudioManager.Instance?.Play(sfxClothRustle);

        // 꺼낸 현관문 열쇠 오버레이 컷(F-3-9). 주머니를 더듬는 손은 E-64 에서 내렸다.
        OverlayCut.Instance.Show(OverlayIds.HouseFrontDoorKey);
        yield return new WaitForSeconds(0.8f);

        GiveItem(coatItem);
        GiveItem(frontDoorKeyItem);
        GameState.isFrontDoorKeyFound = true;

        yield return WaitForAcquisitionNotice();

        // 아빠의 유품 발견
        CorruptionManager.Instance?.AddCorruption(corruptionOnFindingKeepsakes);
        Dbg.Log($"[AtticBoxInteraction] S#10 유품 발견 — 인형화 {corruptionOnFindingKeepsakes}");

        if (!string.IsNullOrEmpty(yarnNode_S10_Coat))
            yield return YarnDialogue.PlayAndWait(yarnNode_S10_Coat, false);

        OverlayCut.Instance.Hide();
    }

    // ─── S#11 — 라디오 ───────────────────────────────────────────────────
    // ⚠ 라디오가 스스로 재생되는 것은 이 씬이 유일하다.
    //   2026-08-30 — 「이후로는 [라디오] 선택지로 호출된다」는 구 설계였다(E-52 폐기).
    //   이후의 유의 반응은 대비 오브젝트를 조사하면 결과 뒤에 한 줄이 붙는 형태이며,
    //   대비 노드 안에서 $라디오소지 로 조건 분기한다(F-8-4).
    IEnumerator RunS11_Radio()
    {
        // 지속음은 여기서 끊는다 — 유의 목소리 외에 아무 소리도 없어야 한다(D 문단 362).
        if (!string.IsNullOrEmpty(droneLoopName))
            AudioManager.Instance?.StopLoop(droneLoopName);

        var cutscene = radioCutscene
                       ?? AtticRadioCutscene.Instance
                       ?? Object.FindAnyObjectByType<AtticRadioCutscene>();

        if (cutscene == null)
            Debug.LogWarning("[AtticBoxInteraction] AtticRadioCutscene 이 씬에 없어 S#11을 건너뜁니다. " +
                             "Home 씬에 배치해야 합니다 (Assets/Docs/유니티_수동작업.md).");
        else
            yield return StartCoroutine(cutscene.PlayRoutine());

        // 코트는 S#11 다락방에서 입는다(D 467 · 2026-09-27 사용자 결정 — D 420 「현관 앞 코트를 입은 루」와의 모순을 이쪽으로 정했다).
        // 「데리러 갈게요」 직후다. 그래서 S#12 부터 발동하는 BE#01 에서 루는 언제나 코트 차림이다(D 465·467).
        WearCoat();

        // [아이템 획득: 라디오] 는 씬 끝(D 문단 385). 먼저 띄우면 다이얼이 「저 혼자」 떨리기 전에 알림이 앞선다.
        // 2026-09-27 사용자 결정으로 컷씬 앞에서 뒤로 옮겼다. 컷씬이 없어도 라디오는 준다.
        GiveItem(radioItem);
        yield return WaitForAcquisitionNotice();
    }

    // ─── S#12 — 단검 ─────────────────────────────────────────────────────
    IEnumerator RunS12_Dagger()
    {
        var cutscene = daggerCutscene
                       ?? DaggerPickupCutscene.Instance
                       ?? Object.FindAnyObjectByType<DaggerPickupCutscene>();

        if (cutscene != null)
        {
            yield return StartCoroutine(cutscene.PlayRoutine());
            yield break;
        }

        // 폴백 — 컷씬 컴포넌트가 없으면 최소한 단검은 쥐여준다.
        Debug.LogWarning("[AtticBoxInteraction] DaggerPickupCutscene 이 씬에 없어 " +
                         "0.5초 현실 전환을 건너뜁니다. 단검만 지급합니다.");
        GiveItem(daggerItem);
        DaggerSystem.Instance?.Equip();
        GameState.isDaggerAcquired       = true;
        GameState.isDaggerToggleUnlocked = true;
        yield return WaitForAcquisitionNotice();
    }

    // ─── 헬퍼 ────────────────────────────────────────────────────────────
    void WearCoat()
    {
        if (coatedLuAnimator == null) return;
        var lu = Object.FindAnyObjectByType<ClearSky.SimplePlayerController>();
        var anim = lu != null ? lu.GetComponent<Animator>() : null;
        if (anim != null) anim.runtimeAnimatorController = coatedLuAnimator;
    }

    void GiveItem(ItemData item)
    {
        if (item == null) return;
        if (InventoryManager.Instance == null)
        {
            Debug.LogWarning($"[AtticBoxInteraction] InventoryManager가 없어 '{item.itemName}' 획득을 건너뜁니다.");
            return;
        }
        InventoryManager.Instance.AddItem(item);
    }

    /// <summary>획득 알림 UI가 사라질 때까지 기다린다.</summary>
    IEnumerator WaitForAcquisitionNotice()
    {
        float wait = ItemAcquisitionUI.Instance != null
            ? ItemAcquisitionUI.Instance.displayDuration : 2f;
        yield return new WaitForSeconds(wait);
    }
}
