# devian-unity/50-mobile-system — SaveDataManager


## Scope
- SaveLocal/SaveCloud 로직을 단일 매니저로 통합하여 시나리오 기반 동기화(Conflict + 선택)를 수행한다.
- SaveLocalManager/SaveCloudManager는 삭제되었다. **SaveDataManager가 유일한 진입점**이다.
- 현재 구조는 멀티 슬롯이 아니라 **단일 primary save** 기준이다.


## Preconditions
- DeviceId는 string GUID (앱 최초 실행 시 발급, `PlayerPrefs` 저장)
- SaveSeq는 per-device 단조 증가 카운터 (`PlayerPrefs` 저장, same-device 최신성 판정용)
- UtcTime, Checksum은 최신성 판정에 사용하지 않는다


## Singleton
- `SaveDataManager`는 `CompoSingleton<SaveDataManager>` 기반이다.
- 샘플 씬(또는 초기화 루틴)에서 컴포넌트로 배치되어 lifecycle을 가진다.


## Primary Save Binding
- public API는 단일 게임 상태 save만 다룬다.
- Inspector 설정:
  - `_primaryLocalFilename`
  - `_primaryCloudSlot`
- 기본값:
  - local: `save/main.json`
  - cloud: `main`

중요:
- 이 binding은 파일/클라우드 키 매핑일 뿐이다.
- 런타임 게임 상태는 `AccountManager.Storage`, `InventoryManager.Storage`, `PurchaseManager.Storage`의 singleton 집합 하나다.


## SyncState
- `Success` — 동기화 정상 완료, 데이터 존재
- `Conflict` — 자동 승자 결정 불가
- `Initial` — 데이터 없음, 또는 데이터는 있지만 sign-in 성공한 적 없음 (`Account.loginType == NONE`)


## SyncResult
- `SyncState State`
- `SaveLocalPayload LocalPayload`
- `SaveCloudPayload CloudPayload`
- `LocalPayload.account` / `CloudPayload.Account` — 계정 메타 미러
- `string LocalDeviceId`
- `string CloudDeviceId`
- `SaveRecordSummary LocalSummary`
- `SaveRecordSummary CloudSummary`

`SyncGameStorageAsync(ct)` 반환 시 payload가 `SyncResult`에 포함되므로 추가 파일 I/O는 불필요하다.
인메모리 게임 상태 복원은 `SaveDataManager` 내부 책임이다. `SyncGameStorageAsync(ct)` 성공 시 `LoadFromPayload()`가 직접 manager storage를 복원한다.

## Save Summary (payload 해석)
- `SaveRecordSummary`
  - `Exists`, `SchemaVersion`, `UpdateTime`, `SaveSeq`, `DeviceId`, `LoginType`, `SocialUserId`
  - `PayloadSummary`
- `SavePayloadSummary`
  - `HasPayload`, `ParseSuccess`, `ParseError`, `JsonVersion`
  - inventory count: `WalletCurrencyCount`, `HeroCount`, `CardCount`, `EquipCount`, `RentalCount`, `PassCount`
  - runtime count: `MissionRuntimeCount`, `MissionCompletedCount`, `AchieveRuntimeCount`, `AchieveWaitingCount`, `AchieveCompletedCount`
  - message/remoteConfig: `MessageStatCount`, `RemoteConfigServerNowUtcMs`

규칙:
- 요약 생성(복호화 + JSON 파싱)은 `SaveDataManager` 내부에서 수행한다.
- payload 파싱 실패여도 Sync 자체는 실패로 승격하지 않고 `ParseSuccess=false`로 요약에 반영한다.


## Scenario

### NONE (미로그인)
- `LoginType.NONE`은 sign-in 성공 전 초기 상태다.
- primary local 데이터를 로드한 뒤, `Account.loginType == NONE`이면 `Initial`을 반환한다.
- local 데이터가 없어도 `Initial`을 반환한다.

### Guest 로그인
- CloudSave 경로 비활성: Cloud 호출 금지
- `SyncGameStorageAsync(ct)`: primary local save만 로드 — 데이터 있으면 `Success`, 없으면 `Initial`

### Cloud Init 실패 (Non-Guest)
- Cloud 초기화 실패 + Local 없음 → `SyncGameStorageAsync` 실패 (`_initializeCloudAsync` 에러 반환)
- Cloud 초기화 실패 + Local 있음 → local-only 모드로 진행 (`SyncGameStorageAsync`: local payload로 `Success` 반환)

### Cloud Load/Save 실패 (Non-Guest)
- Cloud `LoadAsync`/`SaveAsync` 실패는 `SyncGameStorageAsync`의 실패로 반환한다.
- Cloud 연결 계층 실패는 `COMMON_ERROR_TYPE.CLOUDSAVE_CONNECTION_FAILED`를 사용한다.

