using UnityEngine;

/// <summary>
/// 시나리오 플래그 CRUD 전담 매니저.
/// 내부적으로 GameStateManager.flags 딕셔너리에 위임한다.
///
/// 【플래그 반응 방식】
/// 이 프로젝트는 "폴링 기본" 방식을 채택한다.
/// 오브젝트가 상태를 알고 싶을 때 GetFlag()를 직접 호출한다.
/// </summary>
public class FlagManager : PersistentSingleton<FlagManager>
{
    protected override void OnAwake()
    {
        InitDemoFlags();
    }

    void Start()
    {
        // GameStateManager가 늦게 초기화될 경우를 대비해 Start에도 초기화
        if (GameStateManager.Instance != null && GameStateManager.Instance.flags.Count == 0)
            InitDemoFlags();
    }

    // ── 공개 API ─────────────────────────────────────────────────────────
    public void SetFlag(string key, bool value)
    {
        if (GameStateManager.Instance == null)
        {
            Debug.LogWarning("[FlagManager] GameStateManager.Instance가 null입니다.");
            return;
        }
        GameStateManager.Instance.SetFlag(key, value);
    }

    public bool GetFlag(string key, bool defaultValue = false)
    {
        if (GameStateManager.Instance == null) return defaultValue;
        return GameStateManager.Instance.GetFlag(key, defaultValue);
    }

    public bool HasFlag(string key)
    {
        if (GameStateManager.Instance == null) return false;
        return GameStateManager.Instance.HasFlag(key);
    }

    /// <summary>모든 플래그를 비우고 데모 초기값으로 되돌린다. 세이브 로드 시 사용.</summary>
    public void ResetToDefaults()
    {
        if (GameStateManager.Instance == null) return;
        GameStateManager.Instance.flags.Clear();
        InitDemoFlags();
    }

    // ── 데모 초기 플래그 ─────────────────────────────────────────────────
    void InitDemoFlags()
    {
        if (GameStateManager.Instance == null) return;

        var flags = GameStateManager.Instance.flags;
        if (!flags.ContainsKey("왜곡_체험"))         flags["왜곡_체험"]         = false;
        if (!flags.ContainsKey("쿠루_합류"))          flags["쿠루_합류"]          = false;
        if (!flags.ContainsKey("상인_광장_만남"))      flags["상인_광장_만남"]      = false;
        if (!flags.ContainsKey("빵반죽_획득"))         flags["빵반죽_획득"]         = false;
        if (!flags.ContainsKey("꽃집_탐색"))           flags["꽃집_탐색"]           = false;
    }
}
