using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 감정 ID 하나에 대응하는 빛의 모양. 인스펙터에서 채운다.
///
/// ⚠ <see cref="emotionId"/> 는 <c>Assets/Date/Dialogue/CharacterSpriteData.asset</c> 에
///   등록된 값이어야 한다. 지어내지 않는다 (CLAUDE.md §0-4).
/// </summary>
[Serializable]
public class SeraLightMood
{
    [Tooltip("CharacterSpriteData.asset 에 등록된 감정 ID. 세라는 5종뿐이다 — " +
             "neutral_cold / warm_smile / low_whisper / door_gap / frozen")]
    public string emotionId = "";

    [Tooltip("빛의 세기.")]
    public float intensity = 1f;

    [Tooltip("빛의 색.")]
    public Color color = Color.white;

    [Tooltip("완전히 밝은 안쪽 반경(월드 유닛). ⚠ Point Light2D 반경은 트랜스폼 스케일을 " +
             "무시하므로(CLAUDE.md §11) 부모 배율을 곱하지 않는다.")]
    public float innerRadius = 5f;

    [Tooltip("빛이 닿는 바깥 반경(월드 유닛). 0 이면 엔진이 반경비를 나눌 때 0 으로 나눈다 — " +
             "RendererLighting.cs 가 inner/outer 로 나눈다. 0 보다 커야 한다.")]
    public float outerRadius = 7f;

    [Tooltip("안쪽 각도(도). 바깥 각도와의 차이가 그림자 경계(페넘브라)의 폭이 된다.")]
    public float innerAngle = 94.88f;

    [Tooltip("바깥 각도(도). 360 이면 완전한 방사형이다.")]
    public float outerAngle = 94.88f;

    [Range(0f, 1f)]
    [Tooltip("감쇠. 낮을수록 경계가 날카롭다.")]
    public float falloffIntensity = 0.5f;

    [Tooltip("이 기분으로 옮겨가는 데 걸리는 시간(초). 0 이면 즉시 바뀐다.")]
    public float blendSeconds = 0.6f;
}

/// <summary>
/// 세라의 빛 — 감정 ID 에 따라 <see cref="Light2D"/> 의 세기·색·반경·각도를 옮긴다.
///
/// 세라는 정본에서 「빛」 그 자체다 (B 인물 표제 · B-세라-7 「빛의 장막 — 과거: 치유하고 보호하는 빛 /
/// 현재: 가두고 단절시키는 구속의 빛」). F 문단 176 이 연출 형태를 「손끝에서 빛이 새어나오다
/// 억눌리는 이펙트」로 적어 뒀다. 이 컴포넌트는 그것을 기존 Light2D 값의 보간만으로 표현한다 —
/// 새 셰이더·머티리얼·스프라이트를 만들지 않는다.
///
/// 기분의 출처는 <c>&lt;&lt;showSprite "세라" "감정"&gt;&gt;</c> 다. yarn 을 고치지 않고
/// 이미 있는 호출을 그대로 듣는다.
///
/// ⚠ <b>회전은 건드리지 않는다.</b> 씬에서 사람이 맞춘 각도이며, 회전축은
///   <see cref="BadEndingDirector"/> 가 이미 쓰고 있다.
/// ⚠ <b>인형화·발각 진행도에 연동하지 않는다.</b> CLAUDE.md §7 이 인형화를 어떤 형태로도
///   표시하지 못하게 했고 전달 채널을 다섯으로 열거했는데 세라의 빛은 거기 없다.
///   발각 2초도 「루의 그림자 하나로」 못박혀 있다 (<see cref="SeraVision"/> 주석).
/// </summary>
public class SeraLightDirector : MonoBehaviour
{
    [Header("대상")]
    [Tooltip("움직일 Light2D. 비우면 같은 GameObject 에서 찾는다.")]
    public Light2D targetLight;

    [Tooltip("빛이 따라다닐 대상. 비어 있으면 씬에 박힌 자리에 그대로 선다.\n" +
             "세라 오브젝트를 다시 세우면 여기 꽂기만 하면 된다.")]
    public Transform anchor;

    [Header("기분")]
    [Tooltip("이 인물의 감정만 듣는다. 화자 ID 와 같은 표기다(CLAUDE.md §5).")]
    public string characterId = "세라";