### Local 없음 + Cloud 있음
- Cloud 데이터를 primary local에 저장
- Cloud에는 재저장하지 않는다

### Local 있음 + Cloud 없음
- Cloud 쓰기 가능 시 Local을 Cloud에 저장한다

### Local 있음 + Cloud 있음
- `local.payload == cloud.Payload` → `Success`
- `local.payload != cloud.Payload` AND `local.deviceId == cloud.DeviceId`
  - `saveSeq` 비교로 자동 승자 결정
  - `local.saveSeq > cloud.SaveSeq` → Local → Cloud 저장
  - `cloud.SaveSeq > local.saveSeq` → Cloud → Local 저장
  - `saveSeq` 누락/동률 → `Conflict`
- `local.payload != cloud.Payload` AND `local.deviceId != cloud.DeviceId` → `Conflict`
  - `UseLocal` → Local → Cloud 저장
  - `UseCloud` → Cloud → Local 저장


## Public API

### Sync
- `Task<CommonResult<SyncResult>> SyncGameStorageAsync(CancellationToken ct)`
- `Task<CommonResult<bool>> ResolveConflictAsync(SyncResolution resolution, CancellationToken ct)`
  - local-only(`GUEST/EDITOR`) 모드에서는 cloud conflict 해소를 수행하지 않고 **no-op success(true)**를 반환한다.

### Save
- `Task<CommonResult<bool>> SaveGameStorageAsync(bool saveCloud, CancellationToken ct)`

규칙:
- `SyncGameStorageAsync(ct)` 성공 후에만 저장 가능하다.
- `Initial`도 primary save context를 활성화하므로 첫 저장이 가능하다.
- `saveCloud=false`이면 local save만 수행하고 cloud save는 시도하지 않는다.
- `saveCloud=true`이면 local save 후 cloud save를 best effort로 시도한다.
- cloud 저장 실패는 non-fatal이며 `NeedsCloudSave`만 올린다.

### Load
- `CommonResult<bool> LoadLocalGameState()`

### Clear
- `Task<CommonResult<bool>> ClearSaveAsync(CancellationToken ct)`

### NeedsCloudSave
- `bool NeedsCloudSave`
- `void MarkNeedsCloudSave()`
- `void ClearNeedsCloudSave()`

### Payload Parsing
- `static CommonResult<T> ParsePayloadResult<T>(SaveLocalPayload payload)`
- `static CommonResult<T> ParsePayloadResult<T>(SaveCloudPayload payload)`

### Serialization
- `string ToJson()`

현재 런타임 게임 상태(Inventory, Purchase, Account, Mission)를 평문 JSON으로 직렬화한다.
내부적으로 `SaveDataJsonCodec.Serialize()`를 호출한다.

### Recovery
- `Task<CommonResult<bool>> RestoreFromPlainJsonAsync(string json, bool saveCloud, CancellationToken ct)`

평문 JSON을 런타임에 적용하고 local(+cloud)에 영속화한다.
`_hasPrimarySaveContext`를 활성화하므로 Sync 없이 직접 복원이 가능하다.
RecoveryManager(25-recovery-system)에서 Import 시 사용한다.

내부 동작:
1. `LoadFromJson(json)` — 런타임 스토리지에 적용
2. `_hasPrimarySaveContext = true` — primary save context 활성화
3. `SaveGameStorageAsync(saveCloud, ct)` — local(+cloud) 영속화


## Internal API
- `internal Task<CommonResult<SaveCloudResult>> _initializeCloudAsync(CancellationToken ct)` — `SaveDataManager` 내부 sync/resolve/save 경로에서 호출
- `internal bool _isCloudAvailable`


## Json Ownership
- `SaveDataManager`가 게임 상태 JSON 직렬화/역직렬화의 유일한 진입점이다.
- JSON 구현은 [43-savedata-json-codec](../43-savedata-json-codec/SKILL.md)를 따른다.
- 각 시스템은 자신의 storage를 직접 소유한다.
  - `AccountManager.Instance.Storage`
  - `InventoryManager.Instance.Storage`
  - `PurchaseManager.Instance.Storage`
- `SaveLocalPayload.account` / `SaveCloudPayload.Account` 메타는 account mirror 용도로 유지한다.


## Location
- UPM: `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/SaveData/SaveDataManager.cs`
- UnityExample mirror: `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/SaveData/SaveDataManager.cs`
- Assets/Samples import: `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/SaveData/SaveDataManager.cs`


## Out of Scope
- 멀티 슬롯 UX
- payload 부분 병합
- utcTime 기반 최신성 판정
- cross-device 자동 승자 결정
