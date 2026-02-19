# 11-inventory-storage

Status: ACTIVE
AppliesTo: v10

InventoryStorage는 InventoryManager의 **인벤토리 데이터 컨테이너**이다.
수량 상태에 능력치(ItemAbility) / 장비 슬롯을 **확장(enrichment)**한다.

InventoryStorage는 InventoryManager가 소유하며 `Devian.Samples.MobileSystem` asmdef에 속한다.

---

## 1. ItemData

```csharp
using Devian.Domain.Game;

namespace Devian
{
    public sealed class ItemData
    {
        public string ItemId => mItemAbility.ItemId;
        public ItemAbility Ability => mItemAbility;
        public long Count => mItemAbility[StatType.ItemCount];
        public int EquippedSlotNumber => mItemAbility[StatType.ItemSlotNumber];
        public bool IsEquipped => mItemAbility[StatType.ItemSlotNumber] > 0;

        ItemAbility mItemAbility;

        public ItemData(ItemAbility ability)
        {
            mItemAbility = ability;
        }

        internal void AddCount(long delta)
        {
            mItemAbility.AddStat(StatType.ItemCount, (int)delta);
        }

        internal void SetEquippedSlot(int slotIndex)
        {
            int currentSlot = mItemAbility[StatType.ItemSlotNumber];
            if (currentSlot != 0)
                mItemAbility.AddStat(StatType.ItemSlotNumber, -currentSlot);
            mItemAbility.AddStat(StatType.ItemSlotNumber, slotIndex);
        }

        internal void ClearEquippedSlot()
        {
            int currentSlot = mItemAbility[StatType.ItemSlotNumber];
            if (currentSlot != 0)
                mItemAbility.AddStat(StatType.ItemSlotNumber, -currentSlot);
        }
    }
}
```

- `ItemAbility` 보유 — 스탯 정규화 (40-game-system/12-game-ability)
- **수량은 `mItemAbility[StatType.ItemCount]`로 관리** — `Count` 프로퍼티로 접근
- 장착 슬롯은 `mItemAbility[StatType.ItemSlotNumber]`으로 관리 (0 = 미장착)
- 별도 슬롯/수량 필드 불필요 — StatType 기반 정규화
- `internal` setter — InventoryStorage/InventoryManager만 변경 가능
- `SetEquippedSlot`/`ClearEquippedSlot`은 ItemSlotNumber만 변경한다 (다른 stat 보존)

---

## 2. InventoryStorage

```csharp
using System.Collections.Generic;

namespace Devian
{
    public sealed class InventoryStorage
    {
        // ── Bag ──
        readonly Dictionary<string, ItemData> mBagItems = new();
        public IReadOnlyDictionary<string, ItemData> BagItems => mBagItems;

        // ── Equipment Slots ──
        readonly Dictionary<int, ItemData> mEquipmentSlots = new();
        public IReadOnlyDictionary<int, ItemData> EquipmentSlots => mEquipmentSlots;

        // ── Bag Operations ──

        public ItemData GetItem(string itemId)
        {
            return mBagItems.TryGetValue(itemId, out var data) ? data : null;
        }

        public ItemData AddItem(string itemId, ItemAbility ability)
        {
            if (mBagItems.ContainsKey(itemId)) return mBagItems[itemId];
            var data = new ItemData(ability);
            mBagItems[itemId] = data;
            return data;
        }

        public bool RemoveItem(string itemId)
        {
            if (!mBagItems.TryGetValue(itemId, out var data)) return false;
            if (data.IsEquipped) UnequipItem(itemId);
            mBagItems.Remove(itemId);
            return true;
        }

        // ── Equipment Operations ──

        public bool EquipItem(string itemId, int slotIndex)
        {
            if (!mBagItems.TryGetValue(itemId, out var data)) return false;
            if (data.IsEquipped) UnequipItem(itemId);
            if (mEquipmentSlots.ContainsKey(slotIndex))
                UnequipSlot(slotIndex);
            data.SetEquippedSlot(slotIndex);
            mEquipmentSlots[slotIndex] = data;
            return true;
        }

        public bool UnequipItem(string itemId)
        {
            if (!mBagItems.TryGetValue(itemId, out var data)) return false;
            if (!data.IsEquipped) return false;
            mEquipmentSlots.Remove(data.EquippedSlotNumber);
            data.ClearEquippedSlot();
            return true;
        }

        public bool UnequipSlot(int slotIndex)
        {
            if (!mEquipmentSlots.TryGetValue(slotIndex, out var data)) return false;
            data.ClearEquippedSlot();
            mEquipmentSlots.Remove(slotIndex);
            return true;
        }

        public void Clear()
        {
            mEquipmentSlots.Clear();
            foreach (var kv in mBagItems)
                kv.Value.ClearEquippedSlot();
            mBagItems.Clear();
        }
    }
}
```

