using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InteractionManager : MonoBehaviour
{
    public static InteractionManager Instance
    {
        get
        {
            if (_isQuitting || !Application.isPlaying) return null;
            if (!_instance)
            {
                var go = new GameObject("InteractionManager [Auto]");
                _instance = go.AddComponent<InteractionManager>();
            }
            return _instance;
        }
    }
    /// <summary>
    /// 이미 있을 때만 돌려줍니다. 없으면 <b>만들지 않습니다</b> — 정리 경로 전용.
    /// </summary>
    /// <remarks>
    /// ⚠ <see cref="Instance"/> 는 없으면 그 자리에서 GameObject 를 만든다.
    /// 그래서 씬이 닫히는 중에 부르면 <b>죽어가는 씬 안에</b> 새 오브젝트가 생기고,
    /// 유니티가 "Some objects were not cleaned up when closing the scene" 으로 잡는다
    /// (2026-08-23 실측 — <see cref="InteractionTrigger"/> 의 OnDisable 이 그 경로였다).
    /// 등록 취소는 매니저가 없으면 할 일도 없으므로 이쪽을 쓴다.
    ///
    /// <c>_instance != null</c> 은 유니티의 == 오버로드를 타므로 파괴된 객체(가짜 null)를
    /// 진짜 null 로 바꿔 준다. <c>?.</c> 는 이 변환을 하지 않으므로 이 한 겹이 반드시 필요하다.
    /// </remarks>
    public static InteractionManager InstanceIfExists => _instance != null ? _instance : null;

    private static InteractionManager _instance;
    private static bool _isQuitting = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _instance   = null;
        _isQuitting = false;
    }

    // 인스턴스가 없어도 종료 플래그를 세팅하기 위해 정적 이벤트 사용
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RegisterQuitHandler()
    {
        Application.quitting += () => _isQuitting = true;
    }

    private readonly List<InteractionTrigger> _triggers = new List<InteractionTrigger>();
    private InteractionTrigger _active;
    private Transform          _playerTransform;
    private InteractionTextUI  _ui;

    private float _cooldown     = 0f;
    private float _calcTimer    = 0f;
    private const float CalcInterval = 0.1f; // 거리 계산 주기

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
        }
    }

    void OnApplicationQuit() => _isQuitting = true;

    void Start()
    {
        _ui = InteractionTextUI.Instance;
        TryFindPlayer();
    }

    void Update()
    {
        if (_playerTransform == null) TryFindPlayer();
        if (_playerTransform == null) return;

        // 쿨타임 감소
        if (_cooldown > 0f) _cooldown -= Time.deltaTime;

        // 거리 계산 (매 CalcInterval 마다)
        _calcTimer += Time.deltaTime;
        if (_calcTimer >= CalcInterval)
        {
            _calcTimer = 0f;
            RefreshClosest();
        }

        // 입력 감지 (리바인딩 가능 키)
        KeyCode interactKey = SettingsManager.Instance?.keyInteract ?? KeyCode.E;
        if (Input.GetKeyDown(interactKey))
        {
            if (_cooldown <= 0f && _active != null)
            {
                _active.Interact();
                _cooldown = 0.5f;
            }
        }
    }

    // ─────────────────────────────────────────────
    //  트리거 등록 / 해제
    // ─────────────────────────────────────────────
    public void RegisterTrigger(InteractionTrigger trigger)
    {
        if (!_triggers.Contains(trigger))
            _triggers.Add(trigger);
    }

    public void UnregisterTrigger(InteractionTrigger trigger)
    {
        _triggers.Remove(trigger);
        if (_active == trigger)
        {
            _active = null;
            UpdateUI();
        }
    }

    // ─────────────────────────────────────────────
    //  가장 가까운 트리거 갱신
    // ─────────────────────────────────────────────
    void RefreshClosest()
    {
        if (_triggers.Count == 0)
        {
            if (_active != null) { _active = null; UpdateUI(); }
            return;
        }

        InteractionTrigger closest  = null;
        float              minSqDist = float.MaxValue;
        Vector3            playerPos = _playerTransform.position;

        for (int i = _triggers.Count - 1; i >= 0; i--)
        {
            var t = _triggers[i];
            if (t == null) { _triggers.RemoveAt(i); continue; }
            if (!t.gameObject.activeInHierarchy) continue;

            float d = (t.transform.position - playerPos).sqrMagnitude;
            if (d < minSqDist) { minSqDist = d; closest = t; }
        }

        if (closest != _active)
        {
            _active = closest;
            UpdateUI();
        }
    }

    // ─────────────────────────────────────────────
    //  UI
    // ─────────────────────────────────────────────
    void UpdateUI()
    {
        if (_ui == null) _ui = InteractionTextUI.Instance;
        if (_ui == null) return;

        if (_active == null) { _ui.Hide(); return; }

        // 접근 표시는 세이브 포인트와 솔에만 붙는다 (C-16-8 · F-8-6).
        // 무엇이 중요한지를 게임이 먼저 알려주면 D-S#07 의 헛수고 설계와 충돌한다.
        if (!_active.showPrompt) { _ui.Hide(); return; }

        if (_active.hideTextAfterFirstView && _active.hasShownText)
            _ui.Hide();
        else
        {
            HintManager.ShowHint("interact_key",
                $"[{SettingsManager.Instance?.keyInteract ?? KeyCode.E}] 키로 상호작용할 수 있습니다.");
            _ui.Show(_active.message);
            _active.hasShownText = true;
        }
    }

    // ─────────────────────────────────────────────
    //  유틸
    // ─────────────────────────────────────────────
    public void SetCooldown(float time) => _cooldown = time;
    public bool IsCoolingDown           => _cooldown > 0f;
    public bool HasActiveTarget         => _active != null;

    void TryFindPlayer()
    {
        if (PlayerStats.Instance != null)
            _playerTransform = PlayerStats.Instance.transform;
        else
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) _playerTransform = p.transform;
        }
    }
}