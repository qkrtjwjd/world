using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 인벤토리 하단 아이템 상세 정보 UI.
/// - 슬롯 클릭 / Enter 키 → Show(item) 호출
/// - canFeed 아이템 두 개 선택 + 레시피 존재 시 결합 미리보기
/// - 결합 미리보기 상태에서 Enter → 결합 확정
/// </summary>
public class ItemDetailUI : MonoBehaviour
{
    public static ItemDetailUI Instance { get; private set; }

    [Header("UI 연결")]
    public GameObject panel;
    public Image      itemIcon;
    public TMP_Text   nameText;
    public TMP_Text   descriptionText;
    public TMP_Text   gradeText;
    public TMP_Text   quoteText;

    [SerializeField] private Button useButton;
    [SerializeField] private Button discardButton;

    private ItemData      _selected;
    private ItemData      _combinePartner;
    private CombineRecipe _pendingRecipe;
    private bool          _justShown;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // LayoutGroup 안에 있어도 grid 위치를 밀지 않도록 설정
        if (panel != null)
        {
            var le = panel.GetComponent<LayoutElement>() ?? panel.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
        }

        Hide();

        useButton?.onClick.AddListener(UseSelectedItem);
        discardButton?.onClick.AddListener(DiscardSelectedItem);
    }

    void Update()
    {
        if (panel == null || !panel.activeSelf) return;
        if (_justShown) { _justShown = false; return; }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (_pendingRecipe != null)
                ConfirmCombine();
            else if (_selected != null)
                UseSelectedItem();
        }
    }

    // ─────────────────────────────────────────────
    //  외부 호출
    // ─────────────────────────────────────────────

    /// <summary>슬롯 클릭/Enter 시 호출합니다.</summary>
    public void Show(ItemData item)
    {
        if (item == null) { Hide(); return; }

        // 이미 선택된 아이템을 다시 클릭 → 선택 해제(토글)
        if (_selected == item && _pendingRecipe == null)
        {
            Hide();
            return;
        }

        // 결합 미리보기 중 다른 슬롯 클릭 → 미리보기 취소 후 새 아이템 선택
        if (_pendingRecipe != null)
        {
            _pendingRecipe  = null;
            _combinePartner = null;
            _selected       = null;
        }

        // 첫 번째 선택 아이템이 있고 두 아이템 모두 canFeed → 결합 확인
        if (_selected != null && _selected != item && _selected.canFeed && item.canFeed)
        {
            var recipe = CombineRecipeDatabase.Find(_selected, item);
            if (recipe != null)
            {
                ShowCombinePreview(recipe, item);
                return;
            }
        }

        // 일반 선택
        _selected       = item;
        _pendingRecipe  = null;
        _combinePartner = null;
        DisplayItemInfo(item);
    }

    public void Hide()
    {
        _selected       = null;
        _combinePartner = null;
        _pendingRecipe  = null;
        if (panel != null) panel.SetActive(false);
    }

    // ─────────────────────────────────────────────
    //  내부
    // ─────────────────────────────────────────────

    void DisplayItemInfo(ItemData item)
    {
        SetIcon(item.CurrentIcon);
        if (nameText != null)        nameText.text        = item.DisplayName;
        if (descriptionText != null) descriptionText.text = BuildDescription(item);

        if (gradeText != null)
        {
            gradeText.gameObject.SetActive(true);
            gradeText.text  = GetGradeName(item.grade);
            gradeText.color = GetGradeColor(item.grade);
        }

        if (quoteText != null)
        {
            bool has = !string.IsNullOrEmpty(item.quote);
            quoteText.gameObject.SetActive(has);
            if (has) quoteText.text = item.quote;
        }

        _justShown = true;
        if (panel != null)
        {
            panel.transform.SetAsLastSibling();
            panel.SetActive(true);
        }
    }

    void ShowCombinePreview(CombineRecipe recipe, ItemData partner)
    {
        _pendingRecipe  = recipe;
        _combinePartner = partner;

        if (gradeText != null) gradeText.gameObject.SetActive(false);
        if (quoteText != null) quoteText.gameObject.SetActive(false);

        var result = recipe.resultItem;
        SetIcon(result != null ? result.CurrentIcon : null);

        if (nameText != null)
            nameText.text = result != null ? result.DisplayName : "???";

        if (descriptionText != null)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"{_selected.DisplayName}  +  {partner.DisplayName}");
            if (result != null)
                sb.AppendLine(BuildDescription(result));
            sb.Append("[Enter] 결합");
            descriptionText.text = sb.ToString();
        }

        _justShown = true;
        if (panel != null)
        {
            panel.transform.SetAsLastSibling();
            panel.SetActive(true);
        }
    }

    void ConfirmCombine()
    {
        if (_pendingRecipe == null || InventoryManager.Instance == null) return;

        InventoryManager.Instance.RemoveItem(_pendingRecipe.ingredientA);
        InventoryManager.Instance.RemoveItem(_pendingRecipe.ingredientB);

        if (!InventoryManager.Instance.AddItem(_pendingRecipe.resultItem))
            Debug.LogWarning("[ItemDetailUI] 결합 결과 아이템 추가 실패 (인벤토리 가득).");

        Hide();
    }

    void UseSelectedItem()
    {
        if (_selected == null) return;

        // 비전투 상황: 이미 사용한 아이템이면 차단 (차단 메시지는 TryUseOutsideBattle 내부에서 처리)
        bool isInBattle = BattleSystem.IsActive || HackSlashCombatManager.IsActive;
        if (!isInBattle && ItemUseTracker.Instance != null)
        {
            if (!ItemUseTracker.Instance.TryUseOutsideBattle(_selected))
                return;
        }

        // 사용 대사 표시
        string dialogue = _selected.LocalizedUseDialogue;
        if (!string.IsNullOrEmpty(dialogue))
            ItemNotificationUI.Instance?.ShowDialogue(dialogue);

        // 설명란(BuildAutoStats)과 동일하게 씬 종류에 따라 환상/현실 효과 선택
        bool isRealityScene = SceneNames.IsRealityScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        var effect = isRealityScene ? _selected.realityEffect : _selected.fantasyEffect;

        // 특수 시각/시스템 효과 (realityEffect.specialEffectCode 기준)
        ItemEffectHandler.Instance?.HandleEffect(effect.specialEffectCode);

        // 버프/디버프 적용
        BuffManager.Instance?.AddBuffs(effect.buffs);

        if (PlayerStats.Instance != null)
        {
            if (effect.healthChange > 0)       PlayerStats.Instance.RecoverHealth(effect.healthChange);
            else if (effect.healthChange < 0)  PlayerStats.Instance.TakeDamage(-effect.healthChange);

            if (effect.mentalChange > 0)       PlayerStats.Instance.RecoverMental(effect.mentalChange);
            else if (effect.mentalChange < 0)  PlayerStats.Instance.AddTrauma(-effect.mentalChange);

            // 인형화: 플레이어에게 수치 비표시, 내부 적용만
            if (effect.puppetizationChange > 0)      PlayerStats.Instance.AddPuppetization(effect.puppetizationChange);
            else if (effect.puppetizationChange < 0) PlayerStats.Instance.ReducePuppetization(-effect.puppetizationChange);
        }
        else
        {
            Debug.LogWarning("[ItemDetailUI] PlayerStats 를 찾을 수 없어 효과를 적용하지 못했습니다.");
        }

        InventoryManager.Instance?.RemoveItem(_selected);
        Hide();
    }

    void DiscardSelectedItem()
    {
        if (_selected == null) return;

        // 전투 중 버리기 차단
        if (BattleSystem.IsActive || HackSlashCombatManager.IsActive)
        {
            ItemNotificationUI.Instance?.Show("전투 중에는 아이템을 버릴 수 없습니다.");
            return;
        }

        // 버릴 수 없는 아이템 체크
        if (_selected.isUndroppable)
        {
            string msg = _selected.LocalizedUndiscardableDialogue;
            ItemNotificationUI.Instance?.Show(
                string.IsNullOrEmpty(msg)
                ? $"'{_selected.DisplayName}'은(는) 버릴 수 없습니다."
                : msg);
            return;
        }

        // 버리기 실행 + "X를 버렸습니다." 알림 (1.5초)
        string itemName = _selected.DisplayName;
        InventoryManager.Instance?.RemoveItem(_selected);
        ItemNotificationUI.Instance?.ShowDiscard(itemName);
        Hide();
    }


    void SetIcon(Sprite sprite)
    {
        if (itemIcon == null) return;
        itemIcon.sprite  = sprite;
        itemIcon.enabled = sprite != null;
    }

    // ─────────────────────────────────────────────
    //  설명 빌더
    // ─────────────────────────────────────────────

    static string BuildDescription(ItemData item)
    {
        var sb = new System.Text.StringBuilder();

        string autoStats = BuildAutoStats(item);
        if (!string.IsNullOrEmpty(autoStats))
            sb.Append(autoStats);

        if (!string.IsNullOrEmpty(item.CurrentDescription))
        {
            if (sb.Length > 0) sb.Append("\n");
            sb.Append(item.CurrentDescription);
        }

        return sb.ToString();
    }

    static string BuildAutoStats(ItemData item)
    {
        bool isReality = SceneNames.IsRealityScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        return isReality
            ? BuildEffectString(item.realityEffect)
            : BuildEffectString(item.fantasyEffect);
    }

    static string BuildEffectString(ItemEffect effect)
    {
        var parts = new List<string>();

        if (effect.healthChange != 0)
            parts.Add($"체력 {(effect.healthChange > 0 ? "+" : "")}{effect.healthChange:0.##}");
        if (effect.mentalChange != 0)
            parts.Add($"멘탈 {(effect.mentalChange > 0 ? "+" : "")}{effect.mentalChange:0.##}");
        // 인형화 수치는 플레이어에게 표시하지 않음

        if (effect.buffs != null)
        {
            foreach (var buff in effect.buffs)
            {
                if (buff.type == BuffType.None) continue;
                string bname = GetBuffKoreanName(buff.type);
                parts.Add(buff.duration > 0 ? $"{bname}({buff.duration:0.#}s)" : bname);
            }
        }

        return string.Join(", ", parts);
    }

    static string GetGradeName(ItemGrade grade)
    {
        switch (grade)
        {
            case ItemGrade.Rare:   return "희귀";
            case ItemGrade.Hero:   return "영웅";
            case ItemGrade.Legend: return "전설";
            default:               return "일반";
        }
    }

    static Color GetGradeColor(ItemGrade grade)
    {
        switch (grade)
        {
            case ItemGrade.Rare:   return new Color(0.3f, 0.5f, 1f);
            case ItemGrade.Hero:   return new Color(0.6f, 0.2f, 0.9f);
            case ItemGrade.Legend: return new Color(1f, 0.8f, 0f);
            default:               return Color.white;
        }
    }

    static string GetBuffKoreanName(BuffType type)
    {
        switch (type)
        {
            case BuffType.AttackUp:          return "공격력↑";
            case BuffType.DefenseUp:         return "방어력↑";
            case BuffType.SpeedUp:           return "속도↑";
            case BuffType.CritChanceUp:      return "크리티컬↑";
            case BuffType.HealOverTime:      return "지속회복";
            case BuffType.Shield:            return "보호막";
            case BuffType.Immunity:          return "피해면역";
            case BuffType.DebuffImmunity:    return "디버프면역";
            case BuffType.CooldownReduction: return "쿨타임↓";
            case BuffType.ReflectDamage:     return "피해반사";
            case BuffType.Taunt:             return "도발";
            case BuffType.AttackDown:        return "공격력↓";
            case BuffType.DefenseDown:       return "방어력↓";
            case BuffType.SpeedDown:         return "속도↓";
            case BuffType.CritChanceDown:    return "크리티컬↓";
            case BuffType.DamageOverTime:    return "지속피해";
            case BuffType.ShieldBreak:       return "보호막파괴";
            case BuffType.Vulnerable:        return "취약";
            case BuffType.CooldownIncrease:  return "쿨타임↑";
            case BuffType.Confusion:         return "혼란";
            case BuffType.Stun:              return "기절";
            default:                         return type.ToString();
        }
    }
}
