using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 걸어서 드나드는 구역의 카메라 경계. 플레이어가 구역(이 오브젝트의 트리거) 안에 있는 동안 <see cref="bound"/> 사각형으로
/// 카메라를 제한하고, 어느 구역에도 없으면 씬 기본 경계(<see cref="CameraFollow.SceneBoundName"/>)로 돌아간다.
///
/// <para><b>마을</b> — 돌담 바깥 빈 공간이 화면에 들어오지 않게 한다. MapScene 의 씬 경계는 숲 폭까지 넓어서
/// 마을 서쪽 끝에 서면 화면의 1/3 이 담 밖이었다(2026-10-05 배치 실측 · 보호 구역 §2 사용자 승인).</para>
///
/// <para><b>숲문 접근</b> — 숲문은 남쪽 담에 뚫려 있어 마을 경계만 두면 문으로 내려가는 루가 화면 아래 끝에 걸린다.
/// 문 앞에서는 세로로 숲 쪽까지 내려다보게 하는 구역을 우선순위를 높여 겹쳐 둔다.</para>
///
/// <para>구역이 겹치면 <see cref="priority"/> 가 높은 쪽, 같으면 나중에 들어간 쪽이 이긴다.
/// <see cref="RoomTransfer"/> 와 달리 방 덮개 · <c>OnRoomEntered</c> 를 건드리지 않는다 — 카메라만 맡는다.
/// 경계를 넘을 때는 즉시 자르지 않고 미끄러져 들어간다(<see cref="CameraFollow.SetBound"/> 의 blend).</para>
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class CameraBoundZone : MonoBehaviour
{
    [Tooltip("이 구역에서 쓸 카메라 경계. 비우면 구역 트리거 자신의 사각형을 쓴다.")]
    public BoxCollider2D bound;
    [Tooltip("구역이 겹칠 때 높은 쪽이 이긴다.")]
    public int priority;

    BoxCollider2D _box;
    int _order;

    static readonly List<CameraBoundZone> _inside = new List<CameraBoundZone>();
    static int _enterCounter;

    BoxCollider2D Bound => bound != null ? bound : _box;

    void Awake()
    {
        _box = GetComponent<BoxCollider2D>();
        _box.isTrigger = true;
    }

    void OnDisable()
    {
        if (_inside.Remove(this)) Apply(blend: false);
    }

    void Start()
    {
        // 구역 안에서 씬이 시작되면 바로 건다. 씬 로드 때 CameraFollow 가 씬 경계로 되돌린 뒤에 돈다(RoomTransfer.Start 와 같은 순서).
        var player = GameObject.FindWithTag("Player");
        if (player == null) return;
        Vector3 pp = player.transform.position;
        if (!_box.bounds.Contains(new Vector3(pp.x, pp.y, _box.bounds.center.z))) return;
        Enter();
        Apply(blend: false, snap: true);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || other.isTrigger) return;
        if (_inside.Contains(this)) return;
        Enter();
        Apply(blend: true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || other.isTrigger) return;
        if (_inside.Remove(this)) Apply(blend: true);
    }

    void Enter()
    {
        _inside.RemoveAll(z => z == null);
        _order = ++_enterCounter;
        _inside.Add(this);
    }

    /// <summary>지금 들어가 있는 구역 중 이기는 쪽의 경계를 건다. 없으면 씬 경계.</summary>
    static void Apply(bool blend, bool snap = false)
    {
        var cf = CameraFollow.Instance;
        if (cf == null) return;
        _inside.RemoveAll(z => z == null);
        CameraBoundZone top = null;
        foreach (var z in _inside)
            if (top == null || z.priority > top.priority || (z.priority == top.priority && z._order > top._order)) top = z;
        var target = top != null ? top.Bound : null;
        if (target == cf.currentBound && !snap) return;
        cf.SetBound(target, snap: snap, blend: blend);
    }
}
