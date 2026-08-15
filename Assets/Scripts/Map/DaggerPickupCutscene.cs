using System.Collections;
using UnityEngine;

/// <summary>
/// S#12 단검 획득 — 0.5초 현실 필터 강제 전환 + 토글 조작권 개방.
///
/// AtticBoxInteraction 이 S#11(라디오) 직후에 BeginCutscene() 을 호출합니다.
/// 상자 시퀀스 밖에서 단독으로 쓸 일이 있으면 InteractionTrigger.onInteract 에 연결해도 됩니다.
///
/// ⚠ 정본 규약 (2026-08-07 D 정본 S#12)
///   - 현실 → 환상 복귀는 **페이드가 아니라 컷**이다. "되돌아온 것이 아니라 끊긴 것처럼."
///     그래서 DaggerFilterController.SwitchTo*Forced() 를 쓴다. 이 두 메서드만 페이드 없이 즉시 전환한다.
///   - 강제 전환(컷)과 이후 플레이어가 직접 뽑았을 때의 전환(짧은 노이즈)은 처리가 달라야 한다.
///     그래서 여기서는 글리치를 걸지 않는다. 플레이어 조작 쪽 노이즈는
///     DaggerFilterController.SwitchToReality() 가 이미 담당한다.
///   - 색과 질감만 바뀌고 오브젝트 배치는 건드리지 않는다.
/// </summary>
public class DaggerPickupCutscene : MonoBehaviour
{
    public static DaggerPickupCutscene Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    [Header("Yarn 노드 이름 (S#12 대사)")]
    public string yarnNode = "House_Dagger_Flash";

    [Tooltip("현실 필터를 보여주는 시간(초). 정본 지정값 0.5초.")]
    public float realityFlashDuration = 0.5f;

    [Header("현실 오브젝트 (강제 전환 중 활성)")]
    [Tooltip("DaggerFilterController 가 씬을 훑어 처리하므로 보통 비워 둡니다. 개별 지정이 필요할 때만 채우세요.")]
    public RealityFilterObject[] realityFilterObjects;

    [Header("획득 아이템")]
    public ItemData daggerItem;

    [Header("필터 전환 순간의 고역 노이즈 (AudioManager 등록 이름). 비우면 무음")]
    public string sfxFilterCutName = "";

    /// <summary>단독 호출용 (InteractionTrigger.onInteract 등). 기다리지 않고 시작만 한다.</summary>
    public void BeginCutscene()
    {
        if (GameState.isDaggerAcquired) return;
        StartCoroutine(PlayRoutine());
    }

    /// <summary>
    /// 시퀀스 안에서 순서를 지켜 기다려야 할 때 쓴다 (AtticBoxInteraction 의 S#12).
    /// 이미 재생됐으면 즉시 끝난다.
    /// </summary>
    public IEnumerator PlayRoutine()
    {
        if (GameState.isDaggerAcquired) yield break;
        GameState.isDaggerAcquired = true;
        yield return StartCoroutine(PlayCutscene());
    }

    IEnumerator PlayCutscene()
    {
        var ctrl = YarnDialogue.LockPlayer();

        // ── 단검 획득 ────────────────────────────────────────
        if (daggerItem != null)
            InventoryManager.Instance?.AddItem(daggerItem);
        DaggerSystem.Instance?.Equip();

        // ── 0.5초 현실 — 컷 인 ───────────────────────────────
        // 전환 순간에만 짧은 고역 노이즈. 복귀할 때는 소리를 넣지 않는다(정본).
        if (!string.IsNullOrEmpty(sfxFilterCutName))
            AudioManager.Instance?.Play(sfxFilterCutName);

        foreach (var r in realityFilterObjects)
            if (r != null) r.SetFilter(true);

        DaggerFilterController.Instance?.SwitchToRealityForced();

        yield return new WaitForSeconds(realityFlashDuration);

        // ── 컷 아웃 (페이드 아님) ─────────────────────────────
        DaggerFilterController.Instance?.SwitchToFantasyForced();

        foreach (var r in realityFilterObjects)
            if (r != null) r.SetFilter(false);

        // ── S#12 대사 ────────────────────────────────────────
        if (!string.IsNullOrEmpty(yarnNode))
            yield return YarnDialogue.PlayAndWait(yarnNode, false);

        // ── 토글 조작권 개방 ──────────────────────────────────
        // 여기서부터 F키가 먹는다. 다락방에서 뽑아보지 않고 그냥 내려가도 막지 않는다.
        GameState.isDaggerToggleUnlocked = true;
        HintManager.ShowHint("dagger_filter",
            $"[{SettingsManager.Instance?.keyDagger ?? KeyCode.F}] 키를 누르고 있으면 은빛 단검으로 현실을 볼 수 있습니다.", 5f);

        YarnDialogue.UnlockPlayer(ctrl);
    }
}