### 양방향 링크

- `BagItems[itemId]`와 `EquipmentSlots[slotIndex]`가 **동일 ItemData 인스턴스**를 참조한다.
- 장착: `ItemAbility.AddStat(StatType.ItemSlotNumber, slotIndex)` + `EquipmentSlots[slotIndex] = data`
- 해제: `ItemAbility.ClearStats()` + `EquipmentSlots.Remove(slotIndex)`
- `EquipItem`은 대상 슬롯에 기존 아이템이 있으면 자동 해제한다.
- `RemoveItem`은 장착 상태면 자동 해제 후 제거한다.

---

## 3. InventoryManager 관계

| 타입 | 책임 |
|---|---|
| `InventoryManager` | `_currencyBalances`, `GetAmount`, InventoryStorage 소유, AddRewards 연동 |
| `InventoryStorage` | BagItems (itemId → ItemData), EquipmentSlots (slotIndex → ItemData) |
| `ItemData` | ItemAbility 보유, 수량(`StatType.ItemCount`) + 장비 슬롯(`StatType.ItemSlotNumber`) 관리 |

- `InventoryManager`가 `InventoryStorage`를 소유한다 (싱글톤 등록 안 함).
- 아이템 수량 SSOT = `ItemData.Count` (= `mItemAbility[StatType.ItemCount]`)
- `AddRewards` 시 `RewardType.Item`이면 `_storage.BagItems`에 ItemData를 추가하고 `AddCount(delta)`로 수량 누적한다.

```csharp
public sealed class InventoryManager : MonoBehaviour
{
    readonly InventoryStorage _storage = new();
    public InventoryStorage Storage => _storage;

    // ... CompoSingleton<InventoryManager> 패턴 ...
}
```

---

## 4. Implementation Location (SSOT)

### 파일 위치 (MobileSystem 샘플)

```
MobileSystem/Runtime/InventoryManager/
├── InventoryManager.cs   (10-inventory-manager)
├── InventoryStorage.cs
└── ItemData.cs
```

3경로 미러:
- UPM: `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/InventoryManager/`
- UnityExample: `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/InventoryManager/`
- Assets/Samples: `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/0.1.0/MobileSystem/Runtime/InventoryManager/`

---

## 5. asmdef

`Devian.Samples.MobileSystem.asmdef`에 이미 포함된 참조로 충분하다:
- `Devian.Domain.Game` (StatType — ItemAbility → BaseAbility 경유)

추가 참조 불필요.

---

## 6. namespace

```csharp
namespace Devian
```

(Samples 정책: 단일 namespace `Devian`)

---

## 7. Hard Rules

- InventoryStorage와 ItemData는 **sealed POCO 클래스**이다 (MonoBehaviour 금지).
- 아이템 수량 SSOT = `ItemData.Count` (= `mItemAbility[StatType.ItemCount]`). 별도 `_items` dict 불필요.
- `BagItems` key = `itemId` (string).
- `EquipmentSlots` key = `slotIndex` (int). value = BagItems와 동일 ItemData 인스턴스.
- Equip/Unequip은 양방향 일관성을 유지해야 한다.
- InventoryStorage는 InventoryManager가 소유한다 (싱글톤 등록 안 함).
- "Sample" 접두사 금지 (정책).

---

## 8. Related

- [12-game-ability](../../../40-game-system/12-game-ability/SKILL.md) — BaseAbility, ItemAbility (ItemData 의존)
- [13-game-stat-type](../../../40-game-system/13-game-stat-type/SKILL.md) — StatType enum (ItemSlotNumber 사용)
- [10-inventory-manager](../10-inventory-manager/SKILL.md) — InventoryManager (InventoryStorage 소유자, 수량 SSOT)
- [03-ssot](../03-ssot/SKILL.md) — Inventory State/Apply Rules
- [00-overview](../00-overview/SKILL.md) — Inventory System 개요
