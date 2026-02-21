# 35-account-login-firebase — AccountLoginFirebase (Anonymous Sign-in only)


## Scope
- Firebase Auth의 Anonymous sign-in(익명 로그인)만 샘플 매니저로 제공한다.
- 이 샘플은 Google/Apple/Facebook 로그인 기능을 포함하지 않는다.
- AccountManager의 Guest/Editor 로그인 경로에서 이 매니저를 사용한다.


## Locations (mirrored)
- UPM:
  - `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Account/AccountLoginFirebase.cs`
- UnityExample (Packages mirror):
  - `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/Account/AccountLoginFirebase.cs`
- UnityExample (Assets mirror):
  - `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/0.1.0/MobileSystem/Runtime/Account/AccountLoginFirebase.cs`


## Public API (runtime)
- `Task<CoreResult<bool>> InitializeAsync(CancellationToken ct)`
- `Task<CoreResult<string>> SignInAnonymouslyAsync(CancellationToken ct)`  // returns uid
- `CoreResult<bool> SignOut()`


## Usage (AccountManager)
```csharp
// Guest/Editor login:
await AccountManager.Instance.LoginAsync(LoginType.GuestLogin, CancellationToken.None);

// AccountManager가 new AccountLoginFirebase()를 소유하고 이를 통해 SignInAnonymouslyAsync / SignOut을 호출한다.
```


## Notes
- Firebase SDK가 프로젝트에 설치되어 있어야 컴파일된다(FirebaseApp, FirebaseAuth 사용).
- Firebase 의존을 "AccountManager의 Guest 경로"에만 두고, GPGS/Apple 로그인 매니저는 Firebase와 독립 유지한다.
