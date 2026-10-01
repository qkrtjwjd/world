using UnityEngine;

/// <summary>
/// 그림자 토끼 — 월드에서 저장을 여는 매개다 (CLAUDE.md §8).
///
/// <para>슬롯을 코드가 고르지 않는다. <see cref="PauseSystem.OpenSave"/> 로 저장 패널을 열어
/// 사람이 슬롯 3개 중에서 고른다. 실제 저장과 「중단 저장 삭제」는 <c>SaveManager.SaveGame</c> 가 한다.</para>
///
/// <para>붙이는 법: 같은 오브젝트의 <c>InteractionTrigger.onInteract</c> 에 이 컴포넌트의
/// <see cref="Save"/> 를 연결한다. E키 판정·범위·프롬프트는 InteractionTrigger 가 이미 갖고 있다.</para>
///
/// <para>⚠ 정본 F-145 — 토끼는 <b>필터 무관·광원 무관</b>이다. SpriteRenderer 머티리얼을
/// Sprite-Unlit-Default 로 두어야 환상·현실에서 같게 보이고 주변 광원을 타지 않는다.</para>
///
/// <para><b>앞에 위험이 있는 동안만 있다</b>(C-13-2 · E-33-6) — 위험이 해소되면 사라지고, 지나쳤다는 이유로는 사라지지 않는다.
/// 어떤 위험 앞인지는 <see cref="danger"/> 로 고른다. 데모 배치(C-13-2 · 2026-10-01 사용자 결정): 집 마당 정문 앞 1 · 숲 쿠루 재회 앞 1.</para>
/// </summary>
public class SaveRabbit : MonoBehaviour
{
    /// <summary>이 토끼가 감지한 위험. 그 위험이 앞에 남아 있는 동안만 보인다.</summary>
    public enum Danger
    {
        /// <summary>항상 있다 — 조건 없이 둘 때(시험용).</summary>
        Always,
        /// <summary>집 마당 정문 앞 — 세라의 순찰 구역(마을). 현관문을 통과한 뒤부터, 숲에서 쿠루가 합류하기 전까지.</summary>
        Village,
        /// <summary>숲 쿠루 재회 앞 — 숲 전투 두 번(S#17 · S#19). 2차 전투를 이기기 전까지.</summary>
        ForestBattles,
    }

    [Tooltip("이 토끼가 앞에 둔 위험. 위험이 해소되면 사라진다(C-13-2).")]
    public Danger danger = Danger.Always;

    [Tooltip("저장 패널을 여는 PauseSystem. 비워 두면 씬에서 찾는다.")]
    public PauseSystem pauseSystem;

    Renderer[]           _renderers;
    Collider2D[]         _colliders;
    InteractionTrigger[] _triggers;
    bool                 _shown = true;

    /// <summary>앞에 위험이 남아 있는가.</summary>
    public static bool IsDangerAhead(Danger d)
    {
        switch (d)
        {
            case Danger.Village:       return GameState.isFrontDoorPassed && !GameState.isKuruJoined;
            case Danger.ForestBattles: return GameState.tutorialBattleStep < 2;
            default:                   return true;
        }
    }

    void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _colliders = GetComponentsInChildren<Collider2D>(true);
        _triggers  = GetComponentsInChildren<InteractionTrigger>(true);
        Apply(IsDangerAhead(danger));
    }

    // 오브젝트를 끄지 않고 그림 · 판정만 끈다 — 꺼 버리면 Update 가 돌지 않아 다시 나타날 수 없다(되감기 · 불러오기).
    void Update()
    {
        bool on = IsDangerAhead(danger);
        if (on != _shown) Apply(on);
    }

    void Apply(bool on)
    {
        _shown = on;
        foreach (var r in _renderers) if (r != null) r.enabled = on;
        foreach (var c in _colliders) if (c != null) c.enabled = on;
        foreach (var t in _triggers)  if (t != null) t.enabled = on;
    }

    /// <summary>InteractionTrigger.onInteract 에 연결한다.</summary>
    public void Save()
    {
        var ps = pauseSystem != null
            ? pauseSystem
            : FindAnyObjectByType<PauseSystem>(FindObjectsInactive.Include);

        if (ps == null)
        {
            Debug.LogWarning("[SaveRabbit] PauseSystem 을 찾지 못해 저장 패널을 열 수 없습니다. " +
                             "씬에 PauseSystem 이 있는지 확인하세요.", this);
            return;
        }

        ps.OpenSave();
    }
}
