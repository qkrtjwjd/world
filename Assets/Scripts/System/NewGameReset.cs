using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 새 게임 시작 값으로 진행 상태를 되돌린다. 두 곳에서 부른다(2026-10-01).
/// <list type="bullet">
/// <item>타이틀 「새 게임」 — 예전에는 아무것도 되돌리지 않아, 게임 안에서 타이틀로 갔다 새로 시작하면 이전 진행이 남았다.</item>
/// <item>토끼 저장 없이 숲 전투에서 쓰러졌을 때 — 새 게임 시작 지점으로 돌아간다(F-9-3 · E-66-2).</item>
/// </list>
///
/// <para><b>남기는 것</b> — 주인공 이름(이미 정했다) · 저장 슬롯 3개 · 중단 저장 · 계정 단위 기록
/// (사망 횟수 F-9-2 · 통산 힌트 등). 슬롯은 플레이어의 것이고 계정 기록은 진행과 무관하다.</para>
/// <para><b>지우는 저장 키</b> — 전투 직전 · 체크포인트 · 되감기. 남기면 새 진행에서 BE#01 · BE#02 가 이전 진행의 되감기 지점으로 돌아간다.</para>
/// </summary>
public static class NewGameReset
{
    public static void Apply()
    {
        GameState.ResetForNewGame();
        GameState.inventoryItems = new List<ItemData>();

        CorruptionManager.Instance?.ResetForNewGame();
        GaugeManager.Instance?.CutGauge(GaugeManager.DEFAULT_GAUGE);
        DaggerSystem.Instance?.Unequip();

        PlayerGrowth.Load(1, 0);
        JournalManager.Load(new List<JournalEntrySave>());

        if (FlagManager.Instance != null)          FlagManager.Instance.ResetToDefaults();
        else if (GameStateManager.Instance != null) GameStateManager.Instance.flags.Clear();

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.inventoryItems = GameState.inventoryItems;
            InventoryManager.Instance.UpdateSlotUI();
        }

        SaveManager.Instance?.ResetProgressForNewGame();
        Dbg.Log("[NewGameReset] 새 게임 값으로 되돌림");
    }
}
