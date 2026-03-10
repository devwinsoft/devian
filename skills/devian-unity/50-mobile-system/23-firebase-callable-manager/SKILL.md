# 23-firebase-callable-manager — FirebaseCallableManager


Status: ACTIVE
AppliesTo: v10


## Purpose

Firebase Cloud Functions callable 통합 래퍼.
함수별 typed API, 에러 매핑, 응답 파싱, region 관리, 값 추출 헬퍼를 통합한다.
`FirebaseFunctions` 인스턴스는 외부에 노출하지 않는다.
game 도메인 테이블(TB_PURCHASE 등)은 참조하지 않는다 — 서버 응답의 raw 값을 typed result에 그대로 저장한다.

**`internal sealed class`** — 같은 어셈블리(`Devian.Samples.MobileSystem`) 내부에서만 접근 가능하다.
외부 어셈블리(Assembly-CSharp 등)에서는 직접 접근할 수 없으며, `LoginManager`/각 시스템 매니저를 통해 간접 사용한다.


## Scope

| 포함 | 제외 |
|------|------|
| Functions region 관리 | Firebase Auth (AccountLoginFirebase 소관) |
| 함수별 typed public API (9개) | Firebase 의존성 초기화 (`CheckAndFixDependenciesAsync`) |
| FunctionsException 에러 매핑 (per-function) | 도메인 변환 (ResolveSeasonPassId, ResolveRewardGroupId 등) |
| 응답 파싱 → typed result | Editor 전용 mock (각 manager 자체 처리) |
| 응답 정규화 (internal) | |
| 값 추출 헬퍼 (`ReadLong`/`ReadString`/`ReadBool`) | |


## Sample SSOT
- `com.devian.samples/Samples~/MobileSystem`


## Implementation Location (3-path mirror)

> 3-path mirror 정책: [devian-unity/07-samples-creation-guide](../../07-samples-creation-guide/SKILL.md)

- UPM (정본): `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/FirebaseCallable/FirebaseCallableManager.cs`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/FirebaseCallable/FirebaseCallableManager.cs`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/FirebaseCallable/FirebaseCallableManager.cs`


## Singleton

`CompoSingleton<FirebaseCallableManager>` 기반.
Bootstrap prefab에 부착한다.
`MobileApplication`에 `[RequireComponent(typeof(FirebaseCallableManager))]`로 선언.


## API

> FirebaseCallableManager는 `internal` 클래스이므로 같은 어셈블리 내부에서만 접근 가능하다.

### Instance Methods — Region

- `SetFunctionsRegion(string region)`
  - Firebase Functions 리전을 설정한다.
  - 빈 문자열 또는 null이면 기본 리전(us-central1)을 사용한다.
  - `MobileApplication.onBootAsync()`에서 호출한다.

### Instance Methods — Session Init callable (1개)

- `InitSessionAsync(data, ct) → Task<CommonResult<SessionInitSnapshot>>`
  - `initSession` callable 호출 → `getRemoteConfig` + `getEntitlements` + `getPurchaseAdjustments` 통합 1회 왕복
  - 반환: `SessionInitSnapshot` (RemoteConfig, Entitlements, PurchaseAdjustments)
  - 에러 매핑: `COMMON_NETWORK`, `COMMON_AUTH`, `COMMON_SERVER`
  - **외부 어셈블리에서 직접 호출 불가** — `LoginManager`가 내부 호출한다.
  - 주의: 초기 인벤토리 지급 데이터는 포함하지 않는다 (`getInitialInventory` 별도 호출).

### Instance Methods — Inventory callable (1개)

- `GetInitialInventoryAsync(ct) → Task<CommonResult<RewardData[]>>`
  - `getInitialInventory` callable 호출
  - 서버 transaction marker 기반 1회 지급 데이터(`RewardData[]`)를 반환한다.
  - marker가 이미 있으면 빈 배열을 반환한다.

### Instance Methods — Remote Config callable (1개)

- `GetRemoteConfigAsync(ct) → Task<CommonResult<RemoteConfigSnapshot>>`
  - `getRemoteConfig` callable 호출 → 응답 파싱 → `RemoteConfigSnapshot` 반환
  - 에러 매핑: `COMMON_NETWORK`, `COMMON_AUTH`, `COMMON_SERVER`

### Instance Methods — Purchase callable (7개)

- `VerifyPurchaseAsync(data, ct) → Task<CommonResult<VerifyPurchaseResponse>>`
  - `verifyPurchase` callable 호출 → 응답 파싱 → `VerifyPurchaseResponse` 반환
  - 에러 매핑: `PURCHASE_UNAUTHENTICATED`, `PURCHASE_VERIFY_INVALID_ARGUMENT`, `PURCHASE_VERIFY_FAILED_PRECONDITION`, `PURCHASE_VERIFY_CALL_FAILED`, `PURCHASE_NETWORK_UNAVAILABLE`
- `GetEntitlementsAsync(ct) → Task<CommonResult<EntitlementsSnapshot>>`
  - `getEntitlements` callable 호출 → 응답 파싱 → `EntitlementsSnapshot` 반환
  - `OwnedSeasonPasses`는 서버 원본 ID (raw). domain 변환은 PurchaseManager가 수행.
- `GetRecentPurchases30dAsync(data, ct) → Task<CommonResult<RecentPurchaseItem>>`
  - `getRecentPurchases30d` callable 호출 → items[0] 파싱 → `RecentPurchaseItem` 반환
  - 아이템 없으면 `PURCHASE_RECENT_NOT_FOUND`
