#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

/// <summary>
/// 디버그 전용: 키보드로 환상/현실 게이지를 조작합니다.
/// 에디터 및 Development Build에서만 컴파일됩니다.
///
/// 단축키:
///   F1 → 게이지 0   (완전 환상)
///   F2 → 게이지 25
///   F3 → 게이지 50
///   F4 → 게이지 75
///   F5 → 게이지 100 (완전 현실)
///   ← / → → ±5 조정
///   Shift + ← / → → ±1 조정
/// </summary>
public class DebugGaugeController : MonoBehaviour
{
    void Update()
    {
        var gm = GaugeManager.Instance;
        if (gm == null) return;

        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        float step = shift ? 1f : 5f;

        if (Input.GetKeyDown(KeyCode.F1))              { gm.SetGaugeValue(0f);   Log(gm, "F1"); }
        else if (Input.GetKeyDown(KeyCode.F2))         { gm.SetGaugeValue(25f);  Log(gm, "F2"); }
        else if (Input.GetKeyDown(KeyCode.F3))         { gm.SetGaugeValue(50f);  Log(gm, "F3"); }
        else if (Input.GetKeyDown(KeyCode.F4))         { gm.SetGaugeValue(75f);  Log(gm, "F4"); }
        else if (Input.GetKeyDown(KeyCode.F5))         { gm.SetGaugeValue(100f); Log(gm, "F5"); }
        else if (Input.GetKeyDown(KeyCode.LeftArrow))  { gm.ChangeGauge(-step);  Log(gm, $"←{step}"); }
        else if (Input.GetKeyDown(KeyCode.RightArrow)) { gm.ChangeGauge( step);  Log(gm, $"→{step}"); }
    }

    static void Log(GaugeManager gm, string key)
        => Dbg.Log($"[Debug] {key} → 게이지 {gm.fantasyRealityGauge:F1}");
}
#endif
