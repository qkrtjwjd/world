using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 세라 외출(S#04H) 뒤 집 시계 — 두 가지를 센다.
///
/// ① 마당 각설탕이 녹는다 (D-4-1 거실 창문 · E-72-3 · F-6).
///    외출 후 30초와 다락방 열쇠 획득 중 <b>늦은 쪽</b>에 녹음 플래그를 켠다. 녹음 플래그와
///    「녹은 뒤 대사 출력」 플래그를 따로 둔다. 거실 창 너머와 마당 맵의 각설탕은 같은 플래그로 바꿔 끼운다.
///    녹인 것은 세라다 — 쿠루에게 보내는 거절이다(E-73-4). 게임에서는 설명하지 않는다.
///
/// ② 다락방 미진입 1시간 (C-14-2-4 · E-72-4 · F-6).
///    다락방 문을 열지 않은 채 3600초가 지나면 세라가 돌아온다 — BE#01-a 를 건너뛰고 BE#01-b(소파)부터.
///    다락방 문을 여는 순간 멈춘다. 압박이 아니므로 화면에 시간을 표시하지 않고 조임 연출도 쓰지 않는다.
///    일시정지 메뉴에서는 멈춘다(timeScale 0 → deltaTime 0). 대화창 · 오버레이가 떠 있으면 닫힌 직후 발동한다.
///    복귀는 세라 외출 직후 — 외출하는 순간 되감기 지점(SaveManager.SaveRewindPoint)을 찍어 두고
///    BE#01 이 끝나면 그 지점으로 돌아간다. 아이템 · 인형화(22) · 녹음 · 1회 상호작용 · 획득 확인 · 시계가
///    전부 그 시점 값으로 돌아간다(F-6). 인형화 페널티는 없다.
///
/// 시계 값은 저장 슬롯 · 중단 저장에 함께 들어간다(F-5-5) — 불러올 때 0부터 세면 이 엔딩을 피할 수 있다.
/// </summary>
public class HouseClock : MonoBehaviour
{
    public const float MeltAfterOuting = 30f;
    public const float AtticDeadline   = 3600f;

    public const string FlagSugarMelted   = "house.sugarMelted";
    public const string FlagMeltLineShown = "house.meltLineShown";
    public const string FlagAtticOpened   = "house.atticOpened";
    /// <summary>외출 시점 되감기 지점의 표지. 이 플래그가 있어야 동적 플래그가 비어 보이지 않는다(SaveManager 복원 분기).</summary>
    public const string FlagOutingAnchored = "house.outingAnchored";

    /// <summary>세라 외출 뒤 흐른 시간(초).</summary>
    public static float Elapsed { get; private set; }

    /// <summary>다락방 열쇠를 집은 시각(외출 기준 초). 없으면 -1.</summary>
    public static float AtticKeyAt { get; private set; } = -1f;

    public static bool SugarMelted => Flag(FlagSugarMelted);

    /// <summary>각설탕이 녹았다. 거실 창 너머 · 마당 맵의 배치물이 듣고 바꿔 낀다.</summary>
    public static event System.Action OnSugarMelted;

    static bool _overtimeTriggered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Elapsed = 0f;
        AtticKeyAt = -1f;
        _overtimeTriggered = false;
        OnSugarMelted = null;
    }

    /// <summary>세이브 · 새 게임에서 시계를 되돌린다.</summary>
    public static void Restore(float elapsed, float atticKeyAt)
    {
        Elapsed = Mathf.Max(0f, elapsed);
        AtticKeyAt = atticKeyAt;
        _overtimeTriggered = false;
    }

    /// <summary>
    /// 세라가 나간 순간(S#04H 끝) — 시계를 0 에 두고 되감기 지점을 찍는다.
    /// 1시간 경로의 복귀 지점이다. S#11 에서 90초가 시작되면 그 지점이 「S#11 직후」로 덮인다.
    /// </summary>
    public static void BeginOuting()
    {
        Restore(0f, -1f);
        SetFlag(FlagOutingAnchored, true);
        SaveManager.Instance?.SaveRewindPoint();
        Dbg.Log("[집 시계] 세라 외출 — 시계 시작 · 되감기 지점 저장");
    }

    /// <summary>다락방 열쇠를 집었다(S#08). 각설탕이 녹는 시점에 쓴다.</summary>
    public static void NoteAtticKey()
    {
        if (AtticKeyAt < 0f) AtticKeyAt = Elapsed;
    }

    /// <summary>다락방 문을 열었다(S#09). 1시간 판정이 멈춘다.</summary>
    public static void NoteAtticOpened() => SetFlag(FlagAtticOpened, true);

    static bool Flag(string k) => GameStateManager.Instance != null && GameStateManager.Instance.GetFlag(k);
    static void SetFlag(string k, bool v) => GameStateManager.Instance?.SetFlag(k, v);

    // ── 집 씬에서만 돈다 ─────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded += (s, m) => EnsureFor(s);
        EnsureFor(SceneManager.GetActiveScene());
    }

    static void EnsureFor(Scene s)
    {
        if (s.name != SceneNames.Home) return;
        if (Object.FindAnyObjectByType<HouseClock>() != null) return;
        var go = new GameObject("HouseClock [Auto]");
        SceneManager.MoveGameObjectToScene(go, s);
        go.AddComponent<HouseClock>();
    }

    void Update()
    {
        if (!GameState.isSeraOut || GameState.isFrontDoorPassed) return;
        if (BadEndingDirector.IsPlaying) return;

        Elapsed += Time.deltaTime;

        // ① 각설탕 — 30초와 열쇠 획득 중 늦은 쪽
        if (!SugarMelted && AtticKeyAt >= 0f && Elapsed >= Mathf.Max(MeltAfterOuting, AtticKeyAt))
        {
            SetFlag(FlagSugarMelted, true);
            Dbg.Log($"[집 시계] 마당 각설탕이 녹았다 ({Elapsed:0.0}초)");
            OnSugarMelted?.Invoke();
        }

        // ② 다락방 미진입 1시간
        if (!_overtimeTriggered && !Flag(FlagAtticOpened) && Elapsed >= AtticDeadline)
        {
            _overtimeTriggered = true;
            StartCoroutine(OvertimeRoutine());
        }
    }

    IEnumerator OvertimeRoutine()
    {
        // 대화창 · 오버레이 · 읽는 물건이 떠 있으면 닫힌 직후에 — 대사 도중에 끊지 않는다(C-14-2-4).
        while (YarnDialogue.IsRunning || OverlayCut.IsOpen || ReadableOverlay.IsOpen)
            yield return null;
        Dbg.Log("[집 시계] 다락방에 가지 않은 채 1시간 — 세라가 돌아온다 (BE#01-b 부터)");
        yield return BadEndingDirector.PlayHouseOvertime();
    }
}
