using System.Collections;
using UnityEngine;

/// <summary>전투 모드(환상/현실) 전환 시퀀스를 관리합니다.</summary>
public enum BattleMode { Fantasy, Reality }

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

    [Header("캐릭터")]
    [Tooltip("SpawnMeltParticles 스폰 기준 위치 (루 캐릭터 Transform).")]
    [SerializeField] private Transform _luTransform;

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
            Destroy(gameObject);
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
    //  코루틴 — 환상 → 현실
    //  TimeScale = 0.3 → WaitForSecondsRealtime 사용
    // ─────────────────────────────────────────────
    IEnumerator FantasyToRealitySequence()
    {
        isTransitioning = true;
        Debug.Log("[BattleTransitionManager] 환상 → 현실 전환 시작");

        var inputLock  = PlayerInputLock.Instance;
        var vfx        = TransitionVFXController.Instance;
        var sfx        = TransitionSFXController.Instance;
        var ui         = TransitionUIController.Instance;
        var swap       = ObjectSwapController.Instance;

        // ── 0.0초 ────────────────────────────────
        inputLock.Lock();
        Time.timeScale = 0.3f;
        sfx.PlayGlassShatter();
        vfx.FlashWhite(0.1f);
        yield return _waitR01; // 0.1s 대기

        // ── 0.1초 ────────────────────────────────
        ui.StartCrackOverlay(0.4f);
        ui.ShakeUIElements(0.4f, 5f);
        yield return _waitR02; // 0.2s 대기

        // ── 0.3초 ────────────────────────────────
        vfx.StartScreenCrack(0.5f);
        sfx.PlayCrackingLoop();
        yield return _waitR02; // 0.2s 대기

        // ── 0.5초 ────────────────────────────────
        ui.ExplodeUI(0.3f);
        swap.SwapToReality();
        yield return _waitR01; // 0.1s 대기

        // ── 0.6초 ────────────────────────────────
        vfx.PlayEnemyTransformVFX(0.4f);
        sfx.PlayMetalScrape();
        yield return _waitR02; // 0.2s 대기

        // ── 0.8초 ────────────────────────────────
        vfx.CameraShake(0.3f, 0.15f);
        vfx.LerpColorGrading(_realitySaturation, _realityContrast, _realityColorFilter, 0.4f);
        yield return _waitR02; // 0.2s 대기

        // ── 1.0초 ────────────────────────────────
        sfx.CrossfadeBGM(_fantasyBGM, _realityBGM, 0.5f);
        yield return _waitR02; // 0.2s 대기

        // ── 1.2초 ────────────────────────────────
        Time.timeScale = 1.0f;
        CurrentMode = BattleMode.Reality;
        onModeChanged?.Invoke(BattleMode.Reality);
        inputLock.Unlock();
        isTransitioning = false;
        Debug.Log("[BattleTransitionManager] 환상 → 현실 전환 완료");
    }

    // ─────────────────────────────────────────────
    //  코루틴 — 현실 → 환상
    //  TimeScale 건드리지 않음 → WaitForSeconds 사용
    // ─────────────────────────────────────────────
    IEnumerator RealityToFantasySequence()
    {
        isTransitioning = true;
        Debug.Log("[BattleTransitionManager] 현실 → 환상 전환 시작");

        var inputLock  = PlayerInputLock.Instance;
        var vfx        = TransitionVFXController.Instance;
        var sfx        = TransitionSFXController.Instance;
        var ui         = TransitionUIController.Instance;
        var swap       = ObjectSwapController.Instance;

        Vector3 luPos = (_luTransform != null) ? _luTransform.position : Vector3.zero;

        // ── 0.0초 ────────────────────────────────
        inputLock.Lock();
        sfx.PlaySweetChime();
        onPlayEatMarshmallow?.Invoke();
        vfx.CameraZoomIn(55f, 0.3f);
        vfx.SpawnMeltParticles(luPos);
        yield return _waitS03; // 0.3s 대기

        // ── 0.3초 ────────────────────────────────
        vfx.StartWatercolorSpread(0.5f);
        sfx.ApplyLowPassFilter(800f, 0.5f);
        yield return _waitS02; // 0.2s 대기

        // ── 0.5초 ────────────────────────────────
        vfx.LerpColorGrading(_fantasySaturation, _fantasyContrast, _fantasyColorFilter, 0.5f);
        swap.SwapToFantasy();
        yield return _waitS03; // 0.3s 대기

        // ── 0.8초 ────────────────────────────────
        vfx.PlayEnemyDissolveToFantasy(0.4f);
        ui.BloomInTurnUI(0.7f, 0.1f);
        yield return _waitS02; // 0.2s 대기

        // ── 1.0초 ────────────────────────────────
        sfx.CrossfadeBGM(_realityBGM, _fantasyBGM, 0.5f);
        sfx.RemoveLowPassFilter(0.3f);
        yield return _waitS03; // 0.3s 대기

        // ── 1.3초 ────────────────────────────────
        vfx.CameraZoomOut(60f, 0.2f);
        vfx.CameraShake(0.15f, 0.05f);
        yield return _waitS02; // 0.2s 대기

        // ── 1.5초 ────────────────────────────────
        PuppetizationManager.Instance.Add(2.5f);
        CurrentMode = BattleMode.Fantasy;
        onModeChanged?.Invoke(BattleMode.Fantasy);
        inputLock.Unlock();
        isTransitioning = false;
        Debug.Log("[BattleTransitionManager] 현실 → 환상 전환 완료");
    }
}
