using System;
using UnityEngine;

/// <summary>
/// 장착 슬롯 — 의상 1 + 무기 2 (E-38).
///
/// ⚠ <b>파지 판정의 진실의 출처는 이 클래스가 아니라 <see cref="DaggerSystem"/> 이다</b>(E-38).
/// 무기 슬롯에 단검이 들어가 있는지와 무관하게, "루가 단검을 쥐고 있는가"는
/// <see cref="DaggerSystem.IsEquipped"/> 하나로만 판정한다. 슬롯이 별도 경로를 만들지 않는다.
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
    /// 이 아이템이 해당 슬롯에 들어갈 수 있는지.
    /// 무기 슬롯은 <see cref="ItemCategory.Weapon"/> 만 받는다.
    ///
    /// ⚠ 의상 슬롯에는 판정 근거가 없다 — <see cref="ItemCategory"/> 에 의상 값이 없고,
    /// 현재 아이템 에셋은 category 가 전부 기본값(All)이다. 임의로 enum 값을 만들지 않는다
    /// (직렬화된 인스펙터 값이 밀린다). 의상 분류가 정해지기 전까지 이 슬롯은
    /// <see cref="ForceEquip"/> 로 명시 지정할 때만 채워지고, 평소에는 빈 상태가 정상이다.
    /// </summary>
    public bool CanEquip(ItemData item, Slot slot)
    {
        if (item == null) return false;
        if (slot == Slot.WeaponMain || slot == Slot.WeaponSub)
            return item.category == ItemCategory.Weapon;
        return false;
    }

    /// <returns>장착 성공 여부.</returns>
    public bool TryEquip(ItemData item, Slot slot)
    {
        if (!CanEquip(item, slot)) return false;
        ForceEquip(item, slot);
        return true;
    }

    /// <summary>분류 판정을 건너뛰고 슬롯에 직접 넣는다. 호출자가 책임진다.</summary>
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
