using UnityEngine;

/// <summary>
/// 아이템 결합 레시피. Assets/Resources/Recipes/ 폴더에 저장하세요.
/// [Asset Menu] Item → Combine Recipe 로 생성 가능합니다.
/// </summary>
[CreateAssetMenu(fileName = "New Recipe", menuName = "Item/Combine Recipe")]
public class CombineRecipe : ScriptableObject
{
    [Header("재료 (순서 무관)")]
    public ItemData ingredientA;
    public ItemData ingredientB;

    [Header("결과물")]
    public ItemData resultItem;
}
