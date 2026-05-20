using System.Collections;
using UnityEngine;

[RequireComponent(typeof(InteractionTrigger))]
public class TeleportInteraction : MonoBehaviour
{
    [Header("설정")]
    [Tooltip("플레이어가 이동할 목적지 위치")]
    public Transform targetLocation;

    [Header("야간 시퀀스 차단")]
    [Tooltip("체크하면 야간 시퀀스 미완료 시 이동을 차단합니다.")]
    public bool blockDuringNightSequence = false;
    [Tooltip("차단 시 재생할 Yarn 노드 이름 (비워두면 대사 없이 막기만 함)")]
    public string yarnNode_nightBlocked;

    void Awake()
    {
        GetComponent<InteractionTrigger>().onInteract.AddListener(Teleport);
    }

    public void Teleport()
    {
        if (blockDuringNightSequence && !GameState.isNightSequenceWatched)
        {
            if (!string.IsNullOrEmpty(yarnNode_nightBlocked))
                StartCoroutine(YarnDialogue.PlayAndWait(yarnNode_nightBlocked, lockPlayer: true));
            return;
        }

        if (targetLocation == null)
        {
            Debug.LogError("[TeleportInteraction] Target Location 이 비어있습니다!");
            return;
        }

        Transform player = GetPlayerTransform();
        if (player == null)
        {
            Debug.LogError("[TeleportInteraction] 플레이어를 찾을 수 없습니다. (Player 태그 확인)");
            return;
        }

        // 페이드 전환 후 이동
        Transform playerRef = player;
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

        InteractionManager.Instance?.SetCooldown(1.5f);
    }

    // ─────────────────────────────────────────────
    static Transform GetPlayerTransform()
    {
        if (PlayerStats.Instance != null) return PlayerStats.Instance.transform;
        var p = GameObject.FindGameObjectWithTag("Player");
        return p != null ? p.transform : null;
    }
}