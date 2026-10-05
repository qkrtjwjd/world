using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬마다 놓였지만 하나만 살아야 하는 UI 루트(목표 캔버스) — 처음 것을 DontDestroyOnLoad 로 남기고,
/// 뒤에 오는 씬의 사본은 다른 스크립트가 깨기 전에 꺼서 지운다(2026-10-05).
///
/// <para>목표 캔버스는 Home 에만 있어서 타이틀에서 마을 저장을 바로 불러오면 목표 패널이 없었다. MapScene 에도 두되,
/// Home 을 거쳐 오면 사본이 하나 더 생긴다 — 안의 ItemNotificationUI · ItemAcquisitionUI 가 각자 루트를 DontDestroyOnLoad 하고
/// 중복이면 자기 오브젝트만 지워서, 루트째 정리하는 쪽이 따로 있어야 했다.</para>
///
/// <para>⚠ 실행 순서를 앞당겨 둔다. 사본을 끈 다음에는 자식 스크립트의 Awake 가 돌지 않으므로 싱글톤이 사본을 잡지 않는다.</para>
/// </summary>
[DefaultExecutionOrder(-1000)]
public class PersistentRoot : MonoBehaviour
{
    [Tooltip("같은 키끼리 하나만 산다. 비우면 오브젝트 이름.")]
    public string key = "";

    static readonly Dictionary<string, PersistentRoot> _live = new Dictionary<string, PersistentRoot>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _live.Clear();

    string Key => string.IsNullOrEmpty(key) ? name : key;

    void Awake()
    {
        if (_live.TryGetValue(Key, out var first) && first != null && first != this)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }
        _live[Key] = this;
        if (transform.parent == null) DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (_live.TryGetValue(Key, out var first) && first == this) _live.Remove(Key);
    }
}
