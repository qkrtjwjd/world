using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;
using Yarn.Unity;

/// <summary>
/// 카메라 연출 — 쯔꾸르 문법 4종 (F-3-9 · D-0 · E-64).
///
///   추적(Track)  : 루를 따라가고 맵 경계에서 멈춘다. 조작 구간의 기본값. 진행 방향 치우침은 오프셋으로.
///   고정(Hold)   : 현재 위치에 멈춘다. 컷신의 기본값.
///   스크롤(Scroll): 목표 위치와 속도만 받는 평행 이동. 맵 경계를 넘는 목표는 경계에서 멈춘다.
///   흔들림(Shake): 약한 1종. 강도 단계를 두지 않는다.
///
/// ⛔ 정사영 크기를 바꾸지 않는다(줌 없음). 클로즈업은 카메라가 아니라 오버레이 컷이다.
///    2026-09-27 이전의 closeup · zoom · pan · pov · tilt · cut 명령은 이 개정으로 걷어냈다.
/// 추적·스크롤 중 위치의 1픽셀 스냅은 <see cref="CameraFollow"/> 가 LateUpdate 에서 한다.
/// </summary>
public class CameraDirector : MonoBehaviour
{
    public static CameraDirector Instance { get; private set; }

    public enum Mode { Track, Hold, Scroll }

    /// <summary>[CAM] 값의 한국어 표기 — D-0 과 같은 말을 쓴다.</summary>
    public const string KindTrack  = "추적";
    public const string KindHold   = "고정";
    public const string KindScroll = "스크롤";
    public const string KindShake  = "흔들림";

    [Header("흔들림 — 약한 1종 (F-3-9)")]
    [Tooltip("흔들림 진폭(월드 유닛). 1/32 = 화면 1픽셀. 세기 단계를 만들지 않는다.")]
    public float shakeAmplitude = 2f / 32f;
    [Tooltip("흔들림 길이(초).")]
    public float shakeDuration = 0.25f;

    [Header("스크롤")]
    [Tooltip("스크롤 속도를 주지 않았을 때 쓰는 값(월드 유닛/초).")]
    public float defaultScrollSpeed = 6f;

    public Mode CurrentMode { get; private set; } = Mode.Track;

