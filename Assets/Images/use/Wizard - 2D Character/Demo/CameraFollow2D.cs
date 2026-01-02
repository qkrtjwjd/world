using UnityEngine;

// 심플 2D 카메라 따라가기
// 카메라 오브젝트에 붙이고, target(플레이어 Transform)과 offset을 인스펙터에서 지정하세요.
public class CameraFollow2D : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 0f, -10f);
    public float smoothTime = 0.15f;

    private Vector3 velocity = Vector3.zero;

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = target.position + offset;
        // 부드러운 이동
        transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
    }
}


