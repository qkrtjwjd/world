using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 부엉이 발견 상태를 PlayerPrefs로 영구 저장하는 정적 트래커.
///
/// 흐름:
///   OwlInteractable.Start() → Register(owlId)   (씬 로드마다 등록)
///   플레이어 발견 시         → MarkFound(owlId)  (PlayerPrefs 저장)
///   모두 발견 시             → PREFS_ALL = 1     (이스터에그 활성화)
///   EasterEggLetter.Start() → AllOwlsFound 확인 (편지 표시 여부)
/// </summary>
public static class OwlTracker
{
    private const string PREFS_PREFIX = "easter_owl_";
    private const string PREFS_ALL    = "easter_all_owls_found";

    // 현재 씬에 등록된 부엉이 ID 집합 (씬 로드마다 OwlInteractable이 채움)
    private static readonly HashSet<string> _registeredOwls = new HashSet<string>();

    // PlayerPrefs 결과 런타임 캐시 (씬 진입마다 초기화)
    private static readonly Dictionary<string, bool> _foundCache = new Dictionary<string, bool>();
    private static bool _allFoundCached;

    /// <summary>도메인 리로드(플레이 진입) 시 씬 등록 목록 및 캐시 초기화. PlayerPrefs는 유지.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession()
    {
        _registeredOwls.Clear();
        _foundCache.Clear();
        _allFoundCached = false;
    }

    // ── 공개 API ──────────────────────────────────────────────

    /// <summary>모든 부엉이가 발견됐는지 여부. PlayerPrefs 기반이므로 씬 재시작 후에도 유지.</summary>
    public static bool AllOwlsFound
    {
        get
        {
            if (_allFoundCached) return true;
            _allFoundCached = PlayerPrefs.GetInt(PREFS_ALL, 0) == 1;
            return _allFoundCached;
        }
    }

    /// <summary>씬 내 부엉이 오브젝트가 Start()에서 자신을 등록할 때 호출.</summary>
    public static void Register(string owlId)
    {
        if (!string.IsNullOrEmpty(owlId))
            _registeredOwls.Add(owlId);
    }

    /// <summary>플레이어가 부엉이를 발견했을 때 호출. 이미 발견된 경우 무시.</summary>
    public static void MarkFound(string owlId)
    {
        if (string.IsNullOrEmpty(owlId)) return;
        if (IsFound(owlId)) return;

        PlayerPrefs.SetInt(PREFS_PREFIX + owlId, 1);
        _foundCache[owlId] = true;
        Dbg.Log($"[OwlTracker] 부엉이 발견: {owlId} ({_registeredOwls.Count}마리 중)");

        CheckAllFound();
    }

    /// <summary>특정 부엉이를 이미 발견했는지 여부.</summary>
    public static bool IsFound(string owlId)
    {
        if (_foundCache.TryGetValue(owlId, out bool cached)) return cached;
        bool result = PlayerPrefs.GetInt(PREFS_PREFIX + owlId, 0) == 1;
        _foundCache[owlId] = result;
        return result;
    }

    // ── 내부 ──────────────────────────────────────────────────

    static void CheckAllFound()
    {
        if (_registeredOwls.Count == 0) return;

        foreach (string id in _registeredOwls)
        {
            if (!IsFound(id)) return;
        }

        // 전부 발견
        PlayerPrefs.SetInt(PREFS_ALL, 1);
        _allFoundCached = true;
        PlayerPrefs.Save();
        Dbg.Log("[OwlTracker] 모든 부엉이 발견 완료! 다음 시작 시 이스터에그 편지가 등장합니다.");
    }

    // ── 개발용 ────────────────────────────────────────────────

    /// <summary>Inspector 또는 개발 빌드에서 이스터에그 상태를 초기화합니다.</summary>
    public static void ResetAll()
    {
        foreach (string id in _registeredOwls)
        {
            PlayerPrefs.DeleteKey(PREFS_PREFIX + id);
            _foundCache.Remove(id);
        }
        PlayerPrefs.DeleteKey(PREFS_ALL);
        _allFoundCached = false;
        PlayerPrefs.Save();
        Dbg.Log("[OwlTracker] 이스터에그 상태 초기화 완료.");
    }
}
