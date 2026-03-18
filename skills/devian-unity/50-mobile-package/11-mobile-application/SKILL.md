# 11-mobile-application — MobileApplication (Bootstrap Sample)


Status: ACTIVE
AppliesTo: v10


## Purpose
MobileApplication 기반 부트스트랩 샘플.
`BaseApplication`을 상속한 추상 클래스 `MobileApplication`을 제공하여, 앱별 초기화 로직의 진입점을 정의한다.


## Sample SSOT
- `com.devian.foundation/Samples~/MobilePackage`


## Implementation Location (3-path mirror)

- UPM (정본): `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/Application/MobileApplication.cs`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/Application/MobileApplication.cs`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/{version}/Mobile Package/Runtime/Application/MobileApplication.cs`


## Usage

```csharp
namespace MyApp
{
    public sealed class MyApp : MobileApplication
    {
        protected override async Task onBootAsync()
        {
            // MobilePackage common initialization (Log, GPGS Activate, Account/Login managers)
            await base.onBootAsync();

            // App-specific initialization here.
        }

    }
}
```

1. `MobileApplication`을 상속한 클래스를 만든다.
2. `onBootAsync()`을 override하고, `await base.onBootAsync();`을 호출하여 공통 초기화를 수행한다.
3. `base.onBootAsync()` 이후에 앱별 초기화 로직을 구현한다.
4. 포그라운드 복귀 처리가 필요하면 `BaseApplication.OnEnterForeground()`를 override한다.
5. Bootstrap prefab에 해당 컴포넌트를 부착한다.
6. app/contents layer가 Bootstrap prefab을 명시적으로 생성하고 `BootProc()`를 호출한다.

주의:
- Unity `OnApplicationPause` / `OnApplicationFocus`를 직접 override하지 않는다.
- lifecycle 처리는 `BaseApplication`의 semantic hook을 사용한다.
- manager가 inspector/serialized field로 Firebase region 같은 앱 설정을 직접 소유하지 않는다. 설정 owner는 bootstrap/app layer다.

onLoadCompletedAsync:
- 리소스 로딩 완료 후, 서버와 별개로 독립적으로 동작하는 Manager를 초기화한다.
- `GameMessageManager.Instance.Initialize()` — 로컬 테이블 바인딩 (서버 무관)


## Resource Prefab 생성 규칙

- Application prefab 경로: `Assets/Resources/Devian/Application.prefab`
- prefab에 `MobileApplication` 파생 컴포넌트를 **정확히 1개** 부착해야 한다.
- 프레임워크가 파생 컴포넌트를 자동 추가하지 않는다 — 개발자가 직접 추가해야 한다.
- `InputManager` 같은 `CompoSingleton` 의존성도 bootstrap prefab/object에 미리 부착해야 한다.
- prefab은 수동으로 생성한다 (자동 생성 코드 없음).


## Default Language Ownership

- `MobileApplication`은 `DefaultLanguage` (`SystemLanguage`) 설정값 owner다.
- Inspector에서 `AppVersion`(BaseApplication) 직후에 노출된다.
- 파생 클래스가 `onLoadAsync()`에서 `DefaultLanguage`를 직접 참조하여 리소스 로딩 언어를 결정한다.
- Push 토픽 등 언어별 분기가 필요한 매니저가 이 값을 참조한다.

## Version Check Ownership

- `MobileApplication`은 버전 체크 URL 설정(`VersionCheckAOS`, `VersionCheckIOS`)을 소유한다.
- URL은 `raw.githubusercontent.com` 등 순수 JSON을 반환하는 endpoint여야 한다.
- GitHub blob URL(`github.com/.../blob/...`)은 HTML을 반환하므로 사용 금지.
- 실제 버전 판정/서버 UTC 동기화와 상태(property) 보유는 `RemoteDataManager`가 수행한다.
- 로그인 진입 시 `LoginManager`는 `RemoteDataManager.InitializeAsync`를 가장 먼저 호출한다.

## Custom Editor (MobileApplicationEditor)

- `MobileApplicationEditor.cs` — `MobileApplication`의 Custom Editor.
- **Fix URL 섹션**: 항상 표시된다.
  - URL이 GitHub blob URL이면: 경고 HelpBox + [Fix URL] 버튼을 표시한다.
    - `github.com/{user}/{repo}/blob/{branch}/...` → `raw.githubusercontent.com/{user}/{repo}/{branch}/...` 로 자동 변환.
  - URL이 정상이면: 정보 HelpBox로 "VersionCheck URL 상태: 정상"을 표시한다.
- **Generate key iv 버튼**: AES-256-CBC 키/IV를 랜덤 생성하여 `_cryptoKey`/`_cryptoIv`에 설정.

## Firebase Functions Region Ownership

- `MobileApplication`은 `FirebaseFunctionsRegion` 설정값 owner다.
- `MobileApplication`은 `FirebaseCallableManager`를 직접 초기화(`SetFunctionsRegion`)하지 않는다.
- `FirebaseCallableManager`가 `MobileApplication.Instance.FirebaseFunctionsRegion`을 참조해 자체 초기화한다.


## RequireComponent

MobileApplication에 부착된 RequireComponent:

- `AccountManager`
- `InventoryManager`
- `PurchaseManager`
- `AchieveManager`
- `MissionManager`
- `RemoteDataManager`
- `LeaderboardManager`
- `GameMessageManager`
- `LoginManager`
- `SaveDataManager`
- `InputManager`
- `FirebaseCallableManager`
