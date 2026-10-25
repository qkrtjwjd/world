using UnityEngine;

/// <summary>
/// 집 제한 시간의 복도 축소 — 벽이 <b>콜라이더째</b> 안쪽으로 들어온다(C-14-2-2 · E-74-3 · F-6 v1.41).
///
///   폭      3차 16px(반 타일) · 4차 32px(1 타일). F-6 초안값이며 데모를 돌려본 뒤 조정한다.
///   진행    단계마다 <b>한 번에</b> 들어온다. 단계 사이에는 움직이지 않는다(C-14-2-1).
///   체감    들어오는 순간 짧은 충돌음 1회 · 약한 흔들림 1회(D-0 의 흔들림 1종). 카메라 정사영은 바꾸지 않는다.
///   밀어냄  벽이 들어오는 자리에 루가 있으면 안쪽으로 밀어낸다.
///   해제    현관문 통과 프레임에 전부 원래 자리로(C-14-2-2 · F-6 「페이드로 풀지 않는다」).
///
/// 벽(콜라이더가 딸린 오브젝트)에 붙인다. 그림 자식은 함께 움직이므로 그 자식의 EscapePressureShrinker 는 떼어 낸다 —
/// 둘 다 두면 그림이 두 번 밀린다. 문틀은 그림만 좁히고(EscapePressureShrinker 유지) 출입구 콜라이더는 루가 지나갈 폭을 지킨다.
/// 위쪽 벽에 붙은 문(루의 방 · 세라의 방)의 조사 판정 위치는 벽의 자식으로 두어 함께 옮긴다.
/// 배치가 확정된 뒤 통로 폭이 루 콜라이더보다 좁아지는 자리가 생기면 그 벽의 <see cref="scale"/> 을 줄인다.
///
/// ⚠ 1px = 0.5625/32 = 0.017578 월드 유닛(EscapePressureShrinker 와 같은 기준).
/// </summary>
public class PressureWall : MonoBehaviour
{
    public const float WorldUnitsPerPixel = 0.5625f / 32f;

    [Tooltip("안쪽(방 가운데) 방향. 위쪽 벽이면 (0,-1), 왼쪽 벽이면 (1,0).")]
    public Vector2 inward = Vector2.down;

    [Tooltip("이 벽에만 거는 배율. 통로가 루보다 좁아지는 자리면 줄인다(E-74-3 주석).")]
    [Range(0f, 1f)] public float scale = 1f;

    /// <summary>3차 · 4차 폭(px). F-6 v1.41.</summary>
    public static float Stage3Pixels = 16f;
    public static float Stage4Pixels = 32f;

    /// <summary>충돌음(AudioManager 등록 이름). 벽마다 울리지 않도록 한 단계에 한 번만 낸다.</summary>
    public const string BumpSfx = "pressure_wall_bump";

    Vector3 _base;
    int _appliedStage;
    static int _soundedStage;
    static int _shakenStage;

    void Awake() => _base = transform.localPosition;

    void OnEnable()  => HouseEscapePressureController.OnLevelChanged += OnLevel;
    void OnDisable() => HouseEscapePressureController.OnLevelChanged -= OnLevel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { _soundedStage = 0; _shakenStage = 0; }

    // OnLevelChanged 는 단계 전환 동안 매 프레임 온다. 값이 아니라 단계만 본다 — 한 번에 들어오게.
    void OnLevel(float level)
    {
        int stage = HouseEscapePressureController.Stage;
        int want = stage >= 4 ? 4 : stage == 3 ? 3 : 0;
        if (want == _appliedStage) return;
        _appliedStage = want;

        float px = want == 4 ? Stage4Pixels : want == 3 ? Stage3Pixels : 0f;
        Vector3 offset = (Vector3)(inward.normalized * px * WorldUnitsPerPixel * scale);
        if (transform.parent != null) offset = transform.parent.InverseTransformVector(offset);
        transform.localPosition = _base + offset;

        if (want == 0) { _soundedStage = 0; _shakenStage = 0; return; }

        Physics2D.SyncTransforms();
        PushLuOut();

        if (_soundedStage != want)
        {
            _soundedStage = want;
            AudioManager.Instance?.Play(BumpSfx);
        }
        if (_shakenStage != want)
        {
            _shakenStage = want;
            if (SettingsManager.Instance == null || SettingsManager.Instance.cameraShakeEnabled)
                CameraDirector.Instance?.Shake();
        }
    }

    /// <summary>벽과 겹친 루를 안쪽으로 밀어낸다.</summary>
    void PushLuOut()
    {
        var lu = LuPose.Lu;
        if (lu == null) return;
        var luCol = lu.GetComponent<Collider2D>();
        if (luCol == null) return;

        foreach (var wall in GetComponents<Collider2D>())
        {
            if (wall == null || !wall.enabled || wall.isTrigger) continue;
            var d = Physics2D.Distance(luCol, wall);
            if (!d.isOverlapped) continue;
            // d.distance 는 겹치면 음수다. 벽에서 멀어지는 쪽(안쪽)으로 그만큼 + 여유 1px.
            Vector2 push = inward.normalized * (-d.distance + WorldUnitsPerPixel);
            var rb = lu.GetComponent<Rigidbody2D>();
            Vector2 p = (Vector2)lu.position + push;
            if (rb != null) rb.position = p;
            lu.position = p;
            Physics2D.SyncTransforms();
        }
    }
}
