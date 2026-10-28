using System.Collections;
using UnityEngine;

/// <summary>
/// 집_루의방_침대 (house_lu_room_bed) — 눕기 (D-4-1 · C-16-11 · F-6).
///
/// 이동 키로 침대에 올라가거나 조사하면 루가 눕는다. 누운 채 실제 시간으로
///   600초  눈을 감는다
///   900초  짧은 암전 뒤 이불을 덮고 있다
///   1200초 BE#01 침대 경로 — 암전 후 루의 방 저녁에서 BE#01-d(BadEndingDirector.PlayHouseBed)
/// 눈을 감은 뒤 · 이불을 덮은 뒤의 이동 입력은 졸린 눈으로 뜨게 하고(이불은 걷는다) 누운 채 0부터 다시 센다.
/// 눈을 뜬 상태의 이동 입력은 일어나며 0으로 돌린다.
///
/// 받는 구간은 세라 외출(S#04H) 뒤부터 다락방 상자를 열기 전까지다. 그 밖(외출 전 · 상자를 연 뒤 — S#12 90초 포함)에는
/// 조사 대상이 아니며(InspectableObject 를 꺼 둔다 — 조사 후보로 잡히지 않는다) 올라가지도 못하는 막힌 가구다
/// (D-4-1 · F-6 · 2026-10-28 사용자 확인 — 보조 문서의 「90초 중에도 눕기 가능」은 오류).
/// 일시정지 중에는 세지 않고(timeScale 0 → deltaTime 0) 화면에 시간을 표시하지 않는다.
/// 누워 있는 동안 1시간 판정은 멈춘다 — HouseClock 이 IsLying 을 본다(C-14-2-4).
///
/// 🔴 누운 자세 · 졸린 눈 · 이불 · 이불을 걷는 동작 스프라이트가 아직 없다. 지금은 침대 위 자리로 옮겨 정면을 보고
///   멈추는 것으로 대신한다(소파 앉기와 같은 방식). 그림이 오면 SetPose 에서 애니메이터를 건다.
/// </summary>
[RequireComponent(typeof(InspectableObject))]
public class LuRoomBed : MonoBehaviour, IInspectOverride
{
    public const float EyesCloseAt = 600f;
    public const float BlanketAt   = 900f;
    public const float BedEndingAt = 1200f;

    public enum Pose { Up, Awake, EyesClosed, Blanket }

    [Tooltip("누운 루가 놓이는 자리(침대 위).")]
    public Transform liePoint;

    [Tooltip("이동 키로 올라가기 — 침대 콜라이더에서 이 거리 안에서 침대 쪽으로 누르고 있으면 눕는다(월드 유닛).")]
    public float climbReach = 0.1f;
    [Tooltip("침대 쪽으로 이만큼(초) 누르고 있어야 눕는다. 스쳐 지나가다 눕지 않게 한다.")]
    public float climbHold = 0.15f;

    [Tooltip("15분 짧은 암전의 반쪽 길이(초).")]
    public float blinkFade = 0.35f;

    /// <summary>루가 침대에 누워 있다. 1시간 판정이 이 동안 멈춘다.</summary>
    public static bool IsLying => _instance != null && _instance._pose != Pose.Up;

    /// <summary>지금 누운 지 몇 초(검증용).</summary>
    public float LyingSeconds => _t;
    public Pose CurrentPose => _pose;

    static LuRoomBed _instance;

    Pose  _pose = Pose.Up;
    float _t;
    float _climb;
    bool  _busy;
    bool  _hadInput;
    Vector2 _standPoint;
    Collider2D   _bedCol;
    InspectableObject _obj;
    Collider2D[] _luCols;
    bool[]       _luColWas;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _instance = null;

