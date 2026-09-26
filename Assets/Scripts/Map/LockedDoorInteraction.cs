using System.Collections;
using UnityEngine;

/// <summary>
/// S#6 다락방 문 잠금.
/// - AtticKey 미보유 시 잠겨있다는 대사를 출력한다.
/// - AtticKey 보유 시 문 오브젝트를 비활성화하고 다락방으로 이동한다.
/// InteractionTrigger.onInteract UnityEvent 에 OnAtticDoorInteract() 를 연결하세요.
/// </summary>
public class LockedDoorInteraction : MonoBehaviour
{
    [Header("잠금 해제에 필요한 아이템 이름 (ItemData.itemName 과 일치해야 함)")]
    public string requiredItemName = "다락방 열쇠";

    [Header("잠겨있을 때 Yarn 노드 이름")]
    public string yarnNode_locked;

    [Header("문 열릴 때 활성화할 오브젝트 (다락방 RoomTransfer 등)")]
    public GameObject[] objectsToEnable;

    [Header("문 열릴 때 비활성화할 오브젝트 (문 스프라이트, 이 콜라이더 등)")]
    public GameObject[] objectsToDisable;

    [Header("문 열린 후 플레이어가 이동할 다락방 위치")]
    public Transform targetLocation;

    private bool _unlocked = false;
    private bool _sealedByPressure = false;

    // ── 탈출 압박 중 잠금 (C-14-2-3) ────────────────────────────────────────
    void OnEnable()
    {
        HouseEscapePressureController.OnPressureBegan += SealForEscapePressure;
        HouseEscapePressureController.OnPressureEnded += ReleaseEscapePressureSeal;
    }

    void OnDisable()
    {
        HouseEscapePressureController.OnPressureBegan -= SealForEscapePressure;
        HouseEscapePressureController.OnPressureEnded -= ReleaseEscapePressureSeal;
    }

    /// <summary>
    /// 압박 발동과 동시에 다락방을 닫는다 (C-14-2-3).
    ///
    /// 되돌아갈 수 있게 두면 제한 시간 안에 이미 본 장면을 다시 지나는 경로가 생긴다.
    /// ⚠ <b>대사를 붙이지 않는다.</b> 잠겼다는 말도 하지 않고 그냥 열리지 않는다.
    /// </summary>
    void SealForEscapePressure()
    {
        if (_sealedByPressure) return;
        _sealedByPressure = true;   // 문 상호작용은 지금부터 막힌다(OnAtticDoorInteract)

        // ⚠ 2026-09-27: 압박은 S#11 라디오 끝에서 시작하는데, 그때 루는 **아직 다락방 안**이다.
        //   예전에는 여기서 곧바로 objectsToEnable(= `다락방` 방 전체)을 껐다. 상자에 붙은 S#11·S#12
        //   컷씬 코루틴이 같이 죽어 단검이 나오지 않고, 루는 빈 공간에서 조작이 잠긴 채 영원히 남았다.
        //   봉인의 목적은 「되돌아가지 못하게」다(C-14-2-3). 그래서 루가 그 영역을 벗어난 뒤에 닫는다.
        if (isActiveAndEnabled)
            _sealRoutine = StartCoroutine(SealWhenLuIsOutside());
        else
            ApplySeal();
    }

    Coroutine _sealRoutine;

    IEnumerator SealWhenLuIsOutside()
    {
        while (_sealedByPressure && LuIsInsideSealedArea())
            yield return null;
        _sealRoutine = null;
        if (_sealedByPressure) ApplySeal();
    }

    void ApplySeal()
    {
        // 열어 두었던 통로(다락방 RoomTransfer 등)를 닫고 문을 되돌린다.
        foreach (var obj in objectsToEnable)
            if (obj != null) obj.SetActive(false);
        foreach (var obj in objectsToDisable)
            if (obj != null) obj.SetActive(true);

        Dbg.Log("[탈출압박] 다락방 문 잠금 — 대사 없음 (C-14-2-3)");
    }

