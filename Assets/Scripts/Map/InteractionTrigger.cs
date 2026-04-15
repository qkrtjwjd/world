using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class InteractionTrigger : MonoBehaviour
{
    [Header("설정")]
    public string      message              = "E키를 눌러 상호작용";
    public bool        hideTextAfterFirstView = false;

    [Tooltip("상호작용 시 소모될 멘탈 수치 (0이면 소모 없음)")]
    public float mentalCost = 0f;

    [HideInInspector] public bool hasShownText = false;

    [Tooltip("E키를 눌렀을 때 실행할 기능을 여기에 연결하세요.")]
    public UnityEvent onInteract;

    [Header("거리 감지 (Solid 콜라이더가 트리거 진입을 막는 경우 사용)")]
    [SerializeField] private bool  useDistanceDetection = false;
    [SerializeField] private float interactionRange     = 0f;

    private bool      _canInteract     = false;
    private Transform _playerTransform;

    void Update()
    {
        if (!useDistanceDetection) return;

        if (_playerTransform == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) _playerTransform = p.transform;
            return;
        }

        float dist = Vector2.Distance(transform.position, _playerTransform.position);
        bool inRange = dist <= interactionRange;

        if (inRange && !_canInteract)
        {
            _canInteract = true;
            InteractionManager.Instance?.RegisterTrigger(this);
        }
        else if (!inRange && _canInteract)
        {
            _canInteract = false;
            InteractionManager.Instance?.UnregisterTrigger(this);
        }
    }

    public void Interact()
    {
        if (!_canInteract) return;

        if (mentalCost > 0f)
            PlayerStats.Instance?.AddTrauma(mentalCost);

        onInteract?.Invoke();

        if (hideTextAfterFirstView)
            InteractionTextUI.Instance?.Hide();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (useDistanceDetection || !other.CompareTag("Player") || other.isTrigger) return;
        StartCoroutine(EnableNextFrame());
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (useDistanceDetection || _canInteract || !other.CompareTag("Player") || other.isTrigger) return;
        StartCoroutine(EnableNextFrame());
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (useDistanceDetection || !other.CompareTag("Player") || other.isTrigger) return;
        _canInteract = false;
        StopAllCoroutines();
        InteractionManager.Instance?.UnregisterTrigger(this);
    }

    private void OnDisable()
    {
        _canInteract = false;
        InteractionManager.Instance?.UnregisterTrigger(this);
    }

    IEnumerator EnableNextFrame()
    {
        yield return null;
        _canInteract = true;
        InteractionManager.Instance?.RegisterTrigger(this);
    }
}
