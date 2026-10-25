using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Yarn.Unity;

/// <summary>
/// 조사 가능한 물건 1건. 기획 근거는 C-16, 구현 기준은 F-8 이다.
///
/// ⚠ <see cref="InteractionTrigger"/> 와 별개다. 그쪽은 문·상자 같은 <b>진행</b> 오브젝트용이고
///   접근 시 "E키를 눌러 …" 표시가 붙는데, 조사 오브젝트는 그 표시가 금지돼 있다
///   (C-16-9 · F-8-6 — 표시는 세이브 포인트와 솔에만 붙는다).
///   그래서 기존 컴포넌트를 고치지 않고 조사 전용으로 새로 둔다.
///
/// 대사는 만들지 않는다. 문안의 단일 출처는 D-4 이고(변환기가 Objects.yarn 으로 낸다),
/// 여기는 <b>어느 노드를 언제 부를지</b>와 층마다 붙는 동작(획득 · 1회 · 앉기 …)만 정한다.
///
/// 노드 이름 (변환기 규약 · F-8-9)
///   코드ID          첫 조사(밀도 · 획득 · 획득 확인 · 진행 · 저장)
///   코드ID_again    재조사 — 가져간 뒤 「다른건 없다」
///   코드ID_F / _R   대비 필터 버전 · 코드ID_F2 = 환상에서 두 번째 조사부터
///   라디오 줄       대비 노드 맨 위 &lt;&lt;if $라디오키&gt;&gt; — 라디오 키(Q)로 열 때만 나온다
/// </summary>
public class InspectableObject : MonoBehaviour
{
    public enum Layer
    {
        /// <summary>물건마다 전용 1줄. 필터 분기 없음 (C-16-4)</summary>
        Density,
        /// <summary>구간별 2~3버전. 필터에 따라 루가 다르게 말한다 (C-16-2)</summary>
        Contrast,
        /// <summary>선택지 + 선택별 반응. 결과 없음 (C-16-6)</summary>
        Choice,
        /// <summary>조사하면 얻는다. 다시 조사하면 「다른건 없다」(D-4-1 · 획득)</summary>
        Acquire,
        /// <summary>가져갈지 묻는다. 가져가면 얻고, 안 가져가면 반응 없이 끝난다(C-16-11)</summary>
        AcquireConfirm,
    }

    [Header("정본 대응")]
    [Tooltip("F-8-3 의 코드 ID. 노드 이름이 여기서 나온다 (house_ / town_ / forest_ 접두).\n" +
             "원고 ID(한글)는 코드 경계 안으로 들이지 않는다.")]
    [SerializeField] private string codeId = "";

    [SerializeField] private Layer layer = Layer.Density;

    [Tooltip("조사는 되지만 대사창이 뜨지 않는다. 보여주는 것 자체가 내용이다 (C-16-4).\n" +
             "S#07 의 유의 접시·신발이 그 예다. 앉기와 함께 쓰면 앉는 동작이 반응이다(소파).")]
    [SerializeField] private bool silent = false;

    [Header("1회 · 앉기 (C-16-11)")]
    [Tooltip("첫 조사에만 대사. 두 번째부터는 대사 없이 onRepeatInspect(루의 모션)만 — 유의 의자.")]
    [SerializeField] private bool once = false;
    [Tooltip("조사하면 루가 앉는다. 이동 키로 일어난다 — 거실 소파. seatPoint 가 앉는 자리다.")]
    [SerializeField] private bool sit = false;
    [SerializeField] private Transform seatPoint;

    [Header("획득 (Acquire · AcquireConfirm)")]
    [SerializeField] private ItemData acquireItem;
    [Min(1)] [SerializeField] private int acquireCount = 1;
    [Tooltip("집을 때 인형화 변동(데모 고정값). 부엌 마시멜로 +2 (C-2-2 · F-2-6).")]
    [SerializeField] private float acquireCorruption = 0f;

    [Header("라디오 (C-16-7 · F-8-4)")]
    [Tooltip("라디오 반응 대상. 라디오 키(Q)를 조사 범위 안에서 누르면 유의 한 줄이 나온다. 대비 오브젝트만.")]
    [SerializeField] private bool radioTarget = false;

    [Header("효과음 (AudioManager 등록 이름 · 비우면 무음)")]
    [Tooltip("대사 앞 — 잠긴 문고리 소리(세라 방 문 · D-4-1).")]
    [SerializeField] private string sfxBeforeLine = "";
    [Tooltip("대사 뒤 — 유의 의자의 딱 1회(D-4-1). 1회 오브젝트는 첫 조사에만 낸다.")]
    [SerializeField] private string sfxAfterLine = "";

    [Header("반응 시점")]
    [Tooltip("세라 외출(S#04H) 뒤에만 반응한다. 집 안 자유 탐색이 그때 열린다(D-S#04H · E-74-1).")]
    [SerializeField] private bool requireSeraOut = false;

