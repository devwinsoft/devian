# devian-unity/90-samples — SyncDataManager


## Scope
- Login/LocalSave/CloudSave를 사용해 시나리오 기반 동기화(Conflict + 선택)를 오케스트레이션한다.
- [30-samples-cloud-client-system](../30-samples-cloud-client-system/SKILL.md)의 서브스킬이다.


## Preconditions
- DeviceId는 string GUID (앱 최초 실행 시 발급, `PlayerPrefs` 저장)
- UtcTime, Checksum은 삭제되어 Sync 판단에 사용하지 않음


## Singleton
- `SyncDataManager`는 `CompoSingleton<SyncDataManager>` 기반이다.
- 샘플 씬(또는 초기화 루틴)에서 컴포넌트로 배치되어 lifecycle을 가진다.


## SyncState
- `Success` (0) — 동기화 정상 완료, 데이터 존재
- `Conflict` (1) — deviceId 불일치로 유저 선택 필요
- `Initial` (2) — 어떤 슬롯에도 데이터 없음 (신규 유저 / 초기 상태)


## Scenario

### Guest 로그인
- Cloud 호출 금지 (Guest는 CloudSaveManager 접근 불가)
- Local 슬롯만 검사: 데이터 있으면 `Success`, 없으면 `Initial`

### Local 없음 + Cloud 있음
- Cloud 데이터를 Local에 저장
- Cloud에는 저장하지 않음 (deviceId overwrite 금지)

### Local 있음 + Cloud 없음
- Local을 Cloud에 저장 (클라우드 생성)
- Cloud DeviceId = 현재 deviceId

### Local 있음 + Cloud 있음
- `local.deviceId == cloud.DeviceId` → Conflict 없음
  - Local을 최신으로 취급, 필요 시 Local → Cloud 저장
- `local.deviceId != cloud.DeviceId` → **Conflict 발생**
  - 유저가 Local vs Cloud 선택
  - **Local 선택**: Local → Cloud 저장 + Cloud DeviceId = 현재 deviceId
  - **Cloud 선택**: Cloud → Local 저장 (즉시 Cloud 저장 금지, 이후 cloud save 시 overwrite 가능)
- "핑퐁 충돌" 허용 (의도): 다른 기기에서 local이 존재하면 재실행 시 다시 Conflict 가능

### Initial (데이터 없음)
- Guest: Local 슬롯 전체 검사 후 데이터가 하나도 없으면 `Initial`
- Non-Guest: 모든 슬롯(Local + Cloud) 순회 후 `hasAnyLocal == false && hasAnyCloud == false`이면 `Initial`


## Public API
- `Task<CoreResult<SyncResult>> SyncAsync(CancellationToken ct)`
- `Task<CoreResult<bool>> ResolveConflictAsync(string slot, SyncResolution resolution, CancellationToken ct)`


## Location
- CloudClientSystem 번들 샘플 내부, 단일 asmdef(`Devian.Samples.CloudClientSystem`)에 포함되어 함께 설치된다.
- UPM: `framework-cs/upm/com.devian.samples/Samples~/CloudClientSystem/Runtime/Sync/SyncDataManager.cs`
- UnityExample mirror: `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/CloudClientSystem/Runtime/Sync/SyncDataManager.cs`
- [30-samples-cloud-client-system](../30-samples-cloud-client-system/SKILL.md)


## Out of Scope
- payload 병합 / 부분 병합
- utcTime 기반 최신 판정
- 자동 승자 결정
