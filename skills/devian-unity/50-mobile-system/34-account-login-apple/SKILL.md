# 34-account-login-apple — AccountLoginApple (Apple Sign-in + iCloud Storage)


## 범위
- iOS 기반:
  - Apple Sign-in (플러그인 의존 가능: Reflection 기반으로 컴파일 안전 처리)
  - iCloud Storage (UnityEngine.iOS.iCloud 기반 Key-Value 저장/로드)
- 중복/스텁 로직을 신규 `AccountLoginApple`로 점진 치환한다.


## 파일 위치(미러 구조)
- UPM: `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Account/AccountLoginApple.cs`
- Example: `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/0.1.0/MobileSystem/Runtime/Account/AccountLoginApple.cs`


## 핵심 정책
- iOS 런타임 외에서는 `NotAvailable`로 안전 처리한다.
- Apple Sign-in은 프로젝트에 도입된 플러그인(예: AppleAuth) 유무에 따라 Reflection으로 호출하고, 없으면 NotAvailable 처리한다.
- 기존 `AppleSaveCloudClient`(stub)는 `AccountLoginApple` 기반 구현으로 치환한다.
- 완료 후 기존 스텁/중복 로직은 삭제(리팩터링)한다.


## 사용 예시


### 1) Apple Sign-in
```csharp
var signIn = await AccountLoginApple.Instance.SignInAsync(ct);
if (signIn.IsFailure) { /* 처리 */ }
// 성공 시 idToken/rawNonce 등을 획득해 백엔드 로그인/계정 연결에 사용(구현체는 프로젝트 선택)
```


### 2) iCloud Storage (CloudSave payload 저장/로드)
```csharp
await AccountLoginApple.Instance.SignInIfNeededAsync(ct);
await AccountLoginApple.Instance.SaveAsync(slot, payload, ct);

var (res, loaded) = await AccountLoginApple.Instance.LoadAsync(slot, ct);
```


## 체크리스트(DoD)

- UPM/Example 경로에 동일한 AccountLoginApple.cs 존재
- AppleSaveCloudClient가 더 이상 NotAvailable stub이 아니고 AccountLoginApple 호출 기반으로 동작
- iOS 분기(Installer/SaveCloudManager)의 기본 클라이언트 선택이 Apple(iCloud)로 동작하도록 정리됨
- 컴파일 에러 없음
