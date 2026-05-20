using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 단일 책임 HP바 갱신 컴포넌트.
/// <see cref="BattleEvents.OnUnitDamaged"/> / <see cref="BattleEvents.OnUnitDied"/> 를 구독하여
/// 바인딩된 <see cref="Unit"/> 의 변화에만 반응합니다.
///
/// 기존의 <c>Unit.hpSlider</c> 직접 참조 + <c>RefreshSlider()</c> 직접 호출 패턴을 대체합니다.
/// 같은 슬라이더가 두 경로로 동시에 갱신되더라도 결과는 동일하므로 점진적 마이그레이션이 가능합니다.
/// </summary>
public class HPBarController : MonoBehaviour
{
    [Tooltip("바인딩될 유닛. SetTarget()으로 런타임 주입 가능.")]
    public Unit target;

    [Tooltip("기본 슬라이더. 즉시 값을 갱신합니다 (애니메이션 없음).")]
    public Slider plainSlider;

    [Tooltip("부드러운 HP바 (선택). 있으면 plainSlider 대신 이 컴포넌트로 갱신.")]
    public SmoothHPBar smoothBar;

    [Tooltip("사망 시 게임오브젝트를 비활성화할지. false면 슬라이더만 0으로 둠.")]
    public bool deactivateOnDeath = true;

    void OnEnable()
    {
        BattleEvents.OnUnitDamaged += OnDamaged;
        BattleEvents.OnUnitDied    += OnDied;
        // 인스펙터로 미리 target이 할당된 경우 초기 동기화
        if (target != null) Apply(target.currentHP, target.maxHP, target.unitLevel);
    }

    void OnDisable()
    {
        BattleEvents.OnUnitDamaged -= OnDamaged;
        BattleEvents.OnUnitDied    -= OnDied;
    }

    /// <summary>새 유닛에 바인딩하고 초기 HP를 표시합니다.</summary>
    public void SetTarget(Unit u)
    {
        target = u;
        if (u == null) return;

        if (smoothBar != null)
            smoothBar.Init(u.maxHP, u.currentHP, u.unitLevel);

        if (plainSlider != null)
        {
            plainSlider.minValue = 0f;
            plainSlider.maxValue = u.maxHP;
            plainSlider.value    = u.currentHP;
        }
    }

    void OnDamaged(Unit u, DamageResult result)
    {
        if (u != target || target == null) return;
        Apply(target.currentHP, target.maxHP, target.unitLevel);
    }

    void OnDied(Unit u)
    {
        if (u != target) return;
        Apply(0, target != null ? target.maxHP : 1, target != null ? target.unitLevel : 1);
        if (deactivateOnDeath) gameObject.SetActive(false);
    }

    void Apply(int current, int max, int level)
    {
        if (smoothBar != null)
        {
            smoothBar.SetHP(current, level);
        }
        else if (plainSlider != null)
        {
            // max 변동 가능성 (레벨업 등) 대비
            if (!Mathf.Approximately(plainSlider.maxValue, max)) plainSlider.maxValue = max;
            plainSlider.value = current;
        }
    }
}
