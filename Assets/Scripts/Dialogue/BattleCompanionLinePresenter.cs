using UnityEngine;
using Yarn.Unity;

/// <summary>
/// 턴제 전투 중에만 동작하는 Yarn 프레젠터. 대사를 BattleUI 안에 화자별로 나눠 그린다.
///
/// <para><b>어디에 그리나.</b> 동료(쿠루 등)는 좌측 상단 <see cref="BattleCompanionUI"/> 대화창에
/// <b>이름 없이 본문만</b> — 그 창은 동료 전용이라 누가 말하는지 자리로 구분된다.
/// 루의 대사는 하단 전투 로그 상자를 빌려 쓰고, 이름은 <see cref="BattleSystem.playerNameText"/> 에
/// 따로 찍는다 (<see cref="BattleSystem.ShowPlayerLine"/>).</para>
///
/// <para><b>왜 필요한가.</b> 필드 대화창은 <c>Dialogue.prefab</c> 캔버스(sortingOrder 0)에 있는데
/// BattleUI 캔버스는 sortingOrder 100 이고 전체화면 불투명 배경을 깐다. 그래서 전투 중 대사는
/// 화면에 아예 나오지 않는다. 게다가 턴제 전투는 <c>Time.timeScale = 0</c> 으로 도는데 Yarn 의
/// 페이드·타이프라이터는 스케일 시간을 쓰므로 저절로 진행되지도 않는다.</para>
///
/// <para><b>필드 대화창 억제.</b> <see cref="DialogueRunner"/> 는 <c>enabled == false</c> 인
/// 프레젠터를 건너뛴다. 그래서 전투 중에만 <see cref="fieldPresenter"/> 를 재운다.
/// 전환 시점은 <see cref="BattleCompanionUI"/> 가 <see cref="SyncNow"/> 로 알려주고,
/// <see cref="Update"/> 가 안전망으로 매 프레임 다시 맞춘다 (<c>timeScale = 0</c> 에서도 돈다).</para>
///
/// <para>전투가 아닐 때는 <see cref="RunLineAsync"/> 가 즉시 반환하므로 필드 대화는 그대로다.</para>
///
/// <para><b>부착 위치.</b> 반드시 <c>Dialogue</c> 루트처럼 <b>항상 켜져 있는</b> 오브젝트에 붙인다.
/// <c>DialoguePanel</c> 에 붙이면 <see cref="YarnCommandBridge"/> 가 대화 종료 때 그 오브젝트를 꺼버려
/// <see cref="Update"/> 가 멈추고, 다음 대사의 디스패치가 동기화보다 빨라져 필드 대화창으로 샌다.</para>
/// </summary>
public class BattleCompanionLinePresenter : DialoguePresenterBase
{
    [Tooltip("전투 중 재울 필드 대화창. 비우면 같은 오브젝트에서 찾는다.")]
    [SerializeField] LinePresenter fieldPresenter;

    [Tooltip("초당 글자 수. 0 이하면 타이핑 없이 즉시 표시한다.")]
    [SerializeField] float lettersPerSecond = 40f;

    bool _warnedNoUI;

    static BattleCompanionLinePresenter _instance;

    static bool InBattle => BattleSystem.Instance != null;

    void Awake()
    {
        if (fieldPresenter == null) fieldPresenter = GetComponent<LinePresenter>();
        _instance = this;
        Sync();
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    /// <summary>
    /// 전투 UI 가 생기고 사라지는 <b>바로 그 순간</b> 호출한다 (<see cref="BattleCompanionUI"/> 의 Awake/OnDestroy).
    /// <see cref="Update"/> 만으로 맞추면 전투가 생성된 프레임과 다음 Update 사이에 디스패치된 라인이
    /// 필드 대화창으로 새기 때문이다.
    /// </summary>
    public static void SyncNow()
    {
        if (_instance != null) _instance.Sync();
    }

    void Sync()
    {
        // DialogueRunner 는 enabled 가 꺼진 프레젠터를 건너뛴다.
        // 전투 중에는 필드 대화창을 통째로 재우고 동료 대화창만 쓴다.
        if (fieldPresenter != null) fieldPresenter.enabled = !InBattle;
    }

    void OnEnable() => Sync();
    void Update()   => Sync();   // 안전망

    void OnDisable()
    {
        // 이 컴포넌트가 꺼진 채로 필드 대화창까지 재워두면 대사가 영영 안 보인다.
        if (fieldPresenter != null) fieldPresenter.enabled = true;
    }

    public override YarnTask OnDialogueStartedAsync() => YarnTask.CompletedTask;

    public override YarnTask OnDialogueCompleteAsync()
    {
        Clear();
        return YarnTask.CompletedTask;
    }

    public override async YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
    {
        if (!InBattle) return;

        // 이름은 본문에서 떼어낸다. 동료창은 쿠루 전용이라 이름을 띄우지 않고,
        // 루 대사는 BattleSystem 의 이름칸이 따로 받는다.
        string body = line.TextWithoutCharacterName.Text ?? string.Empty;
        bool toPlayerBox = IsPlayerSpeaker(line);

        if (!toPlayerBox && BattleCompanionUI.Instance == null)
        {
            // 조용히 넘어가면 전투 중 대사가 통째로 사라진 이유를 알 방법이 없다.
            if (!_warnedNoUI)
            {
                _warnedNoUI = true;
                Debug.LogWarning("[BattleCompanionLinePresenter] BattleCompanionUI 가 없습니다. " +
                                 "BattleUI 프리팹의 companion 오브젝트를 확인하세요.");
            }
            return;
        }

        // ── 타이핑 (unscaled) ────────────────────────────────────────────
        if (lettersPerSecond > 0f && body.Length > 0)
        {
            float secondsPerLetter = 1f / lettersPerSecond;
            float accumulated = 0f;
            int   shown = 0;

            Draw(toPlayerBox, string.Empty);

            while (shown < body.Length && !token.HurryUpToken.IsCancellationRequested)
            {
                accumulated += Time.unscaledDeltaTime;
                while (accumulated >= secondsPerLetter && shown < body.Length)
                {
                    accumulated -= secondsPerLetter;
                    shown++;
                }
                Draw(toPlayerBox, body.Substring(0, shown));
                await YarnTask.Yield();
            }
        }

        Draw(toPlayerBox, body);

        // 스킵 입력(스페이스·클릭)이 올 때까지 붙잡는다. 자동 진행은 하지 않는다.
        await YarnTask.WaitUntilCanceled(token.NextContentToken).SuppressCancellationThrow();

        Clear();
    }

    /// <summary>
    /// 이 줄이 루의 대사인가. yarn 에는 루의 화자가 <c>{$이름}</c> 로 적혀 있어
    /// 런타임에 플레이어가 정한 이름으로 치환된다. 독백(이름 없음)도 루 쪽으로 보낸다.
    /// </summary>
    static bool IsPlayerSpeaker(LocalizedLine line)
    {
        string speaker = line.CharacterName;
        if (string.IsNullOrWhiteSpace(speaker)) return true;
        return speaker == PlayerIdentity.Name || speaker == "루독백";
    }

    static void Draw(bool toPlayerBox, string text)
    {
        if (toPlayerBox) BattleSystem.Instance?.ShowPlayerLine(text);
        else             BattleCompanionUI.Instance?.ShowLine(text);
    }

    static void Clear()
    {
        BattleSystem.Instance?.HidePlayerLine();
        BattleCompanionUI.Instance?.HideLine();
    }
}
