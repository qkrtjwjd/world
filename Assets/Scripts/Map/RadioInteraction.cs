using System.Collections;
using UnityEngine;

/// <summary>
/// S#8 라디오 — 아버지의 메시지.
/// - 아버지 목소리 오디오를 재생하고 대화를 출력한다.
/// - 대화 종료 후 GameState.hasResolve = true 로 설정한다.
///   (이후 현관문이 열린다)
/// InteractionTrigger.onInteract UnityEvent 에 OnRadioInteract() 를 연결하세요.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class RadioInteraction : MonoBehaviour
{
    [Header("아버지 목소리 오디오 (AudioClip)")]
    public AudioClip fatherVoiceClip;

    [Header("아버지 메시지 대화 (DialogueData 에셋 연결)")]
    public DialogueData fatherDialogue;

    private bool _played = false;
    private AudioSource _audio;

    void Awake()
    {
        _audio = GetComponent<AudioSource>();
    }

    /// <summary>InteractionTrigger.onInteract 에 연결.</summary>
    public void OnRadioInteract()
    {
        if (_played) return;
        _played = true;

        var trigger = GetComponent<InteractionTrigger>();
        if (trigger != null) trigger.enabled = false;

        StartCoroutine(RadioRoutine());
    }

    IEnumerator RadioRoutine()
    {
        var ctrl = LockPlayer();

        if (fatherVoiceClip != null)
            _audio.PlayOneShot(fatherVoiceClip);

        if (fatherDialogue != null)
            DialogueManager.Instance?.StartDialogue(fatherDialogue);

        yield return null;
        while (DialogueManager.Instance != null && DialogueManager.Instance.isTalking)
            yield return null;

        GameState.hasResolve = true;

        UnlockPlayer(ctrl);
    }

    static ClearSky.SimplePlayerController LockPlayer()
    {
        var ctrl = Object.FindAnyObjectByType<ClearSky.SimplePlayerController>();
        if (ctrl == null) return null;
        var rb = ctrl.GetComponent<Rigidbody2D>();
        ctrl.enabled = false;
        if (rb != null) rb.linearVelocity = Vector2.zero;
        return ctrl;
    }

    static void UnlockPlayer(ClearSky.SimplePlayerController ctrl)
    {
        if (ctrl != null) ctrl.enabled = true;
    }
}
