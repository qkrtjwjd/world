using UnityEngine;

[CreateAssetMenu(fileName = "NewGaugeTrigger", menuName = "무채색낙원/GaugeTriggerDefinition")]
public class GaugeTriggerDefinition : ScriptableObject
{
    public string triggerId;
    public float  amount;            // 양수=현실 방향, 음수=환상 방향 (부호 직접 지정)
    public bool   isRealityDirection;
}
