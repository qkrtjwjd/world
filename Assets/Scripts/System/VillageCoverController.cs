using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 엄폐물 소실 (C-14-3-4 / 수치 F-6).
///
/// 순찰 라운드가 끝날 때마다 한 단계씩 사라진다. 순서는 수레 → 화분 → 간판이며
/// <b>3단계에서 멈춘다</b>. 그 이상 줄이지 않는다.
///
/// 전부 없애면 진행이 불가능해진다. 기다리기만 하면 되는 구조를 막는 것이 목적이지
/// 통행을 막는 것이 목적이 아니다(C-14-3-4). 그래서 잔존 수량에 하한을 둔다 —
/// F-6 「엄폐물 잔존: 3단계 시점 4개 · 마을 출구 경로에 최소 2개를 남긴다」.
///
/// 개별 배치 좌표는 F-6 이 "마을 배치 확정 후 정한다" 로 남겨 두었다. 여기서는 정하지 않는다.
///
/// 배치 불필요 — 씬에 <see cref="VillageCover"/> 가 하나라도 있으면 자동으로 생긴다.
/// </summary>
public class VillageCoverController : MonoBehaviour
{
    /// <summary>소실이 멈추는 단계. F-6 「라운드당 1단계 · 3단계에서 정지」.</summary>
    public const int MaxStage = 3;

    /// <summary>3단계 시점에 남아 있어야 하는 엄폐물 수. F-6 「3단계 시점 4개」.</summary>
    public const int MinRemaining = 4;

    public static VillageCoverController Instance { get; private set; }

    /// <summary>현재 소실 단계 0~3. 0 이면 아무것도 사라지지 않았다.</summary>
    public int Stage { get; private set; }

    /// <summary>아직 몸을 숨길 수 있는 엄폐물 수.</summary>
    public int RemainingCount
    {
        get
        {
            int n = 0;
            foreach (var c in _covers) if (c != null && c.IsIntact) n++;
            return n;
        }
    }

    static readonly List<VillageCover> _covers = new List<VillageCover>();

    internal static void Register(VillageCover cover)
    {
        if (cover != null && !_covers.Contains(cover)) _covers.Add(cover);
        if (Instance == null && _covers.Count > 0) CreateInstance();
    }

    internal static void Unregister(VillageCover cover) => _covers.Remove(cover);

    static void CreateInstance()
    {
        var go = new GameObject("[VillageCoverController]");
        Instance = go.AddComponent<VillageCoverController>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable()  => SeraPatrol.OnRoundCompleted += HandleRoundCompleted;
    void OnDisable() => SeraPatrol.OnRoundCompleted -= HandleRoundCompleted;

    void HandleRoundCompleted(int finishedRound) => AdvanceStage();

    /// <summary>소실을 한 단계 진행합니다. 3단계에 닿으면 아무 일도 하지 않습니다.</summary>
    public void AdvanceStage()
    {
        if (Stage >= MaxStage) return;

        // 단계 번호가 곧 종류 순서다 — 1차 수레 · 2차 화분 · 3차 간판 (F-6).
        var kind = (VillageCoverKind)Stage;
        Stage++;

        int vanished = 0, blockedByFloor = 0, keptForExit = 0;

        foreach (var cover in _covers)
        {
            if (cover == null || !cover.IsIntact || cover.kind != kind) continue;

            // 마을 출구 경로는 건드리지 않는다 — 나갈 길이 막히면 진행이 끊긴다.
            if (cover.keepForExitRoute) { keptForExit++; continue; }

            // 잔존 하한. 이 하나를 없애면 4개 밑으로 떨어지는 경우 남긴다.
            if (RemainingCount <= MinRemaining) { blockedByFloor++; continue; }

            cover.Vanish();
            vanished++;
        }

        Dbg.Log($"[엄폐물] {Stage}단계 — {kind} {vanished}개 소실, 잔존 {RemainingCount}개" +
                (keptForExit    > 0 ? $" (출구 경로 {keptForExit}개 제외)" : "") +
                (blockedByFloor > 0 ? $" (하한 {MinRemaining}개로 {blockedByFloor}개 유지)" : ""));

        if (Stage >= MaxStage && RemainingCount < MinRemaining)
            Debug.LogWarning($"[엄폐물] 3단계 잔존이 {RemainingCount}개입니다. F-6 은 4개를 요구합니다 — " +
                             "배치 수를 늘리거나 출구 경로 표시를 더 하세요.");
    }

    /// <summary>
    /// 엄폐물을 전량 복원하고 단계를 0으로 되돌립니다 (C-14-3-6).
    /// 라운드 카운터를 초기화하지 않으면 복귀 직후 엄폐물이 이미 사라져 있어
    /// 재시도가 첫 시도보다 어려워진다 — 되감기가 처벌이 되는 것이며 이중 처벌 금지와 같은 근거다.
    /// </summary>
    public void ResetAll()
    {
        Stage = 0;
        foreach (var cover in _covers) cover?.Restore();
        Dbg.Log($"[엄폐물] 전량 복원 — 잔존 {RemainingCount}개, 소실 단계 0");
    }
}
