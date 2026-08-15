using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 단검 키(keyDagger, 기본 F)를 공유하는 근접 상호작용 오브젝트의 중재자.
/// - 근접 오브젝트가 등록되어 있으면 DaggerFilterController(전역 현실 필터)는 키를 양보한다.
/// - 등록된 오브젝트가 여럿이면 플레이어 최근접 1개만 키를 소비한다.
/// 사용법: OnTriggerEnter2D 에서 Register(this), OnTriggerExit2D/OnDisable 에서 Unregister(this),
/// 키 처리 전에 IsClosest(this) 확인.
/// </summary>
public static class DaggerKeyRegistry
{
    static readonly List<MonoBehaviour> _nearby = new List<MonoBehaviour>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _nearby.Clear();

    public static void Register(MonoBehaviour owner)
    {
        if (owner != null && !_nearby.Contains(owner))
            _nearby.Add(owner);
    }

    public static void Unregister(MonoBehaviour owner) => _nearby.Remove(owner);

    /// <summary>파괴·비활성 등록자를 정리한 뒤 근접 오브젝트 존재 여부를 반환.</summary>
    public static bool HasNearby
    {
        get
        {
            Prune();
            return _nearby.Count > 0;
        }
    }

    /// <summary>candidate 가 플레이어 최근접 등록자인지. 플레이어를 못 찾으면 첫 등록자가 승리.</summary>
    public static bool IsClosest(MonoBehaviour candidate)
    {
        Prune();
        if (candidate == null || !_nearby.Contains(candidate)) return false;
        if (_nearby.Count == 1) return true;

        Transform player = FindPlayer();
        if (player == null) return _nearby[0] == candidate;

        MonoBehaviour closest = null;
        float best = float.MaxValue;
        foreach (var mb in _nearby)
        {
            float sqrDist = (mb.transform.position - player.position).sqrMagnitude;
            if (sqrDist < best) { best = sqrDist; closest = mb; }
        }
        return closest == candidate;
    }

    static void Prune()
    {
        for (int i = _nearby.Count - 1; i >= 0; i--)
            if (_nearby[i] == null || !_nearby[i].gameObject.activeInHierarchy)
                _nearby.RemoveAt(i);
    }

    static Transform FindPlayer()
    {
        if (PlayerStats.Instance != null) return PlayerStats.Instance.transform;
        var go = GameObject.FindGameObjectWithTag("Player");
        return go != null ? go.transform : null;
    }
}
