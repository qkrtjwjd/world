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
    public string requiredItemName = "AtticKey";

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
        _sealedByPressure = true;

        // 열어 두었던 통로(다락방 RoomTransfer 등)를 닫고 문을 되돌린다.
        foreach (var obj in objectsToEnable)
            if (obj != null) obj.SetActive(false);
        foreach (var obj in objectsToDisable)
            if (obj != null) obj.SetActive(true);

        Dbg.Log("[탈출압박] 다락방 문 잠금 — 대사 없음 (C-14-2-3)");
    }

    /// <summary>압박이 풀리면(정문 통과) 잠금도 푼다. 실패 경로에서는 어차피 씬이 넘어간다.</summary>
    void ReleaseEscapePressureSeal()
    {
        if (!_sealedByPressure) return;
        _sealedByPressure = false;

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
