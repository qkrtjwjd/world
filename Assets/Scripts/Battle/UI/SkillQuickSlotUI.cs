using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 스킬 퀵슬롯 UI. caster.equippedSkills 를 표시하고 클릭/단축키로 사용을 트리거합니다.
///
/// 입력:
/// - 마우스 클릭 → SkillSlotView.Button.onClick
/// - 키보드 1~4 (인스펙터에서 hotkeys 변경 가능)
///
/// 이벤트 구독:
/// - <see cref="BattleEvents.OnTurnStarted"/> → 턴 시작 시 쿨다운 갱신 표시
/// - <see cref="BattleEvents.OnUnitMPChanged"/> → MP 변동 시 사용 가능 여부 재평가
/// - <see cref="BattleEvents.OnSkillUsed"/> → 스킬 사용 직후 즉시 슬롯 갱신
/// </summary>
public class SkillQuickSlotUI : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("슬롯들이 생성될 부모 (예: HorizontalLayoutGroup 가진 패널).")]
    public Transform slotContainer;

    [Tooltip("SkillSlotView 컴포넌트를 가진 슬롯 프리팹.")]
    public GameObject slotPrefab;

    [Tooltip("클릭 시 BattleSystem.OnSkillButton 호출에 사용.")]
    public BattleSystem battleSystem;

    [Tooltip("표시할 유닛 (보통 플레이어). 미할당 시 BattleSystem 시작 시 자동 결정.")]
    public Unit caster;

    [Header("키보드 단축키")]
    [Tooltip("순서대로 슬롯 0~N에 매핑됩니다. 길이가 슬롯보다 적으면 일부만 단축키 사용 가능.")]
    public KeyCode[] hotkeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4 };

    private readonly List<SkillSlotView> _views = new List<SkillSlotView>();

    void OnEnable()
    {
        BattleEvents.OnTurnStarted   += OnTurnStarted;
        BattleEvents.OnUnitMPChanged += OnMPChanged;
        BattleEvents.OnSkillUsed     += OnSkillUsed;
    }

    void OnDisable()
    {
        BattleEvents.OnTurnStarted   -= OnTurnStarted;
        BattleEvents.OnUnitMPChanged -= OnMPChanged;
        BattleEvents.OnSkillUsed     -= OnSkillUsed;
    }

    /// <summary>외부에서 캐스터 변경 시 호출 후 Refresh.</summary>
    public void SetCaster(Unit u)
    {
        caster = u;
        Refresh();
    }

    /// <summary>슬롯을 모두 재생성. caster.equippedSkills 변경 후 호출.</summary>
    public void Refresh()
    {
        // 기존 슬롯 정리
        foreach (var v in _views)
            if (v != null) Destroy(v.gameObject);
        _views.Clear();

        if (caster == null || slotContainer == null || slotPrefab == null) return;

        for (int i = 0; i < caster.equippedSkills.Count; i++)
        {
            SkillData skill = caster.equippedSkills[i];
            if (skill == null) continue;

            var go   = Instantiate(slotPrefab, slotContainer);
            var view = go.GetComponent<SkillSlotView>();
            if (view == null)
            {
                Debug.LogWarning("[SkillQuickSlotUI] slotPrefab에 SkillSlotView가 없습니다.");
                Destroy(go);
                continue;
            }

            // 클로저로 스킬 캡처
            SkillData captured = skill;
            view.Bind(caster, captured, () => InvokeSkill(captured));
            _views.Add(view);
        }
    }

    void Update()
    {
        if (caster == null || _views.Count == 0) return;
        int len = Mathf.Min(hotkeys.Length, _views.Count);
        for (int i = 0; i < len; i++)
        {
            if (Input.GetKeyDown(hotkeys[i]))
            {
                _views[i].TryInvoke();
                break; // 동일 프레임 다중 입력 차단
            }
        }
    }

    void InvokeSkill(SkillData skill)
    {
        if (battleSystem == null)
        {
            Debug.LogWarning("[SkillQuickSlotUI] battleSystem이 연결되지 않았습니다.");
            return;
        }
        battleSystem.OnSkillButton(skill);
    }

    void OnTurnStarted(Unit u)
    {
        if (u == caster) RefreshAvailability();
    }

    void OnMPChanged(Unit u, int cur, int max)
    {
        if (u == caster) RefreshAvailability();
    }

    void OnSkillUsed(Unit cur, SkillData skill, DamageResult result)
    {
        if (cur == caster) RefreshAvailability();
    }

    void RefreshAvailability()
    {
        foreach (var v in _views)
            if (v != null) v.UpdateState();
    }
}
