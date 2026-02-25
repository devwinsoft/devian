# devian-unity/50-mobile-system — SaveDataManager


## Scope
- SaveLocal/SaveCloud 로직을 단일 매니저로 통합하여 시나리오 기반 동기화(Conflict + 선택)를 수행한다.
- SaveLocalManager/SaveCloudManager는 삭제됨. **SaveDataManager가 유일한 진입점**이다.
- [50-mobile-system overview](../../00-overview/SKILL.md)의 서브스킬이다.


## Preconditions
- DeviceId는 string GUID (앱 최초 실행 시 발급, `PlayerPrefs` 저장)
- SaveSeq는 per-device 단조 증가 카운터 (`PlayerPrefs` 저장, Sync same-device 최신성 판정용)
- UtcTime, Checksum은 삭제되어 Sync 판단에 사용하지 않음


## Singleton
- `SaveDataManager`는 `CompoSingleton<SaveDataManager>` 기반이다.
- 샘플 씬(또는 초기화 루틴)에서 컴포넌트로 배치되어 lifecycle을 가진다.


## SyncState
- `Success` (0) — 동기화 정상 완료, 데이터 존재
- `Conflict` (1) — 자동 승자 결정 불가(주로 cross-device payload 충돌, 또는 same-device `saveSeq` fallback)
- `Initial` (2) — 어떤 슬롯에도 데이터 없음 (신규 유저 / 초기 상태)
- `ConnectionFailed` (3) — Cloud 초기화 실패 + Local 데이터 없음. 호출 측에서 재시도 또는 오프라인 안내 필요


## SyncResult
- `SyncState State` — 동기화 결과 상태
- `string Slot` — 처리 대상 슬롯 key (전체 순회 시 Conflict 발생 슬롯, 단일 슬롯 시 해당 슬롯)
- `SaveLocalPayload LocalPayload` — 동기화 후 로컬 payload (nullable)
- `SaveCloudPayload CloudPayload` — 동기화 후 클라우드 payload (nullable)
- `string LocalDeviceId` — 로컬 deviceId (nullable)
- `string CloudDeviceId` — 클라우드 deviceId (nullable)

`SyncAsync(string slot)` 반환 시 payload가 `SyncResult`에 포함되므로 추가 파일 I/O(reload)는 불필요하다.
단, **인메모리 게임 상태 복원은 호출 측 책임**이다: `GameStorageManager.Instance.LoadFromPayload(sync.Value.LocalPayload.payload)`를 별도 호출하여 Inventory/Purchase를 역직렬화해야 한다.


## Scenario

### Guest 로그인
- CloudSave 경로 비활성: Cloud 호출 금지 (Guest는 Cloud 기능 비활성)
- `SyncAsync(ct)`: Local 슬롯 전체 검사 — 데이터 있으면 `Success`, 없으면 `Initial`
- `SyncAsync(slot, ct)`: 해당 슬롯만 로드 — 데이터 있으면 `Success`(payload 포함), 없으면 `Initial`

### Cloud Init 실패 (Non-Guest)
- Cloud 초기화 실패 + Local 없음 → `ConnectionFailed`
- Cloud 초기화 실패 + Local 있음 → local-only 모드로 진행 (`SyncAsync(ct)`: syncAsync 계속 / `SyncAsync(slot)`: local payload로 `Success` 반환)

### Local 없음 + Cloud 있음
- Cloud 데이터를 Local에 저장
- Cloud에는 저장하지 않음 (deviceId overwrite 금지)

### Local 있음 + Cloud 없음
- Cloud 쓰기 불가 시 local 데이터만으로 `Success` 반환
- Cloud 쓰기 가능 시 Local을 Cloud에 저장 (클라우드 생성), Cloud DeviceId = 현재 deviceId

### Local 있음 + Cloud 있음
- `local.payload == cloud.Payload` → `Success` (deviceId가 달라도 충돌 아님)
- `local.payload != cloud.Payload` AND `local.deviceId == cloud.DeviceId`
  - `saveSeq` 비교로 자동 승자 결정 (same-device divergence 자동 복구)
  - `local.saveSeq > cloud.SaveSeq` → Local → Cloud 저장
  - `cloud.SaveSeq > local.saveSeq` → Cloud → Local 저장
  - `saveSeq` 누락/동률(`<=0` 또는 동일) → `Conflict` (레거시 안전 fallback)
- `local.payload != cloud.Payload` AND `local.deviceId != cloud.DeviceId` → **Conflict 발생**
  - 유저가 Local vs Cloud 선택
  - **Local 선택**: Local → Cloud 저장 + Cloud DeviceId = 현재 deviceId
  - **Cloud 선택**: Cloud → Local 저장 (즉시 Cloud 저장 금지, DeviceId overwrite용 Cloud 재저장 금지)
