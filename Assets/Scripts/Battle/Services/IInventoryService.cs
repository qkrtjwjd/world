using System.Collections.Generic;

/// <summary>
/// 배틀 시스템이 InventoryManager에 접근하는 추상 인터페이스.
/// </summary>
public interface IInventoryService
{
    /// <summary>현재 인벤토리. 직접 수정 금지 (읽기 전용 의도).</summary>
    IReadOnlyList<ItemData> Items { get; }

    void RemoveItem(ItemData item);
}
