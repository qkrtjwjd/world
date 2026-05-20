using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 글리치 구간(31~69) 전투 진입 시 또는 게이지 구간 전환 시 표시되는 모드 선택 UI.
/// 단검 버튼 → 현실 모드(액션), 마시멜로 버튼 → 환상 모드(턴제).
/// </summary>
public class PendingModeUI : MonoBehaviour
{
    [Header("버튼 연결")]
    public Button daggerButton;
    public Button marshmallowButton;

    /// <summary>
    /// mid-battle 전환 시 GaugeBoundaryMonitor가 주입하는 콜백.
    /// null이면 EncounterManager.OnPendingModeSelected (전투 시작용)를 호출.
    /// </summary>
    [HideInInspector]
    public System.Action<BattleMode> onModeSelected;

    void Start()
    {
        daggerButton?.onClick.AddListener(OnSelectDagger);
        marshmallowButton?.onClick.AddListener(OnSelectMarshmallow);
    }

    void OnSelectDagger()
    {
        GaugeManager.Instance?.ForceTempReality();

        var callback = onModeSelected;
        Destroy(gameObject);

        if (callback != null)
            callback(BattleMode.Reality);
        else
            EncounterManager.Instance?.OnPendingModeSelected(BattleMode.Reality);
    }

    void OnSelectMarshmallow()
    {
        GaugeManager.Instance?.ForceTempFantasy();

        var callback = onModeSelected;
        Destroy(gameObject);

        if (callback != null)
            callback(BattleMode.Fantasy);
        else
            EncounterManager.Instance?.OnPendingModeSelected(BattleMode.Fantasy);
    }
}