- 다른 기기 간 충돌 자체는 허용 (전용 서버/CAS 없는 스냅샷 저장 제약)

### Initial (데이터 없음)
- Guest: Local 슬롯 전체 검사 후 데이터가 하나도 없으면 `Initial`
- Non-Guest: 모든 슬롯(Local + Cloud) 순회 후 `hasAnyLocal == false && hasAnyCloud == false`이면 `Initial`
- Cloud 연결 실패 + 데이터 없음 → `ConnectionFailed` (Initial이 아닌 ConnectionFailed)


## Unified Settings
- 슬롯 설정이 SaveDataManager 단일 Inspector에 통합됨.
- `SaveSlot` 중첩 타입: `slotKey` + `filename`(local) + `cloudSlot`(cloud).
- payload 난독화는 `ComplexUtil.Encrypt_Base64/Decrypt_Base64`로 수행 (경량 난독화, Key/IV 불필요).


## SlotConfig Interface

SaveDataManager는 Slot 설정을 `SaveSlotConfig`로 캡슐화한다. (단일 파일 내부 중첩 타입)

### Methods
- `List<string> GetLocalSlotKeys()`
- `List<string> GetCloudSlotKeys()`
- `bool TryResolveLocalFilename(string slotKey, out string filename)`
- `bool TryResolveCloudSlot(string slotKey, out string cloudSlot)`


## Public API

### Sync
- `Task<CommonResult<SyncResult>> SyncAsync(CancellationToken ct)`
- `Task<CommonResult<SyncResult>> SyncAsync(string slot, CancellationToken ct)`
- `Task<CommonResult<bool>> ResolveConflictAsync(string slot, SyncResolution resolution, CancellationToken ct)`

### Save
- `Task<CommonResult<bool>> SaveDataAsync(string slot, string data, CancellationToken ct)`
- `Task<CommonResult<bool>> SaveDataAsync(string slot, string data, bool includeCloud, CancellationToken ct)`
- `Task<CommonResult<bool>> SaveDataAsync<T>(string slot, T data, CancellationToken ct)`
- `Task<CommonResult<bool>> SaveDataAsync<T>(string slot, T data, bool includeCloud, CancellationToken ct)`

- `includeCloud` 생략 시 기본값은 `false` (local만 저장).
- `includeCloud: true` — local 저장 후, `isLocalOnly(loginType)`이 false이면 cloud도 저장. Guest/Editor는 자동 스킵.
- `includeCloud: false` — local만 저장. cloud 시도 없음.

### Clear
- `Task<CommonResult<bool>> ClearSlotAsync(string slot, CancellationToken ct)`

항상 local + cloud 모두 삭제.

### NeedsCloudSave
- `bool NeedsCloudSave` — 저장하지 못한 구매 내역이 있는지 상태 조회 (read-only, 인메모리)
- `void MarkNeedsCloudSave()` — 구매/환불 후 cloud 저장 실패 시 호출.
- `void ClearNeedsCloudSave()` — cloud 저장 성공 시 호출.

동작:
- 인메모리 flag. 앱 재시작 시 false로 초기화.
- Cloud→Local 데이터 수신 시 자동 리셋:
  - Sync "Local없음+Cloud있음"
  - Sync same-device `cloud.SaveSeq > local.saveSeq` (Cloud→Local 자동 복구)
  - Resolve UseCloud
- Resolve(UseLocal) 성공 시 자동 Clear.
- Sync/Resolve와 독립적. Conflict 판정은 `payload + deviceId + saveSeq` 조합 기준.

### Payload Parsing
- `static CommonResult<T> ParsePayloadResult<T>(SaveLocalPayload payload)`
- `static CommonResult<T> ParsePayloadResult<T>(SaveCloudPayload payload)`


## Internal API
- `internal Task<CommonResult<SaveCloudResult>> _initializeCloudAsync(CancellationToken ct)` — AccountManager에서 호출
- `internal bool _isCloudAvailable`


## Location
- MobileSystem 번들 샘플 내부, 단일 asmdef(`Devian.Samples.MobileSystem`)에 포함되어 함께 설치된다.
- UPM: `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/SaveData/SaveDataManager.cs`
- UnityExample mirror: `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/SaveData/SaveDataManager.cs`
- [50-mobile-system overview](../../00-overview/SKILL.md)


## Out of Scope
- payload 병합 / 부분 병합
- utcTime 기반 최신 판정
- cross-device 자동 승자 결정
