# devian-unity-samples — Login Manager


## Scope
- Firebase Auth 기반 로그인 오케스트레이터.
- Editor/Guest는 Firebase Anonymous를 사용한다.
- 실기기에서는 Guest + Google(Android) + Apple(iOS)을 지원한다.
- Google(Android)는 GPGS(Google Play Games Services) 인증을 Reflection으로 내부 획득하며, `PlayGamesAuthProvider.GetCredential(serverAuthCode)`로 Firebase에 연결한다.
- Apple(iOS)은 caller(UI/네이티브)에서 IdToken + RawNonce를 받아 LoginManager에 전달한다.
- Anonymous → Google/Apple은 `LinkWithCredentialAsync`로 UID를 유지한다.


## Login Type (Platform)
- **Editor Login**: Firebase Anonymous
- **Guest Login (device)**: Firebase Anonymous
- **Google Login (Android device)**: GPGS Reflection → `PlayGamesAuthProvider.GetCredential(serverAuthCode)` → Firebase sign-in / link
- **Apple Login (iOS device)**: Sign-in/link to Firebase using Apple credential (caller-provided IdToken + RawNonce)


## Order (fixed)
1. Firebase dependencies init (`CheckAndFixDependenciesAsync`)
2. FirebaseAuth sign-in (Anonymous / credential / link — LoginType에 따라 분기)
3. `CloudSaveManager.Instance.InitializeAsync(ct)`
4. (Sync — not implemented yet, entry point only)


## LoginCredential
- Guest/Editor: `LoginCredential.Empty()` 또는 `null`
- Google(Android): `ServerAuthCode` 필수 (convenience 오버로드 사용 시 GPGS Reflection으로 내부 획득)
- Apple(iOS): `IdToken` + `RawNonce` 필수 (caller가 직접 전달)

## Convenience Overload
- `LoginAsync(LoginType, CancellationToken)` — credential을 내부에서 획득한다.
  - Editor/Guest: `LoginCredential.Empty()` 자동 생성
  - Google(Android): `getGoogleGpgsCredentialAsync` → GPGS Reflection으로 `PlayGamesPlatform.Authenticate` + `RequestServerSideAccess` 호출
  - Apple(iOS): 지원하지 않음 — `LoginAsync(LoginType, LoginCredential, CancellationToken)` 사용

## Google GPGS Internal Acquisition
- `getGoogleGpgsCredentialAsync(CancellationToken)` — Reflection 기반, GPGS 플러그인 미설치 시 컴파일 안전.
- Flow: `PlayGamesPlatform.Instance` → `Authenticate(Action<bool>)` → `RequestServerSideAccess(false, Action<string>)` → `ServerAuthCode`
- `GoogleAuthProvider.GetCredential(idToken, accessToken)` 는 더 이상 사용하지 않는다.
- `PlayGamesAuthProvider.GetCredential(serverAuthCode)` 만 사용한다.


## Location
- CloudClientSystem 번들 샘플 내부, 단일 asmdef(`Devian.Samples.CloudClientSystem`)에 포함되어 함께 설치된다.
- UPM: `framework-cs/upm/com.devian.samples/Samples~/CloudClientSystem/Runtime/Login/LoginManager.cs`
- UnityExample mirror: `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/CloudClientSystem/Runtime/Login/LoginManager.cs`
- [30-samples-cloud-client-system](../30-samples-cloud-client-system/SKILL.md)


## Out of Scope
- Apple Sign-in UI/네이티브 토큰 획득 — LoginManager는 Apple 토큰을 "받아서" Firebase Auth에 연결만 한다.
- Sync(merge/resolve) 구현 — 진입점만 유지
- SaveSystem 샘플(CloudSave/LocalSave) 로직 변경
