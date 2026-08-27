using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
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
    public float defaultOrthoSize = 5.625f;

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

    // 씬을 넘어 살아남은 뒤 followVCam 을 다시 찾을 때 쓰는 이름. 아래 OnSceneLoaded 참조.
    private string _followVCamName;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(gameObject); return; }

        _cam = GetComponent<Camera>();
        if (_cam == null) Debug.LogError("[CameraFollow] Camera 컴포넌트가 없습니다!");

        if (followVCam != null)
        {
            _follow         = followVCam.GetComponent<CinemachineFollow>();
            _followVCamName = followVCam.name;
        }

        SetupBackgroundCamera();
    }

    void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    /// <summary>
    /// 씬이 바뀐 뒤 가상 카메라와 추적 대상을 다시 붙입니다.
    /// </summary>
    /// <remarks>
    /// ⚠ 이게 없으면 씬을 런타임에 다시 부를 때 카메라가 원점 (0,0) 에 얼어붙는다(2026-08-23 실측).
    /// 이 GameObject 는 <see cref="CameraDirector"/> 가 <c>DontDestroyOnLoad</c> 로 만들기 때문에
    /// 씬을 넘어 살아남는데, <c>followVCam</c> 이 가리키던 가상 카메라는 이전 씬과 함께
    /// 파괴돼 가짜 null 이 된다. 그러면 <see cref="Start"/> 는 이미 지나갔으므로 아무도 다시 붙이지 않는다.
    /// Home → Home 되감기 복귀 · 배드 엔딩 복귀가 전부 이 경로를 지난다.
    ///
    /// 이름으로 다시 찾는 이유는 씬에 샷 전용 가상 카메라가 여럿 있을 수 있어서다.
    /// 아무거나 잡으면 컷씬용 카메라에 플레이어를 붙여 버린다.
    /// </remarks>
    void Bind(CinemachineCamera vcam)
    {
        followVCam      = vcam;
        _follow         = vcam.GetComponent<CinemachineFollow>();
        _followVCamName = vcam.name;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (followVCam == null)
        {
            var candidates = FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include);

            // 1) 이전 씬에서 쓰던 이름과 같은 것을 먼저 찾는다.
            foreach (var vcam in candidates)
            {
                if (vcam.name != _followVCamName) continue;
                Bind(vcam);
                break;
            }

            // 2) 이름을 모르거나(이전 씬에 가상 카메라가 아예 없었던 경우) 못 찾았으면,
            //    씬에 가상 카메라가 딱 하나일 때만 그것을 쓴다.
            //    여럿이면 어느 것이 추적용인지 알 수 없으므로 건드리지 않는다 —
            //    잘못 잡으면 컷씬용 카메라에 플레이어를 붙여 버린다.
            if (followVCam == null && candidates.Length == 1)
                Bind(candidates[0]);
        }

        if (followVCam == null)
        {
            // 가상 카메라가 없는 씬도 있다(MapScene). 그 씬에서는 원래 따라가지 않으므로 정상이다.
            return;
        }

        if (followVCam.Follow == null)
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null) followVCam.Follow = player.transform;
            else { StartCoroutine(RetryFindPlayer()); return; }
        }

        SnapCameraToFollow();
    }

    /// <remarks>
    /// ⚠ 이걸 빼면 씬을 런타임에 다시 부를 때 카메라가 영영 안 따라온다(2026-08-23 실측).
    /// 가드가 <c>Destroy(gameObject)</c> 라서 파괴된 카메라의 관리 래퍼가 <see cref="Instance"/> 에
    /// 그대로 남고, 새 카메라는 자기를 Instance 로 잡지 못한다. 그러면 <see cref="Start"/> 의
    /// <c>followVCam</c> 배선도 새 카메라 쪽에 걸리지 않아 원점 (0,0) 에 얼어붙는다.
    /// Home → Home 되감기 복귀 · 배드 엔딩 복귀가 전부 이 경로를 지난다.
    /// </remarks>
    void OnDestroy()
    {
        if (Instance == this) Instance = null;
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

    /// <summary>
    /// 목표 ortho 로 줌한다.
    ///
    /// <para>⚠ <b>픽셀퍼펙트가 켜져 있으면 duration 을 무시하고 즉시 적용한다.</b>
    /// <c>CinemachinePixelPerfect</c> 가 ortho 를 <c>base / N</c>(N = 정수)로 스냅하기 때문에
    /// 쓸 수 있는 값이 5.625 · 2.8125 · 1.875 · 1.40625 … 뿐이고 중간값이 아예 없다.
    /// 이때 Lerp 를 돌리면 전반부에는 아무 일도 일어나지 않다가 중간에 한 번 튄다 —
    /// 부드러워지는 게 아니라 지연만 생긴다. 그래서 끊어서 적용하는 편이 낫다.</para>
    ///
    /// <para>연출의 호흡은 호출부가 잡는다. 예를 들어 배드엔딩 문 줌은 단계마다
    /// <c>ZoomTo</c> 뒤에 <c>WaitForSecondsRealtime</c> 로 기다리므로,
    /// 스냅해도 세 박자로 끊어 들어가는 연출이 그대로 유지된다.</para>
    /// </summary>
    public void ZoomTo(float targetSize, float duration)
    {
        if (_zoomCoroutine != null) StopCoroutine(_zoomCoroutine);
        if (followVCam == null) return;
        if (duration <= 0f || PixelPerfectActive) { SetOrthoSize(targetSize); return; }
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

    // 같은 GameObject 의 PixelPerfectCamera. 없을 수도 있으므로 조회 여부를 따로 기억한다.
    PixelPerfectCamera _ppc;
    bool _ppcLookedUp;

    bool PixelPerfectActive
    {
        get
        {
            if (!_ppcLookedUp) { _ppc = GetComponent<PixelPerfectCamera>(); _ppcLookedUp = true; }
            return _ppc != null && _ppc.isActiveAndEnabled;
        }
    }

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