    [Header("감지")]
    [SerializeField] private float interactionRange = 0.75f;

    [Tooltip("1회 오브젝트의 두 번째 조사부터 — 대사 대신 루의 모션(몸을 돌린다 등).")]
    public UnityEvent onRepeatInspect;
    [Tooltip("물건을 얻은 직후(획득 · 획득 확인에서 가져간다).")]
    public UnityEvent onAcquired;

    // ⚠ 상호작용 표시 필드를 두지 않는다 (C-16-9 · F-8-6).
    // ⚠ 물건에 인형화 분기를 붙이지 않는다(C-16-9). 획득 때의 고정값(acquireCorruption)은 「챙기는 행위」의 값이다(E-71-3).

    public string CodeId => codeId;
    public Layer  ObjectLayer => layer;
    public float  Range => interactionRange;
    public bool   IsRadioTarget => radioTarget && layer == Layer.Contrast;

    private bool _running = false;

    /// <summary>진행 상태(저장 슬롯 · GameStateManager.flags)에 쓰는 키. 1시간 배드 엔딩 복귀가 되돌린다(F-6).</summary>
    public static string DoneKey(string id)   => $"obj.{id}.done";
    public static string SeenFKey(string id)  => $"obj.{id}.seenF";

    bool Done
    {
        get => GameStateManager.Instance != null && GameStateManager.Instance.GetFlag(DoneKey(codeId));
        set => GameStateManager.Instance?.SetFlag(DoneKey(codeId), value);
    }

    void OnEnable()  => InspectableObjectManager.Register(this);
    void OnDisable() => InspectableObjectManager.Unregister(this);

    /// <summary>지금 조사를 받는가 — 세라 외출 전 잠금 · 다른 조사 진행 중.</summary>
    public bool CanInspect => !_running && (!requireSeraOut || GameState.isSeraOut);

    /// <summary>조사한다. 판정은 이 시점에 1회이며 조사 중 재판정하지 않는다 (F-8-2).</summary>
    public void Inspect()
    {
        if (!CanInspect) return;

        // 물건별 진행 처리(거실 창문 · 그림자 토끼 · 다락방 상자 …)가 먼저 가로챌 수 있다.
        foreach (var h in GetComponents<IInspectOverride>())
            if (h.TryInspect(this)) return;

        if (sit) { SitHere(); return; }

        // 무대사 — 노드를 만들지 않았으므로 대사창을 띄우지 않고 상태도 바꾸지 않는다.
        if (silent) return;

        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        _running = true;
        try
        {
            switch (layer)
            {
                case Layer.Acquire:        yield return RunAcquire(confirm: false); break;
                case Layer.AcquireConfirm: yield return RunAcquire(confirm: true);  break;
                case Layer.Contrast:       yield return RunContrast();              break;
                default:                   yield return RunDensity();               break;
            }
        }
        finally
        {
            _running = false;
        }
    }

    IEnumerator RunDensity()
    {
        if (once && Done)
        {
            onRepeatInspect?.Invoke();
            yield break;
        }
        PlaySfx(sfxBeforeLine);
        yield return YarnDialogue.PlayIfExists(codeId);
        PlaySfx(sfxAfterLine);
        if (once) Done = true;
    }

    IEnumerator RunAcquire(bool confirm)
    {
        if (Done)
        {
            yield return YarnDialogue.PlayIfExists(codeId + "_again");
            yield break;
        }

        LastChoice = 0;
        yield return YarnDialogue.PlayIfExists(codeId);

        // 획득 확인 — 「가져간다」(첫 선택지)만 얻는다. 「가져가지 않는다」는 반응 없이 끝나고 다음에 같은 질문(C-16-11).
        if (confirm && LastChoice != 1) yield break;

        Give();
        Done = true;
        onAcquired?.Invoke();
    }

    void Give()
    {
        if (acquireItem != null && InventoryManager.Instance != null)
        {
            var list = new System.Collections.Generic.List<ItemData>();
            for (int i = 0; i < acquireCount; i++) list.Add(acquireItem);
            InventoryManager.Instance.AddItems(list);
        }
        if (acquireCorruption != 0f)
            CorruptionManager.Instance?.AddCorruption(acquireCorruption);
    }

    IEnumerator RunContrast()
    {
        string node = ResolveContrastNode(out string version);
        if (string.IsNullOrEmpty(node)) yield break;
        yield return YarnDialogue.PlayAndWait(node);
        if (version == "F") GameStateManager.Instance?.SetFlag(SeenFKey(codeId), true);
    }

    /// <summary>라디오 키(Q)로 연다 — 같은 대비 노드를 열되 맨 위 분기가 유의 줄만 내고 멈춘다(F-8-4).</summary>
    public IEnumerator PlayRadio()
    {
        if (_running) yield break;
        string node = ResolveContrastNode(out _);
        if (string.IsNullOrEmpty(node)) yield break;
        _running = true;
        try
        {
            YarnCommandBridge.RadioKeyPending = true;
            yield return YarnDialogue.PlayAndWait(node);
        }
        finally
        {
            YarnCommandBridge.RadioKeyPending = false;
            _running = false;
        }
    }

