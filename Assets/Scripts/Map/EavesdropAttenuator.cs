using UnityEngine;
using TMPro;

/// <summary>
/// S#04C — 문틈 엿듣기. 플레이어가 문틈에서 멀어지면 부엌 소리가 줄고 자막이 흐려진다.
///
/// 강제하지 않고 유도하는 것이 설계 의도다. 플레이어는 방 안을 자유롭게 돌아다닐 수 있지만
/// 문틈에 붙어 있어야만 대화를 전부 들을 수 있다. 조작을 잠그지 않는다.
///
/// 배치: 루의 방 문틈 위치에 빈 GameObject 를 만들고 이 컴포넌트를 붙인다.
///       doorGap 을 비우면 자기 자신의 transform 을 문틈으로 쓴다.
///
/// KitchenTriggerCutscene 이 S#04C 구간에서 Begin()/End() 로 켜고 끈다.
/// </summary>
public class EavesdropAttenuator : MonoBehaviour
{
    public static EavesdropAttenuator Instance { get; private set; }

    [Header("문틈 위치 (비우면 이 오브젝트의 위치)")]
    public Transform doorGap;

    [Header("거리 → 감쇠")]
    [Tooltip("이 반경 안에서는 100% 들린다.")]
    public float fullHearRadius = 1.5f;
    [Tooltip("이 반경 밖에서는 최소치까지 떨어진다.")]
    public float inaudibleRadius = 6f;

    [Header("최소치 (완전히 0으로 만들지 않는다)")]
    [Range(0f, 1f)] public float minVolume        = 0.12f;
    [Range(0f, 1f)] public float minSubtitleAlpha = 0.18f;

    [Header("반응 속도")]
    [Tooltip("거리 변화에 자막·음량이 따라붙는 속도. 클수록 즉각적.")]
    public float smoothing = 6f;

    // ── 내부 상태 ─────────────────────────────────
    private Transform _player;
    private bool      _active;
    private float     _current = 1f;   // 0~1 청취도

    /// <summary>현재 청취도(0~1). 1이면 문틈에 붙어 있는 상태.</summary>
    public float Clarity => _current;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        if (doorGap == null) doorGap = transform;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            // 씬을 벗어날 때 소리와 자막을 반드시 되돌린다.
            // 안 그러면 다음 씬까지 소리가 죽은 채로 넘어간다.
            AudioManager.ResetMuffle();
            RestoreSubtitle();
            Instance = null;
        }
    }

    /// <summary>S#04C 구간 시작. 이 시점부터 거리에 따라 소리·자막이 줄어든다.</summary>
    public void Begin()
    {
        _player = FindPlayer();
        if (_player == null)
        {
            Debug.LogWarning("[EavesdropAttenuator] 플레이어를 찾을 수 없어 감쇠를 건너뜁니다.");
            return;
        }
        _current = Evaluate();
        _active  = true;
    }

    /// <summary>S#04C 구간 종료. 소리와 자막을 원래대로 되돌린다.</summary>
    public void End()
    {
        _active  = false;
        _current = 1f;
        AudioManager.ResetMuffle();
        RestoreSubtitle();
    }

    // LateUpdate 인 이유: LinePresenter 가 Update 에서 자막을 갱신하므로
    // 그 뒤에 알파를 덮어써야 우리 값이 살아남는다.
    void LateUpdate()
    {
        if (!_active) return;
        if (_player == null) { _player = FindPlayer(); if (_player == null) return; }

        _current = Mathf.Lerp(_current, Evaluate(), Time.deltaTime * smoothing);

        AudioManager.SetMuffle(Mathf.Lerp(minVolume, 1f, _current));

        var text = YarnCommandBridge.LineBodyText;
        if (text != null)
            text.alpha = Mathf.Lerp(minSubtitleAlpha, 1f, _current);
    }

    /// <summary>문틈까지의 거리를 0~1 청취도로 환산한다.</summary>
    float Evaluate()
    {
        if (_player == null || doorGap == null) return 1f;

        float d = Vector2.Distance(_player.position, doorGap.position);
        if (d <= fullHearRadius)  return 1f;
        if (d >= inaudibleRadius) return 0f;

        // fullHearRadius ~ inaudibleRadius 구간을 1 → 0 으로 선형 보간.
        return 1f - Mathf.InverseLerp(fullHearRadius, inaudibleRadius, d);
    }

    static void RestoreSubtitle()
    {
        var text = YarnCommandBridge.LineBodyText;
        if (text != null) text.alpha = 1f;
    }

    static Transform FindPlayer()
    {
        var ctrl = Object.FindAnyObjectByType<ClearSky.SimplePlayerController>();
        return ctrl != null ? ctrl.transform : null;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Vector3 c = doorGap != null ? doorGap.position : transform.position;
        Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.9f);
        Gizmos.DrawWireSphere(c, fullHearRadius);
        Gizmos.color = new Color(0.4f, 0.4f, 0.5f, 0.6f);
        Gizmos.DrawWireSphere(c, inaudibleRadius);
    }
#endif
}
