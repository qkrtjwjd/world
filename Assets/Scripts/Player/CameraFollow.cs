using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    [Range(0.01f, 1f)]
    public float smoothTime = 0.15f;
    public Vector3 offset = new Vector3(0, 0, -10);

    [Header("Bounds")]
    public BoxCollider2D currentBound;

    private Camera _cam;
    private float _camHeight;
    private float _camWidth;
    private Vector3 _currentVelocity = Vector3.zero;
    private bool _needsSnap = true;

    public static CameraFollow Instance;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(gameObject); return; }

        _cam = GetComponent<Camera>();
        if (_cam == null)
            Debug.LogError("[CameraFollow] Camera 컴포넌트가 없습니다!");
    }

    void LateUpdate()
    {
        if (target == null) return;

        if (_needsSnap)
        {
            _needsSnap = false;
            SnapToTarget();
            return;
        }

        transform.position = Vector3.SmoothDamp(
            transform.position, ClampToBounds(target.position + offset),
            ref _currentVelocity, smoothTime);
    }

    public void SetBound(BoxCollider2D newBound, bool snap = false)
    {
        currentBound = newBound;
        if (snap) SnapToTarget();
    }

    public void SnapToTarget()
    {
        if (target == null) return;
        transform.position = ClampToBounds(target.position + offset);
        _currentVelocity = Vector3.zero;
    }

    /// <summary>targetPos 를 currentBound 범위 내로 클램프합니다.</summary>
    Vector3 ClampToBounds(Vector3 targetPos)
    {
        if (currentBound == null || _cam == null) return targetPos;

        _camHeight = _cam.orthographicSize;
        _camWidth  = _cam.orthographicSize * _cam.aspect;

        Bounds bounds = currentBound.bounds;
        float minX = bounds.min.x + _camWidth;
        float maxX = bounds.max.x - _camWidth;
        float minY = bounds.min.y + _camHeight;
        float maxY = bounds.max.y - _camHeight;

        return new Vector3(
            (minX > maxX) ? bounds.center.x : Mathf.Clamp(targetPos.x, minX, maxX),
            (minY > maxY) ? bounds.center.y : Mathf.Clamp(targetPos.y, minY, maxY),
            targetPos.z);
    }

    void OnDrawGizmos()
    {
        if (currentBound == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(currentBound.bounds.center, currentBound.bounds.size);
    }
}
