using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 캐릭터 ID → CharacterPortraitConfig 매핑 레지스트리.
/// Assets/Resources/PortraitConfigs/ 에셋으로 오버라이드 가능.
/// 씬 배치 불필요 — 정적 클래스로 즉시 동작.
/// </summary>
public static class PortraitRegistry
{
    private const string LuId = "루";

    private static Dictionary<string, CharacterPortraitConfig> _configs;

    static Dictionary<string, CharacterPortraitConfig> Configs
    {
        get
        {
            if (_configs == null) Build();
            return _configs;
        }
    }

    static void Build()
    {
        _configs = new Dictionary<string, CharacterPortraitConfig>();
        foreach (var cfg in Resources.LoadAll<CharacterPortraitConfig>("PortraitConfigs"))
        {
            if (cfg != null && !string.IsNullOrEmpty(cfg.characterId))
                _configs[cfg.characterId] = cfg;
        }
    }

    /// <summary>캐릭터 기본 배치 방향. 루→Right, 나머지→Left (Registry 우선).</summary>
    public static PortraitSide GetDefaultSide(string characterId)
    {
        if (characterId == LuId) return PortraitSide.Right;
        if (Configs.TryGetValue(characterId, out var cfg)) return cfg.defaultSide;
        return PortraitSide.Left;
    }

    public static bool TryGet(string characterId, out CharacterPortraitConfig config)
    {
        if (characterId == null) { config = null; return false; }
        return Configs.TryGetValue(characterId, out config);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => _configs = null;
}
