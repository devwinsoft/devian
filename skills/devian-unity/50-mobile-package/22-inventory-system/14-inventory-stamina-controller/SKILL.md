# 14-inventory-stamina-controller

Status: ACTIVE
AppliesTo: v10

`CURRENCY_TYPE.STAMINA`에 대한 설정 로드 및 자동 회복 로직을 `InventoryStaminaController`로 캡슐화한다.
InventoryManager는 컨트롤러에 위임만 하며, 외부 API를 유지한다.

---

## Design

- `InventoryStaminaController` (`internal sealed class`) — 스태미나 전용 컨트롤러
- InventoryManager가 `_staminaController` 필드로 소유, 공개 API를 위임한다
- 컨트롤러는 `InventoryStorage`를 소유하지 않음
- 컨트롤러는 순수 계산기다. live mutation은 `InventoryManager`가 수행한다.

---

## InventoryStaminaController (internal)

### Fields (private)

- `_maxStamina: CInt` — 스태미나 최대치 (InventorySettings에서 로드)
- `_staminaIntervalSeconds: CInt` — 스태미나 1 회복 주기(초) (InventorySettings에서 로드)

### Properties (public)

- `MaxStamina: int` (get) — `_maxStamina` 반환

### Methods

- `LoadSettings()`:
  1. `Resources.Load<InventorySettings>(InventorySettings.ResourcesPath)` 로드
  2. `MobileApplication.Instance`에서 key/iv로 AES 복호화
  3. JSON 파싱 → `_maxStamina`, `_staminaIntervalSeconds` 설정
  - **설정 로드만 수행** — `LastStaminaUpdateUtcMs`를 조작하지 않는다
  - STAMINA 지급은 하지 않는다 (호출자 책임)

- `CalculateRecovery(long currentStamina, long lastUpdateUtcMs, long nowUtcMs) -> StaminaRecoveryResult`:
  1. stamina >= MaxStamina → `(0, 0)` 반환 (추적 불필요, 타임스탬프 무의미)
  2. `lastUpdateUtcMs <= 0` → `(0, now)` 반환 (추적 시작)
  3. 경과 시간 / `_staminaIntervalSeconds` = 회복량 계산
  4. 회복량과 다음 `LastStaminaUpdateUtcMs`만 계산해서 반환
  5. live currency 적용과 메시지 publish는 호출자(`InventoryManager`)가 수행

---

## InventoryManager 위임 API

```
readonly InventoryStaminaController _staminaController = new();

public int MaxStamina => _staminaController.MaxStamina;
public void LoadSettings() => _staminaController.LoadSettings();
public void RecoverStamina()
{
    var result = _staminaController.CalculateRecovery(currentStamina, lastUpdateUtcMs, nowUtcMs);
    _storage.LastStaminaUpdateUtcMs = result.NextLastUpdateUtcMs;
    if (result.RecoveredAmount > 0)
        ApplyCurrency(CURRENCY_TYPE.STAMINA, result.RecoveredAmount);
}
```

외부 호출 시그니처:
- `InventoryManager.Instance.LoadSettings()` — 설정 로드
- `InventoryManager.Instance.RecoverStamina()` — 오프라인 회복 계산
- `InventoryManager.Instance.MaxStamina` — 최대 스태미나 조회

---

## InventoryStorage 추가 멤버

- `LastStaminaUpdateUtcMs: long` (get/set) — 마지막 스태미나 갱신 시각 (UTC ms)

---

## LastStaminaUpdateUtcMs 생명주기

- 값 = 0: 추적 불필요 (stamina가 max이거나 초기 상태)
- 값 > 0: 마지막으로 stamina가 변한 시점 (ServerNowUtcMs 기준)
- **stamina >= MaxStamina이면 무의미** — 회복할 필요 없음
- **stamina 변화가 있을 때만 저장** — stamina가 max일 때 불필요한 갱신 안 함

### SaveDataJsonCodecInventory

- Serialize: `LastStaminaUpdateUtcMs > 0` 이면 `"lastStaminaUpdateUtcMs"` 키로 저장
- Deserialize: 키가 있으면 복원, 없으면 0L (= 추적 불필요)

---

## Stamina Recovery Logic

```
currentStamina = storage.GetCurrencyAmount(CURRENCY_TYPE.STAMINA)

if currentStamina >= maxStamina:
    return (0, 0)  // 추적 불필요

if lastStaminaUpdateUtcMs <= 0:
    return (0, now)  // 추적 시작

elapsedMs = now - lastStaminaUpdateUtcMs
intervalMs = staminaIntervalSeconds * 1000
recoveryCount = elapsedMs / intervalMs (정수 나눗셈)

actualRecovery = min(recoveryCount, maxStamina - currentStamina)

if currentStamina + actualRecovery >= maxStamina:
    return (actualRecovery, 0)  // 추적 종료
else:
    remainderMs = elapsedMs % intervalMs
    return (actualRecovery, now - remainderMs)
```

---

## Boot Flow Integration

### 최초 로그인 (SyncState.Initial)

1. `RewardManager.FirstInitAsync()` → `InventoryManager.LoadSettings()` (설정 로드)
2. `ApplyCurrency(STAMINA, maxStamina)` → stamina = max
3. `LastStaminaUpdateUtcMs = 0` (기본값 유지, 추적 불필요)
4. `SaveGameStorageAsync()` → codec에 lastStaminaUpdateUtcMs 생략 (0이므로)

### 복귀 로그인 (SyncState.Success)

1. `SyncGameStorageAsync()` → codec 복원 (stamina + LastStaminaUpdateUtcMs)
2. `LoginManager.syncGameStateAsync()` → `InventoryManager.LoadSettings()` (설정 로드)
3. `InventoryManager.RecoverStamina()` → 오프라인 회복 계산
4. 실제 회복량이 1 이상이면 `InventoryManager`가 `CURRENCY_CHANGED(STAMINA, delta, currentAmount)`를 publish한다

---

## Implementation Location (3-path mirror)

> 스태미나 로직은 `InventoryStaminaController.cs` (신규) 에 캡슐화된다.
> InventoryManager.cs는 위임만 한다.

- InventoryStaminaController.cs:
  - UPM (정본): `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/Inventory/InventoryStaminaController.cs`
  - Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/Inventory/InventoryStaminaController.cs`
  - Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/{version}/MobilePackage/Runtime/Inventory/InventoryStaminaController.cs`
- InventoryManager.cs — [10-inventory-manager](../10-inventory-manager/SKILL.md) 참조
- InventoryStorage.cs — [11-inventory-storage](../11-inventory-storage/SKILL.md) 참조

---

## Related

- [13-inventory-settings](../13-inventory-settings/SKILL.md) — InventorySettings (설정 소스)
- [10-inventory-manager](../10-inventory-manager/SKILL.md) — InventoryManager (위임 호스트)
- [11-inventory-storage](../11-inventory-storage/SKILL.md) — InventoryStorage (LastStaminaUpdateUtcMs)
- [11-inventory-storage](../11-inventory-storage/SKILL.md) — InventoryStorage currency state (STAMINA 포함)