- `GetPurchaseAdjustmentsAsync(data, ct) → Task<CommonResult<RefundPageResult>>`
  - `getPurchaseAdjustments` callable 호출 → 페이지네이션 파싱 → `RefundPageResult` 반환
  - `PurchaseAdjustmentItem`은 서버 raw 필드만 포함. domain 변환은 PurchaseManager가 수행.
- `AckPurchaseClientGrantAsync(data, ct) → Task<CommonResult>`
  - `ackPurchaseClientGrant` callable 호출
- `AckPurchaseStoreConfirmAsync(data, ct) → Task<CommonResult>`
  - `ackPurchaseStoreConfirm` callable 호출
- `AckRefundAppliedAsync(data, ct) → Task<CommonResult>`
  - `ackRefundApplied` callable 호출

### Static Methods (순수 함수)

- `ReadLong(IDictionary<string, object> source, string key) → long`
  - 지정 키의 값을 `long`으로 추출한다.
  - `long`, `int`, `double`, `float`, `string` 타입을 처리한다.
  - 키가 없거나 변환 불가 시 `0L` 반환.

- `ReadString(IDictionary<string, object> source, string key) → string`
  - 지정 키의 값을 `string`으로 추출한다.
  - 키가 없거나 null이면 `string.Empty` 반환.

- `ReadBool(IDictionary<string, object> source, string key) → bool`
  - 지정 키의 값을 `bool`로 추출한다.
  - `bool`, `int`, `long`, `double`, `string` 타입을 처리한다.
  - 키가 없거나 변환 불가 시 `false` 반환.

### Private Methods (네이밍: 소문자 시작)

- `callFunctionAsync()` — callable 호출 + 응답 정규화 (raw dict 반환)
- `getFunctionsInstance()` — 리전 기반 `FirebaseFunctions` 인스턴스 획득
- `normalizeCallableResponse()` — 재귀적 응답 정규화
- `normalizeStringObjectMap()`, `normalizeAnyMap()`, `normalizeCallableValue()` — 정규화 내부 헬퍼
- `parseRemoteConfigSnapshot()`, `parseVerifyPurchaseResponse()`, `parseEntitlementsSnapshot()`, `parseRecentPurchaseItem()`, `parseRefundPageResult()` — 응답 파싱


## Integration

### LoginManager (InitSession 통합)
`EnsureRuntimeSessionAndInitializeAsync` / `LoginAndInitializeAsync`가 내부에서 `FirebaseCallableManager.Instance.InitSessionAsync()`를 호출한다.
외부 어셈블리는 `LoginManager`의 `CommonResult` 결과만 사용한다.

### InventoryManager (초기 지급 통합)
`InventoryManager.FirstInitAsync`가 `FirebaseCallableManager.Instance.GetInitialInventoryAsync()`를 호출한다.
초기 지급은 `SyncState.Initial` 경로에서만 수행한다.

### PurchaseManager
```csharp
var result = await FirebaseCallableManager.Instance.VerifyPurchaseAsync(data, ct);
if (result.IsFailure)
    return CommonResult<VerifyPurchaseResponse>.Failure(result.Error!);
var response = result.Value!;
```
PurchaseManager는 `FunctionsException`을 catch하지 않는다. 에러 매핑은 FirebaseCallableManager가 수행한다.
domain 변환(ResolveSeasonPassId 등)은 PurchaseManager가 typed result 수신 후 수행한다.
PurchaseManager는 같은 어셈블리이므로 `internal` FirebaseCallableManager에 직접 접근 가능하다.

### RemoteConfigManager
```csharp
var result = await FirebaseCallableManager.Instance.GetRemoteConfigAsync(ct);
```
Editor mock (`#if UNITY_EDITOR`)은 RemoteConfigManager가 자체 처리한다.
RemoteConfigManager는 같은 어셈블리이므로 `internal` FirebaseCallableManager에 직접 접근 가능하다.

### MobileApplication / LoginManager
```csharp
void configureFunctionsRegion()
{
    GetComponent<FirebaseCallableManager>()?.SetFunctionsRegion(FirebaseFunctionsRegion);
}
```
PurchaseManager, RemoteConfigManager에 개별 주입하지 않고 FirebaseCallableManager에만 설정한다.
`LoginManager.VersionCheckAsync(clientVersion, ct)`는 `FirebaseCallableManager.GetRemoteConfigAsync(ct)`를 직접 호출해
`CommonResult<VersionCheckResult>`를 반환한다.


## Hard Rules

- `FirebaseFunctions` 인스턴스를 외부에 노출하지 않는다.
- `FunctionsException` 에러 매핑은 FirebaseCallableManager 각 메서드 내부에서 수행한다. 호출자는 `CommonResult`만 소비한다.
- FirebaseCallableManager는 game 도메인 테이블(TB_PURCHASE 등)을 참조하지 않는다. 서버 응답의 raw 값을 typed result에 그대로 저장한다.
- Firebase Auth 관련 로직은 `AccountLoginFirebase`가 소유한다. FirebaseCallableManager는 Auth를 다루지 않는다.
- 외부에서 사용하지 않는 메서드는 `private`으로 선언하고 소문자로 시작한다.
- Editor mock 응답은 각 manager가 자체 처리한다. FirebaseCallableManager는 Editor 전용 분기를 갖지 않는다.


## Related

- [11-mobile-application](../11-mobile-application/SKILL.md) — Bootstrap, RequireComponent, region 설정
- [30-purchase-system](../30-purchase-system/00-overview/SKILL.md) — PurchaseManager Firebase callable
- [26-remote-config-system](../26-remote-config-system/00-overview/SKILL.md) — RemoteConfigManager getRemoteConfig
- [20-account-system/35-account-login-firebase](../20-account-system/35-account-login-firebase/SKILL.md) — Firebase Auth (별도 소관)