    [Tooltip("씬이 시작할 때의 기분. 아래 목록에 있는 ID 여야 한다.")]
    public string defaultEmotionId = "neutral_cold";

    [Tooltip("감정 ID 별 빛. 등록된 5종을 전부 채운다 — 빠진 ID 가 들어오면 경고만 남기고 무시한다.")]
    public SeraLightMood[] moods;

    [Header("시선")]
    [Tooltip("세라가 보는 방향으로 빛을 돌린다.")]
    public bool followFacing = true;

    [Tooltip("시선의 출처. 비우면 부모(= 세라)를 쓴다.\n" +
             "SeraVision 이 붙어 있으면 그쪽 facing 이 우선이고(마을), " +
             "없으면 Animator 의 dir 과 스프라이트 반전으로 유추한다(집).")]
    public Transform facingSource;

    [Tooltip("dir(0=아래 1=옆 2=위) 을 넘겨받을 Animator. 비우면 facingSource 에서 찾는다.")]
    public Animator facingAnimator;

    [Tooltip("방향 파라미터 이름. Sera.controller 에 있는 것은 dir 과 Speed 뿐이다.")]
    public string facingDirParam = "dir";

    [Tooltip("시선을 못 읽을 때 바라볼 기본 방향.")]
    public Vector2 fallbackFacing = Vector2.down;

    [Tooltip("초당 회전 각도. 0 이면 즉시 돌아본다.")]
    public float turnDegreesPerSecond = 360f;

    [Header("등장")]
    [Range(0f, 1f)]
    [Tooltip("세라가 지금 얼마나 와 있는가. 기분과 곱해진다.\n" +
             "연출(NightSequenceManager 의 S#02 등장·퇴장)이 이 축을 민다 — " +
             "세기를 직접 쓰면 기분과 서로 밀어내기 때문이다.")]
    public float presence = 1f;

    [Header("양보")]
    [Tooltip("배드 엔딩 연출이 도는 동안 빛에 손을 대지 않는다. " +
             "BadEndingDirector 가 몰고 있는 조명(거실 조명)에 이 컴포넌트를 붙일 때만 켠다.\n" +
             "⚠ 세라 본인의 빛에는 끈다 — 배드 엔딩에서도 세라의 기분은 바뀐다.")]
    public bool yieldToBadEnding = false;

    // 현재 값 — 보간의 출발점. Light2D 에서 매번 읽지 않는다.
    // 배드 엔딩이 같은 필드를 덮어쓰므로, 양보가 풀릴 때 실제 값으로 다시 맞춘다.
    // ⚠ 보간은 전부 「기분 공간」에서 한다 — presence 가 곱해지기 <b>전</b>의 값이다.
    //   빛에서 되읽으면 presence 가 두 번 곱해지므로, 지금 값을 여기서 직접 들고 간다.
    SeraLightMood _live;
    SeraLightMood _from;
    SeraLightMood _to;
    float         _elapsed;
    float         _duration;
    bool          _wasYielding;
    SeraVision    _vision;
    Coroutine     _presenceRoutine;

    /// <summary>
    /// Point <see cref="Light2D"/> 는 회전 0 에서 <b>위(+Y)</b> 를 본다.
    ///
    /// ⚠ <b>2026-09-24 화면 실측으로 뒤집혔다.</b> 여기 적혀 있던 「회전 0 = 아래(−Y)」는
    ///   <c>Light2DLookupTexture.CreatePointLightLookupTexture()</c> 의
    ///   <c>Vector2.Dot(Vector2.down, …)</c> 만 보고 세운 추론이었고, 실제 화면은 반대였다.
    ///   (조회 텍스처는 위아래가 뒤집힌 UV 로 샘플된다 — 코드 한 줄로는 안 드러난다.)
    ///
    /// 실측: 세라가 아래를 보는 상태(z = 0)에서 빛의 원뿔이 <b>위로</b> 뻗었다.
    /// 판정은 전용 카메라로 찍은 스틸이고, 카메라를 1유닛 위로 올리면 빛이 화면 아래로
    /// 내려가는 것까지 확인해 PNG 상하 뒤집힘 가능성을 배제했다.
    ///
    /// 따라서 방향 <c>d</c> 를 보게 하려면 Z 각이 <c>atan2(d.y, d.x) − 90°</c> 다.
    /// </summary>
    const float ZeroRotationFacingDegrees = 90f;

