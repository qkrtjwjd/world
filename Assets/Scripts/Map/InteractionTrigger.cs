using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class InteractionTrigger : MonoBehaviour
{
    [Header("기본 설정")]
    public string      message               = "E키를 눌러 상호작용";
    public bool        hideTextAfterFirstView = false;

    [Header("수치 변화 (상호작용 시 즉시 적용)")]
    [Tooltip("멘탈 변화량. 양수 = 감소(트라우마), 음수 = 회복. 0이면 변화 없음.")]
    public float mentalCost = 0f;

    [Tooltip("인형화 변화량. 양수 = 증가, 음수 = 감소. 0이면 변화 없음.")]
    public float corruptionChange = 0f;

    [HideInInspector] public bool hasShownText = false;

    [Tooltip("E키를 눌렀을 때 실행할 기능을 여기에 연결하세요.")]
    public UnityEvent onInteract;

    [Header("라디오 연동 (objectID를 RadioManager에 등록한 경우만 버튼 표시)")]
    public string objectID;
    public Button radioButton;

    [Header("거리 감지 (Solid 콜라이더가 트리거 진입을 막는 경우 사용)")]
    [SerializeField] private bool  useDistanceDetection = false;
    [SerializeField] private float interactionRange     = 0f;

    [Header("대사 Yarn 노드 (선택 — 비워두면 대사 없음)")]
    [Tooltip("상호작용 시 재생할 Yarn 노드 이름.")]
    public string yarnNode;
    [Tooltip("playOnce=true 일 때 두 번째 이후 상호작용에서 재생할 Yarn 노드 이름.")]
    public string yarnNodeRepeat;
    [Tooltip("대사 중 플레이어 이동을 잠글지 여부.")]
    public bool lockPlayerDuringDialogue = false;
    [Tooltip("체크 시: 첫 번째 대사 한 번만 재생. 이후엔 repeatDialogue 사용.")]
    public bool playOnce = false;
    [Tooltip("대사가 완전히 끝났을 때 호출됩니다.")]
    public UnityEvent onDialogueComplete;

    [Header("대사 후 아이템 스폰 (선택 — pickupPrefab 비워두면 비활성)")]
    [Tooltip("ItemPickup 컴포넌트가 포함된 프리팹. 대화 후 인스턴스화합니다.")]
    public GameObject pickupPrefab;
    [Tooltip("스폰할 아이템 데이터. 프리팹의 ItemData를 덮어씁니다.")]
    public ItemData itemToSpawn;
    [Min(1)] public int spawnQuantity = 1;
    [Tooltip("아이템이 생성될 위치. 비워두면 이 오브젝트 위치에 스폰됩니다.")]
    public Transform spawnPoint;
    [Tooltip("체크 시 최초 1회만 스폰.")]
    public bool spawnOnce = true;

    private bool      _canInteract     = false;
    private Transform _playerTransform;
    private bool      _hasPlayed       = false;
    private bool      _spawned         = false;
    private bool      _statsApplied    = false;

    void Start()
    {
        if (radioButton != null)
            radioButton.onClick.AddListener(OnRadioButtonClicked);
        RefreshRadioButton(false);

        if (useDistanceDetection)
            CachePlayerTransform();
    }

    private void CachePlayerTransform()
    {
        if (PlayerStats.Instance != null)
            _playerTransform = PlayerStats.Instance.transform;
        else
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) _playerTransform = p.transform;
        }
    }

    private void OnRadioButtonClicked()
    {
        RadioManager.Instance?.PlayRadio(objectID);
    }

    private void OnDestroy()
    {
        if (radioButton != null)
            radioButton.onClick.RemoveListener(OnRadioButtonClicked);
    }

    void Update()
    {
        if (!useDistanceDetection) return;
        if (_playerTransform == null) return;

        float dist = Vector2.Distance(transform.position, _playerTransform.position);
        bool inRange = dist <= interactionRange;

        if (inRange && !_canInteract)
        {
            _canInteract = true;
            InteractionManager.Instance?.RegisterTrigger(this);
            RefreshRadioButton(true);
        }
        else if (!inRange && _canInteract)
        {
            _canInteract = false;
            InteractionManager.Instance?.UnregisterTrigger(this);
            RefreshRadioButton(false);
        }
    }

    public void Interact()
    {
        if (!_canInteract) return;

        // 멘탈·인형화 수치는 최초 1회만 적용
        if (!_statsApplied)
        {
            _statsApplied = true;

            // 멘탈 변화: 양수 = 감소, 음수 = 회복
            if (mentalCost > 0f)
                PlayerStats.Instance?.AddTrauma(mentalCost);
            else if (mentalCost < 0f)
                PlayerStats.Instance?.RecoverMental(-mentalCost);

            // 인형화 변화: 양수 = 증가, 음수 = 감소
            if (corruptionChange != 0f)
                CorruptionManager.Instance?.AddCorruption(corruptionChange);
        }

        onInteract?.Invoke();

        if (!string.IsNullOrEmpty(yarnNode))
            HandleDialogue();

        if (hideTextAfterFirstView)
            InteractionTextUI.Instance?.Hide();
    }

    // ── 대사 처리 ─────────────────────────────────────────────────────────

    private void HandleDialogue()
    {
        if (YarnDialogue.IsRunning) return;

        string toPlay;
        if (playOnce && _hasPlayed)
        {
            if (string.IsNullOrEmpty(yarnNodeRepeat)) return;
            toPlay = yarnNodeRepeat;
        }
        else
        {
            toPlay = yarnNode;
            _hasPlayed = true;
        }

        // ItemPickup이 함께 있으면 오브젝트 Destroy 후에도 안전한 영속 호스트에서 실행
        if (GetComponent<ItemPickup>() != null)
            YarnDialogue.StartCoroutine(WaitForAcquisitionThenPlay(toPlay));
        else
            StartCoroutine(PlayAndNotify(toPlay));
    }

    private IEnumerator PlayAndNotify(string nodeName)
    {
        yield return YarnDialogue.PlayAndWait(nodeName, lockPlayerDuringDialogue);
        onDialogueComplete?.Invoke();
        TrySpawnItem();
    }

    private IEnumerator WaitForAcquisitionThenPlay(string nodeName)
    {
        // ItemPickup이 gameObject를 Destroy할 수 있으므로 yield 전에 값 캡처
        bool lockPlayer = lockPlayerDuringDialogue;
        yield return null;
        yield return new WaitUntil(() =>
        {
            var ui = ItemAcquisitionUI.Instance;
            return (ui == null || !ui.IsShowing) && !YarnDialogue.IsRunning;
        });
        yield return YarnDialogue.PlayAndWait(nodeName, lockPlayer);
        // 오브젝트가 Destroy됐을 수 있으므로 생존 확인 후 접근
        if (this != null)
        {
            onDialogueComplete?.Invoke();
            TrySpawnItem();
        }
    }

    // ── 대사 후 아이템 스폰 ───────────────────────────────────────────────

    private void TrySpawnItem()
    {
        if (pickupPrefab == null) return;
        if (spawnOnce && _spawned) return;

        Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
        var spawned = Instantiate(pickupPrefab, pos, Quaternion.identity);

        var pickup = spawned.GetComponent<ItemPickup>();
        if (pickup != null)
        {
            if (itemToSpawn != null) pickup.itemData = itemToSpawn;
            pickup.quantity = spawnQuantity;
        }
        else
        {
            Debug.LogWarning($"[InteractionTrigger] pickupPrefab '{pickupPrefab.name}' 에 ItemPickup 컴포넌트가 없습니다.");
        }

        _spawned = true;
    }

    // ── 콜라이더/거리 감지 ────────────────────────────────────────────────

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
        RefreshRadioButton(false);
    }

    private void OnDisable()
    {
        _canInteract = false;
        InteractionManager.Instance?.UnregisterTrigger(this);
        RefreshRadioButton(false);
    }

    IEnumerator EnableNextFrame()
    {
        yield return null;
        _canInteract = true;
        InteractionManager.Instance?.RegisterTrigger(this);
        RefreshRadioButton(true);
    }

    void RefreshRadioButton(bool inRange)
    {
        if (radioButton == null) return;
        bool show = inRange
                 && RadioManager.Instance != null
                 && RadioManager.Instance.HasRadioData(objectID);
        radioButton.gameObject.SetActive(show);
    }
}