    /// <summary>
    /// 대비 노드를 고른다. 심리 게이지 구간으로 버전을 정하고, 없는 버전은 fantasy 로 폴백한다 — 빈 노드를
    /// 두지 않기 때문에 폴백이 필요하다 (F-8-2). 환상에서 두 번째 조사부터는 _F2 가 있으면 그것(D-4-1 마네킹).
    /// </summary>
    private string ResolveContrastNode(out string version)
    {
        version = null;
        if (string.IsNullOrEmpty(codeId))
        {
            Debug.LogWarning($"[InspectableObject] '{name}': 코드 ID 가 비어 있습니다. " +
                             "F-8-3 매핑표의 코드 ID 를 넣어 주세요.");
            return null;
        }

        string v = VersionSuffix();
        string wanted = $"{codeId}_{v}";
        if (!YarnDialogue.NodeExists(wanted))
        {
            v = "F";
            wanted = $"{codeId}_F";
            if (!YarnDialogue.NodeExists(wanted))
            {
                Debug.LogWarning($"[InspectableObject] '{name}': '{codeId}_{VersionSuffix()}' 도 '{wanted}' 도 없습니다. " +
                                 "D-4 원고와 매핑표를 확인해 주세요.");
                return null;
            }
        }

        bool seenF = GameStateManager.Instance != null && GameStateManager.Instance.GetFlag(SeenFKey(codeId));
        if (v == "F" && seenF && YarnDialogue.NodeExists($"{codeId}_F2"))
            wanted = $"{codeId}_F2";

        version = v;
        return wanted;
    }

    /// <summary>심리 게이지 구간. F-2-2 를 그대로 쓴다 — 30 미만 F / 30~69 N / 70 이상 R.</summary>
    private static string VersionSuffix()
    {
        float gauge = GaugeManager.Instance != null
            ? GaugeManager.Instance.fantasyRealityGauge
            : 0f;
        if (gauge >= 70f) return "R";
        if (gauge >= 30f) return "N";
        return "F";
    }

    // ── 앉기 (C-16-11) ─────────────────────────────────────────────────
    // 앉은 스프라이트(코트 미착용 · 착용 두 벌)는 아직 없다(🔴). 자리로 옮겨 정면을 보고 멈춰 서는 것으로 대신한다.
    // 일어나는 것은 이동 키 — 따로 처리하지 않아도 이동하면 자리를 떠난다.
    void SitHere()
    {
        var lu = LuPose.Lu;
        if (lu == null || seatPoint == null) return;
        var rb = lu.GetComponent<Rigidbody2D>();
        if (rb != null) { rb.linearVelocity = Vector2.zero; rb.position = seatPoint.position; }
        lu.position = seatPoint.position;
        LuPose.Face(lu, Vector2.down);
    }

    static void PlaySfx(string soundName)
    {
        if (!string.IsNullOrEmpty(soundName)) AudioManager.Instance?.Play(soundName);
    }

    // ── 획득 확인 선택 결과 ─────────────────────────────────────────────
    /// <summary>마지막 &lt;&lt;object_choice n&gt;&gt; 값. 0 = 아직 고르지 않음.</summary>
    public static int LastChoice { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => LastChoice = 0;

    /// <summary>&lt;&lt;object_choice n&gt;&gt; — 변환기가 획득 확인 선택지마다 붙인다(F-8-9). 물건 쪽이 결과를 읽는다.</summary>
    [YarnCommand("object_choice")]
    public static void ObjectChoice(int index) => LastChoice = index;
}

/// <summary>
/// 물건별 진행 처리. <see cref="InspectableObject.Inspect"/> 가 층 처리보다 먼저 묻는다.
/// true 를 돌려주면 그 조사를 이쪽이 맡은 것이다.
/// </summary>
public interface IInspectOverride
{
    bool TryInspect(InspectableObject obj);
}

// ---------------------------------------------------------------------------
// 선택지 축소에 대하여 (C-16-6 · F-8-2)
//
// 인형화 31 이상에서 가장 능동적인 항목 하나가 빠지는데, 그 판정은 여기가 아니라 Yarn 에 있다.
// 변환기가 (31-) 표기를 `-> 항목 <<if $인형화 < 31>>` 로 내보내고, $인형화 는 대화 시작 시
// YarnCommandBridge 가 주입한다. 구현 측이 어느 항목을 뺄지 판단하지 않는다는 F-8-2 주석과 맞는다.
//
// ⚠ 그것이 "흔적을 남기지 않는다" 를 지키는 것은 Dialogue.prefab ▸ OptionsPanel 의
//   OptionsPresenter.showUnavailableOptions 가 0 이기 때문이다. 그 값이 1 이 되면 세 번째
//   항목이 회색으로 남아 정본을 어긴다.
