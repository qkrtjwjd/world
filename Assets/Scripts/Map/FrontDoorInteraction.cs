using System.Collections;
using UnityEngine;

/// <summary>
/// S#06 돌아가지 않는 손잡이 + S#13 자기 발로.
/// InteractionTrigger.onInteract UnityEvent 에 OnDoorInteract() 를 연결하세요.
///
/// 2026-08-08 개편 (D 정본 2026-08-07)
///   이전 구현은 "단검을 장착했는가"로 문을 열었다. 정본은 다르다.
///
///   S#06 — 현관문 열쇠가 없을 때
///     1~3회 : 완전히 동일한 애니메이션 재사용. 무반응. 대사 없음.
///     4회   : 아주 낮은 저역음 + 손잡이가 뜨거워진다 → 손을 뗀다.
///             화상 이펙트나 붉은 표시는 넣지 않는다. 루는 아파하지 않는다.
///             이 집에서 이런 일은 처음이 아니라는 것이 무반응으로 전달되어야 한다.
///             → 목표 갱신 "나갈 방법을 찾으세요" + 집 안 전체 탐색 개방
///     5회 이후 : 다시 무반응. 4회째 연출은 한 번뿐이다.
///
///   S#13 — 현관문 열쇠를 가졌을 때 (유의 코트 주머니, S#10)
///     딸깍 → 문이 열린다 → 마당으로 나간다.
///     ⚠ 컷신으로 처리하지 않는다. 마당~정문은 플레이어가 직접 걷는다.
///        씬 전환은 FrontGateTrigger 가 맡는다.
///
/// ※ 손잡이가 뜨거워지는 것은 자물쇠가 아니라 결계의 거부다. 대사로 설명하지 않는다.
/// ※ 정본 S#06: "세라가 금지해서 움직이는 것이 아니라, 나가려다 막혀서 움직인다.
///    이 차이가 쿠루의 계획 전체를 성립시킨다."
/// </summary>
public class FrontDoorInteraction : MonoBehaviour
{
    [Header("문을 여는 데 필요한 아이템 이름 (ItemData.itemName 과 일치해야 함)")]
    public string requiredItemName = "현관문 열쇠";

    [Header("S#06 — 손잡이가 거부하는 Yarn 노드 (4번째 시도에서 1회만)")]
    public string yarnNode_refused = "House_Doorknob_Refused";

    [Header("S#13 — 문을 열고 나가는 Yarn 노드")]
    public string yarnNode_departure = "House_FrontDoor_Depart";

    [Header("손잡이 애니메이션")]
    [Tooltip("1~3회 시도에 재사용할 Animator. 같은 트리거를 매번 그대로 쓴다.")]
    public Animator doorknobAnimator;
    public string   doorknobTurnTrigger    = "Turn";
    [Tooltip("4번째에만 붙는 '손을 떼는' 트리거.")]
    public string   doorknobRecoilTrigger  = "Recoil";

    [Header("효과음 (AudioManager 등록 이름. 비우면 무음)")]
    [Tooltip("1~3회 — 매번 정확히 같은 음.")]
    public string sfxKnobTurnName  = "";
    [Tooltip("4회 — 소리가 나지 않고 대신 아주 낮은 저역음이 깔린다.")]
    public string sfxRefusalLowName = "";
    [Tooltip("4회 — 손을 떼는 마찰음. 짧게.")]
    public string sfxHandReleaseName = "";
    [Tooltip("S#13 — 열쇠가 구멍에 들어가고 돌아가는 딸깍.")]
    public string sfxKeyUnlockName = "";

    [Header("S#13 — 문이 열린 뒤")]
    [Tooltip("코트를 입은 루 스프라이트. 비우면 교체하지 않는다.")]
    public Sprite coatedPlayerSprite;
    [Tooltip("문이 열리면 활성화할 오브젝트 (마당 배경·정문 등).")]
    public GameObject[] objectsToEnable;
    [Tooltip("문이 열리면 비활성화할 오브젝트 (닫힌 문 스프라이트·막는 콜라이더 등).")]
    public GameObject[] objectsToDisable;
    [Tooltip("문이 열린 뒤 플레이어가 서 있을 마당 위치. 비우면 이동하지 않는다.")]
    public Transform yardSpawnPoint;

    [Header("목표")]
    public string refusedObjectiveHeader = "[목표 갱신]";
    public string refusedObjectiveBody   = "나갈 방법을 찾으세요";

    /// <summary>정본 지정 — 4번째 시도에서 손잡이가 뜨거워진다.</summary>
    private const int RefusalAttempt = 4;

    private int  _attemptCount;
    private bool _isBusy;
    private bool _departed;
    private bool _sealed;

    /// <summary>탈출 압박(집 90초)에 실패해 문이 영구 폐쇄됐는지 여부.</summary>
    public bool IsSealed => _sealed;

    /// <summary>
    /// 현관문을 영구히 봉인합니다. 열쇠를 가지고 있어도 열리지 않습니다(C-14-2 "열쇠가 통하지 않는다").
    /// 집 90초 탈출 압박 실패 시 <see cref="HouseEscapePressureController"/> 가 호출합니다.
    /// </summary>
    /// <remarks>
    /// 컴포넌트를 비활성화하는 방식은 쓰지 않습니다 — 상호작용 프롬프트가 통째로 사라져
    /// '닫혔다'는 정보 자체가 전달되지 않습니다. 문은 남아 있고 반응만 없어야 합니다.
    /// </remarks>
    public void SealPermanently()
    {
        if (_sealed) return;
        _sealed = true;
        PlaySfxIfNamed(sfxRefusalLowName);
    }

