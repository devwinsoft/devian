# 11-rewarddata-interpretation


Status: ACTIVE
AppliesTo: v10


## Purpose

`RewardData`를 어디서 받아도 동일하게 해석하기 위한 실무 가이드다.

- 대상: `TB_REWARD` 그룹 해석, `FirstRewardSettings.InitialRewards` 설정값
- 목표: `type/id/amount`를 인벤토리 적용 의미로 일관되게 해석


---


## Canonical Shape

런타임 타입(`RewardData.cs`) 기준:

- `Type: REWARD_TYPE`
- `Id: string`
- `Amount: int`

정본 스키마/용어는 [03-ssot](../03-ssot/SKILL.md)를 따른다.


---


## Interpretation Order (정본)

1. 입력을 `{ type, id, amount }` 형태로 정규화한다.
2. 공통 검증을 수행한다.
3. `type`별 의미로 해석한다.
4. 적용 호출(`RewardManager.ApplyRewardDatas`)로 전달한다.


---


## Common Validation

- `type`은 허용 enum 값이어야 한다:
  - `CARD`, `CURRENCY`, `EQUIP`, `HERO`, `MATERIAL`, `RENTAL`, `PASS`, `TREASURE`
- `id`는 공백이 아니어야 한다.
- `amount`는 정수여야 한다.
- 적용 경로 기준으로 `amount <= 0`은 지급 대상에서 제외(no-op 또는 skip)한다.
- 이 검증 실패는 public reward 경계에서 `CommonResult` 실패로 표현한다. 해석 경로를 `throw` 중심으로 바꾸지 않는다.


---


## Type Semantics

| `type` | `id` 의미 | `amount` 의미 | 적용 결과 |
|---|---|---|---|
| `CURRENCY` | `CURRENCY_TYPE` enum name | 증가 수량 | 잔고 누적 |
| `EQUIP` | `itemId` | 생성 개수 | 개수만큼 `itemUid` 인스턴스 생성 |
| `CARD` | `itemId` | 증가 수량 | 카드 보유량 누적 |
| `MATERIAL` | `itemId` | 증가 수량 | 재료 보유량(`ITEM_AMOUNT`) 누적 |
| `HERO` | `itemId` | 증가 수량 | 영웅 수량(`ITEM_AMOUNT`) 누적 |
| `RENTAL` | `itemId` | 활성화 플래그(양수) | 활성 상태 설정 (`SetRental`) |
| `PASS` | `itemId` | 소유 플래그(양수) | 소유 상태 설정 (`SetPassOwnership`) |
| `TREASURE` | `TREASURE_GRADE_TYPE` enum name | chest 수량 | treasure chest count 누적 |

주의:
- `RENTAL`/`PASS`는 양수 여부만 의미가 있다(값의 크기 자체는 의미 없음).
- `EQUIP`은 `amount`를 무시하지 않는다. `amount`만큼 인스턴스를 생성한다.


---


## Source-specific Parse Rules

### A) RewardGroup (`TB_REWARD.GetByGroup`)

구현: `RewardManager.ResolveRewardDeltas`

- 행의 `Id`가 비어 있거나 `Amount <= 0`이면 skip
- 나머지는 `new RewardData(row.Type, row.Id, row.Amount)`로 변환


### B) FirstRewardSettings Initial Rewards

구현: `RewardManager.FirstInitAsync` -> `parseFirstRewardSettings`

- 입력 소스: `Assets/Resources/Devian/FirstRewardSettings.asset` -> `FirstRewardSettings.InitialRewards` (`CString`)
- 허용 JSON 형태:
  - `RewardData[]`
  - `{ "rewards": RewardData[] }`
- `SEASON_PASS`는 legacy 호환으로 `PASS`로 변환
- `amount`는 정수이며 `1..int.MaxValue` 범위만 허용
- 검증 실패 시 `CommonResult` 실패로 중단한다
- first-init payload는 운영 데이터 경계다. parse 실패는 호출자에게 결과 코드로 보고한다.


---


## Apply Contract (Reward)

`RewardManager.ApplyRewardDatas` 기준:

- `rewards == null`은 실패
- 각 row에서
  - invalid `type` 실패
  - empty `id` 실패
  - `amount < 0` 실패
  - `amount == 0` no-op
- 검증 통과 후 type별 InventoryManager 구체 API 호출
- RewardManager의 two-phase 검증은 원자성 보장의 일부이므로 유지한다.


---


## Quick Examples

```json
[
  { "type": "CURRENCY", "id": "GOLD", "amount": 1000 },
  { "type": "EQUIP", "id": "equip_sword_001", "amount": 2 },
  { "type": "MATERIAL", "id": "material_stone_001", "amount": 10 },
  { "type": "RENTAL", "id": "NO_ADS", "amount": 1 }
]
```

해석 결과:
- GOLD +1000
- `equip_sword_001` 인스턴스 2개 생성
- `material_stone_001` 수량 +10
- NO_ADS rental 활성화


---


## Code References

- RewardData: `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/0.1.0/MobilePackage/Runtime/Reward/RewardData.cs`
- RewardGroup 해석: `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/0.1.0/MobilePackage/Runtime/Reward/RewardManager.cs`
- 실제 적용(type switch): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/0.1.0/MobilePackage/Runtime/Reward/RewardManager.cs`
- InventoryManager(타입별 구체 API): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/0.1.0/MobilePackage/Runtime/Inventory/InventoryManager.cs`


---


## Related

- [03-ssot](../03-ssot/SKILL.md) — RewardData 스키마/용어 정본
- [10-reward-manager](../10-reward-manager/SKILL.md) — rewardGroupId -> RewardData[] 변환/적용
- [12-first-reward-settings](../12-first-reward-settings/SKILL.md) — FirstRewardSettings ScriptableObject
- [22-inventory-system/10-inventory-manager](../../22-inventory-system/10-inventory-manager/SKILL.md) — InventoryManager (타입별 구체 API)
