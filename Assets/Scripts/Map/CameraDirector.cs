using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;
using Yarn.Unity;

public class CameraDirector : MonoBehaviour
{
    public static CameraDirector Instance { get; private set; }

    private const int DEFAULT_PRIORITY = 10;
    private const int SHOT_PRIORITY    = 20;

    private readonly Dictionary<string, CinemachineCamera> _shots = new();
    private Coroutine _activeRoutine;

    private Transform _origTarget;
    private float     _origOrthoSize;
    private Vector3   _origDamping;
    private Vector3   _origOffset;
    private bool      _stateSaved;
    private float     _origTiltAngle;
    private float     _origTimeScale = 1f;
    private GameObject _staticTargetGo;

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);
    }

    // ─── VCam 등록/해제 ──────────────────────────────────────────────

    public void RegisterVCam(string shotName, CinemachineCamera vcam)
    {
        _shots[shotName] = vcam;
        vcam.Priority = 0;
    }

    public void UnregisterVCam(string shotName)
    {
        if (_shots.TryGetValue(shotName, out var vcam))
            vcam.Priority = 0;
        _shots.Remove(shotName);
    }

    public void ClearVCams()
    {
        foreach (var vcam in _shots.Values)
            vcam.Priority = 0;
        _shots.Clear();
    }

    // ─── 등록된 VCam으로 전환 ────────────────────────────────────────

    public void TriggerShot(string shotName)
    {
        if (!_shots.TryGetValue(shotName, out var vcam)) return;
        var cam = CameraFollow.Instance;
        if (cam == null) return;
        SaveState();
        cam.SetFollowPriority(0);
        vcam.Priority = SHOT_PRIORITY;
    }

    // ─── 1. CloseUp ─────────────────────────────────────────────────

    public void TriggerCloseUp(Transform target, float duration, float zoomAmount = 2f)
        => RunExclusive(DoCloseUp(target, duration, zoomAmount));

    IEnumerator DoCloseUp(Transform target, float duration, float zoomAmount)
    {
        var cam = CameraFollow.Instance;
        if (cam == null) yield break;

        SaveState();
        float zoomTarget = Mathf.Max(cam.currentOrthoSize - zoomAmount, 1f);
        if (target != null) cam.SetTarget(target);
        cam.ZoomTo(zoomTarget, 0.35f);

        yield return new WaitForSeconds(duration);

        RestoreState(cam);
    }

    // ─── 2. CutTo ───────────────────────────────────────────────────

    public void TriggerCutTo(Transform target)
    {
        var cam = CameraFollow.Instance;
        if (cam == null || target == null) return;
        SaveState();
        cam.SetTarget(target);
        cam.SnapToTarget();
    }

    // ─── 3. PanTo ───────────────────────────────────────────────────

    public void TriggerPanTo(Transform target, float speed = 3f)
        => RunExclusive(DoPanTo(target, speed));

    IEnumerator DoPanTo(Transform target, float speed)
    {
        var cam = CameraFollow.Instance;
        if (cam == null || target == null) yield break;

        SaveState();
        cam.smoothTime = 1f / Mathf.Max(speed, 0.1f);
        cam.SetTarget(target);

        float timeout = 5f;
        while (timeout > 0f &&
               Vector2.Distance(cam.transform.position, target.position) > 0.15f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
    }

    // ─── 4. PanUp ───────────────────────────────────────────────────

    public void TriggerPanUp(float height, float speed = 2f)
        => RunExclusive(DoPanUp(height, speed));

    IEnumerator DoPanUp(float height, float speed)
    {
        var cam = CameraFollow.Instance;
        if (cam == null) yield break;

        SaveState();
        float startOffset = cam.charHeightOffset;
        float endOffset   = startOffset + height;
        float duration    = Mathf.Abs(height) / Mathf.Max(speed, 0.01f);
        float elapsed     = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            cam.charHeightOffset = Mathf.Lerp(startOffset, endOffset, elapsed / duration);
            yield return null;
        }
        cam.charHeightOffset = endOffset;
    }

    // ─── 5. SlowFollow ──────────────────────────────────────────────

    public void TriggerSlowFollow(float lag)
    {
        var cam = CameraFollow.Instance;
        if (cam == null) return;
        SaveState();
        cam.smoothTime = lag;
    }

    // ─── 6. Shake ───────────────────────────────────────────────────

    public void TriggerShake(float intensity, float duration)
        => RunExclusive(DoShake(intensity, duration));

    IEnumerator DoShake(float intensity, float duration)
    {
        var cam = CameraFollow.Instance;
        if (cam == null) yield break;

        var noise = cam.GetNoise();
        if (noise != null)
        {
            // Cinemachine noise 기반 쉐이크
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                noise.AmplitudeGain = intensity * (1f - elapsed / duration);
                yield return null;
            }
            noise.AmplitudeGain = 0f;
        }
        else
        {
            // fallback: shakeOffset 직접 조작 (CameraFollow.LateUpdate에서 적용)
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float fade = 1f - elapsed / duration;
                cam.shakeOffset = new Vector3(
                    Random.Range(-1f, 1f) * intensity * fade,
                    Random.Range(-1f, 1f) * intensity * fade, 0f);
                yield return null;
            }
            cam.shakeOffset = Vector3.zero;
        }
    }

    // ─── 상태 저장/복귀 ─────────────────────────────────────────────

    public void RestoreDefault()
    {
        var cam = CameraFollow.Instance;
        if (cam != null) RestoreState(cam);
    }

    void SaveState()
    {
        if (_stateSaved) return;
        var cam = CameraFollow.Instance;
        if (cam == null) return;

        _origTarget    = cam.target;
        _origOrthoSize = cam.currentOrthoSize;
        _origDamping   = new Vector3(cam.smoothTime, cam.smoothTime, 0f);
        _origOffset    = new Vector3(0f, cam.charHeightOffset, 0f);
        _origTiltAngle = cam.tiltAngle;
        _origTimeScale = Time.timeScale;
        _stateSaved    = true;
    }

    void RestoreState(CameraFollow cam)
    {
        if (_origTarget != null) cam.SetTarget(_origTarget);
        cam.ZoomTo(_origOrthoSize, 0.4f);
        cam.smoothTime       = _origDamping.x;
        cam.charHeightOffset = _origOffset.y;

        // Shot VCam 비활성화, follow VCam 복귀
        foreach (var vcam in _shots.Values)
            vcam.Priority = 0;
        cam.SetFollowPriority(DEFAULT_PRIORITY);

        var noise = cam.GetNoise();
        if (noise != null) noise.AmplitudeGain = 0f;
        cam.shakeOffset = Vector3.zero;
        cam.tiltAngle   = 0f;
        Time.timeScale  = _origTimeScale;

        if (_staticTargetGo != null)
        {
            Destroy(_staticTargetGo);
            _staticTargetGo = null;
        }

        _stateSaved = false;
    }

    void RunExclusive(IEnumerator routine)
    {
        if (_activeRoutine != null) StopCoroutine(_activeRoutine);
        _activeRoutine = StartCoroutine(routine);
    }

    // ─── 7. ZoomIn ──────────────────────────────────────────────────

    public void TriggerCamZoomIn(string targetName, float zoomAmount, float duration)
    {
        var cam = CameraFollow.Instance;
        if (cam == null) return;
        SaveState();
        Transform t = GameObject.Find(targetName)?.transform ?? cam.target;
        if (t != null) cam.SetTarget(t);
        cam.ZoomTo(Mathf.Max(cam.currentOrthoSize - zoomAmount, 1f), duration);
    }

    // ─── 8. ZoomOut ─────────────────────────────────────────────────

    public void TriggerCamZoomOut(float zoomAmount, float duration)
    {
        var cam = CameraFollow.Instance;
        if (cam == null) return;
        SaveState();
        cam.ZoomTo(cam.currentOrthoSize + zoomAmount, duration);
    }

    // ─── 9. CamCut ──────────────────────────────────────────────────

    public void TriggerCamCut(string targetName)
    {
        var cam = CameraFollow.Instance;
        if (cam == null) return;
        Transform t = GameObject.Find(targetName)?.transform;
        if (t == null) return;
        SaveState();
        cam.SetTarget(t);
        cam.SnapToTarget();
    }

    // ─── 10. CamPan (시작→끝) ────────────────────────────────────────

    public void TriggerCamPan(string fromName, string toName, float speed)
        => StartCoroutine(DoCamPan(fromName, toName, speed));

    IEnumerator DoCamPan(string fromName, string toName, float speed)
    {
        var cam = CameraFollow.Instance;
        if (cam == null) yield break;
        Transform from = GameObject.Find(fromName)?.transform;
        Transform to   = GameObject.Find(toName)?.transform;
        if (from == null || to == null) yield break;

        SaveState();
        cam.SetTarget(from);
        cam.SnapToTarget();
        yield return null;

        cam.smoothTime = 1f / Mathf.Max(speed, 0.1f);
        cam.SetTarget(to);
    }

    // ─── 11. CamPanUp ───────────────────────────────────────────────

    public void TriggerCamPanUp(float height, float speed)
        => StartCoroutine(DoCamPanUp(height, speed));

    IEnumerator DoCamPanUp(float height, float speed)
    {
        var cam = CameraFollow.Instance;
        if (cam == null) yield break;

        SaveState();
        float startOffset = cam.charHeightOffset;
        float endOffset   = startOffset + height;
        float duration    = Mathf.Abs(height) / Mathf.Max(speed, 0.01f);
        float elapsed     = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            cam.charHeightOffset = Mathf.Lerp(startOffset, endOffset, elapsed / duration);
            yield return null;
        }
        cam.charHeightOffset = endOffset;
    }

    // ─── 12. CamPOV ─────────────────────────────────────────────────

    public void TriggerCamPov(string targetName, float rotationAngle)
    {
        var cam = CameraFollow.Instance;
        if (cam == null) return;
        SaveState();
        Transform t = GameObject.Find(targetName)?.transform;
        if (t != null) cam.SetTarget(t);
        cam.tiltAngle = rotationAngle;
    }

    // ─── 13. CamStatic ──────────────────────────────────────────────

    public void TriggerCamStatic()
    {
        var cam = CameraFollow.Instance;
        if (cam == null) return;
        SaveState();

        if (_staticTargetGo != null) Destroy(_staticTargetGo);
        _staticTargetGo = new GameObject("_CamStaticAnchor");
        _staticTargetGo.transform.position = cam.transform.position;
        DontDestroyOnLoad(_staticTargetGo);

        cam.SetTarget(_staticTargetGo.transform);
        cam.SnapToTarget();
    }

    // ─── 14. CamTilt ────────────────────────────────────────────────

    public void TriggerCamTilt(float angle, float returnTime)
        => StartCoroutine(DoCamTilt(angle, returnTime));

    IEnumerator DoCamTilt(float angle, float returnTime)
    {
        var cam = CameraFollow.Instance;
        if (cam == null) yield break;

        cam.tiltAngle = angle;
        yield return new WaitForSeconds(returnTime);

        float elapsed    = 0f;
        float retDuration = 0.3f;
        float startAngle = cam.tiltAngle;
        while (elapsed < retDuration)
        {
            elapsed += Time.deltaTime;
            cam.tiltAngle = Mathf.Lerp(startAngle, 0f, elapsed / retDuration);
            yield return null;
        }
        cam.tiltAngle = 0f;
    }

    // ─── 15. CamSlowmo ──────────────────────────────────────────────

    public void TriggerCamSlowmo(float timeScale, float duration)
        => StartCoroutine(DoCamSlowmo(timeScale, duration));

    IEnumerator DoCamSlowmo(float timeScale, float duration)
    {
        SaveState();
        Time.timeScale = Mathf.Clamp(timeScale, 0.01f, 1f);
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = _origTimeScale;
    }

    // ─── 16. CamShake (cam_ 접두어) ─────────────────────────────────

    public void TriggerCamShake(float intensity, float duration)
        => StartCoroutine(DoShake(intensity, duration));

    // ─── 17. CamFadeDown ────────────────────────────────────────────

    public void TriggerCamFadeDown(float duration)
        => StartCoroutine(DoCamFadeDown(duration));

    IEnumerator DoCamFadeDown(float duration)
    {
        var tm = TransitionManager.Instance;
        if (tm == null) yield break;
        yield return StartCoroutine(tm.FadeToBlack(duration));
    }

    // ─── Yarn Commands ───────────────────────────────────────────────
    // Yarn Spinner 3.x: 인스턴스 [YarnCommand]는 첫 인자를 GameObject 이름으로
    // 해석하므로 static + Instance 패턴을 사용한다 (YarnCommandBridge와 동일 규약).

    // <<camera_closeup "오브젝트명" 시간>>
    // <<camera_closeup "오브젝트명" 시간 줌량>>
    [YarnCommand("camera_closeup")]
    public static void YarnCloseUp(string objectName, float duration, float zoomAmount = 2f)
    {
        if (Instance == null) return;
        Transform t = GameObject.Find(objectName)?.transform
                   ?? CameraFollow.Instance?.target;
        Instance.TriggerCloseUp(t, duration, zoomAmount);
    }

    // <<camera_cut "오브젝트명">>
    [YarnCommand("camera_cut")]
    public static void YarnCutTo(string objectName)
    {
        if (Instance == null) return;
        Transform t = GameObject.Find(objectName)?.transform;
        if (t != null) Instance.TriggerCutTo(t);
    }

    // <<camera_pan "오브젝트명" 속도>>
    [YarnCommand("camera_pan")]
    public static void YarnPanTo(string objectName, float speed = 3f)
    {
        if (Instance == null) return;
        Transform t = GameObject.Find(objectName)?.transform;
        if (t != null) Instance.TriggerPanTo(t, speed);
    }

    // <<camera_pan_up 높이 속도>>
    [YarnCommand("camera_pan_up")]
    public static void YarnPanUp(float height, float speed = 2f) =>
        Instance?.TriggerPanUp(height, speed);

    // <<camera_shake 강도 시간>>
    [YarnCommand("camera_shake")]
    public static void YarnShake(float intensity, float duration) =>
        Instance?.TriggerShake(intensity, duration);

    // <<camera_restore>>
    [YarnCommand("camera_restore")]
    public static void YarnRestore() => Instance?.RestoreDefault();

    // <<camera_shot "샷이름">>
    [YarnCommand("camera_shot")]
    public static void YarnShot(string shotName) => Instance?.TriggerShot(shotName);

    // ─── cam_* 커맨드 ────────────────────────────────────────────────

    // <<cam_zoom_in "타겟" 줌량 지속시간>>
    [YarnCommand("cam_zoom_in")]
    public static void YarnCamZoomIn(string targetName, float zoomAmount, float duration)
        => Instance?.TriggerCamZoomIn(targetName, zoomAmount, duration);

    // <<cam_zoom_out 줌량 지속시간>>
    [YarnCommand("cam_zoom_out")]
    public static void YarnCamZoomOut(float zoomAmount, float duration)
        => Instance?.TriggerCamZoomOut(zoomAmount, duration);

    // <<cam_cut "타겟">>
    [YarnCommand("cam_cut")]
    public static void YarnCamCut(string targetName)
        => Instance?.TriggerCamCut(targetName);

    // <<cam_pan "시작타겟" "끝타겟" 속도>>
    [YarnCommand("cam_pan")]
    public static void YarnCamPan(string fromName, string toName, float speed)
        => Instance?.TriggerCamPan(fromName, toName, speed);

    // <<cam_pan_up 높이 속도>>
    [YarnCommand("cam_pan_up")]
    public static void YarnCamPanUp(float height, float speed = 2f)
        => Instance?.TriggerCamPanUp(height, speed);

    // <<cam_pov "타겟" 회전각도>>
    [YarnCommand("cam_pov")]
    public static void YarnCamPov(string targetName, float rotationAngle)
        => Instance?.TriggerCamPov(targetName, rotationAngle);

    // <<cam_static>>
    [YarnCommand("cam_static")]
    public static void YarnCamStatic()
        => Instance?.TriggerCamStatic();

    // <<cam_tilt 각도 복귀시간>>
    [YarnCommand("cam_tilt")]
    public static void YarnCamTilt(float angle, float returnTime)
        => Instance?.TriggerCamTilt(angle, returnTime);

    // <<cam_slowmo 배속 지속시간>>
    [YarnCommand("cam_slowmo")]
    public static void YarnCamSlowmo(float timeScale, float duration)
        => Instance?.TriggerCamSlowmo(timeScale, duration);

    // <<cam_shake 강도 지속시간>>
    [YarnCommand("cam_shake")]
    public static void YarnCamShake(float intensity, float duration)
        => Instance?.TriggerCamShake(intensity, duration);

    // <<cam_fade_down 지속시간>>
    [YarnCommand("cam_fade_down")]
    public static void YarnCamFadeDown(float duration)
        => Instance?.TriggerCamFadeDown(duration);
}
