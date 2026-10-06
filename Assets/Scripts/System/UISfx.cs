using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// UI 시스템음 — 결정 · 불가 · 저장 · 아이템 획득 · 목표 갱신 (2026-10-06 사용자 계획, 오모리 · 언더테일식).
///
/// <para>소리는 <c>AudioManager</c> 프리팹에 이름으로 등록돼 있다(<c>ui_*</c>). 지금은 절차 합성 임시음
/// (<c>Assets/Sound/_generated</c>)이며, 진짜 소리가 오면 프리팹의 clip 만 갈아끼운다 — 코드는 그대로다.</para>
///
/// <para><b>결정음은 버튼마다 배선하지 않는다.</b> 메뉴 버튼이 코드로 만들어지는 곳이 많아(MainMenuUI · 거래창 · 선택지)
/// 프리팹 배선으로는 빠진다. 대신 <see cref="Watcher"/> 가 선택되거나 눌리려는 UI 요소에
/// <see cref="UiSubmitSfx"/> 를 그때 붙인다. 소리는 그 요소가 실제로 제출 · 클릭을 받을 때 난다.</para>
///
/// ⚠ 다가가면 소리가 나는 식으로 쓰지 않는다 — 「상호작용 가능 표시를 띄우지 않는다」(C 1540)와 같은 효과가 된다.
///   누른 결과에만 낸다.
/// </summary>
public static class UISfx
{
    public const string ConfirmName   = "ui_confirm";
    public const string BuzzerName    = "ui_buzzer";
    public const string SaveName      = "ui_save";
    public const string ItemName      = "ui_item";
    public const string ObjectiveName = "ui_objective";

    public static void Confirm()   => Play(ConfirmName);
    public static void Buzzer()    => Play(BuzzerName);
    public static void Save()      => Play(SaveName);
    public static void Item()      => Play(ItemName);
    public static void Objective() => Play(ObjectiveName);

    /// <summary>UI 시스템음이 실제로 재생될 때 이름과 함께 발행된다. 배치 검증 · 이후 자막(소리 표시) 용.</summary>
    public static event System.Action<string> OnPlayed;

    static void Play(string name)
    {
        var am = AudioManager.Instance;
        if (am == null || !am.HasSound(name)) return;
        am.Play(name);
        OnPlayed?.Invoke(name);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        var go = new GameObject("UISfxWatcher");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<Watcher>();
    }

    /// <summary>
    /// 선택된 UI 요소 · 마우스를 누른 UI 요소에 <see cref="UiSubmitSfx"/> 를 붙인다.
    /// 누르는 프레임에 붙여도 클릭(떼는 순간)보다 먼저라 그 클릭부터 소리가 난다.
    /// </summary>
    [DefaultExecutionOrder(-1000)]   // EventSystem 이 같은 프레임의 입력을 처리하기 전에 붙인다
    class Watcher : MonoBehaviour
    {
        GameObject _lastSelected;
        static readonly System.Collections.Generic.List<RaycastResult> _hits = new();

        void Update()
        {
            var es = EventSystem.current;
            if (es == null) return;

            var sel = es.currentSelectedGameObject;
            if (sel != _lastSelected)
            {
                _lastSelected = sel;
                if (sel != null) Attach(sel);
            }

            if (Input.GetMouseButtonDown(0))
            {
                var ped = new PointerEventData(es) { position = Input.mousePosition };
                _hits.Clear();
                es.RaycastAll(ped, _hits);
                if (_hits.Count > 0)
                {
                    var target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(_hits[0].gameObject);
                    if (target != null) Attach(target);
                }
            }
        }

        static void Attach(GameObject go)
        {
            if (go.GetComponent<UiSubmitSfx>() != null) return;
            if (!UiSubmitSfx.IsPressable(go)) return;
            go.AddComponent<UiSubmitSfx>();
        }
    }
}
