# 36-account-login-gpgs — AccountLoginGpgs (Sign-in + Cloud Save)


## 범위
- Google Play Games Services(GPGS) **v2** 기반 (assembly: `Google.Play.Games`):
  - Sign-in (`Action<SignInStatus>`)
  - Saved Games(Cloud Save) Load/Save/Delete
  - RequestServerSideAccess (`Action<AuthResponse>`)
- 기존 중복 로직을 신규 `AccountLoginGpgs`로 점진 치환한다.
- GPGS v1 (`GooglePlayGames` assembly)은 지원하지 않는다.


## 파일 위치(미러 구조)
- UPM: `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Account/AccountLoginGpgs.cs`
- Example: `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/0.1.0/MobileSystem/Runtime/Account/AccountLoginGpgs.cs`


## 핵심 정책
- **GPGS v2 전용** — `Google.Play.Games` 어셈블리만 참조한다.
- GPGS 플러그인이 없는 환경에서도 컴파일 가능해야 한다(Reflection 기반).
- Android 런타임 외에서는 `NotAvailable`로 안전하게 처리한다.
- 인증 시도 순서: `Authenticate`(silent) 우선, 실패 시 `ManuallyAuthenticate`(UI) fallback.
- AccountManager 로그인 경로에서는 `RequestServerSideAccess` auth code가 필수다. auth code가 비어 있으면 성공으로 처리하지 않고 실패로 반환한다.
- 구매 진입 자동보정 경로에서는 `GetServerAuthCodeCredentialSilentAsync`(silent-only, UI 없음)를 사용한다.
- 기존 기능(1) AccountManager의 GPGS auth code 획득, (2) SaveCloudClientGoogle의 Saved Games 호출을 `AccountLoginGpgs`로 모은다.
- 완료 후 기존 분산 로직은 삭제(리팩터링)한다.


## 사용 예시


### 1) 백엔드 로그인/계정 연결(서버 Auth Code 기반) — AccountManager에서 호출
```csharp
// before: AccountManager 내부에서 Reflection으로 auth code 획득
// after: AccountLoginGpgs.Instance.GetServerAuthCodeCredentialAsync(ct)

var credResult = await AccountLoginGpgs.Instance.GetServerAuthCodeCredentialAsync(ct);
if (credResult.IsFailure) return CoreResult<LoginCredential>.Failure(credResult.Error!);

// LoginCredential.ServerAuthCode를 사용해 백엔드 sign-in/link 로직 진행(구현체는 프로젝트 선택)
```


### 2) Cloud Save — SaveCloudManager/SaveCloudClientGoogle 내부에서 호출
```csharp
await AccountLoginGpgs.Instance.SignInIfNeededAsync(ct);

var (result, payload) = await AccountLoginGpgs.Instance.LoadAsync(slot, ct);
if (result == SaveCloudResult.Success) { /* payload 사용 */ }

await AccountLoginGpgs.Instance.SaveAsync(slot, payload, ct);
```


## 체크리스트(DoD)

- UPM/Example 경로에 동일한 AccountLoginGpgs.cs 존재
- AccountManager의 Google credential 획득 로직이 AccountLoginGpgs 호출로 치환됨
- SaveCloudClientGoogle의 Saved Games 핵심 로직이 AccountLoginGpgs 호출로 치환됨(중복 로직 삭제)
- 빌드 컴파일 에러 없음(플러그인 미설치 환경에서도 컴파일)
