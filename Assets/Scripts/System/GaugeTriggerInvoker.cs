using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Inspector에서 GaugeTrigger를 선택하고 UnityEvent 슬롯에 연결해 호출.
/// 또는 ChangeGauge로 수치를 직접 지정해 호출 가능.
/// </summary>
public class GaugeTriggerInvoker : MonoBehaviour
{
    [Header("트리거 방식 선택")]
    public InvokeMode mode = InvokeMode.Trigger;

    [Header("트리거 방식")]
    public GaugeTrigger trigger;

    [Header("수치 직접 지정 방식 (mode = DirectAmount)")]
    public float amount;

    public enum InvokeMode { Trigger, DirectAmount }

    /// <summary>UnityEvent 슬롯에 연결하거나 코드에서 직접 호출.</summary>
    public void Invoke()
    {
        if (GaugeManager.Instance == null) return;

        if (mode == InvokeMode.Trigger)
        {
            // enum 이름에서 __ 앞의 triggerId 부분만 추출해 string 버전으로 호출
            string triggerId = trigger.ToString().Split(new[] { "__" }, System.StringSplitOptions.None)[0];
            GaugeManager.Instance.ApplyTrigger(triggerId);
        }
        else
        {
            GaugeManager.Instance.ChangeGauge(amount);
        }
    }

    public void ForceRealityMax()  => GaugeManager.Instance?.ForceRealityMax();
    public void ForceFantasyMax()  => GaugeManager.Instance?.ForceFantasyMax();
}