    void Awake()
    {
        if (targetLight == null) targetLight = GetComponent<Light2D>();

        if (targetLight == null)
        {
            Dbg.LogWarning($"[{nameof(SeraLightDirector)}] targetLight 가 비어 있습니다 — " +
                           $"'{name}' 에서 Light2D 를 찾지 못했습니다. 컴포넌트를 끕니다.");
            enabled = false;
            return;
        }

        if (targetLight.lightType != Light2D.LightType.Point)
            Dbg.LogWarning($"[{nameof(SeraLightDirector)}] '{name}' 의 Light2D 가 Point 타입이 아닙니다 " +
                           $"({targetLight.lightType}). 반경·각도는 Point 에서만 먹습니다.");

        if (facingSource == null)  facingSource  = anchor != null ? anchor : transform.parent;
        if (facingAnimator == null && facingSource != null)
            facingAnimator = facingSource.GetComponentInChildren<Animator>();
        if (facingSource != null)
            _vision = facingSource.GetComponentInChildren<SeraVision>();

        // 출발점은 씬에 박혀 있는 현재 값이다. 프리셋이 비어 있어도 화면이 갑자기 바뀌지 않는다.
        _live = Capture();
        _from = _live;
        _to   = _live;
    }

    void OnEnable()
    {
        DialogueEvents.OnPortraitEmotion += HandlePortraitEmotion;

        // 씬 진입 기분. 목록에 없으면 Apply 쪽이 경고를 남기고 현재 값을 유지한다.
        if (!string.IsNullOrWhiteSpace(defaultEmotionId))
            SetMood(defaultEmotionId, instant: true);
    }

    void OnDisable()
    {
        DialogueEvents.OnPortraitEmotion -= HandlePortraitEmotion;
    }

    void LateUpdate()
    {
        if (targetLight == null) return;

        // 배드 엔딩 중에는 아무것도 쓰지 않는다. 그쪽이 같은 조명의 밝기와 회전을 몰고 있다.
        bool yielding = yieldToBadEnding && BadEndingDirector.IsPlaying;
        if (yielding)
        {
            _wasYielding = true;
            return;
        }

        // 양보가 막 풀렸다면 실제 값에서 이어붙인다 — 저장해 둔 옛 출발점으로 튀지 않게.
        // 양보 중에는 남이 세기를 몰았으므로 기분 공간으로 되돌려 읽는다.
        if (_wasYielding)
        {
            _wasYielding = false;
            _live        = CaptureFromLight();
            _from        = _live;
            _elapsed     = 0f;
        }

        if (anchor != null) transform.position = anchor.position;

        if (followFacing) FaceTowardCurrent();

        if (_to == null) return;

        float t = _duration <= 0f ? 1f : Mathf.Clamp01(_elapsed / _duration);
        Apply(_from, _to, t);

        if (t < 1f) _elapsed += Time.deltaTime;
    }

    // ── 시선 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 세라가 보는 쪽으로 빛을 돌립니다.
    ///
    /// ⚠ <b>월드 회전을 직접 씁니다.</b> 세라는 좌우를 <c>localScale.x</c> 부호로 뒤집는데
    ///   (CLAUDE.md §11), Point Light2D 의 행렬은 <c>TRS(position, rotation, outerRadius)</c> 라
    ///   <b>스케일을 통째로 버립니다.</b> 그래서 부모가 뒤집혀도 빛은 따라 뒤집히지 않고,
    ///   월드 회전만 맞춰 주면 됩니다. <see cref="SeraVisionDisplay"/> 가 반전을 물려받지 않으려고
    ///   메시를 형제로 띄우는 것과 같은 문제를 여기서는 회전으로 푼 것입니다.
    /// </summary>
    void FaceTowardCurrent()
    {
        Vector2 facing = ResolveFacing();
        if (facing.sqrMagnitude < 0.0001f) return;

        float target  = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg - ZeroRotationFacingDegrees;
        float current = transform.eulerAngles.z;

        float z = turnDegreesPerSecond <= 0f
            ? target
            : Mathf.MoveTowardsAngle(current, target, turnDegreesPerSecond * Time.deltaTime);

        transform.rotation = Quaternion.Euler(0f, 0f, z);
    }

