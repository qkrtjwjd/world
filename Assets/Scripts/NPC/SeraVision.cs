using System;
using System.Collections;
using UnityEngine;

/// <summary>세라의 시야 상태. 상태마다 시야의 모양이 다르다 (C-14-3-2).</summary>
public enum SeraVisionState
{
    /// <summary>이동 — 진행 방향으로 좁고 길다. 회전하지 않는다.</summary>
    Moving,
    /// <summary>점검 — 넓고 짧다. 느리게 회전한다.</summary>
    Inspecting,
    /// <summary>돌아봄 — 소리가 난 방향으로 고정된다. 좁고 길다.</summary>
    LookingBack,
}

/// <summary>
/// 세라의 시야 판정 (C-14-3-2 / 수치 F-6).
///
/// 시야는 상시 같은 모양이 아니라 <see cref="SeraVisionState"/> 셋으로 나뉜다.
/// 상태를 구분하지 않으면 일반적인 스텔스의 시야 콘이 되고, 세라가 지금 무엇을 하는 중인지를
/// 시야만으로 읽을 수 없게 된다.
///
/// 시야 안에 2초 머물러야 발각으로 판정한다 — 즉시 판정은 하지 않는다.
/// 발각 처리 자체는 <see cref="VillagePatrolController"/> 가 한다.
/// 이 컴포넌트는 "보였다"까지만 판정한다.
///
/// ⚠ 상태가 바뀔 때 각도와 거리를 <b>보간하지 않고 즉시 교체</b>한다 (F-6).
/// ⚠ 장애물 차단은 세 상태 <b>모두</b>에 적용한다 (F-6).
/// ⚠ 딱딱 소리 자체는 발각이 아니다. 소리 → 회전 → 돌아봄 유지 → 원복 순이며,
///   돌아봄 중에 시야 안에 들어가야 판정이 시작된다 (C-14-3-2).
/// ⚠ 시야는 UI 도형으로 그리지 않는다. 바닥에 깔리는 빛으로 표현한다 (C-14-3-2).
///   아트가 없으므로 지금은 기즈모만 그린다.
/// </summary>
public class SeraVision : MonoBehaviour
{
    /// <summary>
    /// 타일 하나의 월드 크기. F-6 이 시야 거리를 타일로 적으므로 여기서 월드 단위로 옮긴다.
    ///
    /// MapScene 의 Tilemap 은 전부 비어 있고(<c>m_Tiles: {}</c>) 마을은 스프라이트로 지어져 있다.
    /// 도트 에셋이 PPU 32 이고 씬 오브젝트가 0.5625 배로 놓여 있으므로 32px = 0.5625 월드유닛이다.
    /// 기존 <c>viewDistance 2.25</c> 가 「4타일」로 기록돼 있던 것과도 맞는다(유니티_수동작업 16-C).
    ///
    /// ⚠ CLAUDE.md §11 의 「타일 32×32 = 1 월드유닛」과는 어긋난다. 캐릭터 크기 0.5625 vs 1 의
    ///   미결 사항과 같은 뿌리이며, 그쪽이 정해지면 <b>이 상수 한 줄만</b> 바꾸면 된다.
    /// </summary>
    public const float TileUnit = 0.5625f;

    [Header("시야 — 이동 (F-6: 60도 · 6타일)")]
    [Tooltip("진행 방향으로 고정된다. 회전하지 않는다.")]
    public float movingAngle = 60f;
    public float movingTiles = 6f;

    [Header("시야 — 점검 (F-6: 90도 · 4타일)")]
    [Tooltip("넓고 짧다. 아래 주기로 느리게 회전한다.")]
    public float inspectAngle = 90f;
    public float inspectTiles = 4f;
    [Tooltip("점검 중 한 바퀴 도는 데 걸리는 시간(초). F-6 「12초에 1회전」.")]
    public float inspectRotationPeriod = 12f;

    [Header("시야 — 돌아봄 (F-6: 60도 · 6타일)")]
    [Tooltip("소리 방향으로 고정된다.")]
    public float lookBackAngle = 60f;
    public float lookBackTiles = 6f;
    [Tooltip("돌아본 자세를 유지하는 시간(초). F-6 「소리 방향 고정 · 3초 유지」.")]
    public float lookBackHold = 3f;
    [Tooltip("소리가 난 뒤 실제로 돌아보기까지의 지연(초). F-6 「딱딱 → 회전 지연 1초」.")]
    public float clickTurnDelay = 1f;

    [Header("발각")]
    [Tooltip("시야 안에 이 시간(초)만큼 머물러야 발각된다. 정본 2초.")]
    public float detectionTime = 2f;

    [Header("차단")]
    [Tooltip("시야를 막는 엄폐물 레이어 (수레·화분·간판). 세 상태 모두에 적용된다.")]
    public LayerMask obstacleMask;

    [Header("방향")]
    [Tooltip("기본 바라보는 방향. 딱딱 소리가 나면 그쪽으로 바뀐다.")]
    public Vector2 facing = Vector2.down;

    /// <summary>발각됐을 때 발행된다. 라운드 규칙에 따른 처리는 구독자가 한다.</summary>
    public static event Action OnPlayerSpotted;

    /// <summary>
    /// 지연이 끝나고 실제로 소리 쪽을 돌아본 순간 발행된다. 인자는 돌아본 방향.
    /// 스프라이트를 같은 쪽으로 돌리는 데 쓴다 — 시야와 그림이 따로 놀지 않게 하려는 것이다.
    /// </summary>
    public event Action<Vector2> OnTurnedToSound;

    /// <summary>현재 시야 안에 플레이어가 있는지. 1회차의 "그 자리를 벗어나면 넘어간다" 판정에 쓴다.</summary>
    public bool PlayerInSight { get; private set; }

