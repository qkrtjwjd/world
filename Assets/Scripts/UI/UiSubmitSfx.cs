using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 이 UI 요소가 제출(키보드 결정) · 클릭을 받으면 결정음, 못 누르는 상태면 불가음을 낸다(<see cref="UISfx"/>).
///
/// <para>보통은 <c>UISfx</c> 가 실행 중에 알아서 붙인다. 프리팹에 직접 붙이는 것은
/// 소리를 바꿔야 할 때뿐이다 — 예: 거래창의 덮인 칸은 Button 이지만 무반응이어야 하므로 <see cref="Mode.Buzzer"/>.</para>
///
/// <para><b>판정은 앞 프레임의 상태로 한다.</b> 같은 오브젝트의 Button 이 먼저 이벤트를 받아 패널을 닫거나
/// interactable 을 끄면, 그 뒤에 불리는 여기서는 「못 누름」으로 보이기 때문이다.</para>
/// </summary>
public class UiSubmitSfx : MonoBehaviour, ISubmitHandler, IPointerClickHandler
{
    public enum Mode { Auto, Buzzer, Mute }

    [Tooltip("Auto: 누를 수 있으면 결정음 · 아니면 불가음 / Buzzer: 늘 불가음 / Mute: 소리 없음")]
    public Mode mode = Mode.Auto;

    Selectable _sel;
    bool _wasPressable = true;
    int  _lastFrame = -1;

    void Awake()
    {
        _sel = GetComponent<Selectable>();
        _wasPressable = _sel == null || _sel.IsInteractable();
    }

    void LateUpdate() => _wasPressable = _sel == null || _sel.IsInteractable();

    public void OnSubmit(BaseEventData e)          => Fire();
    public void OnPointerClick(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) Fire(); }

    void Fire()
    {
        if (_lastFrame == Time.frameCount) return;   // 한 프레임에 제출 · 클릭이 겹쳐도 한 번만
        _lastFrame = Time.frameCount;

        switch (mode)
        {
            case Mode.Mute:   return;
            case Mode.Buzzer: UISfx.Buzzer(); return;
            default:
                if (_wasPressable) UISfx.Confirm(); else UISfx.Buzzer();
                return;
        }
    }

    /// <summary>결정음을 붙일 대상인지. 슬라이더 · 스크롤바처럼 「누르는」 요소가 아닌 것은 뺀다.</summary>
    public static bool IsPressable(GameObject go)
    {
        var s = go.GetComponent<Selectable>();
        if (s == null) return false;
        return s is Button || s is Toggle || s is TMPro.TMP_Dropdown || s is Dropdown
            || s is Yarn.Unity.OptionItem;
    }
}