    /// <summary>
    /// 시선을 읽습니다. 마을에서는 <see cref="SeraVision"/> 이 단일 출처이고,
    /// 집에서는 Animator 의 <c>dir</c> 과 스프라이트 반전으로 유추합니다.
    ///
    /// ⚠ 부호 규약은 루·세라 공통입니다 — <c>left</c> 스프라이트가 왼쪽을 보므로
    ///   <b>localScale.x 가 양수면 왼쪽</b>입니다 (<see cref="SeraPatrol"/> 의 SetFacing 주석).
    /// </summary>
    Vector2 ResolveFacing()
    {
        if (_vision != null && _vision.facing.sqrMagnitude > 0.0001f)
            return _vision.facing.normalized;

        if (facingAnimator != null && !string.IsNullOrEmpty(facingDirParam) && facingSource != null)
        {
            int dir = facingAnimator.GetInteger(facingDirParam);
            if (dir == 2) return Vector2.up;
            if (dir == 1) return facingSource.localScale.x >= 0f ? Vector2.left : Vector2.right;
            return Vector2.down;
        }

        return fallbackFacing.sqrMagnitude > 0.0001f ? fallbackFacing.normalized : Vector2.down;
    }

    // ── 등장 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 「세라가 얼마나 와 있는가」를 옮깁니다. 기분과 <b>곱해지므로</b>
    /// 연출이 세기를 직접 쓰지 않고 이 축만 밀면 둘이 서로 밀어내지 않습니다.
    /// </summary>
    public IEnumerator FadePresence(float target, float duration)
    {
        target = Mathf.Clamp01(target);

        if (duration <= 0f) { presence = target; yield break; }

        float start = presence;
        float t     = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            presence = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        presence = target;
    }

    /// <summary>등장 정도를 즉시 세웁니다. 페이드가 돌고 있으면 끊습니다.</summary>
    public void SetPresence(float value)
    {
        if (_presenceRoutine != null) { StopCoroutine(_presenceRoutine); _presenceRoutine = null; }
        presence = Mathf.Clamp01(value);
    }

    /// <summary>등장 정도를 페이드합니다. 이전 페이드는 끊습니다.</summary>
    public void FadePresenceTo(float target, float duration)
    {
        if (!isActiveAndEnabled) { presence = Mathf.Clamp01(target); return; }
        if (_presenceRoutine != null) StopCoroutine(_presenceRoutine);
        _presenceRoutine = StartCoroutine(FadePresence(target, duration));
    }

    // ── 기분 ─────────────────────────────────────────────────────────────────

    void HandlePortraitEmotion(string character, string emotionId)
    {
        if (character != characterId) return;
        SetMood(emotionId, instant: false);
    }

    /// <summary>
    /// 포트레이트 없이 세라의 빛만 바꾼다. 클로즈업이 포트레이트에서 오버레이 컷으로 옮겨 가며
    /// showSprite 가 빠진 자리에 쓴다(S#02 문틈 — 2026-10-01). 감정 ID 는 showSprite 와 같은 등록값이다.
    /// <c>&lt;&lt;sera_light "door_gap"&gt;&gt;</c>
    /// </summary>
    [Yarn.Unity.YarnCommand("sera_light")]
    public static void SetMoodFromYarn(string emotionId)
    {
        bool found = false;
        foreach (var d in FindObjectsByType<SeraLightDirector>(FindObjectsSortMode.None))
        {
            if (d.characterId != "세라") continue;
            d.SetMood(emotionId, instant: false);
            found = true;
        }
        if (!found)
            Dbg.LogWarning($"[{nameof(SeraLightDirector)}] sera_light '{emotionId}' — 켜져 있는 세라 빛이 없습니다.");
    }