    private readonly Dictionary<string, CinemachineCamera> _shots = new();
    private GameObject _anchor;          // 고정·스크롤이 따라가는 빈 오브젝트
    private Coroutine  _scrollRoutine;
    private Coroutine  _shakeRoutine;
    private float      _trackSmoothTime = -1f;

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);
    }

    // ─── 샷 VCam 등록 (SceneCameraSetup 호환) ─────────────────────────
    // 등록만 받는다. 샷 전환은 4종에 없어 걷어냈다(E-64).

    public void RegisterVCam(string shotName, CinemachineCamera vcam)
    {
        _shots[shotName] = vcam;
        vcam.Priority = 0;
    }

    public void UnregisterVCam(string shotName)
    {
        if (_shots.TryGetValue(shotName, out var vcam)) vcam.Priority = 0;
        _shots.Remove(shotName);
    }

    public void ClearVCams()
    {
        foreach (var vcam in _shots.Values) vcam.Priority = 0;
        _shots.Clear();
    }

    // ─── 1. 추적 ─────────────────────────────────────────────────────

    /// <summary>루를 따라간다. offset 은 진행 방향 치우침 등(D-S#16A). 기본 (0,0).</summary>
    public void Track(Vector2 offset = default)
    {
        var cam = CameraFollow.Instance;
        if (cam == null) return;
        StopScroll();

        var player = PlayerTransform();
        if (player != null) cam.SetTarget(player);
        cam.charLookAheadOffset = offset.x;
        cam.charHeightOffset    = offset.y;
        if (_trackSmoothTime >= 0f) { cam.smoothTime = _trackSmoothTime; _trackSmoothTime = -1f; }

        if (_anchor != null) { Destroy(_anchor); _anchor = null; }
        CurrentMode = Mode.Track;
    }

    // ─── 2. 고정 ─────────────────────────────────────────────────────

    /// <summary>지금 카메라가 있는 자리에 멈춘다.</summary>
    public void Hold()
    {
        var cam = CameraFollow.Instance;
        if (cam == null) return;
        StopScroll();
        EnsureAnchorAt(cam.transform.position);
        cam.SetTarget(_anchor.transform);
        CurrentMode = Mode.Hold;
    }

    // ─── 3. 스크롤 ───────────────────────────────────────────────────

    /// <summary>
    /// 목표 위치까지 등속으로 평행 이동한 뒤 그 자리에 고정된다. 맵 경계 밖 목표는 경계에서 멈춘다.
    /// 도착할 때까지 기다리려면 반환값을 yield 한다.
    /// </summary>
    public Coroutine ScrollTo(Vector2 worldTarget, float speed = 0f)
    {
        var cam = CameraFollow.Instance;
        if (cam == null) return null;
        StopScroll();
        EnsureAnchorAt(cam.transform.position);
        cam.SetTarget(_anchor.transform);
        if (_trackSmoothTime < 0f) _trackSmoothTime = cam.smoothTime;
        cam.smoothTime = 0f;   // 앵커를 그대로 따라가야 등속이 된다
        CurrentMode = Mode.Scroll;
        Vector2 anchorTarget = ClampToBounds(worldTarget) - FollowOffset();
        _scrollRoutine = StartCoroutine(DoScroll(anchorTarget, speed > 0f ? speed : defaultScrollSpeed));
        return _scrollRoutine;
    }

    IEnumerator DoScroll(Vector2 target, float speed)
    {
        Vector2 p = _anchor.transform.position;
        while ((p - target).sqrMagnitude > 0.0001f)
        {
            p = Vector2.MoveTowards(p, target, speed * Time.deltaTime);
            _anchor.transform.position = new Vector3(p.x, p.y, _anchor.transform.position.z);
            yield return null;
        }
        _anchor.transform.position = new Vector3(target.x, target.y, _anchor.transform.position.z);
        _scrollRoutine = null;
        CurrentMode = Mode.Hold;   // 스크롤이 끝나면 그 자리에 고정된다
    }

    void StopScroll()
    {
        if (_scrollRoutine != null) { StopCoroutine(_scrollRoutine); _scrollRoutine = null; }
    }

    // ─── 4. 흔들림 ───────────────────────────────────────────────────

    /// <summary>약한 흔들림 1회. 설정에서 흔들림을 끄면 아무 일도 없다.</summary>
    public void Shake()
    {
        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        _shakeRoutine = StartCoroutine(DoShake());
    }

    IEnumerator DoShake()
    {
        var cam = CameraFollow.Instance;
        if (cam == null) yield break;
        if (!(SettingsManager.Instance?.cameraShakeEnabled ?? true)) yield break;

        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            float fade = 1f - elapsed / shakeDuration;
            cam.shakeOffset = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * (shakeAmplitude * fade);
            yield return null;
        }
        cam.shakeOffset = Vector3.zero;
        _shakeRoutine = null;
    }

    // ─── [CAM] 값 하나로 부르기 ───────────────────────────────────────

    /// <summary>
    /// [CAM] 연출 데이터의 값으로 카메라를 움직인다. 값은 추적·고정·스크롤·흔들림 넷 중 하나다(F-3-9).
    /// 목록에 없는 값은 경고를 남기고 고정으로 처리한다 — 조용히 버리지 않는다.
    /// </summary>
    public Coroutine Apply(string kind, Vector2 offsetOrTarget = default, float speed = 0f, bool hasTarget = false)
    {
        switch (kind)
        {
            case KindTrack:  Track(offsetOrTarget); return null;
            case KindHold:   Hold(); return null;
            case KindScroll:
                if (!hasTarget)
                {
                    Debug.LogWarning("[CameraDirector] 스크롤에 목표가 없다 — 고정으로 처리한다.");
                    Hold(); return null;
                }
                return ScrollTo(offsetOrTarget, speed);
            case KindShake:  Shake(); return null;
            default:
                Debug.LogWarning($"[CameraDirector] 모르는 [CAM] 값 '{kind}' — 추적·고정·스크롤·흔들림 중 하나여야 한다. 고정으로 처리한다(F-3-9).");
                Hold(); return null;
        }
    }

    /// <summary>예전 호출부 호환 — 연출이 끝나면 추적으로 돌아간다.</summary>
    public void RestoreDefault() => Track();

    // ─── 내부 ────────────────────────────────────────────────────────

    /// <summary>카메라 중심이 cameraCenter 에 오도록 앵커를 놓는다. 추적 오프셋만큼 빼 둔다.</summary>
    void EnsureAnchorAt(Vector3 cameraCenter)
    {
        if (_anchor == null)
        {
            _anchor = new GameObject("_CamAnchor");
            DontDestroyOnLoad(_anchor);
        }
        Vector2 a = (Vector2)cameraCenter - FollowOffset();
        _anchor.transform.position = new Vector3(a.x, a.y, 0f);
    }

    static Vector2 FollowOffset()
    {
        var cf = CameraFollow.Instance;
        return cf != null ? new Vector2(cf.charLookAheadOffset, cf.charHeightOffset) : Vector2.zero;
    }

    static Transform PlayerTransform()
    {
        if (PlayerStats.Instance != null) return PlayerStats.Instance.transform;
        var p = GameObject.FindWithTag("Player");
        return p != null ? p.transform : null;
    }

    /// <summary>카메라 중심이 맵 경계 밖을 보지 않도록 목표를 자른다. 경계가 화면보다 작으면 경계 중앙.</summary>
    static Vector2 ClampToBounds(Vector2 target)
    {
        var cf = CameraFollow.Instance;
        var cam = Camera.main;
        if (cf == null || cam == null || !cf.TryGetBoundRect(out Rect r)) return target;

        // CameraFollow.ClampToBound 와 같은 규칙 — 경계가 화면보다 작은 축은 중앙.
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        float x = r.width  <= halfW * 2f ? r.center.x : Mathf.Clamp(target.x, r.xMin + halfW, r.xMax - halfW);
        float y = r.height <= halfH * 2f ? r.center.y : Mathf.Clamp(target.y, r.yMin + halfH, r.yMax - halfH);
        return new Vector2(x, y);
    }

    // ─── Yarn Commands ───────────────────────────────────────────────
    // Yarn Spinner 3.x: 인스턴스 [YarnCommand] 는 첫 인자를 GameObject 이름으로 해석하므로
    // static + Instance 패턴을 쓴다 (YarnCommandBridge 와 같은 규약).

    /// <summary>
    /// <<cam "추적">>                    — 루를 따라간다
    /// <<cam "추적" "오프셋x" "오프셋y">>  — 진행 방향 치우침
    /// <<cam "고정">>                    — 그 자리에 멈춘다
    /// <<cam "스크롤" "오브젝트명" "속도">> — 그 오브젝트 위치까지 평행 이동(도착까지 대사 대기). 속도 생략 가능
    /// <<cam "흔들림">>                  — 약한 흔들림 1회
    /// 목록에 없는 값은 경고 후 고정.
    /// </summary>
    [YarnCommand("cam")]
    public static IEnumerator YarnCam(string kind, string arg1 = "", string arg2 = "")
    {
        var cd = Instance;
        if (cd == null) yield break;

        switch (kind)
        {
            case KindTrack:
            {
                float.TryParse(arg1, out float ox);
                float.TryParse(arg2, out float oy);
                cd.Track(new Vector2(ox, oy));
                yield break;
            }
            case KindScroll:
            {
                var go = string.IsNullOrEmpty(arg1) ? null : GameObject.Find(arg1);
                if (go == null)
                {
                    Debug.LogWarning($"[CameraDirector] cam 스크롤: 오브젝트 '{arg1}' 을 찾지 못했다 — 고정으로 처리한다.");
                    cd.Hold();
                    yield break;
                }
                float.TryParse(arg2, out float speed);
                yield return cd.ScrollTo(go.transform.position, speed);
                yield break;
            }
            default:
                cd.Apply(kind);
                yield break;
        }
    }

    // ── 예전 C# 호출부 호환 (숲 ForestBarrierDirector) ──────────────────
    // 강도 인자를 받지만 쓰지 않는다 — 흔들림은 약한 1종뿐이다(F-3-9).
    public static void YarnCamShake(float intensityIgnored, float durationIgnored) => Instance?.Shake();

    // 슬로모션은 카메라 동작이 아니라 시간 연출이다. 숲 결계가 쓴다. 4종 제한과 무관하게 남긴다.
    public static void YarnCamSlowmo(float timeScale, float duration)
    {
        if (Instance != null) Instance.StartCoroutine(Instance.DoSlowmo(timeScale, duration));
    }

    IEnumerator DoSlowmo(float timeScale, float duration)
    {
        float orig = Time.timeScale;
        Time.timeScale = Mathf.Clamp(timeScale, 0.01f, 1f);
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = orig;
    }

    // <<cam_fade_down 지속시간>> — 카메라가 아니라 화면 페이드다. 구역 카메라의 TransitionFade 가 쓴다.
    public IEnumerator FadeDown(float duration)
    {
        var tm = TransitionManager.Instance;
        if (tm == null) yield break;
        yield return StartCoroutine(tm.FadeToBlack(duration));
    }
}
