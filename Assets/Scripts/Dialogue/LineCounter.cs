using Yarn.Unity;

/// <summary>
/// Yarn Spinner 대화에서 표시된 라인 수를 추적하는 카운터 Presenter.
/// DialogueRunner GameObject에 추가하고, DialogueRunner의 Dialogue Views 배열에 등록하세요.
/// </summary>
public class LineCounter : DialoguePresenterBase
{
    public static int Current { get; private set; }
    public static event System.Action<int> OnLineAdvanced;

    public override YarnTask OnDialogueStartedAsync()
    {
        Current = 0;
        return YarnTask.CompletedTask;
    }

    public override YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
    {
        Current++;
        OnLineAdvanced?.Invoke(Current);
        return YarnTask.CompletedTask;
    }

    public override YarnTask OnDialogueCompleteAsync() => YarnTask.CompletedTask;
}
