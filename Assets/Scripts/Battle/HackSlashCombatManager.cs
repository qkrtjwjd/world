using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 핵앤슬래시 전투 세션을 총괄합니다.
/// - 적 스폰 / AI 활성화
/// - 전투 종료 판정 (적 처치 or 플레이어 사망)
/// - 결과 UI 표시 후 쿨타임 세팅
/// </summary>
public class HackSlashCombatManager : MonoBehaviour
{
    public static HackSlashCombatManager Instance;

    // ─────────────────────────────────────────────
    //  인스펙터 연결
    // ─────────────────────────────────────────────
    [Header("전투 결과 UI")]
    [Tooltip("'승리!' / '패배...' 를 띄울 텍스트 (없으면 생략)")]
    public Text resultText;
    [Tooltip("결과 텍스트가 자동으로 사라질 시간(초)")]
    public float resultDisplayTime = 2.5f;

    [Header("플레이어 무적 시간")]
    [Tooltip("전투 시작 직후 플레이어가 피해를 받지 않는 시간(초)")]
    public float startInvincibleTime = 1.0f;

    [Header("적 스폰 옵션")]
    [Tooltip("씬에 이미 있는 적 오브젝트를 쓸 때 Prefab 대신 이 참조를 씁니다.\n"
           + "비워두면 Prefab 을 플레이어 근처에 소환합니다.")]
    public Transform spawnOffset = null; // 플레이어 기준 스폰 오프셋

    // ─────────────────────────────────────────────
    //  내부 상태
    // ─────────────────────────────────────────────
    private bool _isCombatActive = false;
    private GameObject _activeEnemy;

    // 플레이어 컨트롤러 캐시
    private RealityCombatController _combatCtrl;

    // ─────────────────────────────────────────────
    //  Unity
    // ─────────────────────────────────────────────
    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        // 플레이어에 RealityCombatController 가 없으면 자동 추가
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _combatCtrl = player.GetComponent<RealityCombatController>();
            if (_combatCtrl == null)
                _combatCtrl = player.AddComponent<RealityCombatController>();

            // 전투 외 시간에는 비활성화
            _combatCtrl.enabled = false;
        }

        if (resultText != null) resultText.gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────────
    //  전투 시작
    // ─────────────────────────────────────────────
    public void BeginCombat(GameObject existingEnemy, GameObject enemyPrefab)
    {
        if (_isCombatActive) return;
        _isCombatActive = true;

        // 1. 적 준비
        if (existingEnemy != null)
        {
            _activeEnemy = existingEnemy;
            ActivateEnemyAI(_activeEnemy);
        }
        else if (enemyPrefab != null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            Vector3 spawnPos  = player != null
                ? player.transform.position + new Vector3(2f, 0f, 0f)
                : Vector3.zero;

            _activeEnemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
            ActivateEnemyAI(_activeEnemy);
        }
        else
        {
            Debug.LogWarning("[HackSlashCombatManager] 적 오브젝트/프리팹이 없습니다.");
        }

        // 2. 플레이어 공격 활성화
        if (_combatCtrl != null) _combatCtrl.enabled = true;

        // 3. 무적 시간 + 폴링 루프 시작
        StartCoroutine(CombatLoop());
    }

    // ─────────────────────────────────────────────
    //  전투 루프 (매 0.2 초마다 종료 조건 체크)
    // ─────────────────────────────────────────────
    IEnumerator CombatLoop()
    {
        // 무적 구간
        float invTimer = startInvincibleTime;
        EnemyAI ai = _activeEnemy != null ? _activeEnemy.GetComponent<EnemyAI>() : null;
        if (ai != null) ai.SetChase(false); // 무적 중 추격 정지
        yield return new WaitForSeconds(startInvincibleTime);
        if (ai != null) ai.SetChase(true);

        // 전투 중 종료 조건 폴링
        while (_isCombatActive)
        {
            yield return new WaitForSeconds(0.2f);

            // 플레이어 사망
            if (PlayerStats.Instance != null && PlayerStats.Instance.currentHealth <= 0)
            {
                EndCombat(false);
                yield break;
            }

            // 적 소멸
            if (_activeEnemy == null)
            {
                EndCombat(true);
                yield break;
            }

            // EnemyHealth 로도 체크
            EnemyHealth eh = _activeEnemy.GetComponent<EnemyHealth>();
            if (eh != null && eh.currentHealth <= 0)
            {
                EndCombat(true);
                yield break;
            }
        }
    }

    // ─────────────────────────────────────────────
    //  전투 종료
    // ─────────────────────────────────────────────
    void EndCombat(bool playerWon)
    {
        _isCombatActive = false;

        // 공격 비활성화
        if (_combatCtrl != null) _combatCtrl.enabled = false;

        // 적 AI 정지
        if (_activeEnemy != null)
        {
            EnemyAI ai = _activeEnemy.GetComponent<EnemyAI>();
            if (ai != null) ai.SetChase(false);

            if (playerWon)
            {
                GameState.RegisterDefeatedEnemy(EncounterManager.currentEnemyID);
                Destroy(_activeEnemy);
            }
        }

        _activeEnemy = null;

        // 결과 UI
        ShowResult(playerWon);

        // 쿨타임 (전투 직후 재인카운터 방지)
        GameState.battleReturn.SetReturning(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, 3f);
    }

    // ─────────────────────────────────────────────
    //  결과 표시
    // ─────────────────────────────────────────────
    void ShowResult(bool playerWon)
    {
        if (resultText == null) return;

        resultText.text = playerWon ? "⚔ 전투 승리!" : "💀 전투 패배...";
        resultText.gameObject.SetActive(true);
        StartCoroutine(HideResultAfter(resultDisplayTime));
    }

    IEnumerator HideResultAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (resultText != null) resultText.gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────────
    //  유틸
    // ─────────────────────────────────────────────
    static void ActivateEnemyAI(GameObject enemy)
    {
        // EnemyAI 없으면 자동 추가
        EnemyAI ai = enemy.GetComponent<EnemyAI>();
        if (ai == null) ai = enemy.AddComponent<EnemyAI>();
        ai.enabled = true;

        // EnemyHealth 없으면 자동 추가
        EnemyHealth eh = enemy.GetComponent<EnemyHealth>();
        if (eh == null)
        {
            eh = enemy.AddComponent<EnemyHealth>();
            eh.maxHealth     = 100f;
            eh.currentHealth = 100f;
        }
    }

    // ─────────────────────────────────────────────
    //  외부 알림 (EnemyHealth 에서 호출)
    // ─────────────────────────────────────────────
    public void NotifyEnemyDead(GameObject enemy)
    {
        if (_isCombatActive && enemy == _activeEnemy)
        {
            _activeEnemy = null; // 루프가 다음 체크에서 EndCombat 호출
        }
    }
}
