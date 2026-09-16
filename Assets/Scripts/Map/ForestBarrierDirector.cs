using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// S#21 데모 종료 — 숲 끝 · 결계 (D-3 S#21A · S#21C).
///
/// <para>
/// <b>발동은 거리다.</b> 정본의 [TRIGGER] 는 「S#20 종료 후 이동 중 자동 발동」이고,
/// [CAM] 은 「걷는 둘을 뒤에서 잡는다. 결계에 도달할 때까지 컷을 바꾸지 않는다」이다.
/// 걸어가다 결계가 가까워지면 그대로 넘어가는 그림이므로, 밟는 순간 켜지는 콜라이더가 아니라
/// 결계로부터의 거리로 연다. <see cref="triggerDistance"/> 안으로 들어오면 1회 발동한다.
/// </para>
///
/// <para>
/// <b>구성은 2단이다.</b> S#21A(결계 도달 · 열쇠) → S#21C(강화 · 종료).
/// S#21B 는 폐기됐고 씬 번호를 재배열하지 않아 자리가 비어 있다(E-62-2).
/// 인형화 분기도 폐기됐으므로 대사는 한 벌이다(E-62-3) — 여기서 분기를 만들지 말 것.
/// </para>
///
/// <para>
/// <b>에셋이 비어 있어도 돈다.</b> 결계 스프라이트 3상태도 표정도 SFX 도 아직 없다.
/// 슬롯을 비워 두면 해당 연출만 조용히 건너뛰고 대사는 끝까지 진행한다
/// (<see cref="BadEndingDirector"/> · <c>KitchenTriggerCutscene</c> 과 같은 방식).
/// SFX 는 AudioManager 에 등록된 이름만 넣는다 — 이름을 지어내지 않는다(CLAUDE.md §0-4).
/// </para>
///
/// <para>
/// ⚠ <b>카메라 정사영 크기를 바꾸지 않는다.</b> 결계가 화면 안쪽으로 밀려오는 것은
/// 스프라이트 오프셋으로만 만든다. 줌으로 당기면 픽셀 정수배가 깨진다(F-6).
/// </para>
///
/// <para>
/// ⚠ <b>세라는 등장하지 않는다.</b> 목소리만 나오며 스프라이트를 만들지 않는다.
/// 화자 이름도 포트레이트도 띄우지 않는다 — 그 처리는 yarn 쪽 화자 ID <c>세라목소리</c> 가
/// 맡는다(E-62-4 · F-7-5). 여기서 세라 오브젝트를 세우지 말 것.
/// </para>
///
/// <para>
/// ⚠ <b>루가 저항하는 동작을 넣지 않는다.</b> 필터가 환상으로 덮이는 것을 그대로 둔다.
/// 저항을 넣으면 루가 상황을 파악한 것이 되어 은폐 축이 무너진다(정본 S#21C 주석).
/// </para>
///
/// 배치: 숲 끝의 결계 오브젝트에 붙이거나, 빈 오브젝트에 붙이고 <see cref="barrier"/> 를 꽂는다.
/// </summary>
public class ForestBarrierDirector : MonoBehaviour
{
    public static ForestBarrierDirector Instance { get; private set; }

    /// <summary>종료 연출이 도는 중. 다른 시스템이 끼어들면 안 된다.</summary>
    public static bool IsPlaying { get; private set; }

    // ── 발동 ────────────────────────────────────────────────────────────────
    [Header("발동 — 결계와의 거리")]
    [Tooltip("결계의 기준 위치. 비우면 이 오브젝트 자신을 쓴다.")]
    public Transform barrier;

    [Tooltip("이 거리(월드 유닛) 안으로 들어오면 발동한다.\n" +
             "⚠ 너무 짧게 주면 결계에 코를 박은 뒤에야 열려 「도착한다」가 아니라 「부딪힌다」가 된다.\n" +
             "   카메라가 둘을 뒤에서 잡고 있으므로 결계가 화면에 들어올 즈음이 적당하다.")]
    public float triggerDistance = 3.5f;

    [Tooltip("플레이어. 비우면 Player 태그로 찾는다.")]
    public Transform player;

    [Tooltip("거리 검사 간격(초). 매 프레임 잴 필요가 없다.")]
    public float checkInterval = 0.1f;

