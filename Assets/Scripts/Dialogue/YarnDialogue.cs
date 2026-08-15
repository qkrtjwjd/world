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
                _runner = Object.FindAnyObjectByType<DialogueRunner>(FindObjectsInactive.Include);
            return _runner;
        }
    }

    public static bool IsRunning => Runner != null && Runner.IsDialogueRunning;

    // 씬 전환 후 캐시 무효화 (YarnCommandBridge.Awake에서 호출)
    public static void Register(DialogueRunner runner) => _runner = runner;

    public static IEnumerator PlayAndWait(string nodeName, bool lockPlayer = true)
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

        // 비활성 GO에서는 코루틴을 시작할 수 없으므로 강제 활성화
        if (!runner.gameObject.activeInHierarchy)
            runner.gameObject.SetActive(true);

        ClearSky.SimplePlayerController ctrl = lockPlayer ? LockPlayer() : null;
        bool locked = lockPlayer && ctrl != null; // LockPlayer는 컨트롤러를 찾았을 때만 Lock을 건다

        try
        {
            runner.StartDialogue(nodeName);
            yield return null; // IsDialogueRunning 활성화 보장
            // runner가 씬 전환 등으로 파괴되면 MissingReferenceException 없이 탈출
            yield return new WaitUntil(() => runner == null || !runner.IsDialogueRunning);
        }
        finally
        {
            // ctrl이 파괴됐어도 lockCount 균형을 위해 반드시 Unlock (PlayerInputLock은 DontDestroyOnLoad)
            if (locked)
                PlayerInputLock.Instance?.Unlock();
        }
    }

    /// <summary>해당 이름의 노드가 실제로 컴파일돼 있는지. 미작성 노드를 호출하기 전에 확인한다.</summary>
    public static bool NodeExists(string nodeName)
    {
        if (string.IsNullOrEmpty(nodeName)) return false;
        var runner = Runner;
        return runner != null && runner.Dialogue != null && runner.Dialogue.NodeExists(nodeName);
    }

    /// <summary>
    /// 노드가 있으면 PlayAndWait, 없으면 경고만 남기고 즉시 반환한다.
    /// 대사가 아직 작성되지 않은 기능(솔 거래 등)에서 UI가 멈추지 않도록 쓴다.
    /// </summary>
    public static IEnumerator PlayIfExists(string nodeName, bool lockPlayer = true)
    {
        if (string.IsNullOrEmpty(nodeName)) yield break;
        if (!NodeExists(nodeName))
        {
            Debug.LogWarning($"[YarnDialogue] '{nodeName}': 노드가 아직 없습니다. 대사를 건너뜁니다.");
            yield break;
        }
        yield return PlayAndWait(nodeName, lockPlayer);
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
        PlayerInputLock.Instance?.Lock();
        return ctrl;
    }

    public static void UnlockPlayer(ClearSky.SimplePlayerController ctrl)
    {
        if (ctrl != null)
            PlayerInputLock.Instance?.Unlock();
    }

    private class CoroutineHost : MonoBehaviour { }
}
