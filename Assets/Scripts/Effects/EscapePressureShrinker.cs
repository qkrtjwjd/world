using UnityEngine;

/// <summary>
/// 탈출 압박 강도에 맞춰 오브젝트를 실제로 좁히거나 당깁니다.
/// C-14-2 의 "복도가 실제로 짧아지고 문틀이 조금씩 좁아진다" 를 담당합니다.
///
/// 문틀·벽·복도 끝 오브젝트에 붙이고 <see cref="pressedLocalPosition"/> ·
/// <see cref="pressedLocalScale"/> 을 눌린 상태로 맞춰두면 됩니다.
/// 압박이 해제되면 원래 값으로 되돌아갑니다.
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
