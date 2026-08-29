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
///   ⚠ 코트를 **입는** 것은 여기가 아니라 S#13 현관이다(FrontDoorInteraction).
///      여기서는 끌어안기만 한다. 스프라이트 교체를 이 컴포넌트에서 하지 말 것.
///   ⚠ 루가 둘러보는 모션(좌→우→원위치)은 정본에 없어 삭제했다.
/// </summary>
[RequireComponent(typeof(InteractionTrigger))]
public class AtticBoxInteraction : MonoBehaviour
{
    // ── S#09 ─────────────────────────────────────────────────────────────
    [Header("S#09 — 상자 개방")]
    [Tooltip("세 물건이 함께 놓인 상태를 보여주는 Image (Canvas). 순서대로 비추지 않는다.")]
    public UnityEngine.UI.Image boxContentsImage;
    public string yarnNode_S9_Box = "House_Attic_Box";
    [Tooltip("상자 뚜껑이 열리는 소리.")]
    public AudioClip sfxBoxOpen;

    // ── S#10 ─────────────────────────────────────────────────────────────
    [Header("S#10 — 코트 주머니")]
    public string yarnNode_S10_Coat = "House_Coat_Key";
    [Tooltip("Resources/Items/Coat.asset")]
    public ItemData coatItem;
    [Tooltip("Resources/Items/FrontDoorKey.asset — S#13 현관문을 여는 열쇠")]
    public ItemData frontDoorKeyItem;
    [Tooltip("주머니를 더듬는 손 / 꺼낸 열쇠 클로즈업 Image.")]
    public UnityEngine.UI.Image coatPocketCloseupImage;
    public AudioClip sfxClothRustle;

    // ── S#11 ─────────────────────────────────────────────────────────────
    [Header("S#11 — 라디오")]
    [Tooltip("Resources/Items/radio.asset")]
    public ItemData radioItem;
    [Tooltip("비우면 씬에서 자동 탐색한다. 없으면 S#11을 건너뛴다.")]
    public AtticRadioCutscene radioCutscene;

    // ── S#12 ─────────────────────────────────────────────────────────────
    [Header("S#12 — 단검")]
    [Tooltip("비우면 씬에서 자동 탐색한다. 없으면 단검을 직접 지급한다.")]
    public DaggerPickupCutscene daggerCutscene;
    [Tooltip("daggerCutscene 이 없을 때만 쓰는 폴백. Resources/Items/dagger.asset")]
    public ItemData daggerItem;

    // ── 공통 ─────────────────────────────────────────────────────────────
    [Header("목표 갱신")]
    public string objectiveHeader = "현재 목표";
    public string objectiveBody   = "아빠를 찾으러 가세요.";

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

        ObjectiveManager.Instance?.ShowObjective(objectiveHeader, objectiveBody);

        YarnDialogue.UnlockPlayer(ctrl);
    }

    // ─── S#09 — 상자 ─────────────────────────────────────────────────────
    // 유가 남기고 간 것이 아니라, 누군가 치워둔 배치다.
    // 세 물건이 한 상자에 있는 이유는 데모에서 설명하지 않는다. 2회차 정보다.
    IEnumerator RunS9_Box()
    {
        AudioManager.Instance?.Play(sfxBoxOpen);

        if (boxContentsImage != null)
        {
            boxContentsImage.gameObject.SetActive(true);
            yield return new WaitForSeconds(0.8f);
        }

        if (!string.IsNullOrEmpty(yarnNode_S9_Box))
            yield return YarnDialogue.PlayAndWait(yarnNode_S9_Box, false);

        if (boxContentsImage != null) boxContentsImage.gameObject.SetActive(false);
    }

    // ─── S#10 — 코트 주머니 ──────────────────────────────────────────────
    // 코트가 여기 있다는 것은 유가 코트를 입고 나가지 않았다는 뜻이다.
    // 루는 이 사실이 무엇을 의미하는지 끝까지 생각하지 않는다.
    IEnumerator RunS10_CoatPocket()
    {
        AudioManager.Instance?.Play(sfxClothRustle);

        if (coatPocketCloseupImage != null)
        {
            coatPocketCloseupImage.gameObject.SetActive(true);
            yield return new WaitForSeconds(0.8f);
        }

        GiveItem(coatItem);
        GiveItem(frontDoorKeyItem);
        GameState.isFrontDoorKeyFound = true;

        yield return WaitForAcquisitionNotice();

        // 아빠의 유품 발견
        CorruptionManager.Instance?.AddCorruption(corruptionOnFindingKeepsakes);
        Dbg.Log($"[AtticBoxInteraction] S#10 유품 발견 — 인형화 {corruptionOnFindingKeepsakes}");

        if (!string.IsNullOrEmpty(yarnNode_S10_Coat))
            yield return YarnDialogue.PlayAndWait(yarnNode_S10_Coat, false);

        if (coatPocketCloseupImage != null) coatPocketCloseupImage.gameObject.SetActive(false);
    }

    // ─── S#11 — 라디오 ───────────────────────────────────────────────────
    // ⚠ 라디오가 스스로 재생되는 것은 이 씬이 유일하다.
    //   2026-08-30 — 「이후로는 [라디오] 선택지로 호출된다」는 구 설계였다(E-52 폐기).
    //   이후의 유의 반응은 대비 오브젝트를 조사하면 결과 뒤에 한 줄이 붙는 형태이며,
    //   대비 노드 안에서 $라디오소지 로 조건 분기한다(F-8-4).
    IEnumerator RunS11_Radio()
    {
        GiveItem(radioItem);
        yield return WaitForAcquisitionNotice();

        var cutscene = radioCutscene
                       ?? AtticRadioCutscene.Instance
                       ?? Object.FindAnyObjectByType<AtticRadioCutscene>();

        if (cutscene == null)
        {
            Debug.LogWarning("[AtticBoxInteraction] AtticRadioCutscene 이 씬에 없어 S#11을 건너뜁니다. " +
                             "Home 씬에 배치해야 합니다 (Assets/Docs/유니티_수동작업.md).");
            yield break;
        }

        yield return StartCoroutine(cutscene.PlayRoutine());
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
