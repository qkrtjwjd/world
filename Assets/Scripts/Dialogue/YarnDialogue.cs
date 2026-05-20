using System.Collections;
using UnityEngine;
using Yarn.Unity;

public static class YarnDialogue
{
    private static DialogueRunner _runner;
    private static CoroutineHost  _host;

    public static DialogueRunner Runner
    {
        get
        {
            if (_runner == null)
                _runner = Object.FindAnyObjectByType<DialogueRunner>();
            return _runner;
        }
    }

    public static bool IsRunning => Runner != null && Runner.IsDialogueRunning;

    // 씬 전환 후 캐시 무효화 (YarnCommandBridge.Awake에서 호출)
    public static void Register(DialogueRunner runner) => _runner = runner;

    public static IEnumerator PlayAndWait(string nodeName, bool lockPlayer = false)
    {
        if (string.IsNullOrEmpty(nodeName)) yield break;
        var runner = Runner;
        if (runner == null)
        {
            Debug.LogWarning($"[YarnDialogue] '{nodeName}': DialogueRunner를 찾을 수 없습니다.");
            yield break;
        }
        if (runner.IsDialogueRunning)
        {
            Debug.LogWarning($"[YarnDialogue] '{nodeName}': 이미 대화가 진행 중입니다.");
            yield break;
        }

        ClearSky.SimplePlayerController ctrl = lockPlayer ? LockPlayer() : null;

        runner.StartDialogue(nodeName);
        yield return null; // IsDialogueRunning 활성화 보장
        yield return new WaitUntil(() => !runner.IsDialogueRunning);

        if (lockPlayer && ctrl != null)
            UnlockPlayer(ctrl);
    }

    // 오브젝트가 Destroy돼도 살아있어야 하는 코루틴에 사용 (ItemPickup, InteractionTrigger 등)
    public static Coroutine StartCoroutine(IEnumerator routine)
    {
        if (_host == null)
        {
            var go = new GameObject("[YarnDialogueHost]");
            Object.DontDestroyOnLoad(go);
            _host = go.AddComponent<CoroutineHost>();
        }
        return _host.StartCoroutine(routine);
    }

    public static ClearSky.SimplePlayerController LockPlayer()
    {
        var ctrl = Object.FindAnyObjectByType<ClearSky.SimplePlayerController>();
        if (ctrl == null)
        {
            Debug.LogWarning("[YarnDialogue] LockPlayer: SimplePlayerController를 찾을 수 없습니다.");
            return null;
        }
        ctrl.Lock();
        return ctrl;
    }

    public static void UnlockPlayer(ClearSky.SimplePlayerController ctrl)
    {
        if (ctrl != null) ctrl.Unlock();
    }

    private class CoroutineHost : MonoBehaviour { }
}
