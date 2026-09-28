using System.Collections;
using UnityEngine;

/// <summary>
/// S#16A 마을 담벼락 → 숲 입구 · 낮 (개정 D 문단 712~730). 마을 돌담의 틈 「숲문」에 붙인다.
///
///   · [BGM] 마을 BGM 이 담벼락을 지나는 지점에서 <b>끊긴다</b>(페이드 없음). 이후 아주 얇은 지속음 하나만 남는다.
///     중간에 0.5초 무음 구간을 한 번 만들고, 거기에 딱 소리를 맞춘다(715 · 725).
///   · [CAM] 루를 따라가되 진행 방향 쪽으로 치우친다. 담벼락을 지나는 순간 줌 없이 진행 방향으로 몇 타일 앞서
///     스크롤했다가 루에게 돌아온다. 뒤쪽은 한 프레임도 비추지 않는다(717).
///   · [SFX] 딱 — 한 번. 루가 손을 내려다보고, 아무 말 없이 다시 앞을 본다. 대사도 독백도 붙이지 않는다(724 · 725).
///   · 압박 해제(721 · C-14-4) — 마을에는 가장자리 어두워짐 · 저음을 걸지 않기로 확정돼 있어(C-14 채택) 풀 것이 없다.
///   · 「뒤를 돌아보면 안 된다」는 루의 판단이지 게임의 금지가 아니다 — 뒤로 걷는 것은 막지 않는다(729).
///
/// 2026-09-27 신설. 전에는 S#16A 가 배선돼 있지 않았다 — ForestKnockTrigger 는 씬에 없었고
/// Forest_Entrance 노드(주석뿐)를 부르는 곳도 없었다. 「숲문」은 콜라이더 없는 빈 오브젝트였다.
///
/// 소리 이름은 전부 비어 있으면 무음이다(오디오 파일 미제작 — 수동작업 4).
/// </summary>
public class ForestEntranceDirector : MonoBehaviour
{
    [Header("소리 (AudioManager 등록 이름. 비우면 무음)")]
    [Tooltip("담벼락 너머에 남는 아주 얇은 지속음(루프).")]
    public string forestDroneLoopName = "";
    [Tooltip("숲 안쪽에서 나뭇잎을 밟는 소리 — 규칙적이고 서두르지 않는다(716 · 726).")]
    public string distantStepsName = "";
    [Tooltip("딱 — 도자기 손가락(S#13 과 같은 소리).")]
    public string knockSfxName = "ceramic_tap";

    [Header("카메라 (D 717)")]
    [Tooltip("숲에서 카메라가 루보다 앞서는 방향·거리(월드 유닛). 숲길은 남쪽으로 내려가 남서쪽으로 이어진다(배치 실측).")]
    public Vector2 forwardOffset = new Vector2(-1.0f, -1.5f);
    [Tooltip("담벼락을 지나는 순간 앞서 스크롤하는 거리(월드 유닛 = 타일).")]
    public float leadScrollTiles = 4f;
    [Tooltip("⚠ 루의 걸음(4/초 · 달리기 7.2/초)보다 충분히 빨라야 한다 — 느리면 루가 카메라를 앞질러 뒤쪽이 비친다(2026-09-27 실측, 4/초일 때).")]
    public float leadScrollSpeed = 12f;
    [Tooltip("앞서 본 자리에 머무는 시간(초). 그 뒤 루에게 돌아온다. 길면 루가 따라잡는다.")]
    public float leadHold = 0.3f;

    [Header("딱 (D 724 · 725)")]
    [Tooltip("담벼락을 지난 뒤 지속음이 0.5초 끊기기까지(초).")]
    public float silenceDelay = 4f;
    public float silenceSeconds = 0.5f;
    [Tooltip("루가 손을 내려다보는 시간(초).")]
    public float lookDownSeconds = 0.9f;

    bool _passed;
    bool _inForest;
    bool _droneStopped;

    /// <summary>S#16C — 라디오가 나오는 순간 지속음을 끊는다(D 752). 이후 다시 켜지 않는다.</summary>
    public void StopDrone()
    {
        _droneStopped = true;
        StopLoopIfNamed(forestDroneLoopName);
    }
    Collider2D _col;

