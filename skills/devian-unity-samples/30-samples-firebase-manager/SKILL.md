# devian-unity-samples — FirebaseManager


## Scope
- Firebase 초기화 및 Auth(Anonymous/Google/Apple/Facebook) 공통 흐름을 Devian 샘플 매니저로 제공한다.
- 이 샘플은 특정 제공자(Anonymous) 전용이 아니라, Firebase Auth의 공통 사용 패턴을 보여준다.


## Dependencies
- FirebaseManager는 Firebase/Google/Apple 의존이 있는 환경에서 사용하는 매니저이며, 해당 SDK/플러그인이 프로젝트에 설치되어 있어야 한다.
- Facebook은 API/흐름만 설계되어 있으며, 현재는 `FIREBASE_FACEBOOK_NOT_IMPLEMENTED`로 실패를 반환한다.


## Error handling
- FirebaseManager의 실패 코드는 `ErrorClientType.FIREBASE_*` enum만 사용한다.
- `"firebase.xxx"` 같은 문자열 코드와 `CoreError.Details`는 사용하지 않는다. (details는 버리고 enum만 남긴다)
- 필요한 분기는 enum 항목을 `ERROR_CLIENT` 테이블에 추가하는 방식으로만 확장한다.


## Assembly Definition (asmdef) and References
- FirebaseManager는 샘플 코드로서 `Devian.Samples.AuthFirebase` asmdef에 포함된다.
  - UPM: `framework-cs/upm/com.devian.samples/Samples~/Auth-Firebase/Runtime/Devian.Samples.AuthFirebase.asmdef`
  - UnityExample mirror: `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/Auth-Firebase/Runtime/Devian.Samples.AuthFirebase.asmdef`
- Firebase SDK는 프로젝트에 설치되어 있어야 한다(FirebaseApp/FirebaseAuth 사용).
- UPM Samples UI 표시는 `com.devian.samples/package.json`의 `samples[].displayName/description`를 따른다. Auth-Firebase 샘플은 `(Anonymous)`가 아닌 일반 Firebase Auth 샘플로 표시된다.


## Locations (mirrored)
- UPM:
  - `framework-cs/upm/com.devian.samples/Samples~/Auth-Firebase/Runtime/FirebaseManager.cs`
- UnityExample mirror:
  - `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/Auth-Firebase/Runtime/FirebaseManager.cs`


## Public API (runtime)
- `Task<CoreResult<bool>> InitializeAsync(CancellationToken ct)`
- `Task<CoreResult<string>> SignInAnonymouslyAsync(CancellationToken ct)`
- `Task<CoreResult<string>> SignInWithGoogleAsync(string idToken, CancellationToken ct)`
- `Task<CoreResult<string>> SignInWithAppleAsync(CancellationToken ct)`
- `Task<CoreResult<string>> SignInWithFacebookAsync(CancellationToken ct)` (designed only; not implemented)
- `Task<CoreResult<string>> GetIdTokenAsync(bool forceRefresh, CancellationToken ct)`
- `Task<CoreResult<string>> GetIdToken(CancellationToken ct)` (convenience; may re-login based on current type)
- `CoreResult<bool> SignOut()`


## Usage (example)
```csharp
using System.Threading;
using UnityEngine;
using Devian;

public sealed class FirebaseAuthFlowExample : MonoBehaviour
{
    private async void Start()
    {
        var ct = CancellationToken.None;

        var init = await FirebaseManager.Instance.InitializeAsync(ct);
        if (init.IsFailure)
        {
            Debug.LogError(init.Error?.Message);
            return;
        }

        var signIn = await FirebaseManager.Instance.SignInAnonymouslyAsync(ct);
        if (signIn.IsFailure)
        {
            Debug.LogError(signIn.Error?.Message);
            return;
        }

        Debug.Log($"Signed in uid={signIn.Value}");

        var token = await FirebaseManager.Instance.GetIdToken(ct);
        if (token.IsFailure)
        {
            Debug.LogError(token.Error?.Message);
            return;
        }

        Debug.Log($"Token len={token.Value?.Length ?? 0}");
    }
}
```


## Known Compile Errors (Fix)
- `CS0246: The type or namespace name 'Google' could not be found`
  - 원인: asmdef가 Google 플러그인 어셈블리를 참조하지 않음
  - Devian 해결 방침: Devian 패키지는 Google 플러그인에 직접 의존하지 않는다.
  - 해결: FirebaseManager에서 `using Google;` 및 `GoogleSignIn` 직접 호출을 제거하고,
    앱 레이어에서 얻은 `idToken`을 `SignInWithGoogleAsync(idToken, ...)`로 전달한다.
- `CS0433/CS0121` 대량 발생(Task/CancellationToken/Tuple 등)
  - 원인: `Unity.Tasks`/`Unity.Compat`가 `netstandard`/`mscorlib`과 타입 중복 충돌
  - 해결: 충돌을 유발한 패키지(예: UPM `google-signin-unity`)를 제거/롤백한다.
- `CS0712/CS0176: OAuthProvider`
  - `new OAuthProvider(...)`는 불가 (SDK에서 static 형태)
  - `OAuthProvider.GetCredential("apple.com", idToken, rawNonce)`처럼 정적 호출로 수정한다.
- `CS1061: AuthResult does not contain a definition for UserId` 또는 `FirebaseUser does not contain a definition for User`
  - 원인: Firebase Unity SDK 버전에 따라 `SignInAnonymouslyAsync()` / `SignInWithCredentialAsync()`의 반환 타입이 `AuthResult` 또는 `FirebaseUser`로 다를 수 있다.
  - Devian 해결: 로그인 결과에서 UserId를 추출하는 내부 로직이 두 형태를 모두 처리한다(옵션/컴파일 분기 없이).
