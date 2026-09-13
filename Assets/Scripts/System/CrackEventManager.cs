using System.Collections;
using UnityEngine;

public class CrackEventManager : MonoBehaviour
{
    public static CrackEventManager Instance { get; private set; }

    public event System.Action OnCrackEvent;

    // ── 인형화 데모 고정값 ────────────────────────────────────────────
    // 굴복 +2 는 「데모 범위 확정」 v0.5 4절 · F-2-6 문단 144 의 고정값이다.
    // C 의 범위값(3~5)을 데모에서 굴리지 않는다 — 상한 32 안에 들어가야 하고,
    // 31 돌파가 균열 굴복 한 방으로 확정돼 있어(C-2-4) 값이 흔들리면 안 된다.
    const float CorruptionOnSubmit = +2f;
    // ⚠ 저항 값은 고정값 목록에 없다. 종전 동작(-1~-2 난수)을 그대로 둔다.
    //   확정되면 여기 상수로 올린다.

    [Header("1급 균열 사운드")]
    [SerializeField] private AudioClip seraVoiceClip;

    [Header("굴복/저항 선택 UI")]
    [SerializeField] private GameObject crackChoiceUIPrefab;

    private bool      _choiceResolved;
    private Coroutine _crackSequenceCoroutine;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            SingletonGuard.DestroyDuplicate(this);
        }
    }

    public static void TriggerCrackEvent()
    {
        if (Instance == null)
        {
            Debug.LogWarning("[CrackEventManager] Instance가 null입니다. 씬에 CrackEventManager 프리팹을 배치해 주세요.");
            return;
        }
        Instance.FireCrackEvent();
    }

    void FireCrackEvent()
    {
        // 이전 시퀀스가 진행 중이면 무시 — 중단 시 입력 잠금이 해제되지 않은 채 남는 것 방지
        if (_crackSequenceCoroutine != null) return;

        OnCrackEvent?.Invoke();

        // 기존 글리치 유지
        if (GlitchManager.Instance != null)
            GlitchManager.Instance.PlayGlitch(1.5f, GlitchManager.PresetCrash);

        Dbg.Log("[CrackEvent] 균열 이벤트 발동");

        _crackSequenceCoroutine = StartCoroutine(CrackSequenceRoutine());
    }

    IEnumerator CrackSequenceRoutine()
    {
        // 1. TikTok 연속음
        SFXManager.Instance?.PlaySnap(81f);

        // 2. 세라 목소리 재생
        if (seraVoiceClip != null)
            AudioManager.Instance?.Play(seraVoiceClip);

        // 3. 입력 잠금
        PlayerInputLock.Instance?.Lock();

        // 4. 2~3초 연출 대기
        float lockDuration = Random.Range(2f, 3f);
        yield return new WaitForSeconds(lockDuration);

        // 5. 굴복/저항 UI 표시
        if (crackChoiceUIPrefab != null)
        {
            _choiceResolved = false;
            GameObject   uiGo     = null;
            CrackChoiceUI choiceUI = null;
            try
            {
                uiGo     = Instantiate(crackChoiceUIPrefab);
                choiceUI = uiGo != null ? uiGo.GetComponent<CrackChoiceUI>() : null;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CrackEventManager] CrackChoiceUI 인스턴스화 실패: {e.Message}. 저항 처리합니다.");
            }

            if (choiceUI != null)
            {
                choiceUI.OnSubmit = OnSubmit;
                choiceUI.OnResist = OnResist;
                yield return new WaitUntil(() => _choiceResolved);
                Destroy(uiGo);
            }
            else
            {
                if (uiGo != null) Destroy(uiGo);
                Debug.LogError("[CrackEventManager] CrackChoiceUI 컴포넌트를 찾지 못했습니다. 저항 처리합니다.");
                OnResist();
            }
        }
        else
        {
            Debug.LogError("[CrackEventManager] crackChoiceUIPrefab이 연결되지 않았습니다. 저항 처리합니다.");
            OnResist();
        }

        // 6. 입력 잠금 해제
        PlayerInputLock.Instance?.Unlock();
        SFXManager.Instance?.StopSnap();
        _crackSequenceCoroutine = null;
    }

    void OnSubmit()
    {
        const float delta = CorruptionOnSubmit;
        GameStateManager.Instance?.AddDollification(delta);
        Dbg.Log($"[CrackEvent] 굴복 — 인형화 +{delta:F1}");
        _choiceResolved = true;
    }

    void OnResist()
    {
        float delta = -Random.Range(1f, 2f);
        GameStateManager.Instance?.AddDollification(delta);
        Dbg.Log($"[CrackEvent] 저항 — 인형화 {delta:F1}");
        _choiceResolved = true;
    }
}

/// <summary>균열 선택 UI에 붙는 컴포넌트. 버튼에서 호출한다.</summary>
public class CrackChoiceUI : MonoBehaviour
{
    public System.Action OnSubmit;
    public System.Action OnResist;

    public void ClickSubmit() => OnSubmit?.Invoke();
    public void ClickResist() => OnResist?.Invoke();
}