    void Awake()
    {
        _instance = this;
        _obj = GetComponent<InspectableObject>();
        _bedCol = GetComponent<Collider2D>();
        foreach (var c in GetComponents<Collider2D>())
            if (!c.isTrigger) { _bedCol = c; break; }
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    /// <summary>눕기를 받는 구간 — 세라 외출 뒤 · 다락방 상자를 열기 전(D-4-1 · F-6).</summary>
    static bool InWindow =>
        GameState.isSeraOut && !GameState.isAtticBoxOpened && !GameState.isFrontDoorPassed
        && !BadEndingDirector.IsPlaying;

    public bool TryInspect(InspectableObject obj)
    {
        if (_pose == Pose.Up && InWindow && !_busy) LieDown();
        return true;
    }

    void Update()
    {
        // 구간 밖에서는 조사 후보에서 뺀다. 누워 있는 동안은 켜 둔다(일어날 때까지 이 물건이 루를 쥐고 있다).
        bool target = InWindow || _pose != Pose.Up;
        if (_obj != null && _obj.enabled != target) _obj.enabled = target;

        if (_busy) return;

        if (_pose == Pose.Up)
        {
            WatchClimb();
            return;
        }

        // 누운 채 구간이 끝나는 경로는 없지만(상자는 루의 방 밖), 배드 엔딩 · 씬 상태가 바뀌면 조용히 일어난다.
        if (!InWindow) { GetUp(silent: true); return; }

        // 이동 입력 — 누르는 순간 한 번만 받는다. 누른 채 누우면(올라가기) 떼기 전까지는 받지 않는다.
        bool input = MoveInput().sqrMagnitude > 0.01f;
        bool pressed = input && !_hadInput;
        _hadInput = input;
        if (pressed && Time.timeScale > 0f)
        {
            if (_pose == Pose.Awake) { GetUp(silent: false); return; }
            // 눈을 감은 뒤 · 이불을 덮은 뒤 — 졸린 눈으로 뜬다. 이불이면 걷는다. 누운 채 0부터.
            SetPose(Pose.Awake, sleepy: true);
            _t = 0f;
            return;
        }

        _t += Time.deltaTime;
        if (_pose == Pose.Awake && _t >= EyesCloseAt) SetPose(Pose.EyesClosed);
        else if (_pose == Pose.EyesClosed && _t >= BlanketAt) StartCoroutine(BlanketBlink());
        else if (_pose == Pose.Blanket && _t >= BedEndingAt) StartCoroutine(BedEnding());
    }

    // ── 이동 키로 올라가기 ───────────────────────────────────────────────
    void WatchClimb()
    {
        if (!InWindow || _bedCol == null || YarnDialogue.IsRunning
            || (PlayerInputLock.Instance != null && PlayerInputLock.Instance.IsLocked)
            || Time.timeScale == 0f) { _climb = 0f; return; }

        var lu = LuPose.Lu;
        if (lu == null) { _climb = 0f; return; }
        Vector2 p = lu.position;
        Vector2 edge = _bedCol.ClosestPoint(p);
        var luCol = lu.GetComponent<Collider2D>();
        float gap = luCol != null ? Physics2D.Distance(luCol, _bedCol).distance : Vector2.Distance(p, edge);
        Vector2 move = MoveInput();
        Vector2 toward = (Vector2)_bedCol.bounds.center - p;
        bool pushing = move.sqrMagnitude > 0.01f && Vector2.Dot(move.normalized, toward.normalized) > 0.3f;

        if (gap <= climbReach && pushing) _climb += Time.deltaTime;
        else _climb = 0f;

        if (_climb >= climbHold) { _climb = 0f; LieDown(); _hadInput = true; }
    }

    static Vector2 MoveInput() => new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

    // ── 눕기 · 일어나기 ─────────────────────────────────────────────────
    public void LieDown()
    {
        var lu = LuPose.Lu;
        if (lu == null || liePoint == null || _pose != Pose.Up) return;

        _standPoint = lu.position;
        PlayerInputLock.Instance?.Lock();

        // 침대 콜라이더가 밀어내지 않게 누워 있는 동안 루의 충돌을 끈다(BadEndingDirector 소파 자리와 같은 이유).
        _luCols = lu.GetComponents<Collider2D>();
        _luColWas = new bool[_luCols.Length];
        for (int i = 0; i < _luCols.Length; i++) { _luColWas[i] = _luCols[i].enabled; _luCols[i].enabled = false; }

        PlaceLu(liePoint.position);
        _t = 0f;
        _hadInput = MoveInput().sqrMagnitude > 0.01f;
        SetPose(Pose.Awake);
        Dbg.Log("[루의 방 침대] 눕는다");
    }

    void GetUp(bool silent)
    {
        if (_pose == Pose.Up) return;
        SetPose(Pose.Up);
        _t = 0f;
        RestoreLuColliders();
        if (!silent) PlaceLu(_standPoint);
        PlayerInputLock.Instance?.Unlock();
        Dbg.Log(silent ? "[루의 방 침대] 눕기 해제" : "[루의 방 침대] 일어난다");
    }

    /// <summary>배드 엔딩이 루를 데려가기 직전 — 자세 · 잠금 · 충돌을 되돌린다. 자리는 옮기지 않는다.</summary>
    public static void ReleaseForCutscene()
    {
        if (_instance != null) _instance.GetUp(silent: true);
    }

    void RestoreLuColliders()
    {
        if (_luCols == null) return;
        for (int i = 0; i < _luCols.Length; i++)
            if (_luCols[i] != null) _luCols[i].enabled = _luColWas[i];
        _luCols = null;
    }

    static void PlaceLu(Vector2 p)
    {
        var lu = LuPose.Lu;
        if (lu == null) return;
        var rb = lu.GetComponent<Rigidbody2D>();
        if (rb != null) { rb.linearVelocity = Vector2.zero; rb.position = p; }
        lu.position = new Vector3(p.x, p.y, lu.position.z);
        LuPose.Face(lu, Vector2.down);
    }

    void SetPose(Pose pose, bool sleepy = false)
    {
        var was = _pose;
        _pose = pose;
        // 🔴 그림 대기 — 누운 자세(눈 뜸 · 반쯤 뜬 졸린 눈 · 눈 감음) · 이불을 덮은 자세 · 이불을 걷는 동작.
        if (pose == Pose.EyesClosed) Dbg.Log("[루의 방 침대] 600초 — 눈을 감는다");
        if (pose == Pose.Blanket)    Dbg.Log("[루의 방 침대] 900초 — 이불을 덮고 있다");
        if (sleepy) Dbg.Log(was == Pose.Blanket ? "[루의 방 침대] 이불을 걷고 졸린 눈으로 뜬다 — 0부터"
                                               : "[루의 방 침대] 졸린 눈으로 뜬다 — 0부터");
    }

    IEnumerator BlanketBlink()
    {
        _busy = true;
        var tm = TransitionManager.Instance;
        if (tm != null) yield return tm.FadeToBlack(blinkFade);
        SetPose(Pose.Blanket);
        if (tm != null) yield return tm.FadeFromBlack(blinkFade);
        _hadInput = MoveInput().sqrMagnitude > 0.01f;
        _busy = false;
    }

    IEnumerator BedEnding()
    {
        _busy = true;
        Dbg.Log("[루의 방 침대] 1200초 — BE#01 침대 경로");
        GetUp(silent: true);
        yield return BadEndingDirector.PlayHouseBed();
        _busy = false;
    }

    /// <summary>검증용 — 누운 시간을 건너뛴다(배치 플레이모드에서 20분을 기다리지 않기 위해).</summary>
    public void DebugSkipTo(float seconds) => _t = seconds;
}
