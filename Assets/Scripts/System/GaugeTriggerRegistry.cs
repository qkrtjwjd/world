using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 환상/현실 게이지 트리거 ID → 변화량 매핑 레지스트리.
/// 기본 12개 값은 코드에 내장되어 있으며, Assets/Resources/GaugeTriggers/ 폴더에
/// GaugeTriggerDefinition 에셋을 배치하면 런타임에 오버라이드할 수 있다.
/// MonoBehaviour 불필요 — 씬 배치 없이 즉시 동작한다.
/// </summary>
public static class GaugeTriggerRegistry
{
    private static Dictionary<string, float> _amounts;

    static Dictionary<string, float> Amounts
    {
        get
        {
            if (_amounts == null) Build();
            return _amounts;
        }
    }

    static void Build()
    {
        _amounts = new Dictionary<string, float>
        {
            ["무서운것_목격"]      =  15f,
            ["신체_고통"]          =  10f,
            ["쿠루_직접_대화"]     =  10f,
            ["아버지_유품_접촉"]   =  10f,
            ["루_감정_폭발"]       =  10f,
            ["환상_평화주의_성공"] =   5f,
            ["세라_목소리_들림"]   = -25f,
            ["무서운것_회피"]      = -15f,
            ["NPC_눈_마주침"]      = -10f,
            ["쿠루_부재"]          = -10f,
            ["마시멜로_냄새"]      =  -5f,
            ["세라_흔적_발견"]     =  -5f,
        };

        // Resources/GaugeTriggers/ 에셋으로 오버라이드
        foreach (var def in Resources.LoadAll<GaugeTriggerDefinition>("GaugeTriggers"))
        {
            if (def == null || string.IsNullOrEmpty(def.triggerId)) continue;
            _amounts[def.triggerId] = def.amount;
        }
    }

    /// <summary>triggerId에 해당하는 게이지 변화량을 반환합니다.</summary>
    public static bool TryGetAmount(string triggerId, out float amount)
        => Amounts.TryGetValue(triggerId, out amount);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => _amounts = null;
}
