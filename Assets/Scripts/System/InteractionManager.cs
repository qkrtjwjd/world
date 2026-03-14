using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InteractionManager : MonoBehaviour
{
    public static InteractionManager Instance;

    // 상호작용 가능한 트리거 목록
    private List<InteractionTrigger> _triggersInRange = new List<InteractionTrigger>();
    private InteractionTrigger _activeTrigger;
    private Transform _playerTransform;
    private InteractionTextUI _uiInstance;
    
    // 쿨타임 (연속 상호작용 방지)
    private float _cooldownTimer;
    private float _calcTimer; // 거리 계산 주기용

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // ★ 씬 이동 시에도 파괴되지 않도록 설정
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // UI 인스턴스 캐싱
        _uiInstance = InteractionTextUI.Instance;
        
        // 플레이어 찾기 (PlayerStats 싱글톤 활용 권장)
        if (PlayerStats.Instance != null)
            _playerTransform = PlayerStats.Instance.transform;
    }

    void Update()
    {
        // 플레이어가 없으면 다시 찾기 시도
        if (_playerTransform == null)
        {
            if (PlayerStats.Instance != null) _playerTransform = PlayerStats.Instance.transform;
            else return; 
        }

        // 쿨타임 감소
        if (_cooldownTimer > 0)
        {
            _cooldownTimer -= Time.deltaTime;
        }

        // [최적화] 거리 계산은 0.1초마다 수행 (매 프레임 X)
        _calcTimer += Time.deltaTime;
        if (_calcTimer >= 0.1f)
        {
            _calcTimer = 0f;
            RefreshClosestTrigger();
        }

        // 입력 감지 (매 프레임)
        if (Input.GetKeyDown(KeyCode.E))
        {
            // 쿨타임 중이면 무시 (중복 입력 방지 핵심)
            if (_cooldownTimer > 0)
            {
                // Debug.Log("쿨타임 중이라 상호작용 무시됨");
                return;
            }

            if (_activeTrigger != null)
            {
                // 상호작용 실행
                _activeTrigger.Interact();

                // ★ 즉시 글로벌 쿨타임 0.5초 적용 (어떤 트리거도 연속 실행 불가)
                _cooldownTimer = 0.5f;
            }
        }
    }

    // 가장 가까운 트리거 갱신
    private void RefreshClosestTrigger()
    {
        if (_triggersInRange.Count == 0)
        {
            if (_activeTrigger != null)
            {
                _activeTrigger = null;
                UpdateUI();
            }
            return;
        }

        InteractionTrigger closest = null;
        float minSqrDist = float.MaxValue;
        Vector3 playerPos = _playerTransform.position;

        // 리스트 역순 순회 (삭제 시 안전)
        for (int i = _triggersInRange.Count - 1; i >= 0; i--)
        {
            InteractionTrigger t = _triggersInRange[i];
            
            // 파괴된 객체 정리
            if (t == null)
            {
                _triggersInRange.RemoveAt(i);
                continue;
            }
            if (!t.gameObject.activeInHierarchy) continue;

            float sqrDist = (t.transform.position - playerPos).sqrMagnitude;
            if (sqrDist < minSqrDist)
            {
                minSqrDist = sqrDist;
                closest = t;
            }
        }

        // 타겟이 바뀌었을 때만 UI 업데이트
        if (closest != _activeTrigger)
        {
            _activeTrigger = closest;
            UpdateUI();
        }
    }

    public void RegisterTrigger(InteractionTrigger trigger)
    {
        if (!_triggersInRange.Contains(trigger))
        {
            _triggersInRange.Add(trigger);
        }
    }

    public void UnregisterTrigger(InteractionTrigger trigger)
    {
        _triggersInRange.Remove(trigger);
        if (_activeTrigger == trigger)
        {
            _activeTrigger = null;
            UpdateUI();
        }
    }

    private void UpdateUI()
    {
        if (_uiInstance == null) _uiInstance = InteractionTextUI.Instance;
        if (_uiInstance == null) return;

        if (_activeTrigger != null)
        {
            // 한 번만 보여주기 옵션 체크
            if (_activeTrigger.hideTextAfterFirstView && _activeTrigger.hasShownText)
            {
                _uiInstance.Hide();
            }
            else
            {
                _uiInstance.Show(_activeTrigger.message);
                _activeTrigger.hasShownText = true;
            }
        }
        else
        {
            _uiInstance.Hide();
        }
    }

    public void SetCooldown(float time)
    {
        _cooldownTimer = time;
    }

    public bool IsCoolingDown => _cooldownTimer > 0;
}
