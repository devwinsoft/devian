# 24-login-manager — LoginManager

Status: ACTIVE
AppliesTo: v10

## Purpose

로그인 세션 복구 + 게임 진입용 데이터 초기화 오케스트레이터.
`AccountManager`(인증)와 각 시스템 초기화를 연결하지만, Scene/UI는 제어하지 않는다.


## Scope

포함:
- 런타임 인증 세션 복구 (`AccountManager.EnsureRuntimeAuthSessionAsync`)
- 명시 로그인 (`AccountManager.LoginAsync`)
- 세션 초기 스냅샷 조회 (`FirebaseManager.InitSessionAsync`)
- 저장 동기화 (`SaveDataManager.SyncGameStorageAsync`)
- 충돌 해소 + 재초기화 (`ResolveConflictAndInitializeAsync`)
- 초기 지급 (`InventoryManager.FirstInitAsync`) + 저장
- 게임 시스템 초기화 (Message/RemoteConfig/Mission/Achieve/Ad/Leaderboard)
- 최종 저장 (`SaveDataManager.SaveGameStorageAsync`)
- 구매 진입 인증 보정 (`EnsurePurchaseLoginReadyAsync`, Android silent Google restore)
- 결과 반환 (`CommonResult`)

제외:
- Scene 전환 (`SceneTransManager`) 호출
- UI 표시/버튼 제어 (`UICanvas*`) 호출
- 로그인 credential 획득 세부 구현 (AccountManager 소관)


## API

- `EnsureRuntimeSessionAndInitializeAsync(CancellationToken ct = default) : Task<CommonResult<LoginInitializeResult>>`
  - 앱 시작 시 인증 복구 + InitSession + 초기화 경로
  - 이전 로그인 정보가 없거나(`loginType=NONE`) 자동 복구가 불가능하면 `Failure`가 아니라 `Success(null)`을 반환한다. (UI가 명시 로그인 선택 상태로 전환)
- `LoginAndInitializeAsync(LoginType loginType, CancellationToken ct = default) : Task<CommonResult<LoginInitializeResult>>`
  - 사용자 선택 로그인 + InitSession + 초기화 경로
- `ResolveConflictAndInitializeAsync(SyncResolution resolution, CancellationToken ct = default) : Task<CommonResult<LoginInitializeResult>>`
  - 충돌 해소(`SaveDataManager.ResolveConflictAsync`) 후 재초기화 경로
  - 재초기화 후에도 `Conflict`면 `SAVEDATA_SYNC_RESOLVE_FAILED` 실패 반환
- `IsPurchaseLoginReady() : bool`
  - 현재 Firebase 인증 세션 존재 여부
- `EnsurePurchaseLoginReadyAsync(CancellationToken ct = default) : Task<CommonResult<bool>>`
  - 구매 진입 시 인증 세션 보정(필요 시 silent restore)

`LoginInitializeResult`:
- `SyncState` (`Success` / `Initial` / `Conflict`)
- `IsConflict`
- `LocalDeviceId`, `CloudDeviceId` (Conflict 디버깅 정보)
- `LocalSummary`, `CloudSummary` (`SaveRecordSummary`)
  - 충돌 시 local/cloud 저장 요약(메타 + payload 해석 결과)

반환 규약:
- 성공: `CommonResult.Success(LoginInitializeResult)`
- 실패: `CommonResult.Failure(CommonError, ...)`


## Fatal / Non-fatal

fatal (실패 반환):
- 계정 로그인/세션 복구 실패
- InitSession 조회 실패
- 저장 동기화 실패
- Resolve 후 재충돌(명시적 resolve 경로)
- 초기 지급/저장 실패
- GameMessage/RemoteConfig/Mission/Achieve 초기화 실패
- 최종 저장 실패

non-fatal (로그만 남기고 진행):
- `PurchaseManager.SyncAsync` 실패
- `AdManager.InitializeAsync` 실패
- `LeaderboardManager.InitializeAsync` 실패
- `LeaderboardManager.SyncSeasonTransitionRewardsAsync` 실패


## Hard Rules

- LoginManager는 Scene/UI 레이어를 참조하지 않는다.
- LoginManager 공개 API는 `CommonResult` 계열만 반환한다.
- 인증 상태 판단은 `AccountManager`를 통해서만 수행한다.
- Save payload 해석/요약 생성은 LoginManager에서 구현하지 않는다. (`SaveDataManager` 책임)

## Sample Wiring

- `SceneLoading`은 부트 시 `EnsureRuntimeSessionAndInitializeAsync`를 호출한다.
- `UICanvasLoading`은 버튼 로그인 시 `LoginAndInitializeAsync`를 호출한다.
- `Conflict` 감지/선택 UI는 Scene/UI 레이어에서 처리하고, 실제 해소는 `ResolveConflictAndInitializeAsync`를 호출한다.


## Implementation Location (3-path mirror)

- UPM (정본): `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Login/LoginManager.cs`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/Login/LoginManager.cs`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/Login/LoginManager.cs`


## Related

- [20-account-system/33-account-manager](../20-account-system/33-account-manager/SKILL.md)
- [21-savedata-system/10-savedata-manager](../21-savedata-system/10-savedata-manager/SKILL.md)
- [11-mobile-application](../11-mobile-application/SKILL.md)
