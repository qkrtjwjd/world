using System.Collections;
using UnityEngine;

/// <summary>
/// 핵앤슬래시 전투에서 적의 체력을 관리합니다.
/// - 사망 시 HackSlashCombatManager 에 통보합니다.
/// - 체력 10% 미만 → 도주 → 숨김 → 플레이어가 멀어지면 원래 위치로 귀환.
/// </summary>
public class EnemyHealth : MonoBehaviour
{
    [Header("체력")]
    public float maxHealth = 100f;
    [HideInInspector] public float currentHealth; // Start()에서 maxHealth로 초기화됨

    [Header("사망 이펙트")]
    public GameObject deathEffect;

    [Header("도주 설정")]
    [Tooltip("체력이 최대 체력의 이 비율 미만이 되면 도주 시작 (0.1 = 10%)")]
    public float fleeHealthRatio = 0.1f;

    [Header("귀환 설정")]
    [Tooltip("플레이어가 이 거리 이상 멀어지면 적이 원래 위치로 귀환합니다.")]
    public float respawnDistance = 20f;
    [Tooltip("귀환 감시 간격 (초)")]
    public float respawnCheckInterval = 0.5f;

    // ─────────────────────────────────────────────
    //  내부 상태
    // ─────────────────────────────────────────────
    private Vector3       _originalPosition;
    private bool          _isFleeing = false;
    private Transform     _playerTransform;   // RespawnWatch에서 반복 검색 방지용 캐시
    private SpriteRenderer[] _spriteRenderers; // SetVisible 반복 GetComponents 방지용 캐시
    private Collider2D[]     _colliders;

    // ─────────────────────────────────────────────
    //  Unity
    // ─────────────────────────────────────────────
    void Start()
    {
        currentHealth     = maxHealth;
        _originalPosition = transform.position;

        // 컴포넌트 캐시
        _spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        _colliders       = GetComponents<Collider2D>();

        // 플레이어 참조 캐시
        if (PlayerStats.Instance != null)
            _playerTransform = PlayerStats.Instance.transform;
        else
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) _playerTransform = p.transform;
        }
    }

    // ─────────────────────────────────────────────
    //  피격
    // ─────────────────────────────────────────────

    /// <summary>RealityCombatController 에서 호출됩니다.</summary>
    public void TakeRealityDamage(float damage)
    {
        currentHealth = Mathf.Max(0f, currentHealth - damage);

        Debug.Log($"[EnemyHealth] {gameObject.name} 피격: -{damage:F1}  남은 HP: {currentHealth:F1}");

        if (currentHealth <= 0f) { Die(); return; }

        if (!_isFleeing && currentHealth < maxHealth * fleeHealthRatio)
        {
            _isFleeing = true;
            EnemyAI ai = GetComponent<EnemyAI>();
            if (ai != null) ai.StartFlee();
        }
    }

    // ─────────────────────────────────────────────
    //  사망
    // ─────────────────────────────────────────────
    void Die()
    {
        HackSlashCombatManager.Instance?.NotifyEnemyDead(gameObject);

        if (deathEffect != null)
            Instantiate(deathEffect, transform.position, Quaternion.identity);

        Destroy(gameObject, 0.1f);
    }

    // ─────────────────────────────────────────────
    //  도주 완료 → 귀환 대기
    // ─────────────────────────────────────────────

    /// <summary>EnemyAI.FleeRoutine 완료 시 호출됩니다.</summary>
    public void OnFledComplete()
    {
        HackSlashCombatManager.Instance?.NotifyEnemyFled(gameObject);

        transform.position = _originalPosition;
        SetVisible(false);

        EnemyAI ai = GetComponent<EnemyAI>();
        if (ai != null) ai.enabled = false;

        StartCoroutine(RespawnWatch());
    }

    IEnumerator RespawnWatch()
    {
        var wait = new WaitForSeconds(respawnCheckInterval);

        while (true)
        {
            yield return wait;

            // 캐시된 참조가 없으면 재시도 (씬 로드 등으로 플레이어가 교체된 경우)
            if (_playerTransform == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p == null) continue;
                _playerTransform = p.transform;
            }

            if (Vector2.Distance(_originalPosition, _playerTransform.position) >= respawnDistance)
            {
                Respawn();
                yield break;
            }
        }
    }

    void Respawn()
    {
        currentHealth = maxHealth;
        _isFleeing    = false;

        EnemyAI ai = GetComponent<EnemyAI>();
        if (ai != null)
        {
            ai.enabled = true;
            ai.ResetFlee();
        }

        SetVisible(true);
        Debug.Log($"[EnemyHealth] {gameObject.name} 귀환 완료.");
    }

    // ─────────────────────────────────────────────
    //  유틸
    // ─────────────────────────────────────────────
    void SetVisible(bool visible)
    {
        foreach (var sr in _spriteRenderers) sr.enabled = visible;
        foreach (var col in _colliders)      col.enabled = visible;
    }
}
