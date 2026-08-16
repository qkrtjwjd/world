using System;
using UnityEngine;

/// <summary>
/// 장착 슬롯 — 의상 1 + 무기 2 (E-38).
///
/// ⚠ <b>파지 판정의 진실의 출처는 이 클래스가 아니라 <see cref="DaggerSystem"/> 이다</b>(E-38).
/// 무기 슬롯에 단검이 들어가 있는지와 무관하게, "루가 단검을 쥐고 있는가"는
/// <see cref="DaggerSystem.IsEquipped"/> 하나로만 판정한다. 슬롯이 별도 경로를 만들지 않는다.
///
/// ⚠ <b>슬롯에서 <see cref="DaggerSystem"/> 을 호출하지도 않는다.</b> 이 프로젝트에서 파지는
/// 사실상 '획득'과 같게 쓰이고 있어서 — <c>SaveManager</c> 가 불러오기 때임
/// <c>GameState.isDaggerAcquired</c> 로 파지 상태를 되돌린다 — 슬롯에서 해제해도
/// 저장/로드 한 번이면 되살아난다. 파지는 스토리(획득 컷씬)가 정한다.
///
/// 씬 배치가 필요 없는 순수 상태 보관소다. 아직 저장 대상이 아니므로
/// <see cref="SaveData"/> 스키마는 건드리지 않는다(v8 유지).
/// </summary>
public class EquipmentManager : MonoBehaviour
{
    public enum Slot { Clothing = 0, WeaponMain = 1, WeaponSub = 2 }

    public static EquipmentManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("EquipmentManager [Auto]");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<EquipmentManager>();
            }
            return _instance;
        }
    }
    static EquipmentManager _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _instance = null;

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // ── 상태 ──────────────────────────────────────────────────────────────
    readonly ItemData[] _slots = new ItemData[3];

    /// <summary>장착 내용이 바뀔 때. 전신도가 구독한다.</summary>
    public event Action OnEquipmentChanged;

    public ItemData Get(Slot slot) => _slots[(int)slot];

    public ItemData Clothing   => _slots[(int)Slot.Clothing];
    public ItemData WeaponMain => _slots[(int)Slot.WeaponMain];
    public ItemData WeaponSub  => _slots[(int)Slot.WeaponSub];

    // ── 조작 ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 이 아이템이 해당 슬롯에 들어갈 수 있는지. 판정은 <see cref="ItemData.equipSlot"/> 하나로 한다.
    /// <see cref="ItemCategory"/>(아이템창 필터)와는 별개 축이라 서로 영향을 주지 않는다.
    /// </summary>
    public bool CanEquip(ItemData item, Slot slot)
    {
        if (item == null) return false;
        if (slot == Slot.Clothing) return item.equipSlot == EquipSlotType.Clothing;
        return item.equipSlot == EquipSlotType.Weapon;   // WeaponMain / WeaponSub
    }

    /// <returns>장착 성공 여부.</returns>
    public bool TryEquip(ItemData item, Slot slot)
    {
        if (!CanEquip(item, slot)) return false;

        // 같은 아이템이 두 무기 슬롯에 동시에 들어가지 않게 한다
        for (int i = 0; i < _slots.Length; i++)
            if (i != (int)slot && _slots[i] == item) _slots[i] = null;

        ForceEquip(item, slot);
        OnEquipmentChanged?.Invoke();   // 위에서 비운 슬롯도 반영되게 한 번 더 알린다
        return true;
    }

    /// <summary>
    /// 분류 판정을 건너뛰고 슬롯에 직접 넣는다. 호출자가 책임진다.
    /// 컷씬처럼 스토리가 강제로 입히는 경우에 쓴다.
    /// </summary>
    public void ForceEquip(ItemData item, Slot slot)
    {
        if (_slots[(int)slot] == item) return;
        _slots[(int)slot] = item;
        OnEquipmentChanged?.Invoke();
    }

    public void Unequip(Slot slot)
    {
        if (_slots[(int)slot] == null) return;
        _slots[(int)slot] = null;
        OnEquipmentChanged?.Invoke();
    }

    public void UnequipAll()
    {
        bool changed = false;
        for (int i = 0; i < _slots.Length; i++)
            if (_slots[i] != null) { _slots[i] = null; changed = true; }
        if (changed) OnEquipmentChanged?.Invoke();
    }

    /// <summary>바깥에서 상태를 바꾼 뒤 전신도만 다시 그리게 할 때.</summary>
    public void NotifyChanged() => OnEquipmentChanged?.Invoke();
}
