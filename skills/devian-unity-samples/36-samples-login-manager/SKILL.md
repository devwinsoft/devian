# devian-unity-samples — Login Manager


## Scope
- Firebase Auth 기반 로그인 오케스트레이터.
- Editor/Guest는 Firebase Anonymous를 사용한다.
- 실기기에서는 Guest + Google(Android) + Apple(iOS)을 지원한다.
- Google/Apple은 caller(UI/네이티브)에서 토큰을 받아 LoginManager에 전달한다.
- Anonymous → Google/Apple은 `LinkWithCredentialAsync`로 UID를 유지한다.


## Login Type (Platform)
- **Editor Login**: Firebase Anonymous
- **Guest Login (device)**: Firebase Anonymous
- **Google Login (Android device)**: Sign-in/link to Firebase using Google credential
- **Apple Login (iOS device)**: Sign-in/link to Firebase using Apple credential


## Order (fixed)
1. Firebase dependencies init (`CheckAndFixDependenciesAsync`)
2. FirebaseAuth sign-in (Anonymous / credential / link — LoginType에 따라 분기)
3. `CloudSaveManager.Instance.InitializeAsync(ct)`
4. (Sync — not implemented yet, entry point only)


## LoginCredential
- Caller(UI / native sign-in)가 토큰을 획득해서 `LoginCredential`에 담아 전달한다.
- Guest/Editor: `LoginCredential.Empty()` 또는 `null`
- Google(Android): `IdToken` 필수, `AccessToken` 선택
- Apple(iOS): `IdToken` + `RawNonce` 필수


## Location
- CloudClientSystem 번들 샘플 내부, 단일 asmdef(`Devian.Samples.CloudClientSystem`)에 포함되어 함께 설치된다.
- UPM: `framework-cs/upm/com.devian.samples/Samples~/CloudClientSystem/Runtime/Login/LoginManager.cs`
- UnityExample mirror: `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/CloudClientSystem/Runtime/Login/LoginManager.cs`
- [30-samples-cloud-client-system](../30-samples-cloud-client-system/SKILL.md)


## Out of Scope
- UI/네이티브 Sign-in 구현(토큰 획득) — LoginManager는 토큰을 "받아서" Firebase Auth에 연결만 한다.
- Sync(merge/resolve) 구현 — 진입점만 유지
- SaveSystem 샘플(CloudSave/LocalSave) 로직 변경
