using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 숲 전투 사망 화면 — F-9 · C-13-5 · E-66 · 원고 D-7. 턴제 · 액션 공통.
///
/// <para><b>흐름</b> (F-9-1 · 시간 값은 초안값)</para>
/// <list type="number">
/// <item>쓰러지는 즉시 입력을 막는다 — 검은 판(처음엔 투명)이 클릭을 받고, 세계는 멈춘다(timeScale 0).</item>
/// <item>암전 <see cref="BlackoutSeconds"/>. 전투 BGM 을 끊는다.</item>
/// <item>검은 화면 가운데 문장 한 줄. 이름창 · 포트레이트 · 인형화 수치 · 소리 없음(F-9-4).</item>
/// <item>문장이 뜬 뒤 <see cref="InputBlockSeconds"/> 동안 입력을 받지 않는다 — 연타가 화면을 넘기지 않게.</item>
/// <item>입력 → 마지막 저장 지점(<see cref="SaveManager.ReturnToLastRabbit"/>). 입력이 없으면 <see cref="AutoAdvanceSeconds"/> 뒤 자동.</item>
/// </list>
/// <para>확인창을 띄우지 않는다. BE#01 · BE#02 의 엔딩 화면 UI 와 공유하지 않는다(F-9-1 ※).
/// 인형화 100 배드 엔딩에도 쓰지 않는다(C-2-6) — 숲 전투에서 쓰러졌을 때만 부른다.</para>
///
/// <para><b>문장</b> (F-9-2) — 사망 횟수는 <b>계정 단위</b>라 PlayerPrefs 에 둔다(Yarn 변수 아님 · 슬롯 삭제로 지워지지 않음).
/// 1회째 첫 사망 문장, 2회째부터 두 줄을 번갈아(2회 앞 줄 · 3회 뒷 줄 · 4회 앞 줄 …).
/// 문장은 D-7 의 [UI] 줄이며 변환 대상이 아니다 — UI 문자열 표(<c>Resources/Localization/ko.json</c> 의 <c>death_screen</c>)에 손으로 둔다.
/// 원고가 바뀌면 표를 손으로 맞춘다.</para>
/// </summary>
public class BattleDeathScreen : MonoBehaviour
{
    public const float BlackoutSeconds    = 0.5f;
    public const float InputBlockSeconds  = 0.8f;
    public const float AutoAdvanceSeconds = 5f;

    /// <summary>계정 단위 사망 횟수 키(F-9-2). 「모든 데이터 초기화」(PlayerPrefs.DeleteAll)로만 지워진다.</summary>
    public const string DeathCountKey = "Account.BattleDeathCount";

    const string KeyFirst   = "death_screen.first";
    const string KeyRepeatA = "death_screen.repeat_a";
    const string KeyRepeatB = "death_screen.repeat_b";

    /// <summary>사망 화면이 떠 있는가. 떠 있는 동안 새 조우를 받지 않는다.</summary>
    public static bool IsShowing { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => IsShowing = false;

    GameObject _card;
    TMP_Text   _line;
    UnityEngine.UI.Image _black;
    bool       _locked;

    /// <summary>루가 쓰러졌을 때 전투 쪽이 부른다. 이미 떠 있으면 무시한다.</summary>
    public static void Show()
    {
        if (IsShowing) return;
        IsShowing = true;
        var go = new GameObject("BattleDeathScreen [Auto]");
        DontDestroyOnLoad(go);
        go.AddComponent<BattleDeathScreen>();
    }

    /// <summary>n 번째 사망의 문장 키(1부터). F-9-2 의 번갈아 규칙.</summary>
    public static string LineKeyFor(int deathCount)
    {
        if (deathCount <= 1) return KeyFirst;
        return deathCount % 2 == 0 ? KeyRepeatA : KeyRepeatB;
    }

    void Awake()
    {
        // ① 입력을 즉시 막는다. 검은 판은 투명하게 시작해 암전으로 짙어진다.
        _card  = BlackLineCard.Build("BattleDeathScreen Card", "", alpha: 0f);
        _black = _card.GetComponentInChildren<UnityEngine.UI.Image>();
        _line  = _card.GetComponentInChildren<TMP_Text>();
        if (_black != null) _black.color = new Color(0f, 0f, 0f, 0f);

        var pil = PlayerInputLock.Instance;
        if (pil != null) { pil.Lock(); _locked = true; }
        Time.timeScale = 0f;   // 세계를 멈춘다 — 화면 뒤에서 적이 다가와 새 전투가 붙지 않게
    }

    IEnumerator Start()
    {
        // ② 암전 · 전투 BGM 끊기
        AudioManager.Instance?.StopAllBGM();
        BattleTransitionManager.Instance?.StopBattleBGM();
        float t = 0f;
        while (t < BlackoutSeconds)
        {
            t += Time.unscaledDeltaTime;
            if (_black != null) _black.color = new Color(0f, 0f, 0f, Mathf.Clamp01(t / BlackoutSeconds));
            yield return null;
        }
        if (_black != null) _black.color = Color.black;

        // ③ 문장 한 줄
        int count = PlayerPrefs.GetInt(DeathCountKey, 0) + 1;
        PlayerPrefs.SetInt(DeathCountKey, count);
        PlayerPrefs.Save();
        string key  = LineKeyFor(count);
        string text = LocalizationManager.Instance != null ? LocalizationManager.Instance.GetText(key) : key;
        if (string.IsNullOrEmpty(text) || text == key)
        {
            Debug.LogError($"[BattleDeathScreen] UI 문자열 표에 '{key}' 가 없습니다 — Resources/Localization 을 확인하세요(F-9-2).");
            text = "";
        }
        if (_line != null) { _line.text = text; _line.alpha = 1f; }
        Dbg.Log($"[BattleDeathScreen] 사망 {count}회째 — {key}");

        // ④ 입력 차단
        yield return new WaitForSecondsRealtime(InputBlockSeconds);

        // ⑤ 입력 또는 자동 진행
        float waited = 0f;
        while (waited < AutoAdvanceSeconds && !AdvanceRequested())
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
        if (SaveManager.Instance != null) SaveManager.Instance.ReturnToLastRabbit();
        else
        {
            Debug.LogWarning("[BattleDeathScreen] SaveManager 가 없어 S#01 로 바로 갑니다.");
            NewGameReset.Apply();
            SceneManager.LoadScene(SceneNames.Home);
        }
    }

    static bool AdvanceRequested() => Input.anyKeyDown;   // 키 · 마우스 버튼 모두

    /// <summary>복귀할 씬이 올라오면 화면을 걷는다. 그 뒤 페이드 인은 TransitionManager 가 맡는다.</summary>
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Time.timeScale = 1f;
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_card != null) Destroy(_card);
        if (_locked) PlayerInputLock.Instance?.Unlock();
        IsShowing = false;
    }
}
