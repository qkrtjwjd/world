using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InteractionManager : MonoBehaviour
{
    public static InteractionManager Instance;

    private readonly List<InteractionTrigger> _triggers = new List<InteractionTrigger>();
    private InteractionTrigger _active;
    private Transform          _playerTransform;
    private InteractionTextUI  _ui;

    private float _cooldown     = 0f;
    private float _calcTimer    = 0f;
    private const float CalcInterval = 0.1f; // 거리 계산 주기

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

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

        // 입력 감지
        if (Input.GetKeyDown(KeyCode.E) && _cooldown <= 0f && _active != null)
        {
            _active.Interact();
            _cooldown = 0.5f;
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

        if (_active.hideTextAfterFirstView && _active.hasShownText)
            _ui.Hide();
        else
        {
            _ui.Show(_active.message);
            _active.hasShownText = true;
        }
    }

    // ─────────────────────────────────────────────
    //  유틸
    // ─────────────────────────────────────────────
    public void SetCooldown(float time) => _cooldown = time;
    public bool IsCoolingDown           => _cooldown > 0f;

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