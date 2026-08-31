using UnityEngine;

/// <summary>엄폐물 종류. 소실 순서가 이 순서다 (F-6: 수레 → 화분 → 간판).</summary>
public enum VillageCoverKind
{
    Cart = 0,   // 수레
    Pot  = 1,   // 화분
    Sign = 2,   // 간판
}

/// <summary>
/// 마을 엄폐물 하나 (C-14-3-2 · C-14-3-4 / 수치 F-6).
///
/// 세라의 시야를 막아 준다. 순찰 라운드가 끝날 때마다 종류 순서대로 한 단계씩 사라진다 —
/// 기다리기만 하면 되는 구조를 막는 장치다.
///
/// ⚠ 소실은 결계가 조여드는 결과다. 세라가 치우는 것이 아니므로 세라의 동선과 무관하다 (C-14-3-4).
/// ⚠ 사라진다고 오브젝트를 끄지 않는다. 잔해 스프라이트로 바꾸고 <b>콜라이더만</b> 끈다 —
///   통째로 없어지면 플레이어가 버그로 읽는다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class VillageCover : MonoBehaviour
{
    [Tooltip("엄폐물 종류. 소실 순서는 수레 → 화분 → 간판이다 (F-6).")]
    public VillageCoverKind kind = VillageCoverKind.Cart;

    [Tooltip("마을 출구 경로에 있는 엄폐물. F-6 「마을 출구 경로에 최소 2개를 남긴다」에 따라 " +
             "소실 대상에서 제외된다. 출구 쪽 2개 이상에 체크한다.")]
    public bool keepForExitRoute = false;

    [Tooltip("소실했을 때 갈아끼울 잔해 스프라이트. 비우면 스프라이트는 그대로 두고 콜라이더만 끈다.")]
    public Sprite goneSprite;

    /// <summary>지금 몸을 숨길 수 있는 상태인지.</summary>
    public bool IsIntact { get; private set; } = true;

    SpriteRenderer _renderer;
    Sprite         _intactSprite;
    Collider2D[]   _colliders;

    void Awake()
    {
        _renderer     = GetComponent<SpriteRenderer>();
        _intactSprite = _renderer.sprite;
        _colliders    = GetComponentsInChildren<Collider2D>(includeInactive: true);
    }

    void OnEnable()  => VillageCoverController.Register(this);
    void OnDisable() => VillageCoverController.Unregister(this);

    /// <summary>엄폐물을 소실 처리합니다. 시야 차단이 풀립니다.</summary>
    public void Vanish()
    {
        if (!IsIntact) return;
        IsIntact = false;

        if (goneSprite != null) _renderer.sprite = goneSprite;
        SetCollidersEnabled(false);
    }

    /// <summary>원래 상태로 되돌립니다. BE#02 복귀에서 전량 복원할 때 씁니다 (C-14-3-6).</summary>
    public void Restore()
    {
        if (IsIntact) return;
        IsIntact = true;

        _renderer.sprite = _intactSprite;
        SetCollidersEnabled(true);
    }

    void SetCollidersEnabled(bool on)
    {
        if (_colliders == null) return;
        foreach (var c in _colliders)
            if (c != null) c.enabled = on;
    }
}
