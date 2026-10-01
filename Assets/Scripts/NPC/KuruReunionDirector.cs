using System.Collections;
using UnityEngine;

/// <summary>
/// S#16B~D 숲 입구 · 쿠루와의 재회 → 동행 시작 (개정 D 문단 731~796). 숲 입구 나무 그늘 밑의 쿠루에 붙인다.
///
///   16B 안녕 — 루를 따라가다 발소리가 멈추면 고정하고, 나무 그늘 밑의 쿠루가 화면에 들어오도록 짧게 스크롤한다(736).
///            쿠루가 루를 훑는 시선은 카메라로 따라가지 않는다. 손끝 오버레이 컷 0.5초(742 — 노드 안 show_overlay).
///   16C 아빠 물건 — 라디오가 나오는 순간 지속음을 끊는다(752). 고정, 컷을 잇지 않는다(754).
///   16D 둘 다 — 각설탕 획득(788) → 대화 종료 → <b>쿠루 동행 시작 / 숲 진입 개방 → S#17</b>(789).
///
///   ⚠ 쿠루는 루의 손을 잡지 않는다(748). 동행은 뒤따르는 것이다.
///   ⚠ 튜토리얼 늑대(S#17A)는 합류 뒤에만 발동한다 — TutorialEnemyTrigger 가 GameState.isKuruJoined 를 본다.
///
/// 2026-09-27 신설. 전에는 S#16B~D 가 배선돼 있지 않았다 — 노드 3개를 부르는 곳이 없었고 쿠루는 스크립트 없는 스프라이트였다.
/// 튜토리얼 늑대는 쿠루 바로 옆(1.3)에서 처음부터 살아 있어, 쿠루에게 다가가다 먼저 전투가 날 수 있었다.
/// </summary>
public class KuruReunionDirector : MonoBehaviour
{
    [Header("Yarn 노드")]
    public string yarnNode_16B = "Forest_Kuru_Greet";
    public string yarnNode_16C = "Forest_Kuru_Radio";
    public string yarnNode_16D = "Forest_Kuru_SugarCube";

    [Header("발동")]
    [Tooltip("루가 쿠루에게서 이 거리(월드 유닛) 안에 들어오면 발소리가 멈추고 재회가 시작된다.")]
    public float triggerRadius = 3.0f;

    [Header("카메라 (D 736)")]
    [Tooltip("고정 뒤 쿠루 쪽으로 짧게 스크롤하는 속도. 목적지는 루와 쿠루의 가운데 — 둘이 한 화면에 든다(754 · 778).")]
    public float scrollSpeed = 3f;

    [Header("동행")]
    [Tooltip("합류 뒤 루와 유지할 거리(CompanionFollow).")]
    public float followDistance = 0.84375f;
    [Tooltip("합류 뒤 따라오는 속도 — 루의 걸음(4/초)에 맞춘다.")]
    public float followSpeed = 4f;

    bool _running;

    void Start()
    {
        if (GameState.isKuruJoined) BeginFollowing();
    }

    void Update()
    {
        if (_running || GameState.isKuruJoined || YarnDialogue.IsRunning) return;
        var lu = FindLu();
        if (lu == null) return;
        if (Vector2.Distance(lu.transform.position, transform.position) > triggerRadius) return;
        StartCoroutine(ReunionRoutine(lu));
    }

    IEnumerator ReunionRoutine(ClearSky.SimplePlayerController lu)
    {
        _running = true;

        // ── 16B ── 발소리가 멈춘다 → 고정 → 쿠루가 화면에 들도록 짧게 스크롤.
        var ctrl = YarnDialogue.LockPlayer();
        FaceToward(lu.transform, transform.position);
        FaceToward(transform, lu.transform.position);
        var cd = CameraDirector.Instance;
        if (cd != null)
        {
            cd.Hold();
            Vector2 mid = ((Vector2)lu.transform.position + (Vector2)transform.position) * 0.5f;
            yield return cd.ScrollTo(mid, scrollSpeed);
        }
        yield return new WaitForSeconds(0.4f);

        yield return YarnDialogue.PlayAndWait(yarnNode_16B, false);

        // ── 16C ── 라디오가 나오는 순간 지속음을 끊는다(752).
        var entrance = FindAnyObjectByType<ForestEntranceDirector>();
        entrance?.StopDrone();
        yield return YarnDialogue.PlayAndWait(yarnNode_16C, false);

        // ── 16D ── 각설탕(노드 안 give_item) → 따라와.
        yield return YarnDialogue.PlayAndWait(yarnNode_16D, false);

        // 대화 종료 → 쿠루 동행 시작 / 숲 진입 개방 → S#17(789).
        GameState.isKuruJoined = true;
        BeginFollowing();
        cd?.Track(entrance != null ? entrance.forwardOffset : Vector2.zero);
        YarnDialogue.UnlockPlayer(ctrl);
        _running = false;
    }

    /// <summary>뒤따르기 시작한다. 손을 잡지 않는다(748) — 루가 간 길을 조금 뒤에서 밟는다.</summary>
    void BeginFollowing()
    {
        // 나무 그늘 밑에 서 있던 단단한 몸을 풀어야 루와 부딪히지 않는다.
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

        var follow = GetComponent<CompanionFollow>();
        if (follow == null) follow = gameObject.AddComponent<CompanionFollow>();   // Rigidbody2D 는 RequireComponent 로 붙는다
        follow.followDistance = followDistance;
        follow.moveSpeed      = followSpeed;
        follow.enabled        = true;
    }

    static ClearSky.SimplePlayerController FindLu() =>
        PlayerStats.Instance != null
            ? PlayerStats.Instance.GetComponent<ClearSky.SimplePlayerController>()
            : FindAnyObjectByType<ClearSky.SimplePlayerController>();

    /// <summary>0=아래 1=옆 2=위, 옆은 localScale.x 부호만 뒤집는다(양수가 왼쪽 — CLAUDE.md §11).</summary>
    static void FaceToward(Transform who, Vector2 target)
    {
        if (who == null) return;
        Vector2 d = target - (Vector2)who.position;
        if (d.sqrMagnitude < 0.0001f) return;
        int dir = Mathf.Abs(d.x) >= Mathf.Abs(d.y) ? 1 : (d.y > 0f ? 2 : 0);
        Vector3 s = who.localScale;
        s.x = dir == 1 ? Mathf.Abs(s.x) * (d.x > 0f ? -1f : 1f) : Mathf.Abs(s.x);
        who.localScale = s;
        var anim = who.GetComponent<Animator>();
        if (anim != null) anim.SetInteger("dir", dir);
    }
}
