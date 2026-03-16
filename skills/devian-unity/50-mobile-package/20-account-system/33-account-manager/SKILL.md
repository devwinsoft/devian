# 33-account-manager — Account Manager

Status: ACTIVE
AppliesTo: v10

## Singleton
- `AccountManager`는 `CompoSingleton<AccountManager>` 기반이다.
- 샘플 씬(또는 초기화 루틴)에서 컴포넌트로 배치되어 lifecycle을 가진다.

## Scope
포함:
- Firebase Auth 기반 인증 처리
- EDITOR/GUEST anonymous 로그인
- GOOGLE(Android) 인증 (GPGS server auth code -> Firebase credential)
- APPLE(iOS) 인증 (caller 제공 credential -> Firebase credential)
- Anonymous -> Social link 처리 (`LinkWithCredentialAsync`)
- 로그인 타입/계정 메타(`AccountStorage`) 보관
- 런타임 인증 세션 복구 (`EnsureRuntimeAuthSessionAsync`)

제외:
- SaveData sync/초기화
- 구매 진입 인증 게이트 오케스트레이션
- Scene/UI 제어

## Login Type (Platform)
- `NONE` (0): 초기/로그아웃 상태
- `EDITOR` (1): Firebase anonymous
- `GUEST` (2): Firebase anonymous
- `GOOGLE` (3): Android + GPGS + Firebase credential
- `APPLE` (4): iOS + Apple credential + Firebase credential

## Public API
- `LoginAsync(LoginType, CancellationToken) : Task<CommonResult>`
- `LoginAsync(LoginType, LoginCredential, CancellationToken) : Task<CommonResult>`
- `EnsureRuntimeAuthSessionAsync(CancellationToken) : Task<CommonResult<bool>>`
  - `true`: 인증 세션 존재/복구 성공
  - `false`: 복구 대상 없음 또는 silent 복구 불가
- `TryRestoreGoogleAuthAsync(CancellationToken) : Task<CommonResult<bool>>`
- `Logout() : void`
- `ApplyStorage(AccountStorage)`

## Hard Rules
- AccountManager는 인증/계정 메타만 책임진다.
- 저장 동기화/세션 데이터 초기화는 `LoginManager` 책임이다.
- 구매 인증 readiness 오케스트레이션은 `LoginManager` 책임이다.
- 다른 시스템은 로그인 여부 판정을 위해 FirebaseAuth를 직접 사용하지 않는다.

## Storage Ownership
- `AccountManager`는 `AccountStorage`를 직접 소유한다.
- 상위 시스템은 `AccountManager.Instance.Storage`를 저장/복원 대상으로 사용한다.
- JSON 직렬화 규약은 `21-savedata-system/43-savedata-json-codec`를 따른다.

## Location (3-path mirror)
- UPM: `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/Account/AccountManager.cs`
- Packages: `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/Account/AccountManager.cs`
- Assets/Samples: `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/Account/AccountManager.cs`

## Related
(upstream dependencies only — consumers reference this skill from their own Related sections)

## Out of Scope
- Apple Sign-in UI/네이티브 토큰 획득
- SaveData sync 오케스트레이션
- 구매/원격 데이터 초기화 오케스트레이션
