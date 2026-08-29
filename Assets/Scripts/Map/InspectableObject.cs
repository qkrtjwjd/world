using System.Collections;
using UnityEngine;

/// <summary>
/// 조사 가능한 물건 1건. 기획 근거는 C-16, 구현 기준은 F-8 이다.
///
/// ⚠ <see cref="InteractionTrigger"/> 와 별개다. 그쪽은 문·상자 같은 <b>진행</b> 오브젝트용이고
///   접근 시 "E키를 눌러 …" 표시가 붙는데, 조사 오브젝트는 그 표시가 금지돼 있다
///   (C-16-8 · F-8-6 — 표시는 세이브 포인트와 솔에만 붙는다). 라디오도 마찬가지로
///   InteractionTrigger 는 [라디오] 버튼을 띄우지만 E-52 가 그 방식을 폐기했다.
///   그래서 기존 컴포넌트를 고치지 않고 조사 전용으로 새로 둔다.
///
/// 대사는 만들지 않는다. 문안의 단일 출처는 D-4 이고, 여기는 <b>어느 노드를 부를지</b>만 정한다.
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
    }

    [Header("정본 대응")]
    [Tooltip("F-8-3 의 코드 ID. 노드 이름이 여기서 나온다 (house_ / town_ / forest_ 접두).\n" +
             "원고 ID(한글)는 코드 경계 안으로 들이지 않는다.")]
    [SerializeField] private string codeId = "";

    [SerializeField] private Layer layer = Layer.Density;

    [Tooltip("조사는 되지만 대사창이 뜨지 않는다. 보여주는 것 자체가 내용이다 (C-16-4).\n" +
             "S#07 의 유의 접시·신발이 그 예다.")]
    [SerializeField] private bool silent = false;

    [Header("감지")]
    [SerializeField] private float interactionRange = 0.75f;

    // ⚠ 상호작용 표시 필드를 두지 않는다 (C-16-8 · F-8-6).
    // ⚠ 라디오 버튼 필드를 두지 않는다 — 라디오는 조사 결과 뒤에 붙으며 대비 노드 안에서
    //    조건 분기한다 (E-52 · F-8-4). 입력 축을 늘리지 않는다.
    // ⚠ 인형화·아이템·진행 상태를 바꾸는 필드를 두지 않는다. 선택 오브젝트는 결과가 없고
    //    (C-16-6), 밀도·대비도 조사만으로 무언가를 움직이지 않는다.

    public string CodeId => codeId;
    public Layer  ObjectLayer => layer;
    public float  Range => interactionRange;

    private bool _running = false;

    void OnEnable()  => InspectableObjectManager.Register(this);
    void OnDisable() => InspectableObjectManager.Unregister(this);

    /// <summary>조사한다. 판정은 이 시점에 1회이며 조사 중 재판정하지 않는다 (F-8-2).</summary>
    public void Inspect()
    {
        if (_running) return;

        // 무대사 — 노드를 만들지 않았으므로 대사창을 띄우지 않고 상태도 바꾸지 않는다.
        if (silent) return;

        string node = ResolveNode();
        if (string.IsNullOrEmpty(node)) return;

        StartCoroutine(Run(node));
    }

    private IEnumerator Run(string node)
    {
        _running = true;
        try
        {
            yield return YarnDialogue.PlayAndWait(node);
        }
        finally
        {
            _running = false;
        }
    }

    /// <summary>
    /// 부를 노드를 고른다. 대비 오브젝트만 심리 게이지를 보고, 없는 버전은 fantasy 로 폴백한다.
    /// 빈 노드를 두지 않기 때문에 폴백이 필요하다 (F-8-2).
    /// </summary>
    private string ResolveNode()
    {
        if (string.IsNullOrEmpty(codeId))
        {
            Debug.LogWarning($"[InspectableObject] '{name}': 코드 ID 가 비어 있습니다. " +
                             "F-8-3 매핑표의 코드 ID 를 넣어 주세요.");
            return null;
        }

        // 밀도·선택은 인형화도 필터도 참조하지 않는다.
        // 선택지 축소(3→2)는 Yarn 쪽 <<if $인형화 < 31>> 이 처리한다 — 자세한 것은 아래 주석.
        if (layer != Layer.Contrast)
            return codeId;

        string wanted = $"{codeId}_{VersionSuffix()}";
        if (YarnDialogue.NodeExists(wanted))
            return wanted;

        string fallback = $"{codeId}_F";
        if (YarnDialogue.NodeExists(fallback))
            return fallback;

        Debug.LogWarning($"[InspectableObject] '{name}': '{wanted}' 도 '{fallback}' 도 없습니다. " +
                         "D-4 원고와 매핑표를 확인해 주세요.");
        return null;
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
