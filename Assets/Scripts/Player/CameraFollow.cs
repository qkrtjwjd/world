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

    [Header("씬 기본 바운드")]
    [Tooltip("방 바운드가 풀릴 때 되돌아갈 씬 전체 바운드. 씬의 \"SceneCameraBounds\" 에서 자동으로 찾으므로 직접 꽂지 않아도 된다.")]
    [SerializeField] BoxCollider2D defaultBound;

    /// <summary>씬 기본 바운드로 쓸 오브젝트의 이름. 이 이름이어야 씬 로드 때 자동으로 잡힌다.</summary>
    public const string SceneBoundName = "SceneCameraBounds";

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

        // ⚠ confiner 도 같이 다시 잡는다. 가상 카메라와 같은 GameObject 에 붙어 있으므로
        //   이전 씬의 가상 카메라가 파괴되면 confiner 도 같이 가짜 null 이 된다.
        //   이걸 빼면 씬을 넘어간 뒤 SetBound 가 조용히 아무 일도 하지 않는다.
        confiner = vcam.GetComponent<CinemachineConfiner2D>();
        DisableCinemachineConfiner();
    }

    /// <summary>씬의 기본 바운드를 이름으로 다시 찾습니다.</summary>
    /// <remarks>
    /// ⚠ <see cref="followVCam"/> 과 똑같은 이유로 매 씬마다 다시 찾아야 한다.
    /// 이 GameObject 는 <c>DontDestroyOnLoad</c> 라 씬을 넘어 살아남지만,
    /// <see cref="defaultBound"/> 가 가리키던 씬 오브젝트는 이전 씬과 함께 파괴돼 가짜 null 이 된다.
    /// </remarks>
    void BindDefaultBound()
    {
        var go = GameObject.Find(SceneBoundName);
        defaultBound = go != null ? go.GetComponent<BoxCollider2D>() : null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindDefaultBound();

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
            // 가상 카메라가 없는 씬에서는 따라갈 대상이 없다. 플레이가 있는 세 씬
            // (Home · MapScene · Shelter)에는 전부 "Follow VCam" 이 있어야 한다.
            return;
        }

        // 씬 기본 바운드를 적용한다. confiner 를 다시 잡은 뒤라야 의미가 있다.
        // 방에서 시작하는 경우에는 씬 오브젝트의 Start() 가 sceneLoaded 뒤에 돌기 때문에
        // RoomTransfer.Start 가 방 바운드로 덮어쓴다 — 순서가 맞다.
        SetBound(null);

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
        // 첫 씬에서는 sceneLoaded 가 이 컴포넌트의 OnEnable 보다 먼저 지나갈 수 있으므로
        // 여기서도 한 번 잡는다.
        if (defaultBound == null) BindDefaultBound();
        if (_bound == null) _bound = defaultBound;
        DisableCinemachineConfiner();

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
        // 정사영 크기는 5.625 에서 움직이지 않는다(F-3-9 · CLAUDE.md §11). 씬 설정이 달라도 여기서 되돌린다.
        LockOrthoSize();

        // 방 경계 안으로 제한한다 — 경계가 화면보다 작으면 그 축은 경계 중앙에 고정된다(아래 「경계 제한」).
        ClampToBound();

        bool shakeOn = SettingsManager.Instance?.cameraShakeEnabled ?? true;
        if (shakeOn && shakeOffset != Vector3.zero)
            transform.position += shakeOffset;

        // 카메라 위치를 내부 해상도 1픽셀(1/PPU 유닛) 단위로 스냅한다(F-3-9).
        // 소수 좌표에 멈추면 정수배 스케일에서 타일 경계가 떨린다.
        Vector3 pos = transform.position;
        pos.x = Mathf.Round(pos.x * PixelsPerUnit) / PixelsPerUnit;
        pos.y = Mathf.Round(pos.y * PixelsPerUnit) / PixelsPerUnit;
        transform.position = pos;

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

    /// <summary>지금 걸린 방 경계. 카메라 위치는 <see cref="ClampToBound"/> 가 이 사각형 안으로 제한한다.</summary>
    public BoxCollider2D currentBound => _bound;

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

    /// <summary>
    /// 카메라 바운드를 바꿉니다. <paramref name="newBound"/> 가 <c>null</c> 이면
    /// "방을 나간다"는 뜻이므로 <b>씬 기본 바운드</b>(<see cref="SceneBoundName"/>)로 되돌아갑니다.
    /// </summary>
    /// <remarks>
    /// null 을 "바운드 없음" 으로 처리하면 방을 한 번 드나든 뒤로는 씬 경계가 영영 사라져,
    /// 마을 가장자리에서 담 밖 빈 공간이 화면 절반까지 들어온다.
    /// </remarks>
    /// <param name="blend">
    /// true 면 즉시 자르지 않고 <see cref="boundBlendSeconds"/> 에 걸쳐 새 경계 안으로 미끄러져 들어간다.
    /// 걸어서 넘는 경계(마을 ↔ 숲 — <see cref="CameraBoundZone"/>)용이다. 옛 경계 밖에 있던 카메라가
    /// 한 프레임에 몇 유닛씩 튀면 컷처럼 보인다(2026-10-05 · 보호 구역 §2 사용자 승인).
    /// </param>
    public void SetBound(BoxCollider2D newBound, bool snap = false, bool blend = false)
    {
        var prev = _bound;
        _bound = newBound != null ? newBound : defaultBound;
        DisableCinemachineConfiner();
        if (snap) { SnapToTarget(); _boundBlend = 1f; }
        else if (blend && _bound != prev && _hasLastClamped) { _blendFrom = _lastClamped; _boundBlend = 0f; }
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

    /// <summary>화면 1픽셀 = 1/PPU 유닛. 기준 삼각형(CLAUDE.md §11)의 PPU 32.</summary>
    const float PixelsPerUnit = 32f;

    /// <summary>
    /// 정사영 크기를 기본값에 묶어 둔다. 2026-09-27 줌 폐기(E-64 · F-3-9)로 ZoomTo 를 걷어냈다.
    /// 옛 씬 값이나 다른 코드가 렌즈를 바꿔도 다음 프레임에 돌아온다.
    /// </summary>
    void LockOrthoSize()
    {
        if (followVCam == null) return;
        var lens = followVCam.Lens;
        if (Mathf.Approximately(lens.OrthographicSize, defaultOrthoSize)) return;
        lens.OrthographicSize = defaultOrthoSize;
        followVCam.Lens = lens;
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
        if (_bound != null && TryGetBoundRect(out Rect r))
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(r.center, r.size);
        }
    }

    // ─── 경계 제한 ───────────────────────────────────────────────────
    //
    // ⚠ 2026-09-27 (보호 구역 §2 사용자 승인): 방 경계 제한을 Cinemachine Confiner2D 에서 여기로 옮겼다.
    //   다락방 경계(`다락방 계단 ▸ CameraBounds`)를 Confiner2D 에 넣으면 빈 해가 나와 보정이 카메라를
    //   정확히 (0,0,0) 으로 끌어가 화면이 통째로 비었다(z 까지 0). 줌을 쓰던 시절에도 같았고, OversizeWindow ·
    //   대리 경계로도 고쳐지지 않았다. Confiner2D 는 BoxCollider2D 의 offset 도 다르게 다룬다.
    //   이제 콜라이더의 네 꼭짓점을 월드로 변환해 사각형을 잡고, 카메라 중심을 그 안으로 자른다.
    //     · 경계가 화면보다 작은 축 → 그 축은 경계 중앙에 고정(개정 D 의 「방 전경 고정」)
    //     · 큰 축 → 화면 가장자리가 경계에 닿으면 멈춘다(추적 · 스크롤이 맵 경계에서 멈춘다 — F-3-9)

    BoxCollider2D _bound;

    void DisableCinemachineConfiner()
    {
        if (confiner == null) return;
        confiner.BoundingShape2D = null;
        confiner.enabled = false;
    }

    // 경계 전환 보간(SetBound 의 blend). 1 이면 보간 없음.
    [Header("Bound Blend")]
    [Tooltip("걸어서 넘는 경계가 바뀔 때 새 경계 안으로 미끄러져 들어가는 시간(초).")]
    public float boundBlendSeconds = 0.6f;
    float   _boundBlend = 1f;
    Vector3 _blendFrom;
    Vector3 _lastClamped;
    bool    _hasLastClamped;

    void ClampToBound()
    {
        if (_cam == null || !TryGetBoundRect(out Rect r)) return;
        float halfH = _cam.orthographicSize;
        float halfW = halfH * _cam.aspect;
        Vector3 p = transform.position;
        p.x = r.width  <= halfW * 2f ? r.center.x : Mathf.Clamp(p.x, r.xMin + halfW, r.xMax - halfW);
        p.y = r.height <= halfH * 2f ? r.center.y : Mathf.Clamp(p.y, r.yMin + halfH, r.yMax - halfH);

        // 경계가 바뀐 직후 — 직전 자리에서 지금 목표로 옮겨 간다. 목표는 매 프레임 다시 계산하므로 루가 걸어도 따라간다.
        if (_boundBlend < 1f)
        {
            _boundBlend = Mathf.Min(1f, _boundBlend + Time.deltaTime / Mathf.Max(0.01f, boundBlendSeconds));
            float k = Mathf.SmoothStep(0f, 1f, _boundBlend);
            p.x = Mathf.Lerp(_blendFrom.x, p.x, k);
            p.y = Mathf.Lerp(_blendFrom.y, p.y, k);
        }

        transform.position = p;
        _lastClamped    = p;
        _hasLastClamped = true;
    }

    /// <summary>
    /// 현재 경계의 월드 사각형. 네 꼭짓점을 변환해 잰다 — 회전 · 비균일 스케일 · offset 을 그대로 반영하고,
    /// 콜라이더가 꺼져 있어도 값이 나온다(CLAUDE.md §11 — m_Size 로 재지 않는다).
    /// </summary>
    public bool TryGetBoundRect(out Rect rect)
    {
        rect = default;
        var c = _bound;
        if (c == null) return false;
        Vector2 h = c.size * 0.5f, o = c.offset;
        var t = c.transform;
        Vector2 a = t.TransformPoint(o + new Vector2(-h.x, -h.y));
        Vector2 b = t.TransformPoint(o + new Vector2( h.x, -h.y));
        Vector2 d = t.TransformPoint(o + new Vector2(-h.x,  h.y));
        Vector2 e = t.TransformPoint(o + new Vector2( h.x,  h.y));
        float xMin = Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(d.x, e.x));
        float xMax = Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(d.x, e.x));
        float yMin = Mathf.Min(Mathf.Min(a.y, b.y), Mathf.Min(d.y, e.y));
        float yMax = Mathf.Max(Mathf.Max(a.y, b.y), Mathf.Max(d.y, e.y));
        rect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        return true;
    }
}
