using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Assets/Resources/Recipes/ 폴더의 CombineRecipe 에셋을 자동 로드합니다.
/// </summary>
public static class CombineRecipeDatabase
{
    private static List<CombineRecipe> _recipes;

    static void EnsureLoaded()
    {
        if (_recipes != null) return;
        _recipes = new List<CombineRecipe>(Resources.LoadAll<CombineRecipe>("Recipes"));
    }

    /// <summary>두 아이템에 해당하는 레시피를 반환합니다. 없으면 null.</summary>
    public static CombineRecipe Find(ItemData a, ItemData b)
    {
        EnsureLoaded();
        foreach (var r in _recipes)
        {
            if (r == null || r.ingredientA == null || r.ingredientB == null || r.resultItem == null)
                continue;
            if ((r.ingredientA == a && r.ingredientB == b) ||
                (r.ingredientA == b && r.ingredientB == a))
                return r;
        }
        return null;
    }

    /// <summary>씬 재로드 시 캐시를 초기화합니다 (필요 시 호출).</summary>
    public static void ClearCache() => _recipes = null;
}