    // ── 결계 3상태 (F-6-2) ──────────────────────────────────────────────────
    [Header("결계 — 3상태. 없으면 건너뛴다")]
    [Tooltip("① 평상. 빛으로 된 얇은 판이며 황금색이 언뜻 비칠 뿐이다.\n" +
             "자세히 봐야 알 수 있는 정도이므로 걷는 동안에는 한 번만 스치게 두고 아무도 반응하지 않는다.")]
    public SpriteRenderer barrierIdle;

    [Tooltip("② 접촉 시 국소 가시화. 쿠루가 열쇠를 댄 자리 주변만 드러난다.\n" +
             "세라의 결계 점검(F-6-1)과 같은 원리다. 범위와 지속을 크게 쓰지 않는다 — ③과 구분돼야 한다.")]
    public SpriteRenderer barrierTouch;

    [Tooltip("③ 강화. 전면 가시화. 이 상태로 데모가 끝난다.")]
    public SpriteRenderer barrierReinforced;

    [Tooltip("③ 이 화면 안쪽으로 밀려오는 거리(월드 유닛). 스프라이트 오프셋으로만 만든다.\n" +
             "⚠ 카메라 정사영 크기를 바꾸지 않는다. 줌으로 당기면 픽셀 정수배가 깨진다(F-6).")]
    public float reinforcedPushIn = 0.5f;

    [Tooltip("밀려오는 데 걸리는 시간(초).")]
    public float reinforcedPushDuration = 1.2f;

    // ── 화면 효과 ───────────────────────────────────────────────────────────
    [Header("화면 효과")]
    [Tooltip("강화 순간 화면 가장자리에 번지는 황금색.")]
    public Color reinforceEdgeColor = new Color(0.95f, 0.78f, 0.30f, 0.55f);

    [Tooltip("번짐이 이어지는 시간(초).")]
    public float reinforceEdgeDuration = 1.4f;

    // ── 박자 ────────────────────────────────────────────────────────────────
    [Header("박자")]
    [Tooltip("S#21A 가 끝나고 결계가 강화되기까지의 사이(초).\n" +
             "쿠루가 고개를 돌리는 순간에 맞춘다 — 「…뭐?」 직후다.")]
    public float delayBeforeReinforce = 0.6f;

    [Tooltip("「아, 그 생각을 못 했어요」 뒤의 무음(초).\n" +
             "정본: 「잠깐 아무 소리도 나지 않는다.」 목소리가 오기 전의 공백이다.")]
    public float silenceBeforeVoice = 1.6f;

    [Tooltip("목소리가 끝나고 둘이 돌아보기까지(초).")]
    public float delayBeforeLookBack = 0.5f;

    [Tooltip("돌아본 뒤 아무것도 없는 채로 두는 시간(초).\n" +
             "⚠ 이 공백이 끝난 다음에 부엉이가 운다. 순서를 바꾸지 말 것 — 공백이 먼저다.")]
    public float emptyBeatDuration = 1.2f;

    [Tooltip("부엉이가 울고 다시 걷기 시작하기까지(초).")]
    public float delayAfterOwl = 0.9f;

    // ── 음향 ────────────────────────────────────────────────────────────────
    [Header("음향 — AudioManager 에 등록된 이름만. 비우면 무음")]
    [Tooltip("부엉이 울음 「후우」. 대사창을 쓰지 않고 SFX 로 처리한다(F-4-4). 표기는 고정이다.\n" +
             "S#01 의 부엉이 SFX 를 재활용할 수 있는지는 F 에서 확인 중이다.")]
    public string sfxOwl = "";

    [Tooltip("결계가 강화되는 소리.")]
    public string sfxReinforce = "";

    [Tooltip("딱딱. 두 번 — 다시 걷기 시작하는 순간에 맞춘다(C-7-2 숲 후반).\n" +
             "「분노 이후의 당혹감」 곡선과 같은 강도이며 새 강도를 만들지 않는다.")]
    public string sfxClicking = "";

    [Tooltip("딱딱 두 번 사이의 간격(초).")]
    public float clickingGap = 0.22f;

    // ── 종료 화면 ───────────────────────────────────────────────────────────
    [Header("종료 화면")]
    [Tooltip("암전 시간(초).")]
    public float fadeOutDuration = 2f;

    [Tooltip("「인형화 00%」 를 띄워 두는 시간(초).")]
    public float endCardDuration = 4f;

