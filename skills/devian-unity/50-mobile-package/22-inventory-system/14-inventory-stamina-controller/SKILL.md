# 14-inventory-stamina-controller

Status: ACTIVE
AppliesTo: v10

`CURRENCY_TYPE.STAMINA`에 대한 설정 로드 및 자동 회복 로직을 `InventoryStaminaController`로 캡슐화한다.
InventoryManager는 컨트롤러에 위임만 하며, 외부 API를 유지한다.

---

## Design

- `InventoryStaminaController` (`internal sealed class`) — 스태미나 전용 컨트롤러
- InventoryManager가 `_staminaController` 필드로 소유, 공개 API를 위임한다
- 컨트롤러는 `InventoryStorage`를 소유하지 않음 — 메서드 파라미터로 전달받음

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

- `RecoverStamina(InventoryStorage storage)`:
  1. stamina >= MaxStamina → return (추적 불필요, 타임스탬프 무의미)
  2. `LastStaminaUpdateUtcMs <= 0` → `LastStaminaUpdateUtcMs = now`, return (추적 시작)
  3. 경과 시간 / `_staminaIntervalSeconds` = 회복량 계산
  4. 회복 적용 (clamp to max)
  5. 회복 후 stamina >= max → `LastStaminaUpdateUtcMs = 0` (추적 종료)
  6. 아직 max 미만 → `LastStaminaUpdateUtcMs = now - remainderMs` (잔여 시간 보존)

---

## InventoryManager 위임 API

```
readonly InventoryStaminaController _staminaController = new();

public int MaxStamina => _staminaController.MaxStamina;
public void LoadSettings() => _staminaController.LoadSettings();
public void RecoverStamina() => _staminaController.RecoverStamina(_storage);
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
currentStamina = wallet.Get(CURRENCY_TYPE.STAMINA)

if currentStamina >= maxStamina:
    return  // 추적 불필요, 타임스탬프 건드리지 않음

if lastStaminaUpdateUtcMs <= 0:
    lastStaminaUpdateUtcMs = now  // 추적 시작
    return

elapsedMs = now - lastStaminaUpdateUtcMs
intervalMs = staminaIntervalSeconds * 1000
recoveryCount = elapsedMs / intervalMs (정수 나눗셈)

actualRecovery = min(recoveryCount, maxStamina - currentStamina)
if actualRecovery > 0:
    wallet.TryAdd(CURRENCY_TYPE.STAMINA, actualRecovery)

if currentStamina + actualRecovery >= maxStamina:
    lastStaminaUpdateUtcMs = 0  // 추적 종료
else:
    remainderMs = elapsedMs % intervalMs
    lastStaminaUpdateUtcMs = now - remainderMs
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

---

## Implementation Location (3-path mirror)

> 스태미나 로직은 `InventoryStaminaController.cs` (신규) 에 캡슐화된다.
> InventoryManager.cs는 위임만 한다.

- InventoryStaminaController.cs:
  - UPM (정본): `framework-cs/upm/com.devian.samples/Samples~/MobilePackage/Runtime/Inventory/InventoryStaminaController.cs`
  - Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobilePackage/Runtime/Inventory/InventoryStaminaController.cs`
  - Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/Inventory/InventoryStaminaController.cs`
- InventoryManager.cs — [10-inventory-manager](../10-inventory-manager/SKILL.md) 참조
- InventoryStorage.cs — [11-inventory-storage](../11-inventory-storage/SKILL.md) 참조

---

## Related

- [13-inventory-settings](../13-inventory-settings/SKILL.md) — InventorySettings (설정 소스)
- [10-inventory-manager](../10-inventory-manager/SKILL.md) — InventoryManager (위임 호스트)
- [11-inventory-storage](../11-inventory-storage/SKILL.md) — InventoryStorage (LastStaminaUpdateUtcMs)
- [12-inventory-wallet](../12-inventory-wallet/SKILL.md) — InventoryWallet (STAMINA 통화)
