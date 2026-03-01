# 33-account-manager — Account Manager


## Singleton
- `AccountManager`는 `CompoSingleton<AccountManager>` 기반이다.
- 샘플 씬(또는 초기화 루틴)에서 컴포넌트로 배치되어 lifecycle을 가진다.


## Scope
- Firebase Auth 기반 로그인 오케스트레이터.
- EDITOR/GUEST는 Firebase Anonymous를 사용한다.
- 실기기에서는 GUEST + GOOGLE(Android) + APPLE(iOS)을 지원한다.
- GOOGLE(Android)는 GPGS(Google Play Games Services) 인증을 Reflection으로 내부 획득하며, `PlayGamesAuthProvider.GetCredential(serverAuthCode)`로 Firebase에 연결한다.
- APPLE(iOS)은 caller(UI/네이티브)에서 IdToken + RawNonce를 받아 AccountManager에 전달한다.
- Anonymous → Google/Apple은 `LinkWithCredentialAsync`로 UID를 유지한다.


## Version requirement
- Google Play Games Services (GPGS) Unity 플러그인: **Play Games Plugin for Unity v2.0.0+** (권장)
  - AccountManager는 GPGS 플러그인을 **Reflection**으로 호출한다.
  - v2 기준 흐름을 문서화하며, 필요 시 구버전(v1 계열) 시그니처도 호환 경로로 처리한다.


## Login Type (Platform)
- **NONE**: 초기 상태 / 로그아웃 상태 (값: 0)
- **EDITOR**: Firebase Anonymous (값: 1)
- **GUEST (device)**: Firebase Anonymous — SaveCloudManager를 Initialize/Load/Save 호출하지 않음 (로컬 저장만 사용). Sync 호출이 있어도 Cloud 접근 없이 no-op. (값: 2)
- **GOOGLE (Android device)**: GPGS Reflection → `PlayGamesAuthProvider.GetCredential(serverAuthCode)` → Firebase sign-in / link (값: 3)
- **APPLE (iOS device)**: Sign-in/link to Firebase using Apple credential (caller-provided IdToken + RawNonce) (값: 4)


## Order (fixed)
1. Firebase dependencies init (`CheckAndFixDependenciesAsync`)
2. FirebaseAuth sign-in (Anonymous / credential / link — LoginType에 따라 분기)
3. `SaveCloudManager.Instance.InitializeAsync(ct)` — Guest 로그인에서는 스킵

Sync는 AccountManager의 책임이 아니며, [10-savedata-manager](../../21-savedata-system/10-savedata-manager/SKILL.md)가 담당한다.

## Auth Ownership (Hard Rule)
- 로그인/인증 상태의 최종 판단은 **AccountManager 단일 책임**이다.
- PurchaseManager/SaveDataManager 등 다른 시스템은 `FirebaseAuth.DefaultInstance.CurrentUser`를 직접 읽어
  "로그인 여부"를 판정하지 않는다.
- 구매 전 인증 게이트는 AccountManager 공개 상태(`IsPurchaseLoginReady` 등)를 통해 판단한다.
- FirebaseAuth 접근은 AccountManager 내부의 sign-in/link 구현 범위에서만 사용한다.
- Android에서는 구매 진입 시 `NONE`/`EDITOR` 상태라도, AccountManager가 GPGS silent 인증을 사용해
  GOOGLE 로그인(Firebase credential sign-in/link)을 자동 보정할 수 있다(UI 없는 silent 경로 우선).


## LoginCredential
- GUEST/EDITOR: `LoginCredential.Empty()` 또는 `null`
- GOOGLE(Android): `ServerAuthCode` 필수 (convenience 오버로드 사용 시 GPGS Reflection으로 내부 획득)
- APPLE(iOS): `IdToken` + `RawNonce` 필수 (caller가 직접 전달)

## Convenience Overload
- 반환형: `Task<CommonResult>` (성공 payload 없음, 실패 시 `CommonErrorType` 기반 `CommonError`)
- `LoginAsync(LoginType, CancellationToken)` — credential을 내부에서 획득한다.
  - EDITOR/GUEST: `LoginCredential.Empty()` 자동 생성
  - GOOGLE(Android): `getGoogleGpgsCredentialAsync` → GPGS Reflection으로 `PlayGamesPlatform.Authenticate` + `RequestServerSideAccess` 호출
  - APPLE(iOS): 지원하지 않음 — `LoginAsync(LoginType, LoginCredential, CancellationToken)` 사용

