using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    [Range(0.01f, 1f)]
    public float smoothTime = 0.15f;
    public Vector3 offset = new Vector3(0, 0, -10);

    [Header("Character")]
    [Tooltip("캐릭터 피벗(발)부터 시각적 중심까지의 Y 오프셋. 발 기준 피벗일 때 캐릭터 키의 절반 값을 입력하면 몸이 카메라 밖으로 잘리지 않습니다.")]
    public float charHeightOffset = 0f;

    [Header("Snap Settings")]
    public float snapDistance = 5f;

    [Header("Bounds")]
    public BoxCollider2D currentBound;

    [Header("Zoom")]
    [Tooltip("룸 밖(currentBound == null)일 때 복귀하는 기본 orthographicSize")]
    public float defaultOrthoSize = 5f;

    [Header("Background")]
    [Tooltip("카메라 바운드 밖 영역(레터박스)에 표시할 배경색")]
    public Color outsideColor = Color.black;

    private Camera _cam;
    private float _camHeight;
    private float _camWidth;
    private Vector3 _currentVelocity = Vector3.zero;
    private bool _needsSnap = true;
    private Rect _targetRect = new Rect(0f, 0f, 1f, 1f);
    private const float RectLerpSpeed = 5f;
    private Coroutine _zoomCoroutine;
    private Camera _bgCam;
    private float _lastOrthoSize = -1f;

    public static CameraFollow Instance;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(gameObject); return; }

        currentBound = null; // RoomTransfer.Start()가 SetBound로 제어 (인스펙터 값 무효화)

        _cam = GetComponent<Camera>();
        if (_cam == null)
            Debug.LogError("[CameraFollow] Camera 컴포넌트가 없습니다!");
        else
        {
            CacheCamDimensions();
            SetupBackgroundCamera();
        }
    }

    void SetupBackgroundCamera()
    {
        var bgGo = new GameObject("_BgCamera");
        bgGo.transform.SetParent(transform);
        bgGo.transform.localPosition = Vector3.zero;
        bgGo.transform.localRotation = Quaternion.identity;

        _bgCam = bgGo.AddComponent<Camera>();
        _bgCam.clearFlags     = CameraClearFlags.SolidColor;
        _bgCam.backgroundColor = outsideColor;
        _bgCam.cullingMask    = 0; // 아무 오브젝트도 렌더링하지 않음
        _bgCam.depth          = _cam.depth - 1;
        _bgCam.orthographic   = true;
        _bgCam.rect           = new Rect(0f, 0f, 1f, 1f);

        // URP 설정 — GetUniversalAdditionalCameraData()가 없으면 자동 AddComponent
        var urpData = _bgCam.GetUniversalAdditionalCameraData();
        urpData.renderShadows        = false;
        urpData.requiresColorTexture = false;
        urpData.requiresDepthTexture = false;
        urpData.renderPostProcessing = false;
    }

    void CacheCamDimensions()
    {
        _camHeight = _cam.orthographicSize;
        _camWidth  = _cam.orthographicSize * _cam.aspect;
    }

    void Start()
    {
        if (target == null)
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null) target = player.transform;
        }
    }

    void LateUpdate()
    {
        if (_cam == null || target == null) return;

        if (!Mathf.Approximately(_cam.orthographicSize, _lastOrthoSize))
        {
            CacheCamDimensions();
            _lastOrthoSize = _cam.orthographicSize;
        }

        // Inspector에서 outsideColor를 변경해도 실시간 반영
        if (_bgCam != null && _bgCam.backgroundColor != outsideColor)
            _bgCam.backgroundColor = outsideColor;

        // charHeightOffset: 발 기준 피벗인 캐릭터의 시각적 중심을 추적하기 위한 Y 보정
        Vector3 trackPoint = target.position + new Vector3(0f, charHeightOffset, 0f);
        Vector3 targetPos = ClampToBounds(trackPoint + offset);
        float dist = Vector3.Distance(transform.position, targetPos);

        if (_needsSnap || dist > snapDistance)
        {
            _needsSnap = false;
            transform.position = targetPos;
            _currentVelocity = Vector3.zero;
        }
        else
        {
            transform.position = Vector3.SmoothDamp(
                transform.position, targetPos,
                ref _currentVelocity, smoothTime);
        }

        if (_cam != null)
            _cam.rect = LerpRect(_cam.rect, _targetRect, Time.deltaTime * RectLerpSpeed);
    }

    public void SetBound(BoxCollider2D newBound, bool snap = false)
    {
        currentBound = newBound;
        if (newBound == null)
            ZoomTo(defaultOrthoSize, 0.3f);
        if (_cam != null)
        {
            CacheCamDimensions();
            UpdateCameraRect(); // _targetRect 업데이트
            if (snap) _cam.rect = _targetRect; // snap 시 즉시 적용
        }
        if (snap) SnapToTarget();
    }

    public void ZoomTo(float targetSize, float duration)
    {
        if (_zoomCoroutine != null) StopCoroutine(_zoomCoroutine);
        if (duration <= 0f || _cam == null) { if (_cam != null) _cam.orthographicSize = targetSize; return; }
        _zoomCoroutine = StartCoroutine(ZoomCoroutine(targetSize, duration));
    }

    IEnumerator ZoomCoroutine(float targetSize, float duration)
    {
        float startSize = _cam.orthographicSize;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _cam.orthographicSize = Mathf.Lerp(startSize, targetSize, elapsed / duration);
            yield return null;
        }
        _cam.orthographicSize = targetSize;
        _zoomCoroutine = null;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        _needsSnap = true;
    }

    public void SnapToTarget()
    {
        if (target == null) return;
        Vector3 trackPoint = target.position + new Vector3(0f, charHeightOffset, 0f);
        transform.position = ClampToBounds(trackPoint + offset);
        _currentVelocity = Vector3.zero;
    }

    /// <summary>targetPos 를 currentBound 범위 내로 클램프합니다. X/Y 축 독립 처리.</summary>
    Vector3 ClampToBounds(Vector3 targetPos)
    {
        if (currentBound == null || _cam == null) return targetPos;

        Bounds bounds = currentBound.bounds;
        float minX = bounds.min.x + _camWidth;
        float maxX = bounds.max.x - _camWidth;
        float minY = bounds.min.y + _camHeight;
        float maxY = bounds.max.y - _camHeight;

        float clampedX = (minX > maxX) ? bounds.center.x : Mathf.Clamp(targetPos.x, minX, maxX);
        float clampedY = (minY > maxY) ? bounds.center.y : Mathf.Clamp(targetPos.y, minY, maxY);

        return new Vector3(clampedX, clampedY, targetPos.z);
    }

    /// <summary>바운드가 카메라 뷰포트보다 작을 때 camera.rect를 크롭해 바운드 밖 영역을 차단합니다.</summary>
    void UpdateCameraRect()
    {
        if (currentBound == null || _cam == null)
        {
            if (_cam != null) _targetRect = new Rect(0f, 0f, 1f, 1f);
            return;
        }

        Bounds bounds = currentBound.bounds;
        float camWorldWidth  = _camWidth  * 2f;
        float camWorldHeight = _camHeight * 2f;

        float vpW, vpX;
        if (bounds.size.x < camWorldWidth)
        {
            vpW = bounds.size.x / camWorldWidth;
            vpX = (1f - vpW) * 0.5f;
        }
        else { vpW = 1f; vpX = 0f; }

        float vpH, vpY;
        if (bounds.size.y < camWorldHeight)
        {
            vpH = bounds.size.y / camWorldHeight;
            vpY = (1f - vpH) * 0.5f;
        }
        else { vpH = 1f; vpY = 0f; }

        _targetRect = new Rect(vpX, vpY, vpW, vpH);
    }

    static Rect LerpRect(Rect a, Rect b, float t)
    {
        return new Rect(
            Mathf.Lerp(a.x,      b.x,      t),
            Mathf.Lerp(a.y,      b.y,      t),
            Mathf.Lerp(a.width,  b.width,  t),
            Mathf.Lerp(a.height, b.height, t));
    }

    void OnDrawGizmos()
    {
        if (currentBound == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(currentBound.bounds.center, currentBound.bounds.size);
    }
}
