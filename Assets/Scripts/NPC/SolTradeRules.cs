using System.Collections.Generic;
using UnityEngine;

/// <summary>거래 모드. 마을에서는 이름·설명이 감춰지고 어떤 거래도 성립하지 않는다.</summary>
public enum TradeMode { VillageBrowse, ForestTrade }

/// <summary>
/// 거절 사유. 사유 텍스트는 전부 Yarn 노드에 있고 C# 에는 노드 이름만 있다.
///
/// <para><b>Silence</b> 는 "솔이 아무 말도 하지 않는다" 다 — 대사도 효과음도 연출도 없다.
/// 정본 C-15-4 의 마시멜로가 여기 해당한다. 다른 세 종이 모두 반응을 주기 때문에
/// 이 한 종만 아무것도 없는 것이 연출의 전부다(D-2 15-E-3 · F-7-2).</para>
///
/// <para><b>PlayerWithdraws</b> 는 솔이 거절하는 것이 아니라 <b>루가 스스로 거두는 것</b>이다.
/// 각설탕이 여기 해당하며, 아이콘을 짧게 띄웠다 되돌리고 독백을 부른다(F-7-2 A안).</para>
/// </summary>
public enum RejectReason { GradeMismatch, Contaminated, Empty, PlayerWithdraws, Silence }

/// <summary>거래 판정 결과. 실패해도 에러가 아니라 재생할 Yarn 노드 이름을 돌려준다.</summary>
public struct TradeOutcome
{
    public bool         accepted;
    public RejectReason reason;   // accepted == false 일 때만 의미 있음
    public string       yarnNode; // 성립/거절 어느 쪽이든 재생할 노드
}

/// <summary>
/// 솔 거래의 판정 규칙. UI 와 분리되어 있어 단독으로 검증할 수 있다.
///
/// [금지]
/// - 가격·잔액·통화·되판매 감가 개념을 넣지 않는다.
/// - 품목 이름과 거절 사유 텍스트를 이 파일에 문자열로 넣지 않는다.
/// </summary>
public static class SolTradeRules
{
    /// <summary>인접 등급끼리만 교환 가능.</summary>
    public static bool CanTrade(TradeItem offer, TradeItem want)
    {
        if (offer == null || want == null) return false;
        return Mathf.Abs((int)offer.grade - (int)want.grade) <= 1;
    }

    /// <summary>
    /// 교환비 1:5 (하등급 5개 = 상등급 1개, 양방향 등가). 동급이면 1:1.
    /// </summary>
    public static void GetExchangeCounts(TradeItem offer, TradeItem want,
                                         out int offerCount, out int wantCount)
    {
        offerCount = 1;
        wantCount  = 1;
        if (offer == null || want == null) return;

        if (offer.grade < want.grade)      offerCount = 5;
        else if (want.grade < offer.grade) wantCount  = 5;
    }

    /// <summary>
    /// 루가 offer 를 내밀어 want 를 받으려 할 때의 판정.
    /// </summary>
    public static TradeOutcome Resolve(TradeMode mode, TradeItem offer, TradeItem want)
    {
        if (offer == null || want == null)
            return Reject(mode, offer, RejectReason.Empty);

        // 1. 품목에 지정된 오버라이드가 최우선 (각설탕·빵 반죽·시든 아네모네 등)
        if (offer.hasRejectOverride)
            return Reject(mode, offer, mode == TradeMode.VillageBrowse
                                       ? offer.villageReject
                                       : offer.forestReject);

        // 2. 마을에서는 어떤 거래도 성립하지 않는다.
        //    등급 문제는 아니지만 열거형에 마을 전용 사유가 없으므로 GradeMismatch 로 두고
        //    노드만 마을 전용(Sol_Trade_Reject_Village)으로 분리한다.
        if (mode == TradeMode.VillageBrowse)
        {
            var villageOutcome = Reject(mode, offer, RejectReason.GradeMismatch);
            if (string.IsNullOrEmpty(offer.rejectNodeVillage))
                villageOutcome.yarnNode = YarnNodes.Sol_Trade_Reject_Village;
            return villageOutcome;
        }

        // 3. 숲 — 등급 규칙으로 판정
        if (!CanTrade(offer, want))
            return Reject(mode, offer, RejectReason.GradeMismatch);

        return new TradeOutcome
        {
            accepted = true,
            yarnNode = YarnNodes.Sol_Trade_Success,
        };
    }

