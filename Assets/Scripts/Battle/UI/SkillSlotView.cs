using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 스킬 퀵슬롯의 개별 슬롯. 아이콘, 쿨다운 라디얼 오버레이, 회색(사용불가) 표시, 클릭 처리.
/// SkillQuickSlotUI 가 프리팹 인스턴스를 만들고 Bind() 로 연결합니다.
/// </summary>
[RequireComponent(typeof(Button))]
public class SkillSlotView : MonoBehaviour
{
    [Tooltip("스킬 아이콘 표시. SkillData에 icon 필드가 없는 현 시점에는 비워두어도 됨.")]
    public Image iconImage;

    [Tooltip("스킬 이름 표시. SkillData.displayName이 들어갑니다.")]
    public TMP_Text nameText;

    [Tooltip("쿨다운 라디얼 오버레이. Image.fillAmount 0~1로 표시됨. (Filled type, Radial)")]
    public Image cooldownOverlay;

    [Tooltip("쿨다운 잔여 턴 수 텍스트 (선택).")]
    public TMP_Text cooldownText;

    [Tooltip("MP 부족 / 쿨다운 중일 때 적용할 회색 틴트.")]
    public Color disabledTint = new Color(0.5f, 0.5f, 0.5f, 1f);

    [Tooltip("사용 가능 시 적용할 정상 틴트.")]
    public Color enabledTint = Color.white;

    private Button     _button;
    private Unit       _caster;
    private SkillData  _skill;
    private Action     _onClick;

    void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(OnClick);
    }

    /// <summary>SkillQuickSlotUI 가 호출. 캐스터/스킬/클릭 핸들러를 바인딩.</summary>
    public void Bind(Unit caster, SkillData skill, Action onClick)
    {
        _caster  = caster;
        _skill   = skill;
        _onClick = onClick;

        // 아이콘 (SkillData에 icon이 없으면 비활성)
        if (iconImage != null) iconImage.enabled = false;

        // 이름 — 아이콘이 없는 현 시점에는 슬롯을 구분하는 유일한 단서
        if (nameText != null) nameText.text = skill != null ? skill.displayName : "";

        UpdateState();
    }

    public SkillData Skill => _skill;

    /// <summary>현재 상태(MP, 쿨다운)에 따라 인터랙션/틴트/오버레이 갱신.</summary>
    public void UpdateState()
    {
        if (_skill == null || _caster == null)
        {
            if (_button != null) _button.interactable = false;
            return;
        }

        int  remaining = _caster.GetCooldown(_skill);
        bool canUse    = SkillExecutor.CanUse(_caster, _skill, remaining);

        if (_button != null)
            _button.interactable = canUse;

        if (iconImage != null)
            iconImage.color = canUse ? enabledTint : disabledTint;

        // 아이콘이 꺼져 있는 동안에는 이름 색이 사용 가능 여부를 보여주는 유일한 표시
        if (nameText != null)
            nameText.color = canUse ? enabledTint : disabledTint;

        if (cooldownOverlay != null)
        {
            if (remaining > 0 && _skill.cooldownTurns > 0)
            {
                cooldownOverlay.gameObject.SetActive(true);
                cooldownOverlay.fillAmount = (float)remaining / _skill.cooldownTurns;
            }
            else
            {
                cooldownOverlay.gameObject.SetActive(false);
            }
        }

        if (cooldownText != null)
            cooldownText.text = remaining > 0 ? remaining.ToString() : "";
    }

    /// <summary>키보드 단축키에서 호출. interactable 체크 후 onClick 실행.</summary>
    public void TryInvoke()
    {
        if (_button != null && _button.interactable)
            OnClick();
    }

    void OnClick()
    {
        _onClick?.Invoke();
    }
}
