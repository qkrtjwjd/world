using System.Collections;
using UnityEngine;

/// <summary>
/// 맵 전용 카메라 연출 존. Collider2D(isTrigger)와 함께 사용.
/// 플레이어가 진입/퇴장할 때 CameraFollow/CameraDirector를 통해 카메라를 자동 제어한다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class MapCameraZone : MonoBehaviour
{
    public enum CamZoneType
    {
        // ── 이동 중 지속 (진입 시 적용, 퇴장 시 복귀) ──────────────
        TrackFollow,       // 기본 측면 트래킹
        TrackLag,          // 뒤처지는 슬로 트래킹
        TrackForward,      // 이동 방향으로 X 오프셋 (수풀 진입 등)

        // ── 접근 시 발동 ────────────────────────────────────────────
        TriggerZoom,       // 진입 시 줌인
        TriggerStop,       // 짧은 카메라 정지 후 복귀
        TriggerTilt,       // 기울기 연출 후 자동 복귀

        // ── 씬/방 전환 연출 ─────────────────────────────────────────
        TransitionFade,    // 페이드 아웃
        TransitionCut,     // 즉시 컷 전환
        TransitionPanDown, // 위→아래 팬
        TransitionPov,     // 1인칭 시점 전환
    }

    // ─── 공개 필드 ──────────────────────────────────────────────────

    public CamZoneType type;

    // Track
    [SerializeField] float trackSmoothTime     = 0.15f;
    [SerializeField] float trackLagTime        = 1.0f;
    [SerializeField] float lookAheadAmount     = 1.5f;
    [SerializeField] float lookAheadSmoothTime = 0.05f;

    // Trigger
    [SerializeField] float zoomAmount    = 2f;
    [SerializeField] float zoomDuration  = 0.5f;
    [SerializeField] bool  restoreOnExit = true;
    [SerializeField] float stopDuration  = 0.5f;
    [SerializeField] float tiltAngle     = 20f;
    [SerializeField] float tiltReturn    = 0.8f;
    [SerializeField] bool  fireOnce      = false;

    // Transition
    [SerializeField] float  fadeDuration = 0.4f;
    [SerializeField] string targetName   = "";
    [SerializeField] float  povAngle     = 0f;
    [SerializeField] float  panHeight    = 5f;
    [SerializeField] float  panSpeed     = 3f;

    // ─── 런타임 ─────────────────────────────────────────────────────

    private bool      _fired;
    private bool      _entered;         // Track형: 진입해서 카메라 상태를 저장했는지
    private float     _savedSmoothTime;
    private Coroutine _activeRoutine;

    // ─── 트리거 감지 ─────────────────────────────────────────────────

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (fireOnce && _fired) return;

        bool isTrack = type is CamZoneType.TrackFollow
                            or CamZoneType.TrackLag
                            or CamZoneType.TrackForward;
        if (!isTrack) _fired = true;

        HandleEnter();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        HandleExit();
    }

    // ─── 진입 처리 ───────────────────────────────────────────────────

    void HandleEnter()
    {
        var cam = CameraFollow.Instance;
        var cd  = CameraDirector.Instance;

        switch (type)
        {
            case CamZoneType.TrackFollow:
                if (cam == null) return;
                _savedSmoothTime = cam.smoothTime;
                _entered         = true;
                cam.smoothTime   = trackSmoothTime;
                break;

            case CamZoneType.TrackLag:
                if (cam == null) return;
                _savedSmoothTime = cam.smoothTime;
                _entered         = true;
                cam.smoothTime   = trackLagTime;
                break;

            case CamZoneType.TrackForward:
                if (_activeRoutine != null) StopCoroutine(_activeRoutine);
                _activeRoutine = StartCoroutine(DoTrackForward(true));
                break;

            case CamZoneType.TriggerZoom:
                cd?.TriggerCamZoomIn("", zoomAmount, zoomDuration);
                break;

            case CamZoneType.TriggerStop:
                if (_activeRoutine != null) StopCoroutine(_activeRoutine);
                _activeRoutine = StartCoroutine(DoStop());
                break;

            case CamZoneType.TriggerTilt:
                cd?.TriggerCamTilt(tiltAngle, tiltReturn);
                break;

            case CamZoneType.TransitionFade:
                cd?.TriggerCamFadeDown(fadeDuration);
                break;

            case CamZoneType.TransitionCut:
                if (!string.IsNullOrEmpty(targetName))
                    cd?.TriggerCamCut(targetName);
                break;

            case CamZoneType.TransitionPanDown:
                cd?.TriggerCamPanUp(-panHeight, panSpeed);
                break;

            case CamZoneType.TransitionPov:
                cd?.TriggerCamPov(targetName, povAngle);
                break;
        }
    }

    // ─── 퇴장 처리 ───────────────────────────────────────────────────

    void HandleExit()
    {
        var cam = CameraFollow.Instance;

        switch (type)
        {
            case CamZoneType.TrackFollow:
            case CamZoneType.TrackLag:
                if (cam != null && _entered) cam.smoothTime = _savedSmoothTime;
                _entered = false;
                _fired = false;
                break;

            case CamZoneType.TrackForward:
                if (_activeRoutine != null) StopCoroutine(_activeRoutine);
                _activeRoutine = StartCoroutine(DoTrackForward(false));
                _fired = false;
                break;

            case CamZoneType.TriggerZoom:
                if (restoreOnExit) CameraDirector.Instance?.RestoreDefault();
                if (!fireOnce) _fired = false;
                break;
        }
    }

    // ─── 코루틴 ──────────────────────────────────────────────────────

    IEnumerator DoTrackForward(bool entering)
    {
        var cam    = CameraFollow.Instance;
        var player = GameObject.FindWithTag("Player")?.transform;
        if (cam == null || player == null) yield break;

        if (entering)
        {
            _savedSmoothTime = cam.smoothTime;
            _entered         = true;
            cam.smoothTime   = lookAheadSmoothTime;

            while (true)
            {
                float facing = player.localScale.x >= 0 ? 1f : -1f;
                cam.charLookAheadOffset = Mathf.Lerp(
                    cam.charLookAheadOffset, lookAheadAmount * facing, Time.deltaTime * 5f);
                yield return null;
            }
        }
        else
        {
            if (_entered) cam.smoothTime = _savedSmoothTime;
            _entered = false;
            float elapsed = 0f, start = cam.charLookAheadOffset;
            while (elapsed < 0.3f)
            {
                elapsed += Time.deltaTime;
                cam.charLookAheadOffset = Mathf.Lerp(start, 0f, elapsed / 0.3f);
                yield return null;
            }
            cam.charLookAheadOffset = 0f;
        }
    }

    IEnumerator DoStop()
    {
        var cd = CameraDirector.Instance;
        if (cd == null) yield break;
        cd.TriggerCamStatic();
        yield return new WaitForSeconds(stopDuration);
        cd.RestoreDefault();
    }

    void OnDisable()
    {
        if (_activeRoutine != null)
        {
            StopCoroutine(_activeRoutine);
            _activeRoutine = null;
        }

        // Track 타입이 비활성화되면 카메라 상태 복귀
        // (_entered 체크: 진입한 적 없는 존이 초기값 0으로 smoothTime을 덮어쓰는 것 방지)
        if (!_entered) return;
        _entered = false;

        if (type is CamZoneType.TrackFollow or CamZoneType.TrackLag)
        {
            var cam = CameraFollow.Instance;
            if (cam != null) cam.smoothTime = _savedSmoothTime;
        }
        else if (type == CamZoneType.TrackForward)
        {
            var cam = CameraFollow.Instance;
            if (cam != null)
            {
                cam.smoothTime          = _savedSmoothTime;
                cam.charLookAheadOffset = 0f;
            }
        }
    }
}
