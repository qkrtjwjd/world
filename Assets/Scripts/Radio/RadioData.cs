using UnityEngine;

[CreateAssetMenu(fileName = "New RadioData", menuName = "Radio/Radio Data")]
public class RadioData : ScriptableObject
{
    public string    objectID;
    [Tooltip("Yarn Spinner 노드 이름 (Assets/Dialogue/Scripts/ 폴더의 .yarn 파일에 정의)")]
    public string    yarnNodeName;
    public AudioClip voiceClip;
}