    /// <summary>
    /// 루가 봉인으로 꺼질 방 안에 있는가. 방의 범위는 그 안의 RoomTransfer.roomBound(카메라 경계)로 잰다.
    /// ⚠ 렌더러를 전부 합치면 안 된다 — `다락방` 밑에 아래층 계단까지 들어 있어 집 전체를 덮는다(2026-09-27 실측).
    /// </summary>
    bool LuIsInsideSealedArea()
    {
        var lu = PlayerStats.Instance != null
            ? PlayerStats.Instance.transform
            : Object.FindAnyObjectByType<ClearSky.SimplePlayerController>()?.transform;
        if (lu == null) return false;
        Vector2 p = lu.position;

        foreach (var obj in objectsToEnable)
        {
            if (obj == null || !obj.activeInHierarchy) continue;
            foreach (var room in obj.GetComponentsInChildren<RoomTransfer>())
            {
                if (room.roomBound == null) continue;
                var b = room.roomBound.bounds;
                b.Expand(1f);
                if (p.x >= b.min.x && p.x <= b.max.x && p.y >= b.min.y && p.y <= b.max.y) return true;
            }
        }
        return false;
    }

    /// <summary>압박이 풀리면(정문 통과) 잠금도 푼다. 실패 경로에서는 어차피 씬이 넘어간다.</summary>
    void ReleaseEscapePressureSeal()
    {
        if (!_sealedByPressure) return;
        _sealedByPressure = false;

        // 루가 아직 다락방에 있어 봉인이 미뤄진 채였다면 닫은 적이 없으니 되돌릴 것도 없다.
        if (_sealRoutine != null) { StopCoroutine(_sealRoutine); _sealRoutine = null; return; }

        if (!_unlocked) return;   // 애초에 열린 적이 없으면 되돌릴 것도 없다
        foreach (var obj in objectsToEnable)
            if (obj != null) obj.SetActive(true);
        foreach (var obj in objectsToDisable)
            if (obj != null) obj.SetActive(false);
    }

    /// <summary>InteractionTrigger.onInteract 에 연결.</summary>
    public void OnAtticDoorInteract()
    {
        // 압박 중에는 아무 일도 일어나지 않는다. 대사도 없다 (C-14-2-3).
        if (_sealedByPressure) return;

        if (_unlocked) return;

        var inv = InventoryManager.Instance
                  ?? Object.FindAnyObjectByType<InventoryManager>();

        if (inv == null) return;

        if (inv.HasItem(requiredItemName))
            UnlockAttic();
        else
            StartCoroutine(YarnDialogue.PlayAndWait(yarnNode_locked, lockPlayer: true));
    }

    void UnlockAttic()
    {
        _unlocked = true;

        foreach (var obj in objectsToEnable)
            if (obj != null) obj.SetActive(true);

        foreach (var obj in objectsToDisable)
            if (obj != null) obj.SetActive(false);

        var trigger = GetComponent<InteractionTrigger>();
        if (trigger != null) trigger.enabled = false;

        InteractionManager.Instance?.SetCooldown(1.5f);

        if (AtticDoorCutscene.Instance != null)
        {
            Transform playerRef = GetPlayerTransform();
            Transform dest      = targetLocation;
            RoomTransfer room   = targetLocation != null
                                  ? targetLocation.GetComponentInParent<RoomTransfer>()
                                  : null;

            StartCoroutine(AtticDoorCutscene.Instance.PlayCutscene(() =>
            {
                if (playerRef != null && dest != null)
                    playerRef.position = dest.position;

                if (room != null)
                {
                    room.EnterRoom();
                    CameraFollow.Instance?.SetBound(room.roomBound, snap: true);
                }
                else
                {
                    CameraFollow.Instance?.SetBound(null, snap: true);
                }
            }));
        }
        else
        {
            if (targetLocation == null) return;
            Transform playerRef = GetPlayerTransform();
            if (playerRef == null) return;

            Transform dest = targetLocation;
            RoomTransfer room = targetLocation.GetComponentInParent<RoomTransfer>();

            TransitionManager.Instance?.DoTransition(() =>
            {
                playerRef.position = dest.position;
                if (room != null)
                {
                    room.EnterRoom();
                    CameraFollow.Instance?.SetBound(room.roomBound, snap: true);
                }
                else
                {
                    CameraFollow.Instance?.SetBound(null, snap: true);
                }
            });
        }
    }

    static Transform GetPlayerTransform()
    {
        if (PlayerStats.Instance != null) return PlayerStats.Instance.transform;
        var p = GameObject.FindGameObjectWithTag("Player");
        return p != null ? p.transform : null;
    }
}
