# 13-inventory-settings

Status: ACTIVE
AppliesTo: v10

`InventorySetting`은 초기 인벤토리 지급 데이터를 보관하는 `ScriptableObject` 정본이다.
`InventoryManager.FirstInitAsync()`는 이 설정 에셋을 읽어서 `RewardData[]`를 적용한다.

---

## Class

- 클래스: `InventorySetting : ScriptableObject`
- 네임스페이스: `Devian`
- 필드:
  - `InitialInventory: CString`
- 기본값:
  - `[{\"type\":\"CURRENCY\",\"id\":\"GOLD\",\"amount\":1000}]`

---

## Asset Path (SSOT)

- Resources 경로: `Devian/InventorySettings`
- 프로젝트 에셋 경로: `Assets/Resources/Devian/InventorySettings.asset`

`FirstInitAsync`는 `Resources.Load<InventorySetting>(InventorySetting.ResourcesPath)`로 로드한다.

---

## JSON Contract

`InitialInventory`는 아래 형식을 허용한다.

- `RewardData[]`
- `{ "rewards": RewardData[] }`

검증 규칙은 `InventoryManager.parseInitialInventoryRewards` 정본을 따른다.

---

## Related

- [10-inventory-manager](../10-inventory-manager/SKILL.md) — FirstInit 흐름
- [03-ssot](../03-ssot/SKILL.md) — Inventory 적용 규칙 정본
- [49-reward-system/11-rewarddata-interpretation](../../49-reward-system/11-rewarddata-interpretation/SKILL.md) — RewardData 해석
