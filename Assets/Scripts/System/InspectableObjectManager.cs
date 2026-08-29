using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 조사 오브젝트의 입력을 한 곳에서 받는다. <see cref="InteractionManager"/> 와 같은 구조이지만
/// 대상이 다르다 — 이쪽은 <see cref="InspectableObject"/> 전용이고 <b>표시를 띄우지 않는다</b>
/// (C-16-8 · F-8-6).
///
/// 매니저를 따로 두는 이유는 가장 가까운 하나만 반응시키기 위해서다. 오브젝트마다 입력을 읽으면
/// 두 물건이 겹칠 때 둘 다 발동한다.
/// </summary>
public class InspectableObjectManager : MonoBehaviour
{
    public static InspectableObjectManager Instance
    {
        get
        {
            if (_isQuitting || !Application.isPlaying) return null;
            if (!_instance)
            {
                var go = new GameObject("InspectableObjectManager [Auto]");
                _instance = go.AddComponent<InspectableObjectManager>();
            }
            return _instance;
        }
    }

    /// <summary>
    /// 이미 있을 때만 돌려준다. 없으면 <b>만들지 않는다</b> — 정리 경로 전용.
    /// </summary>
    /// <remarks>
    /// ⚠ <see cref="Instance"/> 는 없으면 그 자리에서 GameObject 를 만든다. 씬이 닫히는 중에
    /// 부르면 죽어가는 씬 안에 오브젝트가 생기고 유니티가 "Some objects were not cleaned up
    /// when closing the scene" 으로 잡는다 (InteractionManager 에서 2026-08-23 실측된 경로다).
    /// </remarks>
    public static InspectableObjectManager InstanceIfExists =>
        _instance != null ? _instance : null;

    private static InspectableObjectManager _instance;
    private static bool _isQuitting = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _instance   = null;
        _isQuitting = false;
        _pending.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RegisterQuitHandler()
    {
        Application.quitting += () => _isQuitting = true;
    }

    // 매니저가 아직 없는 동안 OnEnable 이 도는 오브젝트를 받아 둔다.
    private static readonly List<InspectableObject> _pending = new List<InspectableObject>();

    private readonly List<InspectableObject> _objects = new List<InspectableObject>();
    private Transform _playerTransform;
    private float     _cooldown;

    private const float CalcInterval = 0.1f;
    private float _calcTimer;
    private InspectableObject _closest;

    // ─────────────────────────────────────────────
    public static void Register(InspectableObject obj)
    {
        // 씬 로드 중에는 매니저를 만들지 않고 대기열에 넣는다.
        var m = Instance;
        if (m == null) { if (!_pending.Contains(obj)) _pending.Add(obj); return; }
        if (!m._objects.Contains(obj)) m._objects.Add(obj);
    }

    public static void Unregister(InspectableObject obj)
    {
        _pending.Remove(obj);
        var m = InstanceIfExists;
        if (m == null) return;
        m._objects.Remove(obj);
        if (m._closest == obj) m._closest = null;
    }

    // ─────────────────────────────────────────────
    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            // ⚠ Destroy(gameObject) 가 아니다. 같은 GameObject 에 다른 매니저가 붙어 있으면
            //   그것까지 통째로 날아간다 (SingletonGuard 의 근거와 같다).
            Destroy(this);
            return;
        }
        _instance = this;

        if (_pending.Count > 0)
        {
            foreach (var o in _pending)
                if (o != null && !_objects.Contains(o)) _objects.Add(o);
            _pending.Clear();
        }
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    void Update()
    {
        if (_objects.Count == 0) return;

        if (_playerTransform == null)
        {
            TryFindPlayer();
            if (_playerTransform == null) return;
        }

        if (_cooldown > 0f) _cooldown -= Time.deltaTime;

        _calcTimer += Time.deltaTime;
        if (_calcTimer >= CalcInterval)
        {
            _calcTimer = 0f;
            RefreshClosest();
        }

        if (_closest == null || _cooldown > 0f) return;

        // 대화 중에는 받지 않는다. 조사 결과가 다른 대사를 덮어쓰지 않게.
        if (YarnDialogue.IsRunning) return;
        if (PlayerInputLock.Instance != null && PlayerInputLock.Instance.IsLocked) return;

        // 진행 오브젝트(문·상자 등)가 사정거리에 있으면 그쪽이 우선이다. 조사 오브젝트에는
        // 표시가 없으므로 플레이어가 노린 것은 표시가 뜬 쪽이다.
        if (InteractionManager.InstanceIfExists?.HasActiveTarget == true) return;

        KeyCode key = SettingsManager.Instance?.keyInteract ?? KeyCode.E;
        if (!Input.GetKeyDown(key)) return;

        _closest.Inspect();
        _cooldown = 0.5f;
        // 같은 입력으로 진행 오브젝트가 뒤이어 발동하지 않도록 그쪽 쿨타임도 함께 건다.
        InteractionManager.InstanceIfExists?.SetCooldown(0.5f);
    }

    private void TryFindPlayer()
    {
        if (PlayerStats.Instance != null)
        {
            _playerTransform = PlayerStats.Instance.transform;
            return;
        }
        var p = GameObject.FindWithTag("Player");
        if (p != null) _playerTransform = p.transform;
    }

    private void RefreshClosest()
    {
        _closest = null;
        float best = float.MaxValue;

        for (int i = _objects.Count - 1; i >= 0; i--)
        {
            var o = _objects[i];
            if (o == null) { _objects.RemoveAt(i); continue; }

            float d = Vector2.Distance(o.transform.position, _playerTransform.position);
            if (d > o.Range || d >= best) continue;
            best = d;
            _closest = o;
        }
    }
}
