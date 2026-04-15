using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resources/Items/ 내 모든 ItemData를 최초 조회 시 한 번만 로드하여
/// Dictionary로 캐싱합니다. 에디터 설정 없이 자동으로 동작합니다.
/// </summary>
public static class ItemDatabase
{
    private static Dictionary<string, ItemData> _dict;

    static void EnsureLoaded()
    {
        if (_dict != null) return;
        _dict = new Dictionary<string, ItemData>(System.StringComparer.Ordinal);
        foreach (var item in Resources.LoadAll<ItemData>("Items"))
            if (item != null && !_dict.ContainsKey(item.name))
                _dict[item.name] = item;
    }

    /// <summary>에셋 파일명으로 ItemData를 O(1) 조회합니다.</summary>
    public static ItemData Find(string assetName)
    {
        EnsureLoaded();
        return _dict.TryGetValue(assetName, out ItemData result) ? result : null;
    }
}
