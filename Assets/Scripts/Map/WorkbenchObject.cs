using System.Collections;
using UnityEngine;

/// <summary>
/// 데모 잠금 상태 작업대. 상호작용 시 잠금 메시지를 출력합니다.
/// 본편 해금 시 GameState.isWorkbenchLocked 플래그 해제 후 미니게임 연결 예정.
/// </summary>
public class WorkbenchObject : MonoBehaviour
{
    [Header("잠금 오버레이")]
    [SerializeField] private GameObject lockOverlay;

    [Header("설정")]
    [SerializeField] private string lockedMessage   = "아직 잠겨 있다.";
    [SerializeField] private float  messageHoldTime = 2f;

    private bool _isPlayerNear;
    private bool _isBusy;

    void Awake()
    {
        GameState.isWorkbenchLocked = true; // 데모: 항상 잠김
        if (lockOverlay != null) lockOverlay.SetActive(GameState.isWorkbenchLocked);
    }

    void Update()
    {
        if (!_isPlayerNear || _isBusy) return;
        KeyCode daggerKey = SettingsManager.Instance?.keyDagger ?? KeyCode.F;
        if (!Input.GetKeyDown(daggerKey)) return;
        if (YarnDialogue.IsRunning) return;
        if (!DaggerKeyRegistry.IsClosest(this)) return;

        // TODO: 본편 해금 시 GameState.isWorkbenchLocked == false 분기에 미니게임 연결
        if (GameState.isWorkbenchLocked)
            StartCoroutine(ShowLockedMessage());
    }

    IEnumerator ShowLockedMessage()
    {
        _isBusy = true;
        InteractionTextUI.Instance?.Show(lockedMessage);
        yield return new WaitForSeconds(messageHoldTime);
        InteractionTextUI.Instance?.Hide();
        _isBusy = false;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        _isPlayerNear = true;
        DaggerKeyRegistry.Register(this);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        _isPlayerNear = false;
        DaggerKeyRegistry.Unregister(this);
        if (_isBusy)
        {
            StopAllCoroutines();
            InteractionTextUI.Instance?.Hide();
            _isBusy = false;
        }
    }

    void OnDisable()
    {
        DaggerKeyRegistry.Unregister(this);
    }
}