## Google GPGS Internal Acquisition
- `getGoogleGpgsCredentialAsync(CancellationToken)` — Reflection 기반, GPGS 플러그인 미설치 시 컴파일 안전.
- Flow (v2.0.0+ 권장):
  - `PlayGamesPlatform.Instance`
  - `Authenticate(Action<SignInStatus>)` — silent sign-in (이미 로그인된 경우만 성공)
  - silent 실패 시 → `ManuallyAuthenticate(Action<SignInStatus>)` — **Google Sign-in UI 표시**
  - `RequestServerSideAccess(..., Action<AuthResponse>)` → `AuthResponse.GetAuthCode()` → `ServerAuthCode`
- Flow (legacy v1 호환):
  - `PlayGamesPlatform.Instance` → `Authenticate(Action<bool>)` → `RequestServerSideAccess(false, Action<string>)` → `ServerAuthCode`
- 주의: GPGS v2의 `Authenticate()`는 silent-only (Android SDK `isAuthenticated` 호출). UI를 띄우려면 반드시 `ManuallyAuthenticate()` (Android SDK `signIn` 호출)를 사용해야 한다.
- `GoogleAuthProvider.GetCredential(idToken, accessToken)` 는 더 이상 사용하지 않는다.
- `PlayGamesAuthProvider.GetCredential(serverAuthCode)` 만 사용한다.


## Logout
- `Logout()` — 동기 메서드, 반환형은 `void`.
- `_initialized`(Firebase init 상태)는 유지한다. 재로그인 시 Firebase 의존성 체크를 건너뛴다.
- 플랫폼별 sign-out 처리를 `#if` 전처리기로 분리한다:
  - `#if UNITY_ANDROID && !UNITY_EDITOR` → `signOutGoogle()` (GPGS v2.0.0은 SignOut API 미제공, 현재 no-op)
  - `#if UNITY_IOS && !UNITY_EDITOR` → `signOutApple()` (Apple Sign-In은 토큰 기반, 현재 no-op)
- 공통: `_auth.SignOut()` 호출하여 Firebase 세션 종료.


## LoginType 영속화
- `PlayerPrefs` 기반 LoginType 영속화는 제거되었다.
- 계정 메타 영속화는 `AccountManager`가 직접 소유하는 `AccountStorage`로 통일한다.
- 저장 필드(최소): `loginType`, `socialUserId`, `lastUpdatedAtUtcMs`
- 로그인 성공, 로그아웃, 구매 인증 보정 시 `AccountManager`가 자신의 `Storage`를 갱신한다.
- `SaveDataManager`가 local/cloud payload를 로드한 뒤 `AccountManager.Storage`를 복원하고 `AccountManager.ApplyStorage(...)`로 런타임 `_currentLoginType`을 재적용한다.
- 계정 메타 미복원/유효하지 않은 값이면 `NONE` fallback.


## Storage Ownership
- `AccountManager`는 `AccountStorage`를 직접 소유한다.
- `SaveDataManager`는 `AccountManager.Instance.Storage`를 저장/복원 대상으로 사용한다.
- JSON 직렬화 규약은 [43-savedata-json-codec](../../21-savedata-system/43-savedata-json-codec/SKILL.md)를 따른다.


## 계정 Upgrade (Guest/Editor → Social)
- `LoginAsync`가 내부적으로 Anonymous → Social 업그레이드를 처리한다.
- Anonymous 유저가 존재하면 `signInOrLinkFirebaseCredentialAsync` 경로에서 `LinkWithCredentialAsync`로 UID를 유지한다.
- 별도의 Upgrade 전용 메서드는 없다 — `LoginAsync(LoginType, CancellationToken)`을 직접 호출한다.
- **Sync는 caller 책임** — AccountManager는 Sync를 호출하지 않는다.


## Location
- MobileSystem 번들 샘플 내부, 단일 asmdef(`Devian.Samples.MobileSystem`)에 포함되어 함께 설치된다.
- UPM: `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Account/AccountManager.cs`
- UnityExample mirror: `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/Account/AccountManager.cs`
- [50-mobile-system overview](../../00-overview/SKILL.md)


## Out of Scope
- Apple Sign-in UI/네이티브 토큰 획득 — AccountManager는 Apple 토큰을 "받아서" Firebase Auth에 연결만 한다.
- Sync 오케스트레이션 — SaveDataManager가 담당
- SaveSystem 샘플(CloudSave/LocalSave) 로직 변경