    void Awake()
    {
        // 「숲문」은 콜라이더 없는 스프라이트였다. 그림 크기 그대로 통과 판정을 건다.
        _col = GetComponent<Collider2D>();
        if (_col == null)
        {
            var box = gameObject.AddComponent<BoxCollider2D>();
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null) box.size = sr.sprite.bounds.size;
            _col = box;
        }
        _col.isTrigger = true;
    }

    float WallBottomY => _col != null ? _col.bounds.min.y : transform.position.y;

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || other.isTrigger) return;

        // 남쪽(숲)으로 빠져나갔는가, 북쪽(마을)으로 되돌아갔는가.
        bool south = other.bounds.center.y < WallBottomY + 0.01f;
        if (south && !_inForest)
        {
            _inForest = true;
            CameraDirector.Instance?.Track(forwardOffset);
            if (!_passed) { _passed = true; StartCoroutine(PassRoutine(other.transform)); }
        }
        else if (!south && _inForest)
        {
            _inForest = false;
            CameraDirector.Instance?.Track();   // 마을로 돌아가면 치우침을 푼다
        }
    }

    IEnumerator PassRoutine(Transform lu)
    {
        // 마을 BGM 이 뚝 끊긴다. 페이드하지 않는다.
        AudioManager.Instance?.StopAllBGM();
        PlayLoopIfNamed(forestDroneLoopName);

        // 줌 없이 진행 방향으로 몇 타일 앞서 스크롤했다가 루에게 돌아온다. 뒤쪽은 비추지 않는다.
        var cd = CameraDirector.Instance;
        var cam = Camera.main;
        if (cd != null && cam != null && leadScrollTiles > 0f && lu != null)
        {
            // 카메라 자리가 아니라 루를 기준으로 앞선다 — 걷는 루보다 늘 앞에 있어야 뒤쪽이 비치지 않는다.
            Vector2 dir = forwardOffset.sqrMagnitude > 0.0001f ? forwardOffset.normalized : Vector2.down;
            yield return cd.ScrollTo((Vector2)lu.position + forwardOffset + dir * leadScrollTiles, leadScrollSpeed);
            yield return new WaitForSeconds(leadHold);
            // 루에게 돌아올 때도 스크롤로 — 바로 추적으로 넘기면 2유닛을 한순간에 건너뛰어 컷처럼 보였다(실측).
            if (_inForest && lu != null)
                yield return cd.ScrollTo((Vector2)lu.position + forwardOffset, leadScrollSpeed * 0.5f);
            if (_inForest) cd.Track(forwardOffset);
        }

        PlayIfNamed(distantStepsName);

        // 지속음이 0.5초 끊기는 순간 딱 — 루가 손을 내려다보고, 아무 말 없이 다시 앞을 본다.
        yield return new WaitForSeconds(silenceDelay);
        StopLoopIfNamed(forestDroneLoopName);
        PlayIfNamed(knockSfxName);
        yield return LookAtHand(lu);
        yield return new WaitForSeconds(Mathf.Max(0f, silenceSeconds - lookDownSeconds));
        if (!_droneStopped) PlayLoopIfNamed(forestDroneLoopName);
    }

    /// <summary>손을 내려다보는 동안만 발을 멈춘다. 방향은 원래대로 돌려놓는다(0=아래 · 옆은 localScale.x 부호).</summary>
    IEnumerator LookAtHand(Transform lu)
    {
        var ctrl = lu != null ? lu.GetComponent<ClearSky.SimplePlayerController>() : null;
        var anim = lu != null ? lu.GetComponent<Animator>() : null;
        if (ctrl == null || anim == null) { yield return new WaitForSeconds(lookDownSeconds); yield break; }

        int prevDir = anim.GetInteger("dir");
        Vector3 prevScale = lu.localScale;
        var locked = YarnDialogue.LockPlayer();
        anim.SetBool("isRun", false);
        anim.SetInteger("dir", 0);
        Vector3 s = lu.localScale; s.x = Mathf.Abs(s.x); lu.localScale = s;

        yield return new WaitForSeconds(lookDownSeconds);

        anim.SetInteger("dir", prevDir);
        lu.localScale = prevScale;
        YarnDialogue.UnlockPlayer(locked);
    }

    void PlayIfNamed(string n)       { if (!string.IsNullOrEmpty(n)) AudioManager.Instance?.Play(n); }
    void PlayLoopIfNamed(string n)   { if (!string.IsNullOrEmpty(n)) AudioManager.Instance?.PlayLoop(n); }
    void StopLoopIfNamed(string n)   { if (!string.IsNullOrEmpty(n)) AudioManager.Instance?.StopLoop(n); }
}