    [Tooltip("종료 화면 뒤에 넘어갈 씬. 비우면 화면을 띄운 채 멈춘다.\n" +
             "⚠ 크레딧을 올리지 않는다(정본 [UI]). CreditsScene 을 넣지 말 것 —\n" +
             "   그쪽은 크레딧을 스크롤한 뒤 결과를 띄우는 화면이라 정본과 어긋난다.")]
    public string nextScene = SceneNames.Title;

    // ── 내부 ────────────────────────────────────────────────────────────────
    bool _fired;
    float _nextCheck;
    ClearSky.SimplePlayerController _lockedCtrl;
    Vector3 _reinforcedOrigin;

    void Awake()
    {
        Instance = this;
        if (barrier == null) barrier = transform;

        // 시작 상태 — 평상만 켜 둔다. 나머지는 각 단계에서 켠다.
        SetVisible(barrierIdle, true);
        SetVisible(barrierTouch, false);
        SetVisible(barrierReinforced, false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (_fired || IsPlaying) return;
        if (Time.time < _nextCheck) return;
        _nextCheck = Time.time + Mathf.Max(0.02f, checkInterval);

        if (player == null)
        {
            var go = GameObject.FindWithTag("Player");
            if (go == null) return;
            player = go.transform;
        }

        // 2D 탑다운이라 z 는 보지 않는다. 카메라·정렬 때문에 z 가 서로 다를 수 있다.
        float d = Vector2.Distance(player.position, barrier.position);
        if (d > triggerDistance) return;

        _fired = true;
        StartCoroutine(PlayDemoEnding());
    }

    /// <summary>디버그·에디터에서 거리와 무관하게 강제로 재생한다.</summary>
    public void ForcePlay()
    {
        if (_fired || IsPlaying) return;
        _fired = true;
        StartCoroutine(PlayDemoEnding());
    }

    // ─── 본 시퀀스 ──────────────────────────────────────────────────────────
    IEnumerator PlayDemoEnding()
    {
        IsPlaying = true;
        Time.timeScale = 1f;
        _lockedCtrl = YarnDialogue.LockPlayer();

        // 종료 씬에는 [목표] 가 없다. 떠 있을 수 있는 목표 패널을 먼저 지운다.
        ObjectiveManager.Instance?.ResetCutscene();
        ObjectiveManager.Instance?.HideHUD();

        Dbg.Log("[S#21] 결계 도달 — 데모 종료 연출 시작");

        // ── S#21A 1/2 — 결계 도달 ───────────────────────────────────────────
        // [FILTER] 직전 씬의 필터를 유지한다. 강제하지 않으며 어떤 값도 건드리지 않는다(F 문단 813).
        //   여기서 set_filter 를 부르지 않는 것이 그 지시다.
        //
        // ⚠ 결계는 아직 보이지 않는다. 루가 「아무것도 안 보이는데요」라고 말하는 자리다.
        yield return YarnDialogue.PlayAndWait(YarnNodes.Forest_Barrier_Arrival, false);

        // ── 열쇠를 댄다 ─────────────────────────────────────────────────────
        // 정본 문단 1238 — 「쿠루가 은색 열쇠를 허공에서 꺼내 결계에 가져다 댄다.」
        // 접촉면을 중심으로 그 자리만 드러난다. 세라의 결계 점검(F-6-1)과 같은 원리이며
        // 새 규칙이 아니다. 범위와 지속을 크게 쓰지 않는다 — 강화(③)의 전면 가시화와
        // 구분돼야 한다(F-6-2).
        //
        // ⚠ 이 자리가 노드를 둘로 나눈 이유다. 앞 노드에서 켜면 대사와 화면이 어긋난다.
        SetVisible(barrierTouch, true);

        // ── S#21A 2/2 — 열쇠를 댄 뒤 ────────────────────────────────────────
        yield return YarnDialogue.PlayAndWait(YarnNodes.Forest_Barrier_KeyCheck, false);

        // ── 결계 강화 ───────────────────────────────────────────────────────
        // 쿠루가 고개를 돌리는 순간이다. 「…뭐?」 바로 뒤.
        yield return new WaitForSeconds(delayBeforeReinforce);
        yield return ReinforceBarrier();

        // ── S#21C 1/3 ───────────────────────────────────────────────────────
        yield return YarnDialogue.PlayAndWait(YarnNodes.Forest_Barrier_Reinforced, false);

        // 잠깐 아무 소리도 나지 않는다.
        yield return new WaitForSeconds(silenceBeforeVoice);

        // ── S#21C 2/3 — 세라의 목소리 ───────────────────────────────────────
        // 사방에서 온다. 방향을 특정하지 않는다 — 특정하면 플레이어가 세라의 위치를
        // 계산하게 되고 「어디까지 왔나」가 이 씬의 주제로 바뀐다.
        yield return YarnDialogue.PlayAndWait(YarnNodes.Forest_Sera_Voice, false);

        // ── 돌아보기 ────────────────────────────────────────────────────────
        yield return new WaitForSeconds(delayBeforeLookBack);
        LookBack();

        // 아무것도 없다. 공백을 먼저 둔다.
        yield return new WaitForSeconds(emptyBeatDuration);

        // 공백이 끝난 다음에 부엉이가 운다.
        PlaySfxIfNamed(sfxOwl);
        yield return new WaitForSeconds(delayAfterOwl);

        // ── S#21C 3/3 — 다시 걸음 ───────────────────────────────────────────
        yield return YarnDialogue.PlayAndWait(YarnNodes.Forest_Demo_End, false);

        // 딱딱. 두 번 — 다시 걷기 시작하는 순간에 맞춘다.
        yield return PlayClickingTwice();

        // ── 종료 ────────────────────────────────────────────────────────────
        yield return EndDemo();
    }

    /// <summary>
    /// 결계 강화. 필터를 환상으로 밀고 토글을 봉인한다.
    ///
    /// <para>미는 주체는 세라다. 결계를 강화하는 힘이 필터까지 밀며, 마시멜로가 게이지를
    /// 환상 극값으로 옮기는 것과 같은 계통이다(C-3-2 · S#04E) — 새 규칙이 아니라
    /// 기존 강제 전환의 세 번째 발생원이다.</para>
    ///
    /// <para>⚠ 순서가 중요하다. 환상으로 덮은 <b>뒤에</b> 봉인한다. 봉인이 먼저면 전환이 막힌다.</para>
    /// </summary>
    IEnumerator ReinforceBarrier()
    {
        PlaySfxIfNamed(sfxReinforce);

        // 강화된 결계가 전면으로 드러난다. 국소 가시화는 여기서 넘겨준다.
        SetVisible(barrierTouch, false);
        SetVisible(barrierIdle, false);
        SetVisible(barrierReinforced, true);

        // 필터 강제 — 루가 단검을 파지한 상태여도 환상으로 덮인다.
        //   컷이 아니라 번지는 형태로 처리해 S#12 의 「끊긴 것」과 구분한다.
        //   현실 필터로 버티며 온 플레이어일수록 강하게 걸린다. 유도 연출을 따로 만들지 않는다 —
        //   데려온 것이 아니라 빼앗은 것이다.
        DaggerFilterController.Instance?.SwitchToFantasyForced();
        FilterManager.Instance?.SetFilter(FilterType.Fantasy);

        // 이후 토글 입력을 받지 않는다. 잠겼다는 UI 표시를 두지 않는다(정본 ▶조작).
        DaggerFilterController.SealToggle();

        ScreenEdgeEffectController.ShowEdge(reinforceEdgeColor, reinforceEdgeDuration);

        Dbg.Log("[S#21] 결계 강화 — 필터 환상 강제 · 토글 봉인");

        // 화면 안쪽으로 밀려온다. 스프라이트 오프셋으로만 만든다(F-6).
        if (barrierReinforced == null || reinforcedPushIn <= 0f)
        {
            yield return new WaitForSeconds(reinforceEdgeDuration * 0.5f);
            yield break;
        }

        Transform t = barrierReinforced.transform;
        _reinforcedOrigin = t.localPosition;

        // 플레이어가 있는 쪽으로 민다. 없으면 결계의 아래쪽(화면 안쪽)으로 본다.
        Vector3 dir = player != null
            ? (Vector3)((Vector2)(player.position - barrier.position)).normalized
            : Vector3.down;

        Vector3 to = _reinforcedOrigin + dir * reinforcedPushIn;
        float elapsed = 0f;
        while (elapsed < reinforcedPushDuration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / reinforcedPushDuration));
            t.localPosition = Vector3.Lerp(_reinforcedOrigin, to, k);
            yield return null;
        }
        t.localPosition = to;
    }

    /// <summary>
    /// 둘이 동시에 뒤를 돌아본다.
    ///
    /// <para>소리가 난 방향을 본 것이 아니라 어디서 나는지 몰라서 돌아본 것이므로
    /// 시선을 한 지점으로 모으지 않는다. 그리고 아무것도 없다 — S#16A 의 회수다.</para>
    ///
    /// <para>전용 스프라이트가 없으므로 지금은 좌우 반전으로만 표현한다.
    /// 「뒤를 돌아보는 동작」 에셋이 들어오면 여기서 그것으로 바꾼다(D-3 S#21 신규 에셋).</para>
    /// </summary>
    void LookBack()
    {
        FlipHorizontally(player);
        var companion = FindFirstObjectByType<CompanionFollow>();
        if (companion != null) FlipHorizontally(companion.transform);

        Dbg.Log("[S#21] 둘이 돌아본다 — 아무것도 없다");
    }

    static void FlipHorizontally(Transform t)
    {
        if (t == null) return;
        var sr = t.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) sr.flipX = !sr.flipX;
    }

    IEnumerator PlayClickingTwice()
    {
        if (string.IsNullOrEmpty(sfxClicking)) yield break;
        PlaySfxIfNamed(sfxClicking);
        yield return new WaitForSeconds(clickingGap);
        PlaySfxIfNamed(sfxClicking);
    }

    /// <summary>
    /// 암전 → 「인형화 00%」 한 줄 → 데모 종료.
    ///
    /// <para>⚠ 크레딧을 올리지 않는다(정본 [UI]). 값만 바뀌는 한 줄이다.</para>
    /// <para>⚠ % 는 화면 표시 전용 표기다. 문서와 내부 값은 숫자를 쓰며, 두 표기가 다른 것은
    /// 오류가 아니다(C-11).</para>
    /// </summary>
    IEnumerator EndDemo()
    {
        if (TransitionManager.Instance != null)
            yield return TransitionManager.Instance.FadeToBlack(fadeOutDuration);

        // 남아 있는 대사를 먼저 끊는다. 줄이 페이드 중인 채로 씬을 넘기면 Yarn 의
        // LinePresenter 가 파괴된 CanvasGroup 을 만져 예외를 던진다(BadEndingDirector 실측).
        if (YarnDialogue.IsRunning) YarnDialogue.Runner.Stop();

        GameObject card = BuildEndCard(CurrentPuppetization());
        yield return new WaitForSecondsRealtime(endCardDuration);

        IsPlaying = false;
        YarnDialogue.UnlockPlayer(_lockedCtrl);
        _lockedCtrl = null;

        if (string.IsNullOrEmpty(nextScene))
        {
            // 화면을 띄운 채로 둔다. 「종결이 아니라 정지」를 그대로 두고 싶을 때의 선택지다.
            yield break;
        }

        if (card != null) Destroy(card);
        UnityEngine.SceneManagement.SceneManager.LoadScene(nextScene);
    }

    static float CurrentPuppetization()
    {
        if (CorruptionManager.Instance != null) return CorruptionManager.Instance.currentCorruption;
        return GameState.player.IsInitialized ? GameState.player.puppetization : 0f;
    }

    /// <summary>검은 화면에 한 줄. 페이드(999) 위에 얹는다.</summary>
    static GameObject BuildEndCard(float puppetization)
    {
        var root = new GameObject("S21 EndCard [Auto]");
        DontDestroyOnLoad(root);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;          // TransitionManager 의 암전(999) 위
        UiCanvasScale.Add(root);
        root.AddComponent<GraphicRaycaster>();

        var bgGo = new GameObject("Black");
        bgGo.transform.SetParent(root.transform, false);
        var bg = bgGo.AddComponent<Image>();
        bg.color = Color.black;
        bg.raycastTarget = true;             // 뒤쪽 UI 클릭을 막는다
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        var textGo = new GameObject("Line");
        textGo.transform.SetParent(root.transform, false);
        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.text          = $"인형화 {Mathf.RoundToInt(puppetization):00}%";
        text.fontSize      = 22f;
        text.color         = Color.white;
        text.alignment     = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        return root;
    }

    static void SetVisible(SpriteRenderer sr, bool on)
    {
        if (sr != null) sr.enabled = on;
    }

    static void PlaySfxIfNamed(string soundName)
    {
        if (!string.IsNullOrEmpty(soundName)) AudioManager.Instance?.Play(soundName);
    }

    void OnDrawGizmosSelected()
    {
        Transform origin = barrier != null ? barrier : transform;
        Gizmos.color = new Color(0.95f, 0.78f, 0.30f, 0.9f);
        Gizmos.DrawWireSphere(origin.position, triggerDistance);
    }
}