    /// <summary>
    /// 기분을 바꿉니다. 목록에 없는 ID 는 <b>경고를 남기고 무시</b>합니다 —
    /// 조용히 버리지 않는다 (CLAUDE.md §0-7).
    /// </summary>
    public void SetMood(string emotionId, bool instant = false)
    {
        SeraLightMood mood = Find(emotionId);
        if (mood == null)
        {
            Dbg.LogWarning($"[{nameof(SeraLightDirector)}] '{characterId}' 의 감정 ID " +
                           $"'{emotionId}' 에 해당하는 빛 프리셋이 없습니다. 현재 빛을 유지합니다. " +
                           $"등록 목록은 CharacterSpriteData.asset 입니다.");
            return;
        }

        if (_to != null && ReferenceEquals(_to, mood) && _duration > 0f && _elapsed >= _duration)
            return;   // 이미 그 기분에 도착해 있다

        _from     = _live ?? Capture();
        _to       = mood;
        _elapsed  = 0f;
        _duration = instant ? 0f : Mathf.Max(0f, mood.blendSeconds);

        if (_duration <= 0f) Apply(_from, _to, 1f);
    }

    SeraLightMood Find(string emotionId)
    {
        if (moods == null || string.IsNullOrWhiteSpace(emotionId)) return null;
        foreach (var m in moods)
            if (m != null && m.emotionId == emotionId) return m;
        return null;
    }

    // ── 값 ───────────────────────────────────────────────────────────────────

    /// <summary>씬에 박힌 현재 값을 기분 공간으로 읽습니다. presence 가 1 인 시점(Awake)용입니다.</summary>
    SeraLightMood Capture() => CaptureFromLight();

    /// <summary>
    /// 빛의 실제 값을 기분 공간으로 되돌려 읽습니다.
    /// 세기는 presence 로 나눠야 원래의 기분 세기가 나옵니다 — presence 가 0 에 가까우면
    /// 나눌 수 없으므로 마지막 기분 값을 그대로 둡니다.
    /// </summary>
    SeraLightMood CaptureFromLight()
    {
        float p = Mathf.Clamp01(presence);
        float moodIntensity = p > 0.001f
            ? targetLight.intensity / p
            : (_live != null ? _live.intensity : targetLight.intensity);

        return new SeraLightMood
        {
            emotionId        = "",
            intensity        = moodIntensity,
            color            = targetLight.color,
            innerRadius      = targetLight.pointLightInnerRadius,
            outerRadius      = targetLight.pointLightOuterRadius,
            innerAngle       = targetLight.pointLightInnerAngle,
            outerAngle       = targetLight.pointLightOuterAngle,
            falloffIntensity = targetLight.falloffIntensity,
        };
    }

    void Apply(SeraLightMood a, SeraLightMood b, float t)
    {
        // ⚠ 바깥 반경이 0 이면 엔진이 inner/outer 로 나눈다
        //   (RendererLighting.GetNormalizedInnerRadius). 0 으로 내려가지 않게 막는다.
        float outer = Mathf.Max(0.0001f, Mathf.Lerp(a.outerRadius, b.outerRadius, t));
        float inner = Mathf.Clamp(Mathf.Lerp(a.innerRadius, b.innerRadius, t), 0f, outer);

        // 기분 공간의 지금 값. 다음 전환의 출발점이 된다 — 빛에서 되읽으면 presence 가 두 번 곱해진다.
        _live = new SeraLightMood
        {
            emotionId        = "",
            intensity        = Mathf.Lerp(a.intensity, b.intensity, t),
            color            = Color.Lerp(a.color, b.color, t),
            innerRadius      = inner,
            outerRadius      = outer,
            innerAngle       = Mathf.Lerp(a.innerAngle, b.innerAngle, t),
            outerAngle       = Mathf.Lerp(a.outerAngle, b.outerAngle, t),
            falloffIntensity = Mathf.Lerp(a.falloffIntensity, b.falloffIntensity, t),
        };

        // 기분 × 등장 정도. 연출은 presence 만 밀고 세기는 여기서 한 번만 써진다.
        targetLight.intensity             = _live.intensity * Mathf.Clamp01(presence);
        targetLight.color                 = _live.color;
        targetLight.pointLightInnerAngle  = _live.innerAngle;
        targetLight.pointLightOuterAngle  = _live.outerAngle;
        targetLight.falloffIntensity      = _live.falloffIntensity;
        targetLight.pointLightOuterRadius = _live.outerRadius;
        targetLight.pointLightInnerRadius = _live.innerRadius;
    }
}
