# 11-mobile-application — MobileApplication (Bootstrap Sample)


Status: ACTIVE
AppliesTo: v10


## Purpose
MobileApplication 기반 부트스트랩 샘플.
`BaseApplication`을 상속한 추상 클래스 `MobileApplication`을 제공하여, 앱별 초기화 로직의 진입점을 정의한다.


## Sample SSOT
- `com.devian.samples/Samples~/MobileSystem`


## Implementation Location (3-path mirror)

> 3-path mirror 정책: [devian-unity/07-samples-creation-guide](../../07-samples-creation-guide/SKILL.md)

- UPM (정본): `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Bootstrap/MobileApplication.cs`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/Bootstrap/MobileApplication.cs`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/Bootstrap/MobileApplication.cs`


## Usage

```csharp
namespace MyApp
{
    public sealed class MyApp : MobileApplication
    {
        protected override async Task onBootAsync()
        {
            // MobileSystem common initialization (Log, GPGS Activate, Account/Login managers)
            await base.onBootAsync();

            // App-specific initialization here.
        }

        protected override void OnEnterForeground()
        {
            // Resume sync here.
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
7. 샘플에서는 Firebase Functions region 같은 앱 설정값을 `MobileApplication`에 하드코딩하고, `RemoteConfigManager`/`PurchaseManager` 같은 하위 manager에 전달한다.

주의:
- Unity `OnApplicationPause` / `OnApplicationFocus`를 직접 override하지 않는다.
- lifecycle 처리는 `BaseApplication`의 semantic hook을 사용한다.
- manager가 inspector/serialized field로 Firebase region 같은 앱 설정을 직접 소유하지 않는다. 설정 owner는 bootstrap/app layer다.

foreground 복귀 기준 동작:
- `RemoteConfigManager.RefreshAsync(...)`
- refresh 성공 시 `LeaderboardManager.SyncSeasonTransitionRewardsAsync(...)` best-effort 호출

onLoadCompletedAsync:
- 리소스 로딩 완료 후, 서버와 별개로 독립적으로 동작하는 Manager를 초기화한다.
- `GameMessageManager.Instance.Initialize()` — 로컬 테이블 바인딩 (서버 무관)


## Resource Prefab 생성 규칙

- Application prefab 경로: `Assets/Resources/Devian/Application.prefab`
- prefab에 `MobileApplication` 파생 컴포넌트를 **정확히 1개** 부착해야 한다.
- 프레임워크가 파생 컴포넌트를 자동 추가하지 않는다 — 개발자가 직접 추가해야 한다.
- `InputManager` 같은 `CompoSingleton` 의존성도 bootstrap prefab/object에 미리 부착해야 한다.
- prefab은 수동으로 생성한다 (자동 생성 코드 없음).


## Version Check Ownership

- `MobileApplication`은 버전 판정 API를 소유하지 않는다.
- 버전 체크는 `LoginManager`가 진입 초기 단계에서 수행한다.
- 관련 타입/메서드는 [24-login-manager](../24-login-manager/SKILL.md)를 따른다.


## RequireComponent

MobileApplication에 부착된 RequireComponent:

- `AccountManager`
- `InventoryManager`
- `PurchaseManager`
- `AchieveManager`
- `MissionManager`
- `RemoteConfigManager`
- `LeaderboardManager`
- `GameMessageManager`
- `LoginManager`
- `SaveDataManager`
- `InputManager` — [24-input-manager](../../20-domain-common-system/22-input-manager/SKILL.md)
- `FirebaseCallableManager` — [23-firebase-callable-manager](../23-firebase-callable-manager/SKILL.md)


## Links
- [16-base-application](../../20-domain-common-system/14-base-application/SKILL.md) — BaseApplication 런타임 스펙
- [24-input-manager](../../20-domain-common-system/22-input-manager/SKILL.md) — InputManager 공용 입력 관리자
- [21-savedata-system/43-savedata-json-codec](../21-savedata-system/43-savedata-json-codec/SKILL.md) — SaveData JSON 직렬화 규약
- [50-leaderboard/13-leaderboard-season-reward-manager](../50-leaderboard/13-leaderboard-season-reward-manager/SKILL.md) — 시즌 보상 sync 흐름
- [50-mobile-system overview](../00-overview/SKILL.md) — MobileSystem (Devian Samples) 그룹 개요
