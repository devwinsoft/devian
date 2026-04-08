# 10-reward-manager


RewardManager는 입력된 `reward_group_id` 또는 **RewardData[]**를 해석하여 InventoryManager의 **타입별 구체 API**를 호출하여 로컬 인벤토리에 **적용(지급 실행)** 한다.

RewardManager는 **단일 concrete 클래스**이다.
- `reward_group_id -> RewardData[]` 변환은 `TB_REWARD.GetByGroup()` 을 직접 참조하여 구현한다.
- `RewardData[]` 선검증(type/id/amount) + 원자성(all-or-nothing)을 보장한다.
- 멱등/기록/복구는 RewardManager의 책임이 아니다.


---


## Singleton

```csharp
CompoSingleton<RewardManager>.Instance
```

- Registry key: `RewardManager`
- 다른 매니저에서 접근: `Singleton.Get<RewardManager>()`


---


## Responsibilities (정본)

- `RewardData[]` 입력을 받아 `REWARD_TYPE`별로 해석하고, InventoryManager의 타입별 구체 API를 호출하여 로컬 인벤토리에 적용
  - `type=REWARD_TYPE.CURRENCY`: `inv.ApplyCurrency(currency_type, amount)`
  - `type=REWARD_TYPE.EQUIP`: `inv.ApplyEquip(item_id, amount)`
  - `type=REWARD_TYPE.CARD`: `inv.ApplyCard(item_id, amount)`
  - `type=REWARD_TYPE.HERO`: `inv.ApplyHero(item_id, amount)`
  - `type=REWARD_TYPE.RENTAL`: `inv.ApplyRental(item_id)`
  - `type=REWARD_TYPE.PASS`: `inv.SetPassOwnership(item_id, true)`
  - `type=REWARD_TYPE.TREASURE`: `inv.ApplyTreasure(gradeType, amount)`
- `reward_group_id`를 입력받아 `ResolveRewardDatas(reward_group_id)`로 `TB_REWARD.GetByGroup()` 에서 `RewardData[]`를 만든 뒤 적용
- `RevokeRewardDatas` / `RevokeRewardDatasPartial`로 RewardData[] 기반 회수 처리
- `GetAmount(type, id)`로 RewardData 타입 기반 수량 조회
- `FirstInitAsync()`로 초기 보상 지급 처리 (FirstRewardSettings 로드 + ApplyRewardDatas)
- public reward API의 실패는 `GameResult`로 반환한다.
- `_validateRewardData` 같은 선검증 단계는 all-or-nothing 보장의 일부이며 삭제하지 않는다.

비책임(금지):
- `grantId` 멱등 처리
- 지급 기록(ledger) 저장/조회
- Firebase Functions/Firestore 호출
- pending queue/복구


---


## Dependencies (개념)

- InventoryManager — RewardManager는 Inventory의 타입별 구체 API를 호출하여 적용한다.
- FirstRewardSettings — 초기 보상 지급 데이터 소스 ScriptableObject.
- SaveDataManager ↔ InventoryManager 직접 결합은 금지(상위 조립에서만 결합).

## Crypto (AES)

AES 암복호화 key/iv는 **MobileApplication**이 소유한다.
RewardManager는 `MobileApplication.Instance`에서 key/iv를 읽어 사용한다.

- key/iv 소유: `MobileApplication` (`[SerializeField] CString`, Inspector에 노출)
- key/iv 생성: `MobileApplicationEditor` — "Generate key iv" 버튼
- Static helper (RewardManager에 유지):
  - `EncryptInitialRewardsJson(string plainJson, string keyBase64, string ivBase64) -> string` (AES-CBC + base64)
  - `DecryptInitialRewardsJson(string encryptedBase64, string keyBase64, string ivBase64) -> string`
- `_parseFirstRewardSettings()`에서 `MobileApplication.Instance.CryptoKey/CryptoIv`를 참조한다.


---


## Public API

- `ApplyRewardDatas(RewardData[] rewards) -> GameResult`
  - 입력 전체를 선검증한다 (type/id/amount).
  - item reward(`CARD`/`MATERIAL`/`EQUIP`/`HERO`)는 선검증 단계에서 대응 table row 존재 여부까지 확인한다.
  - 하나라도 invalid면 `GameResult.Failure(error)`를 반환하고 상태를 변경하지 않는다.
  - 전체 valid이면 `REWARD_TYPE`별 switch로 InventoryManager 구체 API를 호출하고, 하위 `GameResult` 실패를 그대로 propagate한다.
  - 이 선검증 단계는 원자성 보장을 위해 유지한다. apply 루프의 예외 전파 모델로 대체하지 않는다.
- `ApplyRewardGroup(reward_group_id, rewardAmountMultiplier) -> GameResult<RewardApplyResult>`
  - `RewardApplyResult.AppliedRewards`로 이번 호출에서 실제 적용한 `RewardData[]`를 조회할 수 있다.
  - `reward_group_id`가 비어 있으면 성공 + 빈 배열(`AppliedRewards=[]`) 반환
- `RevokeRewardDatas(RewardData[] rewards) -> GameResult`
  - 선검증: 잔고 확인 (부족하면 `INVENTORY_REFUND_INSUFFICIENT`).
  - 전체 valid이면 Revoke 적용.
- `RevokeRewardDatasPartial(RewardData[] rewards) -> GameResult`
  - 보유량과 요청량 중 작은 값만큼 차감한다.
- `GetAmount(string type, string id) -> long`
  - `(type,id)`에 대한 현재 수량을 반환한다.
