using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인벤토리 카테고리 탭 버튼을 관리합니다.
/// RightPanel/TopBar/TabBar 오브젝트에 부착하고 인스펙터에서 버튼을 연결하세요.
/// </summary>
public class InventoryTabController : MonoBehaviour
{
    [Header("탭 버튼 (All / Food / Tool / Weapon)")]
    public Button allButton;
    public Button foodButton;
    public Button toolButton;
    public Button weaponButton;

    [Header("탭 색상")]
    public Color activeColor   = Color.white;
    public Color inactiveColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    private Button _active;
    private bool   _initialized;

    void Awake()
    {
        allButton?.onClick.AddListener(   () => Select(allButton,    ItemCategory.All));
        foodButton?.onClick.AddListener(  () => Select(foodButton,   ItemCategory.Food));
        toolButton?.onClick.AddListener(  () => Select(toolButton,   ItemCategory.Tool));
        weaponButton?.onClick.AddListener(() => Select(weaponButton, ItemCategory.Weapon));
        _initialized = true;
    }

    void OnEnable()
    {
        if (!_initialized) return;
        // 인벤토리 재진입 시 현재 필터 탭 하이라이트 복원
        ItemCategory current = InventoryManager.Instance != null
            ? InventoryManager.Instance.CurrentCategory
            : ItemCategory.All;
        Button target = GetButtonForCategory(current);
        ResetAllColors();
        SetColor(target, activeColor);
        _active = target;
        InventoryManager.Instance?.FilterByCategory(current);
    }

    void ResetAllColors()
    {
        SetColor(allButton,    inactiveColor);
        SetColor(foodButton,   inactiveColor);
        SetColor(toolButton,   inactiveColor);
        SetColor(weaponButton, inactiveColor);
    }

    void Select(Button btn, ItemCategory cat)
    {
        SetColor(_active, inactiveColor);
        _active = btn;
        SetColor(btn, activeColor);
        InventoryManager.Instance?.FilterByCategory(cat);
    }

    Button GetButtonForCategory(ItemCategory cat)
    {
        switch (cat)
        {
            case ItemCategory.Food:   return foodButton;
            case ItemCategory.Tool:   return toolButton;
            case ItemCategory.Weapon: return weaponButton;
            default:                  return allButton;
        }
    }

    static void SetColor(Button b, Color c)
    {
        if (b == null) return;
        var img = b.GetComponent<Image>();
        if (img != null) img.color = c;
    }
}
