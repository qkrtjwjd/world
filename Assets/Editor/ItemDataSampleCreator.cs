#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools/Create Sample Items 메뉴에서 샘플 ItemData 에셋 3개를 생성합니다.
/// 생성 위치: Assets/Resources/Items/
/// </summary>
public static class ItemDataSampleCreator
{
    [MenuItem("Tools/Create Sample Items")]
    static void Create()
    {
        string dir = "Assets/Resources/Items";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets/Resources", "Items");

        Make(dir, "허브차",
            ItemCategory.Food, ItemGrade.Normal,
            "들풀을 우려낸 따뜻한 차. 마음을 가라앉힌다.",
            "\"작은 온기가 큰 위로가 되기도 한다.\"",
            hp: 20f, mp: 10f);

        Make(dir, "마법 사과",
            ItemCategory.Food, ItemGrade.Rare,
            "한 입 베어 물면 이상한 달콤함이 퍼진다.",
            "\"현실인지 환상인지 모를 맛이다.\"",
            hp: 30f, mp: 20f);

        Make(dir, "빛의 검",
            ItemCategory.Weapon, ItemGrade.Hero,
            "순수한 빛으로 만들어진 검. 어둠을 가른다.",
            "\"두려움을 버릴 때 비로소 빛이 깃든다.\"",
            hp: 0f, mp: 0f,
            buffType: BuffType.AttackUp, buffVal: 30f, buffDur: 60f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ItemDataSampleCreator] 샘플 아이템 3개 생성 완료: " + dir);
    }

    static void Make(
        string dir, string name,
        ItemCategory cat, ItemGrade grade,
        string desc, string quote,
        float hp, float mp,
        BuffType buffType = BuffType.None,
        float buffVal = 0f, float buffDur = 0f)
    {
        string path = $"{dir}/{name}.asset";
        if (AssetDatabase.LoadAssetAtPath<ItemData>(path) != null)
        {
            Debug.Log($"[ItemDataSampleCreator] 이미 존재함, 건너뜀: {path}");
            return;
        }

        var d         = ScriptableObject.CreateInstance<ItemData>();
        d.itemName    = name;
        d.category    = cat;
        d.grade       = grade;
        d.description = desc;
        d.quote       = quote;
        d.canFeed     = cat == ItemCategory.Food;

        List<BuffInfo> buffs = null;
        if (buffType != BuffType.None)
            buffs = new List<BuffInfo> { new BuffInfo { type = buffType, value = buffVal, duration = buffDur } };

        var fx = new ItemEffect { healthChange = hp, mentalChange = mp, buffs = buffs };
        d.realityEffect = fx;
        d.fantasyEffect = fx;

        AssetDatabase.CreateAsset(d, path);
        Debug.Log($"[ItemDataSampleCreator] 생성됨: {path}");
    }
}
#endif