- `FirstInitAsync(CancellationToken ct) -> Task<GameResult>`
  - `FirstRewardSettings` 로드 → AES 복호화 → JSON 파싱 → `ApplyRewardDatas`로 적용.
  - 보상 적용 후 `InventoryManager.Initialize()` 호출 → InventorySettings 로드 + `LastStaminaUpdateUtcMs` 초기화.
  - `InventoryManager.ApplyCurrency(STAMINA, MaxStamina)`로 초기 스태미나 지급.
  - 리소스/암호화/JSON 실패는 운영 데이터 경계 실패로 보고 `GameResult`를 유지한다.


---


## Error Model (정본)

- RewardManager의 public API는 `GameResult`를 유지한다.
- invalid reward, empty id, amount 음수, item table miss, first-init parse 실패는 모두 boundary failure다.
- recoverable failure를 `throw` 기본 모델로 바꾸지 않는다.
- private helper에서만 내부 invariant 예외를 제한적으로 허용할 수 있다.
- helper 예외가 public 경계를 넘어가야 한다면 boundary에서 다시 `GameResult`로 감싸는 쪽을 우선한다.

---


## Validation Rules (정본)

- `rewards == null`이면 invalid다.
- 각 reward에 대해 아래 조건을 모두 만족해야 valid다.
  - `type`은 `REWARD_TYPE.CURRENCY`, `REWARD_TYPE.EQUIP`, `REWARD_TYPE.CARD`, `REWARD_TYPE.HERO`, `REWARD_TYPE.RENTAL`, `REWARD_TYPE.PASS`, `REWARD_TYPE.TREASURE` 중 하나여야 한다.
  - `id`는 null/empty/whitespace가 아니어야 한다.
  - `amount >= 0` 이어야 한다.
- `rewards.Length == 0`은 valid no-op으로 처리한다(`GameResult.Ok()` 반환).
- `amount == 0`은 valid no-op delta로 처리한다(에러 아님).
- `type=REWARD_TYPE.CURRENCY`일 때 `id`는 유효한 `CURRENCY_TYPE` enum name이어야 한다.
- `type=REWARD_TYPE.TREASURE`일 때 `id`는 유효한 `ITEM_GRADE_TYPE` enum name이어야 하며, `NONE`이면 invalid다.


---


## Apply Atomicity (정본)

- `ApplyRewardDatas`는 원자적으로 동작한다.
- 입력 중 invalid가 하나라도 있으면 전체 실패한다.
- factory/table lookup 실패 가능성도 apply 이전 선검증에서 걸러져야 한다.
- 전체 실패 시 InventoryManager 상태는 호출 전과 동일해야 한다.
- 따라서 `_validateRewardData`는 optional 최적화가 아니라 계약의 일부다.


---


## Error Mapping (정본)

- `ApplyRewardDatas` 실패는 `GameError(GAME_ERROR_TYPE, message, details)`를 사용한다.
- 권장 `GAME_ERROR_TYPE`:
  - `INVENTORY_DELTAS_NULL`
  - `INVENTORY_DELTA_TYPE_INVALID`
  - `INVENTORY_DELTA_ID_EMPTY`
  - `INVENTORY_DELTA_AMOUNT_NEGATIVE`
  - `ABILITY_ITEM_TABLE_NOT_FOUND`
  - `INVENTORY_REFUND_INSUFFICIENT` (RevokeRewardDatas)
- 새 코드는 `GAME_ERROR_TYPE` append-only 규칙과 prefix taxonomy를 따른다.
- private helper 예외를 대체하기 위해 `GAME_ERROR_TYPE`을 추가하지 않는다.


---


## 컨텐츠 테이블 통합 (TB_REWARD 직접 참조)

- `ResolveRewardDatas(reward_group_id) -> RewardData[]`
  - `TB_REWARD.GetByGroup(reward_group_id)` 로 보상 그룹의 행 리스트를 조회하여 `RewardData[]`를 생성한다.
  - 각 행의 `{ Type, Id, Amount }` → `RewardData` 변환. empty Id / amount <= 0 행은 skip.
  - 원격 호출/네트워크 금지. 테이블 조회만 허용.


---


## Implementation Location (3-path mirror)

> 3-path mirror 정책: [devian-unity/04-package-policy](../../../04-package-policy/SKILL.md), [devian-unity/01-policy](../../../01-policy/SKILL.md) §SSOT 원칙

- RewardManager:
  - UPM (정본): `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/Reward/RewardManager.cs`
  - Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/Reward/RewardManager.cs`
  - Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/Reward/RewardManager.cs`
- RewardData:
  - UPM (정본): `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/Reward/RewardData.cs`
  - Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/Reward/RewardData.cs`
  - Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/Reward/RewardData.cs`

asmdef:
- `Devian.Samples.MobilePackage.asmdef`
- 참조: `Devian.Domain.Game` (TB_REWARD 테이블), `Devian.Domain.Game` (GameResult)


---


## Sequence Example

1) 호출자가 `reward_group_id`를 결정한다.
2) 호출자 → `Singleton.Get<RewardManager>().ApplyRewardGroup(reward_group_id)`
3) RewardManager: `ResolveRewardDatas(reward_group_id)` → `RewardData[]` → `ApplyRewardDatas(deltas)`
4) RewardManager.ApplyRewardDatas: 선검증 → type switch → `InventoryManager.ApplyCurrency/ApplyEquip/...` 호출


---


## Related

- [49-reward-system/03-ssot](../03-ssot/SKILL.md) — RewardData 스키마 정본
- [11-rewarddata-interpretation](../11-rewarddata-interpretation/SKILL.md) — RewardData 해석 가이드
- [12-first-reward-settings](../12-first-reward-settings/SKILL.md) — FirstRewardSettings ScriptableObject
- [22-inventory-system/10-inventory-manager](../../22-inventory-system/10-inventory-manager/SKILL.md) — InventoryManager (타입별 구체 API 제공)
