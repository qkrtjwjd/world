using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HUD 캔버스(<c>Canvas.prefab</c> 루트) — 화면에 보이는 HUD 를 하나로 정한다(2026-10-05).
///
/// <para><b>왜 필요한가.</b> HUD 루트에 <see cref="GaugeManager"/>(PersistentSingleton)가 붙어 있어 처음 들어간 씬의 HUD 가
/// 캔버스째 DontDestroyOnLoad 로 남는다. 씬마다 HUD 를 따로 꾸며 두었으므로(Home: 게이지 막대 꺼짐 · MapScene: 광막 · 가장자리 그림)
/// 두 번째 씬부터는 HUD 가 둘 겹쳐 그려졌고, 두 HUD 의 <see cref="PauseSystem"/> 이 같은 인벤토리 패널을 한 프레임에
/// 열었다 닫아 I 키가 듣지 않았다(배치 실측 — 마을에서 PauseSystem 5개).</para>
///
/// <para><b>규칙.</b> 가장 최근에 깨어난 HUD(= 지금 씬에 놓인 HUD)만 보이고 입력을 받는다. 앞의 HUD 는 투명 · 입력 차단으로 남는다 —
/// 지우지 않는 것은 거기 붙은 <see cref="GaugeManager"/> 가 게이지 값과 이벤트 구독(전투 · 후처리 · 캐릭터 그림 등)을 쥐고 있어서다.
/// 대화 · 컷신 중에도 같은 방식으로 숨긴다(예전에는 HUD 루트를 꺼서 GaugeManager 코루틴까지 멈췄다).</para>
/// </summary>
[DefaultExecutionOrder(-500)]
[RequireComponent(typeof(Canvas))]
public class HudCanvas : MonoBehaviour
{
    static readonly List<HudCanvas> _all = new List<HudCanvas>();
    static bool _suppressed;

    /// <summary>지금 화면을 맡은 HUD. HUD 가 없는 씬(타이틀 등)이면 마지막 HUD.</summary>
    public static HudCanvas Current { get; private set; }

    /// <summary>입력을 받을 일시정지 · 인벤토리 처리기 — 지금 HUD 의 것 하나.</summary>
    public static PauseSystem CurrentPause => Current != null ? Current._pause : null;

    /// <summary>지금 HUD 가 화면에 보이는가(대화 · 컷신 중이면 false).</summary>
    public static bool CurrentShown => Current != null && Current._shown;

    CanvasGroup _group;
    PauseSystem _pause;
    bool        _shown = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _all.Clear();
        _suppressed = false;
        Current = null;
    }

    void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        _pause = GetComponent<PauseSystem>();

        _all.Remove(this);
        _all.Add(this);
        Current = this;
        ApplyAll();
    }

    void OnDestroy()
    {
        _all.Remove(this);
        if (Current == this) Current = _all.Count > 0 ? _all[_all.Count - 1] : null;
        ApplyAll();
    }

    // 대화가 시작 · 끝나는 것을 따라간다. 대사창이 HUD 를 가리지 않게 하던 ObjectiveManager.HideHUD 가
    // 없는 씬(타이틀에서 마을 저장을 바로 불러온 경우 등)에서도 같은 동작이 나도록 대화 상태를 직접 본다.
    void LateUpdate() => Apply();

    /// <summary>컷신 · 대화 동안 HUD 를 숨긴다(<see cref="ObjectiveManager.HideHUD"/>).</summary>
    public static void SetSuppressed(bool suppressed)
    {
        _suppressed = suppressed;
        ApplyAll();
    }

    static void ApplyAll()
    {
        for (int i = _all.Count - 1; i >= 0; i--)
            if (_all[i] == null) _all.RemoveAt(i);
        foreach (var h in _all) h.Apply();
    }

    void Apply()
    {
        bool show = this == Current && !_suppressed && !YarnDialogue.IsRunning;
        if (show == _shown && _group.alpha == (show ? 1f : 0f)) return;
        _shown = show;
        _group.alpha          = show ? 1f : 0f;
        _group.blocksRaycasts = show;
        _group.interactable   = show;
    }
}
