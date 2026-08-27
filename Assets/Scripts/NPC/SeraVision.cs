using System;
using UnityEngine;

/// <summary>
/// 세라의 시야 판정 (C-14-3-2 / 수치 F-6).
///
/// 전방 부채꼴 90도 · 4타일. 수레·화분·간판 뒤로 숨을 수 있다.
/// 시야 안에 2초 머물러야 발각으로 판정한다 — 즉시 판정은 하지 않는다.
///
/// 발각 처리 자체는 <see cref="VillagePatrolController"/> 가 한다.
/// 이 컴포넌트는 "보였다"까지만 판정한다.
/// </summary>
public class SeraVision : MonoBehaviour
{
    [Header("시야 (F-6 초안값)")]
    [Tooltip("부채꼴 전체 각도. 정본 90도.")]
    public float viewAngle = 90f;
    [Tooltip("시야 거리(유닛 = 타일). 정본 4타일.")]
    public float viewDistance = 2.25f;
    [Tooltip("시야 안에 이 시간(초)만큼 머물러야 발각된다. 정본 2초.")]
    public float detectionTime = 2f;

    [Header("차단")]
    [Tooltip("시야를 막는 엄폐물 레이어 (수레·화분·간판).")]
    public LayerMask obstacleMask;

    [Header("방향")]
    [Tooltip("기본 바라보는 방향. 딱딱 소리가 나면 그쪽으로 바뀐다.")]
    public Vector2 facing = Vector2.down;

    /// <summary>발각됐을 때 발행된다. 라운드 규칙에 따른 처리는 구독자가 한다.</summary>
    public static event Action OnPlayerSpotted;

    /// <summary>현재 시야 안에 플레이어가 있는지. 1회차의 "그 자리를 벗어나면 넘어간다" 판정에 쓴다.</summary>
    public bool PlayerInSight { get; private set; }

    float     _timeInSight;
    Transform _player;

    void Update()
    {
        if (_player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go == null) { PlayerInSight = false; _timeInSight = 0f; return; }
            _player = go.transform;
        }

        PlayerInSight = CanSeePlayer();

        if (!PlayerInSight)
        {
            _timeInSight = 0f;
            return;
        }

        _timeInSight += Time.deltaTime;
        if (_timeInSight >= detectionTime)
        {
            _timeInSight = 0f;
            OnPlayerSpotted?.Invoke();
        }
    }

    bool CanSeePlayer()
    {
        Vector2 toPlayer = (Vector2)(_player.position - transform.position);
        float   distance = toPlayer.magnitude;
        if (distance > viewDistance) return false;

        Vector2 dir = distance > 0.0001f ? toPlayer / distance : facing;
        if (Vector2.Angle(facing, dir) > viewAngle * 0.5f) return false;

        // 엄폐물에 가리면 보이지 않는다.
        if (obstacleMask.value != 0)
        {
            var hit = Physics2D.Raycast(transform.position, dir, distance, obstacleMask);
            if (hit.collider != null) return false;
        }
        return true;
    }

    /// <summary>바라보는 방향을 바꿉니다. 딱딱 소리에 반응할 때 호출됩니다.</summary>
    public void SetFacing(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return;
        facing = direction.normalized;
        // 방향이 바뀌면 누적을 새로 센다 — 돌아본 순간부터 2초다.
        _timeInSight = 0f;
    }

    void OnDrawGizmosSelected()
    {
        // 시야 부채꼴은 디버그 표시만 한다. 게임 화면 표시는 필터별 아트가 나온 뒤 붙인다(C-14-3-3).
        Gizmos.color = new Color(1f, 0.4f, 0.4f, 0.5f);
        Vector3 origin = transform.position;
        Quaternion left  = Quaternion.AngleAxis(-viewAngle * 0.5f, Vector3.forward);
        Quaternion right = Quaternion.AngleAxis( viewAngle * 0.5f, Vector3.forward);
        Vector3 f = new Vector3(facing.x, facing.y, 0f).normalized * viewDistance;
        Gizmos.DrawLine(origin, origin + left  * f);
        Gizmos.DrawLine(origin, origin + right * f);
    }
}
