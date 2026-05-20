public static class DialogueEvents
{
    public static event System.Action OnDialogueStarted;
    public static event System.Action OnDialogueEnded;
    public static event System.Action OnLineSkipped;

    internal static void RaiseStarted() => OnDialogueStarted?.Invoke();
    internal static void RaiseEnded()   => OnDialogueEnded?.Invoke();
    internal static void RaiseSkipped() => OnLineSkipped?.Invoke();
}
