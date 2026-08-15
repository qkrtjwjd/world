using UnityEngine;
using Unity.Cinemachine;

[System.Serializable]
public class CameraShot
{
    [Tooltip("샷 식별 이름")]
    public string name;

    [Tooltip("씬에 배치된 Cinemachine Virtual Camera")]
    public CinemachineCamera vcam;

    [Tooltip("전환 시간 (초). VCam의 Blend 설정으로도 제어 가능")]
    public float blendTime = 0.4f;
}
