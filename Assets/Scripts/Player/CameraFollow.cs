using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    [Range(0.01f, 1f)]
    public float smoothTime = 0.15f; // Lerp보다 SmoothDamp가 더 부드러움
    public Vector3 offset = new Vector3(0, 0, -10);

    [Header("Bounds")]
    public BoxCollider2D currentBound;

    private Camera _cam;
    private float _camHeight;
    private float _camWidth;
    private Vector3 _currentVelocity = Vector3.zero; // SmoothDamp용

    public static CameraFollow Instance;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else if (Instance != this)
            Destroy(gameObject);

        _cam = GetComponent<Camera>();
        if (_cam == null)
        {
            Debug.LogError("[CameraFollow] Camera 컴포넌트가 없습니다!");
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 1. 목표 위치 계산
        Vector3 targetPos = target.position + offset;
        
        // 2. 바운드(영역) 제한 적용
        if (currentBound != null && _cam != null)
        {
            // 화면 크기가 바뀔 수 있으므로 매 프레임 계산 (최적화보다 정확성 우선)
            _camHeight = _cam.orthographicSize;
            _camWidth = _cam.orthographicSize * _cam.aspect;

            Bounds bounds = currentBound.bounds;

            // 카메라 중심이 이동 가능한 최소/최대 범위 계산
            float minX = bounds.min.x + _camWidth;
            float maxX = bounds.max.x - _camWidth;
            float minY = bounds.min.y + _camHeight;
            float maxY = bounds.max.y - _camHeight;

            // 방 크기가 카메라보다 작을 경우 예외 처리
            float clampedX = (minX > maxX) ? bounds.center.x : Mathf.Clamp(targetPos.x, minX, maxX);
            float clampedY = (minY > maxY) ? bounds.center.y : Mathf.Clamp(targetPos.y, minY, maxY);

            targetPos = new Vector3(clampedX, clampedY, targetPos.z);
        }

        // 3. 부드러운 이동 (SmoothDamp 사용)
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref _currentVelocity, smoothTime);
    }

    public void SetBound(BoxCollider2D newBound, bool snap = false)
    {
        currentBound = newBound;
        // 바운드가 바뀌면 즉시 멈추지 않고 자연스럽게 이동하도록 둠 (snap이 true면 즉시 이동)
        if (snap) SnapToTarget();
    }

    // ★ 카메라 즉시 이동 (텔레포트용)
    public void SnapToTarget()
    {
        if (target == null) return;

        Vector3 targetPos = target.position + offset;

        // 바운드가 있다면 즉시 계산해서 적용
        if (currentBound != null && _cam != null)
        {
            _camHeight = _cam.orthographicSize;
            _camWidth = _cam.orthographicSize * _cam.aspect;
            Bounds bounds = currentBound.bounds;

            float minX = bounds.min.x + _camWidth;
            float maxX = bounds.max.x - _camWidth;
            float minY = bounds.min.y + _camHeight;
            float maxY = bounds.max.y - _camHeight;

            float clampedX = (minX > maxX) ? bounds.center.x : Mathf.Clamp(targetPos.x, minX, maxX);
            float clampedY = (minY > maxY) ? bounds.center.y : Mathf.Clamp(targetPos.y, minY, maxY);

            targetPos = new Vector3(clampedX, clampedY, targetPos.z);
        }

        transform.position = targetPos;
        _currentVelocity = Vector3.zero; // 이동 속도 초기화 (잔상 방지)
    }

    private void OnDrawGizmos()
    {
        if (currentBound != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(currentBound.bounds.center, currentBound.bounds.size);
        }
    }
}