    /// <summary>InteractionTrigger.onInteract 에 연결.</summary>
    public void OnDoorInteract()
    {
        if (_isBusy || _departed || _sealed) return;

        var inv = InventoryManager.Instance ?? Object.FindAnyObjectByType<InventoryManager>();
        bool hasKey = inv != null && inv.HasItem(requiredItemName);

        if (hasKey)
        {
            _isBusy = true;
            StartCoroutine(DepartRoutine());
        }
        else
        {
            _isBusy = true;
            StartCoroutine(RefusedRoutine());
        }
    }

    // ─── S#06 ────────────────────────────────────────────────────────────
    IEnumerator RefusedRoutine()
    {
        _attemptCount++;

        // 1~3회: 완전히 동일한 애니메이션. 반복이 그대로 보여야 한다.
        if (doorknobAnimator != null && !string.IsNullOrEmpty(doorknobTurnTrigger))
            doorknobAnimator.SetTrigger(doorknobTurnTrigger);

        if (_attemptCount < RefusalAttempt)
        {
            PlaySfxIfNamed(sfxKnobTurnName);
            yield return new WaitForSeconds(0.6f);
            _isBusy = false;
            yield break;
        }

        // 4회 이후: 이미 한 번 거부당했다면 다시 무반응으로 되돌린다.
        if (GameState.isDoorknobRefused)
        {
            PlaySfxIfNamed(sfxKnobTurnName);
            yield return new WaitForSeconds(0.6f);
            _isBusy = false;
            yield break;
        }

        // 4회째 — 손잡이가 뜨거워진다.
        GameState.isDoorknobRefused = true;

        var ctrl = YarnDialogue.LockPlayer();

        // 손잡이 도는 소리가 나지 않고, 대신 아주 낮은 저역음이 깔린다.
        PlaySfxIfNamed(sfxRefusalLowName);
        yield return new WaitForSeconds(0.5f);

        if (doorknobAnimator != null && !string.IsNullOrEmpty(doorknobRecoilTrigger))
            doorknobAnimator.SetTrigger(doorknobRecoilTrigger);
        PlaySfxIfNamed(sfxHandReleaseName);

        if (!string.IsNullOrEmpty(yarnNode_refused))
            yield return YarnDialogue.PlayAndWait(yarnNode_refused, false);

        // 목표는 여기서 처음 뜬다. 아침 컷씬은 아무 지시도 주지 않았다.
        ObjectiveManager.Instance?.ShowObjective(refusedObjectiveHeader, refusedObjectiveBody);

        YarnDialogue.UnlockPlayer(ctrl);
        _isBusy = false;
    }

    // ─── S#13 ────────────────────────────────────────────────────────────
    IEnumerator DepartRoutine()
    {
        var ctrl = YarnDialogue.LockPlayer();

        PlaySfxIfNamed(sfxKeyUnlockName);

        // 코트를 입은 루로 교체 — 정본은 다락방이 아니라 현관에서 코트를 입는다.
        // 소매가 손을 덮어 도자기 손가락이 가려지는 것이 이 스프라이트의 핵심이다.
        ApplyCoatedSprite();

        if (!string.IsNullOrEmpty(yarnNode_departure))
            yield return YarnDialogue.PlayAndWait(yarnNode_departure, false);

        foreach (var obj in objectsToEnable)
            if (obj != null) obj.SetActive(true);
        foreach (var obj in objectsToDisable)
            if (obj != null) obj.SetActive(false);

        // 마당으로 내보낸다. 여기서부터 정문까지는 플레이어가 직접 걷는다 —
        // '자신의 발로'라는 문장이 조작으로 성립해야 하므로 컷신을 넣지 않는다.
        MoveToYard();

        _departed = true;

        var trigger = GetComponent<InteractionTrigger>();
        if (trigger != null) trigger.enabled = false;

        YarnDialogue.UnlockPlayer(ctrl);
    }

    void ApplyCoatedSprite()
    {
        if (coatedPlayerSprite == null) return;

        var player = Object.FindAnyObjectByType<ClearSky.SimplePlayerController>();
        if (player == null) return;

        var sr = player.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) sr.sprite = coatedPlayerSprite;
    }

    void MoveToYard()
    {
        if (yardSpawnPoint == null) return;

        var player = Object.FindAnyObjectByType<ClearSky.SimplePlayerController>();
        if (player != null) player.transform.position = yardSpawnPoint.position;

        var room = yardSpawnPoint.GetComponentInParent<RoomTransfer>();
        if (room != null)
        {
            room.EnterRoom();
            CameraFollow.Instance?.SetBound(room.roomBound, snap: true);
        }
        else
        {
            CameraFollow.Instance?.SetBound(null, snap: true);
        }
    }

    /// <summary>이름이 비어 있으면 조용히 건너뛴다. 미등록 SFX 경고 도배를 막는다.</summary>
    void PlaySfxIfNamed(string soundName)
    {
        if (string.IsNullOrEmpty(soundName)) return;
        AudioManager.Instance?.Play(soundName);
    }
}
