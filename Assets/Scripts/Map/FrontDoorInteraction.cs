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
    [Tooltip("1~3회 시도에 재사용할 Animator. 같은 트리거를 매번 그대로 쓴다.\n" +
             "비어 있으면 아래 「임시 동작」으로 루를 움직인다(그림이 오면 여기에 꽂는다).")]
    public Animator doorknobAnimator;
    public string   doorknobTurnTrigger    = "Turn";
    [Tooltip("4번째에만 붙는 '손을 떼는' 트리거.")]
    public string   doorknobRecoilTrigger  = "Recoil";

    // 2026-09-27 개정 D 문단 304 [CAM]: 「고정. 현관 전경에서 루가 문 앞에 선 상태. 손잡이를 따로 당겨 보여주지 않는다.
    //   1~3번째 시도 동안 화면은 전혀 움직이지 않는다. 4번째에만 카메라가 문 반대쪽으로 아주 느리게 반 타일 스크롤한다.」
    //   같은 날 먼저 만든 손잡이 줌 클로즈업은 줌 폐기(E-64 · F-3-9)로 걷어냈다.
    [Header("S#06 — 카메라 (개정 D 문단 304)")]
    [Tooltip("문 위치 기준. 비우면 이 오브젝트(현관문).")]
    public Transform knobCloseupTarget;
    [Tooltip("4번째에 문 반대쪽으로 스크롤하는 거리(월드 유닛). 1 = 타일 하나 → 반 타일 0.5.")]
    public float refusalScrollDistance = 0.5f;
    [Tooltip("그 스크롤 속도(월드 유닛/초). 「아주 느리게」.")]
    public float refusalScrollSpeed = 0.25f;
    [Tooltip("루가 문에서 이 거리(월드 유닛) 넘게 멀어지면 고정을 풀고 추적으로 돌아간다.")]
    public float knobCloseupReleaseDistance = 1.5f;

    // 손잡이 그림·애니메이션이 없어서 루 본인을 움직여 대신한다. 1~3회는 매번 완전히 같은 흔들림이다
    // (D 문단 295 「반복이 그대로 보여야 한다」). 4번째만 반 발짝 물러나 손을 뗀다. 아파하는 기색은 없다.
    [Header("S#06 — 임시 동작 (doorknobAnimator 가 비었을 때)")]
    [Tooltip("손잡이를 돌릴 때 루가 흔들리는 폭(월드 유닛). 1/32 = 화면 1픽셀.")]
    public float knobTurnJiggle = 1f / 32f;
    [Tooltip("4번째에 손을 떼며 물러나는 거리(월드 유닛).")]
    public float knobRecoilDistance = 0.25f;

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

    // 2026-09-27 사용자 결정: D S#07 문단 306 「[UI][목표] 집 안을 뒤져보세요」는
    //   거부 뒤 루가 현관에서 걸어 나가는 순간(S#06 → S#07 경계) 띄운다.
    [Header("S#07 — 현관을 벗어나면 (D 문단 306)")]
    public string searchObjectiveHeader = "[목표]";
    public string searchObjectiveBody   = "집 안을 뒤져보세요";
    [Tooltip("거부 뒤 루가 문에서 이 거리(월드 유닛) 넘게 멀어지면 S#07 목표로 바꾼다.")]
    public float  searchObjectiveDistance = 2f;

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

        // 손잡이를 잡는 동안은 조작을 잠근다 — 돌리는 도중에 걸어 나가면 반복이 안 보인다.
        var ctrl = YarnDialogue.LockPlayer();
        var lu = FindLu();
        FaceLuTowardDoor(lu);
        HoldAtDoor();

        bool refusalNow = _attemptCount >= RefusalAttempt && !GameState.isDoorknobRefused;

        if (!refusalNow)
        {
            // 1~3회(그리고 거부 이후): 완전히 동일한 동작과 소리. 화면은 움직이지 않는다.
            if (doorknobAnimator != null && !string.IsNullOrEmpty(doorknobTurnTrigger))
                doorknobAnimator.SetTrigger(doorknobTurnTrigger);
            PlaySfxIfNamed(sfxKnobTurnName);
            yield return StartCoroutine(KnobTurnMotion(lu));
            yield return new WaitForSeconds(0.3f);

            YarnDialogue.UnlockPlayer(ctrl);
            _isBusy = false;
            yield break;
        }

        // 4회째 — 손잡이가 뜨거워진다.
        GameState.isDoorknobRefused = true;

        // 손잡이 도는 소리가 나지 않고, 대신 아주 낮은 저역음이 깔린다.
        PlaySfxIfNamed(sfxRefusalLowName);
        yield return StartCoroutine(KnobTurnMotion(lu));
        yield return new WaitForSeconds(0.5f);

        // 손을 뗀다. 화상 이펙트·붉은 표시 없음. 아파하지 않는다.
        if (doorknobAnimator != null && !string.IsNullOrEmpty(doorknobRecoilTrigger))
            doorknobAnimator.SetTrigger(doorknobRecoilTrigger);
        PlaySfxIfNamed(sfxHandReleaseName);
        yield return StartCoroutine(KnobRecoilMotion(lu));

        // 4번째에만 카메라가 문 반대쪽으로 아주 느리게 반 타일 스크롤한다(개정 D 문단 304). 기다리지 않는다.
        ScrollAwayFromDoor(lu);

        if (!string.IsNullOrEmpty(yarnNode_refused))
            yield return YarnDialogue.PlayAndWait(yarnNode_refused, false);

        // 목표 갱신. 첫 목표 「마당으로 나가세요」는 세라가 나간 직후(KitchenTriggerCutscene) 이미 떴다.
        ObjectiveManager.Instance?.ShowObjective(refusedObjectiveHeader, refusedObjectiveBody);

        YarnDialogue.UnlockPlayer(ctrl);
        _isBusy = false;

        StartCoroutine(ShowSearchObjectiveWhenLeaving());
    }

    /// <summary>거부 뒤 루가 현관에서 걸어 나가면 S#07 목표로 바꾼다. 한 번만.</summary>
    IEnumerator ShowSearchObjectiveWhenLeaving()
    {
        while (true)
        {
            if (_departed || _sealed) yield break;
            var lu = FindLu();
            if (lu != null && Vector2.Distance(lu.transform.position, KnobTarget.position) > searchObjectiveDistance)
                break;
            yield return null;
        }
        ObjectiveManager.Instance?.ShowObjective(searchObjectiveHeader, searchObjectiveBody);
    }

    // ─── S#06 카메라 · 임시 동작 ───────────────────────────────────────────

    bool      _heldAtDoor;
    Coroutine _doorWatch;

    static ClearSky.SimplePlayerController FindLu() =>
        PlayerStats.Instance != null
            ? PlayerStats.Instance.GetComponent<ClearSky.SimplePlayerController>()
            : Object.FindAnyObjectByType<ClearSky.SimplePlayerController>();

    Transform KnobTarget => knobCloseupTarget != null ? knobCloseupTarget : transform;

    /// <summary>손잡이를 잡는 동안 카메라를 그 자리에 고정한다. 루가 문을 떠나면 추적으로 돌아간다.</summary>
    void HoldAtDoor()
    {
        if (_heldAtDoor || CameraDirector.Instance == null) return;
        CameraDirector.Instance.Hold();
        _heldAtDoor = true;
        _doorWatch = StartCoroutine(WatchLuLeavingDoor());
    }

    void ReleaseDoorHold()
    {
        if (!_heldAtDoor) return;
        _heldAtDoor = false;
        if (_doorWatch != null) { StopCoroutine(_doorWatch); _doorWatch = null; }
        CameraDirector.Instance?.Track();
    }

    void ScrollAwayFromDoor(ClearSky.SimplePlayerController lu)
    {
        var cd = CameraDirector.Instance;
        var cam = Camera.main;
        if (cd == null || cam == null) return;
        Vector2 away = lu != null ? (Vector2)(lu.transform.position - KnobTarget.position) : Vector2.up;
        if (away.sqrMagnitude < 0.0001f) away = Vector2.up;
        Vector2 from = cam.transform.position;
        cd.ScrollTo(from + away.normalized * refusalScrollDistance, refusalScrollSpeed);
    }

    /// <summary>루가 문을 떠나면 고정을 풀고 추적으로 돌아간다. 다시 잡으면 다시 고정한다.</summary>
    IEnumerator WatchLuLeavingDoor()
    {
        while (_heldAtDoor)
        {
            var lu = FindLu();
            if (lu != null && !_isBusy &&
                Vector2.Distance(lu.transform.position, KnobTarget.position) > knobCloseupReleaseDistance)
            {
                _doorWatch = null;
                ReleaseDoorHold();
                yield break;
            }
            yield return null;
        }
    }

    /// <summary>루가 문 쪽을 보게 한다. 0=아래 1=옆 2=위, 옆은 localScale.x 부호만 뒤집는다(양수가 왼쪽).</summary>
    void FaceLuTowardDoor(ClearSky.SimplePlayerController lu)
    {
        if (lu == null) return;
        var anim = lu.GetComponent<Animator>();
        if (anim == null) return;

        Vector2 d = (Vector2)(KnobTarget.position - lu.transform.position);
        int dir = Mathf.Abs(d.x) > Mathf.Abs(d.y) ? 1 : (d.y > 0f ? 2 : 0);
        Vector3 s = lu.transform.localScale;
        s.x = dir == 1 ? Mathf.Abs(s.x) * (d.x > 0f ? -1f : 1f) : Mathf.Abs(s.x);
        lu.transform.localScale = s;
        anim.SetInteger("dir", dir);
        anim.SetBool("isRun", false);
    }

    /// <summary>손잡이를 돌리는 흔들림. 매번 완전히 같은 궤적이고, 끝나면 정확히 제자리로 돌아온다.</summary>
    IEnumerator KnobTurnMotion(ClearSky.SimplePlayerController lu)
    {
        if (doorknobAnimator != null || lu == null) { yield return new WaitForSeconds(0.3f); yield break; }

        var rb = lu.GetComponent<Rigidbody2D>();
        Vector2 home = rb != null ? rb.position : (Vector2)lu.transform.position;
        float[] steps = { 1f, 0f, -1f, 0f };
        foreach (float k in steps)
        {
            SetLuPosition(lu, rb, home + Vector2.right * (k * knobTurnJiggle));
            yield return new WaitForSeconds(0.075f);
        }
        SetLuPosition(lu, rb, home);
    }

    /// <summary>4번째 — 손을 떼며 문에서 반 발짝 물러난다. 빠르게, 한 번.</summary>
    IEnumerator KnobRecoilMotion(ClearSky.SimplePlayerController lu)
    {
        if (doorknobAnimator != null || lu == null) { yield return new WaitForSeconds(0.2f); yield break; }

        var rb = lu.GetComponent<Rigidbody2D>();
        Vector2 from = rb != null ? rb.position : (Vector2)lu.transform.position;
        Vector2 away = from - (Vector2)KnobTarget.position;
        if (away.sqrMagnitude < 0.0001f) away = Vector2.up;
        Vector2 to = from + away.normalized * knobRecoilDistance;

        const float duration = 0.12f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            SetLuPosition(lu, rb, Vector2.Lerp(from, to, t / duration));
            yield return null;
        }
        SetLuPosition(lu, rb, to);
    }

    static void SetLuPosition(ClearSky.SimplePlayerController lu, Rigidbody2D rb, Vector2 p)
    {
        if (rb != null) { rb.position = p; rb.linearVelocity = Vector2.zero; }
        else lu.transform.position = new Vector3(p.x, p.y, lu.transform.position.z);
    }

    // ─── S#13 ────────────────────────────────────────────────────────────
    IEnumerator DepartRoutine()
    {
        var ctrl = YarnDialogue.LockPlayer();

        // S#06 의 고정이 걸린 채면 먼저 푼다.
        ReleaseDoorHold();

        PlaySfxIfNamed(sfxKeyUnlockName);

        // 코트를 입은 루로 교체 — 정본은 다락방이 아니라 현관에서 코트를 입는다.
        // 소매가 손을 덮어 도자기 손가락이 가려지는 것이 이 스프라이트의 핵심이다.
        ApplyCoatedSprite();

        if (!string.IsNullOrEmpty(yarnNode_departure))
            yield return YarnDialogue.PlayAndWait(yarnNode_departure, false);

        // ── 현관문 통과 — 탈출 압박이 여기서 끝난다 (C-14-2-2 · F-6 「타이머·조임 정지 — 현관문 통과」) ──
        //
        // ⚠ 정문이 아니라 여기다. 「출구는 정문이지만 제한 시간은 현관문에서 끝난다」(F-6 문단 788).
        // ⚠ 문이 열리는 프레임에 전부 끈다. 페이드로 서서히 풀지 않는다 — 서서히 풀면 조임이
        //    실외까지 이어지는 것으로 읽힌다(C-14-2-2 문단 1064).
        // ⚠ 이 줄이 아래 objectsToEnable 보다 앞에 있어야 한다. 「문이 열릴 때 들어오는 빛이 이 씬의
        //    핵심」(D-S#13)인데 가장자리가 아직 어두우면 그 빛이 죽는다(F-6 문단 790).
        GameState.isFrontDoorPassed = true;
        HouseEscapePressureController.NotifyEscaped();

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