    static TradeOutcome Reject(TradeMode mode, TradeItem offer, RejectReason reason)
    {
        // 침묵은 노드 배선과 무관하게 무조건 비어 있어야 한다.
        // 에셋에 실수로 노드가 꽂혀 있어도 대사가 새어 나오면 안 된다(C-15-4 · D-2 15-E-3).
        if (reason == RejectReason.Silence)
            return new TradeOutcome { accepted = false, reason = reason, yarnNode = string.Empty };

        string overrideNode = offer == null
            ? null
            : (mode == TradeMode.VillageBrowse ? offer.rejectNodeVillage : offer.rejectNodeForest);

        return new TradeOutcome
        {
            accepted = false,
            reason   = reason,
            yarnNode = string.IsNullOrEmpty(overrideNode) ? DefaultRejectNode(reason) : overrideNode,
        };
    }

    static string DefaultRejectNode(RejectReason reason) => reason switch
    {
        RejectReason.GradeMismatch   => YarnNodes.Sol_Trade_Reject_GradeMismatch,
        RejectReason.Contaminated    => YarnNodes.Sol_Trade_Reject_Contaminated,
        RejectReason.Empty           => YarnNodes.Sol_Trade_Reject_Empty,
        RejectReason.PlayerWithdraws => YarnNodes.Sol_Trade_Reject_PlayerWithdraws,
        // 침묵은 부를 노드가 없다. 빈 문자열이면 PlayIfExists 가 건너뛴다.
        RejectReason.Silence         => string.Empty,
        _                            => YarnNodes.Sol_Trade_Reject_GradeMismatch,
    };

    // ─────────────────────────────────────────────
    //  ItemData ▸ TradeItem 역매핑
    //  루의 소지품은 InventoryManager 의 List<ItemData> 이므로
    //  Resources/TradeItems/ 의 에셋을 TradeItem.source 기준으로 색인해 되짚는다.
    //  (YarnCommandBridge 가 Resources/MerchantData/ 를 읽던 것과 같은 방식)
    // ─────────────────────────────────────────────

    const string ResourceFolder = "TradeItems";
    static Dictionary<ItemData, TradeItem> _bySource;

    /// <summary>씬 재진입·도메인 리로드 시 캐시를 버린다.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _bySource = null;

    public static TradeItem FromItemData(ItemData item)
    {
        if (item == null) return null;
        EnsureCatalog();
        return _bySource.TryGetValue(item, out var trade) ? trade : null;
    }

    static void EnsureCatalog()
    {
        if (_bySource != null) return;

        _bySource = new Dictionary<ItemData, TradeItem>();
        var all = Resources.LoadAll<TradeItem>(ResourceFolder);
        if (all == null || all.Length == 0)
        {
            Debug.LogWarning($"[SolTradeRules] Resources/{ResourceFolder}/ 에서 TradeItem 을 찾지 못했습니다. " +
                              "루가 내밀 수 있는 소지품이 하나도 표시되지 않습니다.");
            return;
        }

        foreach (var trade in all)
        {
            if (trade == null || trade.source == null) continue;
            if (_bySource.ContainsKey(trade.source))
            {
                Debug.LogWarning($"[SolTradeRules] ItemData '{trade.source.name}' 에 TradeItem 이 둘 이상 연결돼 있습니다. " +
                                 $"'{_bySource[trade.source].name}' 를 유지하고 '{trade.name}' 는 무시합니다.");
                continue;
            }
            _bySource[trade.source] = trade;
        }
    }
}
