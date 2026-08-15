using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;

/// <summary>
/// Yarn Spinner 대화의 화자/본문을 기록하는 Presenter.
/// YarnCommandBridge가 런타임에 DialogueRunner에 자동 등록하므로 씬 배치 불필요.
///
/// - static 리스트라 씬 전환 후에도 기록 유지, 최대 <see cref="MaxEntries"/>줄 초과 시 오래된 것부터 삭제
/// - showDialogueLog 설정과 무관하게 항상 기록한다 (설정 false는 "열람 비활성"만 의미 —
///   중간에 켰을 때 이전 기록을 볼 수 있어야 함)
/// - 열람 UI는 <see cref="DialogueLogUI"/> 담당
/// </summary>
public class DialogueLogRecorder : DialoguePresenterBase
{
    public struct Entry
    {
        public string speaker;
        public string text;
    }

    const int MaxEntries = 200;

    static readonly List<Entry> _entries = new List<Entry>();
    public static IReadOnlyList<Entry> Entries => _entries;

    /// <summary>새 대사가 기록될 때마다 발생 (로그 UI 실시간 갱신용).</summary>
    public static event System.Action OnEntryAdded;

    // 도메인 리로드 비활성 환경 대비 static 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _entries.Clear();
        OnEntryAdded = null;
    }

    public override YarnTask OnDialogueStartedAsync() => YarnTask.CompletedTask;

    public override YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
    {
        _entries.Add(new Entry
        {
            speaker = line.CharacterName ?? "",
            text    = line.TextWithoutCharacterName.Text,
        });
        if (_entries.Count > MaxEntries) _entries.RemoveAt(0);
        OnEntryAdded?.Invoke();
        return YarnTask.CompletedTask;
    }

    public override YarnTask OnDialogueCompleteAsync() => YarnTask.CompletedTask;
}
