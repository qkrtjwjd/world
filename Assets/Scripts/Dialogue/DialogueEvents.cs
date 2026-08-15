public static class DialogueEvents
{
    public static event System.Action OnDialogueStarted;
    public static event System.Action OnDialogueEnded;

    internal static void RaiseStarted() => OnDialogueStarted?.Invoke();
    internal static void RaiseEnded()   => OnDialogueEnded?.Invoke();
}
