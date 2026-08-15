using UnityEngine;

public class SceneCameraSetup : MonoBehaviour
{
    [Tooltip("이 씬에서 사용할 카메라 샷 목록")]
    [SerializeField] private CameraShot[] shots;

    void OnEnable()
    {
        if (CameraDirector.Instance == null) return;
        foreach (var shot in shots)
        {
            if (!string.IsNullOrEmpty(shot.name) && shot.vcam != null)
                CameraDirector.Instance.RegisterVCam(shot.name, shot.vcam);
        }
    }

    void OnDisable()
    {
        if (CameraDirector.Instance == null) return;
        foreach (var shot in shots)
        {
            if (!string.IsNullOrEmpty(shot.name))
                CameraDirector.Instance.UnregisterVCam(shot.name);
        }
    }
}
