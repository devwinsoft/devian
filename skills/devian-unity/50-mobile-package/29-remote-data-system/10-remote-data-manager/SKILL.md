---
name: 10-remote-data-manager
description: MobilePackage RemoteDataManager를 통해 VersionCheck(플랫폼 URL 기반)와 Server UTC 동기화(HTTP Date header)를 단일 경로로 제공할 때 사용한다.
---

# 10-remote-data-manager

Status: ACTIVE
AppliesTo: v10

RemoteDataManager는 LoginManager/MobileApplication에서 사용하는 버전 체크와 서버 UTC 동기화 기능의 단일 진입점이다.

---

## 1. Class

```csharp
public sealed class RemoteDataManager : CompoSingleton<RemoteDataManager>
```

- namespace: `Devian`
- asmdef: `Devian.Samples.MobilePackage`
- `MobileApplication`에서 `RequireComponent(typeof(RemoteDataManager))`로 보장한다.

---

## 2. Public API

```csharp
public Task<CommonResult<VersionCheckResult>> InitializeAsync(VersionNumber clientVersion, CancellationToken ct = default)
public Task<CommonResult<VersionCheckResult>> VersionCheckAsync(VersionNumber clientVersion, CancellationToken ct = default)
public Task<CommonResult> SyncServerUtcAsync(CancellationToken ct = default)
public static long ServerNowUtcMs { get; }
public VersionNumber CurrentVersionNumber { get; }
public VersionNumber MinVersionNumber { get; }
```

- `InitializeAsync`: 로그인 진입 초기에 호출되는 단일 초기화 API. 버전 체크와 서버 UTC 동기화를 순서대로 수행한다.
- `VersionCheckAsync`: `MobileApplication.VersionCheckAOS/VersionCheckIOS` URL에서 JSON을 받아 `VersionCheckConfig`([15-version-check-config](../../../../devian/10-module/20-core/15-version-check-config/SKILL.md))로 파싱 후 버전 판정한다.
- `SyncServerUtcAsync`: `HEAD https://worldtimeapi.org` 응답의 `Date` 헤더를 파싱해 서버 UTC 스냅샷을 갱신한다.
- `ServerNowUtcMs`: 추정 서버 UTC(ms)를 반환하는 static property. 동기화 스냅샷 + 경과 시간으로 계산하며, 인스턴스가 없으면 클라이언트 UTC fallback.
- `CurrentVersionNumber`/`MinVersionNumber`: 최근 버전 체크로 해석된 버전값을 보유한다.

---

## 3. Hard Rules

- VersionCheck URL owner는 `MobileApplication`이다. (`VersionCheckAOS`, `VersionCheckIOS`)
- `LoginManager`는 로그인 초기화 진입 시 `RemoteDataManager.InitializeAsync`를 **가장 먼저** 호출한다.
- `LoginManager`는 버전 체크/서버 UTC fetch 실구현을 소유하지 않는다.
- 서버 UTC는 Date header 기반으로 획득한다. (`worldtimeapi` HEAD)
- 실패 시 앱 흐름을 강제 중단하지 않고 fallback 경로를 허용한다.
- 과거 RemoteConfig storage/snapshot/save-codec/firebase-config 연동 같은 중복 기능은 유지하지 않는다.
- VersionCheck 실패 시 전용 에러 코드를 사용한다 (범용 `COMMON_SERVER`/`COMMON_NETWORK` 사용 금지):
  - `VERSION_CHECK_URL_NOT_CONFIGURED` — 플랫폼 URL 미설정
  - `VERSION_CHECK_NETWORK_FAILED` — 네트워크 요청 실패
  - `VERSION_CHECK_RESPONSE_EMPTY` — 응답 본문 비어있음
  - `VERSION_CHECK_PARSE_FAILED` — JSON 파싱 실패

---

## 4. Implementation Location (3-path mirror)

- UPM (정본): `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/RemoteData/RemoteDataManager.cs`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/RemoteData/RemoteDataManager.cs`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/RemoteData/RemoteDataManager.cs`

---

## 5. Related

- [11-mobile-application](../../11-mobile-application/SKILL.md)
- [24-login-manager](../../24-login-manager/SKILL.md)
