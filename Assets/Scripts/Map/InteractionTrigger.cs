using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class InteractionTrigger : MonoBehaviour
{
    /// <summary>
    /// 접근 시 띄울 문구. <see cref="showPrompt"/> 가 켜져 있을 때만 보인다.
    /// </summary>
    [Header("기본 설정")]
    public string      message               = "";

    /// <summary>
    /// 접근 표시를 띄울지. <b>기본값은 꺼짐이다.</b>
    ///
    /// C-16-8 · F-8-6 — 상호작용 가능 표시는 <b>세이브 포인트와 솔에만</b> 붙는다.
    /// 무엇이 중요한지를 게임이 먼저 알려주면 D-S#07 의 헛수고 설계와 충돌한다.
    /// 그래서 켜는 쪽을 예외로 두고, 켤 때는 정본의 어느 항목에 해당하는지 확인할 것.
    /// </summary>
    public bool        showPrompt            = false;

    public bool        hideTextAfterFirstView = false;

    [Header("수치 변화 (상호작용 시 즉시 적용)")]
    [Tooltip("멘탈 변화량. 양수 = 감소(트라우마), 음수 = 회복. 0이면 변화 없음.")]
    public float mentalCost = 0f;

    [Tooltip("인형화 변화량. 양수 = 증가, 음수 = 감소. 0이면 변화 없음.")]
    public float corruptionChange = 0f;

    [HideInInspector] public bool hasShownText = false;

    [Tooltip("E키를 눌렀을 때 실행할 기능을 여기에 연결하세요.")]
    public UnityEvent onInteract;

    // ⚠ 라디오 연동(radioButton · radioYarnNode)은 2026-08-30 삭제했다. 다시 만들지 말 것.
    //   E-52 가 [라디오] 선택지 방식을 폐기했다 — 유의 반응은 조사 키로만 나오고, 대비
    //   오브젝트 노드 안에서 소지 여부로 조건 분기한다(F-8-4). 버튼을 두면 입력 축이 하나
    //   늘고 어떤 물건에 반응이 있는지를 UI 가 먼저 알려주게 된다.
    //   구 반응 대상 목록 16종도 함께 폐기됐다(E-39-2). 새 대상은 51절의 대비 오브젝트에서 고른다.

    [Header("거리 감지 (Solid 콜라이더가 트리거 진입을 막는 경우 사용)")]
    [SerializeField] private bool  useDistanceDetection = false;
    [SerializeField] private float interactionRange     = 0f;

    [Header("대사 Yarn 노드 (선택 — 비워두면 대사 없음)")]
    [Tooltip("상호작용 시 재생할 Yarn 노드 이름.")]
    public string yarnNode;
    [Tooltip("playOnce=true 일 때 두 번째 이후 상호작용에서 재생할 Yarn 노드 이름.")]
    public string yarnNodeRepeat;
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
        }
        else if (!inRange && _canInteract)
        {
            _canInteract = false;
            InteractionManager.InstanceIfExists?.UnregisterTrigger(this);
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
        yield return YarnDialogue.PlayAndWait(nodeName);
        onDialogueComplete?.Invoke();
        TrySpawnItem();
    }

    private IEnumerator WaitForAcquisitionThenPlay(string nodeName)
    {
        yield return null;
        yield return new WaitUntil(() =>
        {
            var ui = ItemAcquisitionUI.Instance;
            return (ui == null || !ui.IsShowing) && !YarnDialogue.IsRunning;
        });
        yield return YarnDialogue.PlayAndWait(nodeName);
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
        InteractionManager.InstanceIfExists?.UnregisterTrigger(this);
    }

    /// <remarks>
    /// ⚠ 여기서 <c>InteractionManager.Instance</c> 를 부르면 안 된다. 씬이 닫힐 때도 이 경로가 도는데,
    /// 그 게터는 매니저가 없으면 새로 만들기 때문에 <b>죽어가는 씬 안에</b> 오브젝트가 생긴다
    /// ("Some objects were not cleaned up when closing the scene", 2026-08-23 실측).
    /// 등록 취소는 매니저가 없으면 할 일도 없다.
    /// </remarks>
    private void OnDisable()
    {
        _canInteract = false;
        InteractionManager.InstanceIfExists?.UnregisterTrigger(this);
    }

    IEnumerator EnableNextFrame()
    {
        yield return null;
        _canInteract = true;
        InteractionManager.Instance?.RegisterTrigger(this);
    }
}
