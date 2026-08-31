using UnityEngine;

/// <summary>
/// 탈출 압박 단계에 맞춰 오브젝트를 좁히거나 당깁니다.
/// C-14-2-2 의 복도 축소·문틀 좁아짐을 담당합니다.
///
/// 문틀·벽·복도 끝 오브젝트에 붙이고 <see cref="pressedLocalPosition"/> ·
/// <see cref="pressedLocalScale"/> 을 눌린 상태로 맞춰두면 됩니다.
/// 압박이 해제되면 원래 값으로 되돌아갑니다.
///
/// ⚠ <b>눌린 상태는 4차 기준으로 만든다.</b> 들어오는 값 1 이 F-6 의 4차(−14%)에 해당하고,
///   3차(−8%)는 그 비율만큼만 적용된다. 3차 값을 기준으로 만들면 4차가 두 배로 눌린다.
///
/// ⚠ <b>이 연출은 시각 처리로만 끝나야 한다 (C-14-2-2).</b> 콜라이더와 이동 가능 범위는
///   바뀌면 안 된다 — 실제로 좁히면 통행 불가 구간이 생기고, 그것은 제한 시간이 아니라 벽이 된다.
///   그런데 <see cref="scaleObject"/> 로 Transform 을 줄이면 같은 계층의 Collider2D 도 함께 줄어든다.
///   그래서 <b>콜라이더가 없는 표시 전용 오브젝트에만 붙인다.</b> 콜라이더가 딸린 벽을 좁히고 싶으면
///   자식으로 스프라이트만 분리해 그쪽에 붙인다. 지키지 못하면 Awake 에서 경고가 뜬다.
///
/// ※ 카메라 줌(CameraFollow.ZoomTo)을 쓰지 않는 이유 — 그 API 는 RoomTransfer(방 이동)와
///   CameraDirector(camera_closeup 연출)가 이미 쓰고 있어, 압박이 끼어들면 방 이동 때마다
///   카메라 크기를 서로 덮어쓴다. 공간 압박은 카메라가 아니라 오브젝트를 움직여 만든다.
/// </summary>
public class EscapePressureShrinker : MonoBehaviour
{
    [Header("압박 100% 일 때의 상태 (기준: 현재 로컬 값)")]
    [Tooltip("체크하면 위치를 보간한다.")]
    public bool  movePosition = false;
    public Vector3 pressedLocalPosition;

    [Tooltip("체크하면 크기를 보간한다. 문틀을 좁힐 때 x 만 줄이는 식으로 쓴다.")]
    public bool  scaleObject = true;
    public Vector3 pressedLocalScale = Vector3.one;

    [Header("보간")]
    [Tooltip("압박 강도에 적용할 곡선. 비워두면 선형.")]
    public AnimationCurve response = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    Vector3 _basePosition;
    Vector3 _baseScale;

    void Awake()
    {
        _basePosition = transform.localPosition;
        _baseScale    = transform.localScale;

        // 인스펙터 기본값이 비어 있으면 현재 값을 그대로 눌린 상태로 둔다(= 아무 변화 없음).
        if (pressedLocalPosition == Vector3.zero && !movePosition) pressedLocalPosition = _basePosition;

        WarnIfCollidersWouldMove();
    }

    /// <summary>
    /// 콜라이더가 함께 움직이면 C-14-2-2 위반이다. 배치 실수는 화면으로 보이지 않으므로
    /// (플레이어가 벽에 끼어야 알게 된다) 여기서 잡아 로그로 남긴다.
    /// </summary>
    void WarnIfCollidersWouldMove()
    {
        if (!scaleObject && !movePosition) return;

        var colliders = GetComponentsInChildren<Collider2D>(includeInactive: true);
        if (colliders.Length == 0) return;

        Debug.LogWarning(
            $"[탈출압박] '{name}' 에 EscapePressureShrinker 가 붙었는데 Collider2D 가 " +
            $"{colliders.Length}개 딸려 있습니다. 압박이 콜라이더까지 움직여 통행 판정이 바뀝니다 — " +
            "C-14-2-2 는 시각 처리만 허용합니다. 스프라이트를 자식으로 분리해 그쪽에 붙이세요.", this);
    }

    void OnEnable()  => HouseEscapePressureController.OnLevelChanged += Apply;
    void OnDisable() => HouseEscapePressureController.OnLevelChanged -= Apply;

    void Apply(float level)
    {
        float t = response != null ? response.Evaluate(Mathf.Clamp01(level)) : Mathf.Clamp01(level);

        if (movePosition) transform.localPosition = Vector3.Lerp(_basePosition, pressedLocalPosition, t);
        if (scaleObject)  transform.localScale    = Vector3.Lerp(_baseScale,    pressedLocalScale,    t);
    }
}