    /// <summary>지금 어떤 시야인지. 바닥 빛 표시가 붙을 때 이 값으로 형태를 고른다.</summary>
    public SeraVisionState State { get; private set; } = SeraVisionState.Inspecting;

    /// <summary>현재 상태의 시야각(도).</summary>
    public float CurrentAngle => State switch
    {
        SeraVisionState.Moving      => movingAngle,
        SeraVisionState.LookingBack => lookBackAngle,
        _                           => inspectAngle,
    };

    /// <summary>현재 상태의 시야 거리(월드 유닛).</summary>
    public float CurrentDistance => TileUnit * (State switch
    {
        SeraVisionState.Moving      => movingTiles,
        SeraVisionState.LookingBack => lookBackTiles,
        _                           => inspectTiles,
    });

    float           _timeInSight;
    Transform       _player;
    SeraVisionState _stateBeforeLookBack = SeraVisionState.Inspecting;
    Coroutine       _lookBackRoutine;

    // ── 상태 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 시야 상태를 <b>즉시</b> 바꿉니다. 각도·거리를 보간하지 않습니다(F-6).
    /// 돌아봄 중에는 무시합니다 — 돌아봄이 끝나면 스스로 원래 상태로 돌아옵니다.
    /// </summary>
    public void SetState(SeraVisionState state)
    {
        if (State == SeraVisionState.LookingBack)
        {
            // 돌아봄이 끝났을 때 복귀할 상태만 갈아둔다.
            _stateBeforeLookBack = state;
            return;
        }
        if (State == state) return;

        State = state;
        // 시야가 바뀌면 누적을 새로 센다 — 새 시야에 들어온 순간부터 2초다.
        _timeInSight = 0f;
    }

    /// <summary>
    /// 딱딱 소리를 들었습니다. <see cref="clickTurnDelay"/> 뒤에 그 방향으로 돌아보고
    /// <see cref="lookBackHold"/> 동안 유지한 뒤 원래 상태로 돌아옵니다 (C-14-3-2).
    ///
    /// 소리 자체는 발각이 아니다. 돌아본 시야 안에 들어가야 판정이 시작된다.
    /// </summary>
    public void NoticeSound(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return;
        if (!isActiveAndEnabled) return;

        if (_lookBackRoutine != null) StopCoroutine(_lookBackRoutine);
        _lookBackRoutine = StartCoroutine(LookBackRoutine(direction.normalized));
    }

    IEnumerator LookBackRoutine(Vector2 direction)
    {
        if (State != SeraVisionState.LookingBack) _stateBeforeLookBack = State;

        if (clickTurnDelay > 0f) yield return new WaitForSeconds(clickTurnDelay);

        State        = SeraVisionState.LookingBack;
        facing       = direction;
        _timeInSight = 0f;   // 돌아본 순간부터 2초다
        OnTurnedToSound?.Invoke(direction);

        yield return new WaitForSeconds(Mathf.Max(0f, lookBackHold));

        State            = _stateBeforeLookBack;
        _timeInSight     = 0f;
        _lookBackRoutine = null;
    }

    // ── 판정 ─────────────────────────────────────────────────────────────────

    void Update()
    {
        // 점검 중에는 느리게 회전한다. 이동·돌아봄은 방향이 고정된다 (C-14-3-2).
        if (State == SeraVisionState.Inspecting && inspectRotationPeriod > 0f)
        {
            float degPerSec = 360f / inspectRotationPeriod;
            facing = (Vector2)(Quaternion.AngleAxis(degPerSec * Time.deltaTime, Vector3.forward)
                               * new Vector3(facing.x, facing.y, 0f));
        }

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
        if (distance > CurrentDistance) return false;

        Vector2 dir = distance > 0.0001f ? toPlayer / distance : facing;
        if (Vector2.Angle(facing, dir) > CurrentAngle * 0.5f) return false;

        // 엄폐물에 가리면 보이지 않는다. 세 상태 모두에 적용한다 (F-6).
        if (obstacleMask.value != 0)
        {
            var hit = Physics2D.Raycast(transform.position, dir, distance, obstacleMask);
            if (hit.collider != null) return false;
        }
        return true;
    }

    /// <summary>
    /// 바라보는 방향을 즉시 바꿉니다. 상태는 그대로입니다.
    /// 딱딱 소리 반응에는 이것 말고 <see cref="NoticeSound"/> 를 쓰세요 — 지연·유지·원복이 붙습니다.
    /// </summary>
    public void SetFacing(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return;
        facing = direction.normalized;
        _timeInSight = 0f;
    }

    void OnDrawGizmosSelected()
    {
        // 시야 부채꼴은 디버그 표시만 한다. 게임 화면 표시는 바닥 빛이며 필터별 아트가 나온 뒤 붙인다(F-6-1).
        Gizmos.color = State == SeraVisionState.LookingBack ? new Color(1f, 0.8f, 0.3f, 0.5f)
                                                            : new Color(1f, 0.4f, 0.4f, 0.5f);
        Vector3 origin = transform.position;
        float   angle  = Application.isPlaying ? CurrentAngle    : inspectAngle;
        float   dist   = Application.isPlaying ? CurrentDistance : inspectTiles * TileUnit;

        Quaternion left  = Quaternion.AngleAxis(-angle * 0.5f, Vector3.forward);
        Quaternion right = Quaternion.AngleAxis( angle * 0.5f, Vector3.forward);
        Vector3 f = new Vector3(facing.x, facing.y, 0f).normalized * dist;
        Gizmos.DrawLine(origin, origin + left  * f);
        Gizmos.DrawLine(origin, origin + right * f);
    }
}
