using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class InteractionTrigger : MonoBehaviour
{
    [Header("설정")]
    public string      message              = "E키를 눌러 상호작용";
    public bool        hideTextAfterFirstView = false;

    [HideInInspector] public bool hasShownText = false;

    [Tooltip("E키를 눌렀을 때 실행할 기능을 여기에 연결하세요.")]
    public UnityEvent onInteract;

    private bool _canInteract = false;

    public void Interact()
    {
        if (!_canInteract) return;
        onInteract?.Invoke();

        if (hideTextAfterFirstView)
            InteractionTextUI.Instance?.Hide();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        StartCoroutine(EnableNextFrame());
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        _canInteract = false;
        StopAllCoroutines();
        InteractionManager.Instance?.UnregisterTrigger(this);
    }

    private void OnDisable()
    {
        _canInteract = false;
        InteractionManager.Instance?.UnregisterTrigger(this);
    }

    // 1프레임 대기 후 등록 (Enter 직후 바로 Interact 되는 오발 방지)
    IEnumerator EnableNextFrame()
    {
        yield return null;
        _canInteract = true;
        InteractionManager.Instance?.RegisterTrigger(this);
    }
}