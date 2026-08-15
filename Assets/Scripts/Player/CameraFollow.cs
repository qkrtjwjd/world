using System.Collections;
using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.Rendering.Universal;

// CinemachineBrain(보통 ExecutionOrder 100)보다 늦게 LateUpdate를 실행하기 위해
[DefaultExecutionOrder(1000)]
public class CameraFollow : MonoBehaviour
{
    [Header("Cinemachine")]
    [SerializeField] CinemachineCamera followVCam;
    [SerializeField] CinemachineConfiner2D confiner;

    [Header("Zoom")]
    public float defaultOrthoSize = 5f;

    [Header("Shake")]
    [HideInInspector] public Vector3 shakeOffset;
    [HideInInspector] public float tiltAngle;

    [Header("Viewport")]
    [Tooltip("0~1 범위 Rect. 기본값 (0,0,1,1)은 전체 화면. 나머지 영역은 outsideColor로 채워짐.")]
    public Rect viewportRect = new Rect(0f, 0f, 1f, 1f);

    [Header("Background")]
    [Tooltip("카메라 바운드 밖 영역에 표시할 배경색")]
    public Color outsideColor = Color.black;

    public static CameraFollow Instance;

    private Camera _cam;
    private Camera _bgCam;
    private CinemachineFollow _follow;
    private Coroutine _zoomCoroutine;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(gameObject); return; }

        _cam = GetComponent<Camera>();
        if (_cam == null) Debug.LogError("[CameraFollow] Camera 컴포넌트가 없습니다!");

        if (followVCam != null)
            _follow = followVCam.GetComponent<CinemachineFollow>();

        SetupBackgroundCamera();
    }

    void Start()
    {
        if (followVCam == null) return;

        if (followVCam.Follow == null)
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null) followVCam.Follow = player.transform;
            else { StartCoroutine(RetryFindPlayer()); return; }
        }

        SnapCameraToFollow();
    }

    // 물리 카메라 직접 이동 + Cinemachine 내부 상태 동기화로 catch-up 없이 즉시 스냅
    public void SnapCameraToFollow()
    {
        if (followVCam == null || followVCam.Follow == null) return;
        Vector3 targetPos = followVCam.Follow.position;
        Vector3 snapPos   = new Vector3(targetPos.x, targetPos.y, transform.position.z);
        transform.position = snapPos;
        followVCam.ForceCameraPosition(snapPos, transform.rotation);
        SnapToTarget();
    }

    IEnumerator RetryFindPlayer()
    {
        for (int i = 0; i < 10; i++)
        {
            yield return null;
            if (followVCam == null) yield break;
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                followVCam.Follow = player.transform;
                SnapCameraToFollow();
                yield break;
            }
        }
        Debug.LogWarning("[CameraFollow] Player 태그를 가진 오브젝트를 찾지 못했습니다.");
    }

    // Cinemachine이 LateUpdate에서 카메라 위치를 설정한 뒤 shakeOffset을 덧붙임.
    // Script Execution Order에서 CameraFollow를 CinemachineBrain보다 늦게 실행해야 합니다.
    void LateUpdate()
    {
        bool shakeOn = SettingsManager.Instance?.cameraShakeEnabled ?? true;
        if (shakeOn && shakeOffset != Vector3.zero)
            transform.position += shakeOffset;

        if (tiltAngle != 0f)
        {
            var rot = transform.rotation.eulerAngles;
            rot.z = tiltAngle;
            transform.rotation = Quaternion.Euler(rot);
        }

        if (_bgCam != null && _bgCam.backgroundColor != outsideColor)
            _bgCam.backgroundColor = outsideColor;

        if (_cam != null && _cam.rect != viewportRect)
            _cam.rect = viewportRect;
    }

    // ─── Public API ───────────────────────────────────────────────────

    public Transform target => followVCam != null ? followVCam.Follow : null;

    public float currentOrthoSize => followVCam != null ? followVCam.Lens.OrthographicSize : defaultOrthoSize;

    public BoxCollider2D currentBound => confiner?.BoundingShape2D as BoxCollider2D;

    public float smoothTime
    {
        get => _follow != null ? _follow.TrackerSettings.PositionDamping.x : 0.15f;
        set
        {
            if (_follow == null) return;
            var ts = _follow.TrackerSettings;
            ts.PositionDamping = new Vector3(value, value, ts.PositionDamping.z);
            _follow.TrackerSettings = ts;
        }
    }

    public float charHeightOffset
    {
        get => _follow != null ? _follow.FollowOffset.y : 0f;
        set
        {
            if (_follow == null) return;
            var off = _follow.FollowOffset;
            off.y = value;
            _follow.FollowOffset = off;
        }
    }

    public float charLookAheadOffset
    {
        get => _follow != null ? _follow.FollowOffset.x : 0f;
        set
        {
            if (_follow == null) return;
            var off = _follow.FollowOffset;
            off.x = value;
            _follow.FollowOffset = off;
        }
    }

    public void SetBound(BoxCollider2D newBound, bool snap = false)
    {
        if (confiner != null)
        {
            confiner.BoundingShape2D = newBound;
            confiner.InvalidateBoundingShapeCache();
        }
        if (newBound == null) ZoomTo(defaultOrthoSize, 0.3f);
        if (snap) SnapToTarget();
    }

    public void ZoomTo(float targetSize, float duration)
    {
        if (_zoomCoroutine != null) StopCoroutine(_zoomCoroutine);
        if (followVCam == null) return;
        if (duration <= 0f) { SetOrthoSize(targetSize); return; }
        _zoomCoroutine = StartCoroutine(ZoomCoroutine(targetSize, duration));
    }

    public void SetTarget(Transform newTarget)
    {
        if (followVCam != null) followVCam.Follow = newTarget;
    }

    public void SnapToTarget()
    {
        StartCoroutine(DoSnap());
    }

    public void SetFollowPriority(int priority)
    {
        if (followVCam != null) followVCam.Priority = priority;
    }

    // ─── CameraDirector 전용 접근자 ───────────────────────────────────

    internal CinemachineBasicMultiChannelPerlin GetNoise() =>
        followVCam != null ? followVCam.GetComponent<CinemachineBasicMultiChannelPerlin>() : null;

    // ─── Private ─────────────────────────────────────────────────────

    void SetOrthoSize(float size)
    {
        if (followVCam == null) return;
        var lens = followVCam.Lens;
        lens.OrthographicSize = size;
        followVCam.Lens = lens;
    }

    IEnumerator ZoomCoroutine(float targetSize, float duration)
    {
        float startSize = followVCam.Lens.OrthographicSize;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetOrthoSize(Mathf.Lerp(startSize, targetSize, elapsed / duration));
            yield return null;
        }
        SetOrthoSize(targetSize);
        _zoomCoroutine = null;
    }

    IEnumerator DoSnap()
    {
        if (_follow == null) yield break;
        var ts = _follow.TrackerSettings;
        var saved = ts.PositionDamping;
        ts.PositionDamping = Vector3.zero;
        _follow.TrackerSettings = ts;
        yield return new WaitForEndOfFrame();
        ts = _follow.TrackerSettings;
        ts.PositionDamping = saved;
        _follow.TrackerSettings = ts;
    }

    void SetupBackgroundCamera()
    {
        if (_cam == null) return;
        var bgGo = new GameObject("_BgCamera");
        bgGo.transform.SetParent(transform);
        bgGo.transform.localPosition = Vector3.zero;
        bgGo.transform.localRotation = Quaternion.identity;

        _bgCam = bgGo.AddComponent<Camera>();
        _bgCam.clearFlags      = CameraClearFlags.SolidColor;
        _bgCam.backgroundColor = outsideColor;
        _bgCam.cullingMask     = 0;
        _bgCam.depth           = _cam.depth - 1;
        _bgCam.orthographic    = true;
        _bgCam.rect            = new Rect(0f, 0f, 1f, 1f);
        _cam.rect              = viewportRect;

        var urpData = _bgCam.GetUniversalAdditionalCameraData();
        urpData.renderShadows        = false;
        urpData.requiresColorTexture = false;
        urpData.requiresDepthTexture = false;
        urpData.renderPostProcessing = false;
    }

    void OnDrawGizmos()
    {
        if (confiner?.BoundingShape2D != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(
                confiner.BoundingShape2D.bounds.center,
                confiner.BoundingShape2D.bounds.size);
        }
    }
}
