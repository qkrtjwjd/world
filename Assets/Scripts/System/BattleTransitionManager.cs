using System.Collections;
using UnityEngine;

/// <summary>전투 모드(환상/현실) 전환 시퀀스를 관리합니다.</summary>
public enum BattleMode { Fantasy, Reality, Pending }

public class BattleTransitionManager : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  싱글톤
    // ─────────────────────────────────────────────
    public static BattleTransitionManager Instance
    {
        get
        {
            if (!_instance)
            {
                var go = new GameObject("BattleTransitionManager [Auto]");
                _instance = go.AddComponent<BattleTransitionManager>();
            }
            return _instance;
        }
    }
    private static BattleTransitionManager _instance;

    // ─────────────────────────────────────────────
    //  Inspector 설정
    // ─────────────────────────────────────────────
    [Header("BGM AudioSource")]
    [Tooltip("환상 전투 BGM AudioSource.")]
    [SerializeField] private AudioSource _fantasyBGM;
    [Tooltip("현실 전투 BGM AudioSource.")]
    [SerializeField] private AudioSource _realityBGM;

    [Header("Color Grading — 현실 톤 (무채색)")]
    [SerializeField] private float _realitySaturation  = -100f;
    [SerializeField] private float _realityContrast    =   20f;
    [SerializeField] private Color _realityColorFilter = Color.white;

    [Header("Color Grading — 환상 톤 (파스텔)")]
    [SerializeField] private float _fantasySaturation  =  15f;
    [SerializeField] private float _fantasyContrast    = -15f;
    [SerializeField] private Color _fantasyColorFilter = new Color(1f, 0.92f, 0.95f);

    // ─────────────────────────────────────────────
    //  외부 시스템 콜백
    // ─────────────────────────────────────────────

    /// <summary>
    /// 현실→환상 전환 시 캐릭터 마시멜로 먹기 애니메이션 트리거.
    /// animController 등 외부 스크립트에서 등록하세요:
    /// BattleTransitionManager.Instance.onPlayEatMarshmallow += () => animator.Play("EatMarshmallow");
    /// </summary>
    public System.Action onPlayEatMarshmallow;

    /// <summary>
    /// 모드 전환 완료 시 호출. BattleSystem 등 외부 시스템에서 등록하세요:
    /// BattleTransitionManager.Instance.onModeChanged += mode => battleSystem.SetMode(mode);
    /// </summary>
    public System.Action<BattleMode> onModeChanged;

    // ─────────────────────────────────────────────
    //  상태
    // ─────────────────────────────────────────────
    /// <summary>현재 전투 모드.</summary>
    public BattleMode CurrentMode { get; private set; } = BattleMode.Fantasy;

    /// <summary>전환 진행 중 여부. true 일 때 중복 전환 불가.</summary>
    public bool isTransitioning { get; private set; } = false;

    // WaitForSecondsRealtime — 환상→현실 전용 (timeScale 0.3 대응)
    private WaitForSecondsRealtime _waitR01; // 0.1s
    private WaitForSecondsRealtime _waitR02; // 0.2s

    // WaitForSeconds — 현실→환상 전용 (timeScale 건드리지 않음)
    private WaitForSeconds _waitS02; // 0.2s
    private WaitForSeconds _waitS03; // 0.3s

    // ─────────────────────────────────────────────
    //  라이프사이클
    // ─────────────────────────────────────────────
    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            SingletonGuard.DestroyDuplicate(this);
            return;
        }

        _waitR01 = new WaitForSecondsRealtime(0.1f);
        _waitR02 = new WaitForSecondsRealtime(0.2f);
        _waitS02 = new WaitForSeconds(0.2f);
        _waitS03 = new WaitForSeconds(0.3f);
    }

    void OnApplicationQuit()
    {
        // TimeScale이 슬로우 상태인 채 종료되지 않도록 복구
        Time.timeScale = 1f;
    }

    // ─────────────────────────────────────────────
    //  공개 API
    // ─────────────────────────────────────────────

    /// <summary>
    /// 전투 시작 시 진입 모드에 맞게 CurrentMode를 동기화합니다.
    /// DDOL 상주로 이전 전투의 모드가 남아 "이미 해당 모드" 경고로
    /// 전환 연출이 거부되는 문제를 방지합니다. 전환 진행 중에는 무시합니다.
    /// </summary>
    public void SyncMode(BattleMode mode)
    {
        if (isTransitioning) return;
        CurrentMode = mode;
    }

    /// <summary>환상 → 현실 전환을 시작합니다.</summary>
    public void TransitionToReality()
    {
        if (isTransitioning)
        {
            Debug.LogWarning("[BattleTransitionManager] 전환 중입니다. 중복 호출을 무시합니다.");
            return;
        }
        if (CurrentMode == BattleMode.Reality)
        {
            Debug.LogWarning("[BattleTransitionManager] 이미 현실 모드입니다.");
            return;
        }
        StartCoroutine(FantasyToRealitySequence());
    }

    /// <summary>현실 → 환상 전환을 시작합니다.</summary>
    public void TransitionToFantasy()
    {
        if (isTransitioning)
        {
            Debug.LogWarning("[BattleTransitionManager] 전환 중입니다. 중복 호출을 무시합니다.");
            return;
        }
        if (CurrentMode == BattleMode.Fantasy)
        {
            Debug.LogWarning("[BattleTransitionManager] 이미 환상 모드입니다.");
            return;
        }
        StartCoroutine(RealityToFantasySequence());
    }

    // ─────────────────────────────────────────────
    //  코루틴 — 환상 → 현실 (턴제 → 핵앤슬래시)
    //  TimeScale = 0.3 → WaitForSecondsRealtime 사용
    // ─────────────────────────────────────────────
    IEnumerator FantasyToRealitySequence()
    {
        isTransitioning = true;
        Dbg.Log("[BattleTransitionManager] 환상 → 현실 전환 시작");

        var inputLock = PlayerInputLock.Instance;
        var vfx  = TransitionVFXController.Instance;
        var sfx  = TransitionSFXController.Instance;
        var ui   = TransitionUIController.Instance;
        var swap = ObjectSwapController.Instance;

        bool locked = false;
        try
        {
            if (inputLock == null || vfx == null || sfx == null || ui == null || swap == null)
            {
                Debug.LogError("[BattleTransitionManager] 전환 컨트롤러 누락 — 환상→현실 전환 취소");
                yield break;
            }

            Vector2 impactUV = new Vector2(0.5f, 0.5f);

            // ── 0.0초: 충격 + 화면 균열 시작 ────────────────
            inputLock.Lock();
            locked = true;
            Time.timeScale = 0.3f;
            sfx.PlayGlassBreak();
            vfx.ImpactFlash(impactUV, 0.1f);
            vfx.GlitchFlash(0.08f);
            yield return _waitR01; // 0.1s

            // ── 0.1초: UI 흔들림 ──────────────────────────────
            ui.ShakeUIElements(0.4f, 5f);
            yield return _waitR02; // 0.2s

            // ── 0.3초: 균열 전파 ──────────────────────────────
            vfx.StartScreenCrack(0.2f, impactUV);
            yield return _waitR02; // 0.2s

            // ── 0.5초: 산산조각 + 현실 전환 ──────────────────
            vfx.ShatterScreen(0.3f);
            ui.ExplodeUI(0.3f);
            swap.SwapToReality();
            vfx.CameraShake(0.3f, 0.15f);
            vfx.LerpColorGrading(_realitySaturation, _realityContrast, _realityColorFilter, 0.4f);
            yield return _waitR01; // 0.1s

            // ── 0.6초: BGM 전환 ───────────────────────────────
            sfx.CrossfadeBGM(_fantasyBGM, _realityBGM, 0.5f);
            yield return _waitR02; // 0.2s

            // ── 0.8초: 전환 완료 ──────────────────────────────
            CurrentMode = BattleMode.Reality;
            onModeChanged?.Invoke(BattleMode.Reality);
            BattleEvents.RaiseModeChanged(BattleMode.Reality);
            Dbg.Log("[BattleTransitionManager] 환상 → 현실 전환 완료");
        }
        finally
        {
            // 예외·중단 시에도 슬로우모션·입력 잠금·전환 플래그가 잔존하지 않도록 보장
            Time.timeScale = 1f;
            if (locked && inputLock != null && inputLock.IsLocked) inputLock.Unlock();
            isTransitioning = false;
        }
    }

    // ─────────────────────────────────────────────
    //  코루틴 — 현실 → 환상 (핵앤슬래시 → 턴제)
    //  수채화 번짐이 화면을 덮으며 현실 위에 환상이 씌워지는 연출.
    //  TimeScale 건드리지 않음 → WaitForSeconds 사용
    // ─────────────────────────────────────────────
    IEnumerator RealityToFantasySequence()
    {
        isTransitioning = true;
        Dbg.Log("[BattleTransitionManager] 현실 → 환상 전환 시작");

        var inputLock = PlayerInputLock.Instance;
        var vfx  = TransitionVFXController.Instance;
        var sfx  = TransitionSFXController.Instance;
        var ui   = TransitionUIController.Instance;
        var swap = ObjectSwapController.Instance;

        bool locked = false;
        try
        {
            if (inputLock == null || vfx == null || sfx == null || ui == null || swap == null)
            {
                Debug.LogError("[BattleTransitionManager] 전환 컨트롤러 누락 — 현실→환상 전환 취소");
                yield break;
            }

            // ── 0.0초: 수채화 번짐 + 마시멜로 등장 ─────────────────────
            inputLock.Lock();
            locked = true;
            sfx.PlaySweetChime();
            onPlayEatMarshmallow?.Invoke();
            vfx.StartWatercolorSpread(0.7f);      // 0.7초간 화면에 수채화가 번짐
            vfx.ShowMarshmallow(0.4f);            // 중앙 투명 구멍에 마시멜로+후광 페이드인
            sfx.ApplyLowPassFilter(800f, 0.4f);   // BGM을 뭉개 꿈결 같은 분위기
            yield return _waitS03; // 0.3s

            // ── 0.3초: 현실 색감 → 환상 색감 + 오브젝트 교체 ─
            vfx.LerpColorGrading(_fantasySaturation, _fantasyContrast, _fantasyColorFilter, 0.5f);
            swap.SwapToFantasy();
            yield return _waitS03; // 0.3s

            // ── 0.6초: 턴제 UI 등장 ──────────────────────────
            ui.BloomInTurnUI(0.5f, 0.08f);
            yield return _waitS02; // 0.2s

            // ── 0.8초: BGM 전환 + 수채화·마시멜로 페이드아웃 ──────────
            sfx.CrossfadeBGM(_realityBGM, _fantasyBGM, 0.6f);
            sfx.RemoveLowPassFilter(0.4f);
            vfx.FadeOutWatercolor(0.5f);          // 수채화 오버레이 서서히 걷힘
            vfx.HideMarshmallow(0.35f);           // 마시멜로+후광 퇴장
            yield return _waitS03; // 0.3s

            // ── 1.1초: 전환 완료 ─────────────────────────────
            PuppetizationManager.Instance?.Add(2.5f);
            CurrentMode = BattleMode.Fantasy;
            onModeChanged?.Invoke(BattleMode.Fantasy);
            BattleEvents.RaiseModeChanged(BattleMode.Fantasy);
            Dbg.Log("[BattleTransitionManager] 현실 → 환상 전환 완료");
        }
        finally
        {
            // 예외·중단 시에도 입력 잠금·전환 플래그가 잔존하지 않도록 보장
            if (locked && inputLock != null && inputLock.IsLocked) inputLock.Unlock();
            isTransitioning = false;
        }
    }
}
