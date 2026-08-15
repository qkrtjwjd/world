using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 목표(저널) 기록을 보관하는 정적 클래스 (GameState 패턴).
/// ObjectiveManager.ShowObjective/CompleteObjective 가 단일 경유점이므로 그 지점에서 기록됩니다.
/// 선형 프롤로그 구조에 맞춰 활성 목표는 항상 1개 — 새 목표 추가 시 직전 활성 목표는 자동 완료 처리.
/// 열람 UI는 JournalUI, 직렬화는 SaveManager(JournalEntrySave) 담당.
/// </summary>
public static class JournalManager
{
    public class JournalEntry
    {
        public string header;
        public string body;
        public bool   isCompleted;
        public float  playTimeAtAcquire;
    }

    static readonly List<JournalEntry> _entries = new List<JournalEntry>();

    public static IReadOnlyList<JournalEntry> Entries => _entries;

    /// <summary>현재 진행 중인 목표. 없으면 null.</summary>
    public static JournalEntry CurrentEntry
    {
        get
        {
            for (int i = _entries.Count - 1; i >= 0; i--)
                if (!_entries[i].isCompleted) return _entries[i];
            return null;
        }
    }

    /// <summary>목표 추가/완료 시 발생. UI 갱신용.</summary>
    public static event System.Action OnJournalChanged;

    // ──────────────────────────────────────────
    //  기록
    // ──────────────────────────────────────────
    /// <summary>새 목표를 기록합니다. 직전 활성 목표는 자동 완료, 동일 body 중복은 무시.</summary>
    public static void Add(string header, string body)
    {
        if (string.IsNullOrEmpty(body)) return;

        // 같은 목표가 다시 표시된 경우 (컷씬 재진입 등) 중복 기록 방지
        var current = CurrentEntry;
        if (current != null && current.body == body) return;

        // 선형 진행 — 직전 활성 목표 자동 완료
        if (current != null) current.isCompleted = true;

        _entries.Add(new JournalEntry
        {
            header            = header,
            body              = body,
            isCompleted       = false,
            playTimeAtAcquire = SaveManager.Instance != null ? SaveManager.Instance.currentPlayTime : 0f,
        });
        OnJournalChanged?.Invoke();

        // 모든 컷씬이 목표 갱신으로 끝나므로 이 지점이 체크포인트 자동 저장의 실질 커버리지
        SaveManager.Instance?.SaveCheckpoint("목표 갱신");
    }

    /// <summary>현재 진행 중인 목표를 완료 처리합니다.</summary>
    public static void CompleteCurrent()
    {
        var current = CurrentEntry;
        if (current == null) return;
        current.isCompleted = true;
        OnJournalChanged?.Invoke();

        SaveManager.Instance?.SaveCheckpoint("목표 달성");
    }

    // ──────────────────────────────────────────
    //  세이브 직렬화 (SaveManager 에서 호출)
    // ──────────────────────────────────────────
    public static List<JournalEntrySave> BuildSaveList()
    {
        var list = new List<JournalEntrySave>();
        foreach (var e in _entries)
            list.Add(new JournalEntrySave
            {
                header            = e.header,
                body              = e.body,
                isCompleted       = e.isCompleted,
                playTimeAtAcquire = e.playTimeAtAcquire,
            });
        return list;
    }

    public static void Load(List<JournalEntrySave> saved)
    {
        _entries.Clear();
        if (saved != null)
            foreach (var s in saved)
                _entries.Add(new JournalEntry
                {
                    header            = s.header,
                    body              = s.body,
                    isCompleted       = s.isCompleted,
                    playTimeAtAcquire = s.playTimeAtAcquire,
                });
        OnJournalChanged?.Invoke();
    }

    // ──────────────────────────────────────────
    //  플레이 시작 시 정적 변수 초기화 (GameState 패턴)
    // ──────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ResetOnPlay()
    {
        _entries.Clear();
        OnJournalChanged = null;
    }
}
