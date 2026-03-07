# 11-mobile-application — MobileApplication (Bootstrap Sample)


Status: ACTIVE
AppliesTo: v10


## Purpose
MobileApplication 기반 부트스트랩 샘플.
`ApplicationManager`을 상속한 추상 클래스 `MobileApplication`을 제공하여, 앱별 초기화 로직의 진입점을 정의한다.


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
        protected override async Task OnBootProc()
        {
            // MobileSystem common initialization (Log, GPGS Activate, AccountManager)
            await base.OnBootProc();

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
2. `OnBootProc()`을 override하고, `await base.OnBootProc();`을 호출하여 공통 초기화를 수행한다.
3. `base.OnBootProc()` 이후에 앱별 초기화 로직을 구현한다.
4. 포그라운드 복귀 처리가 필요하면 `ApplicationManager.OnEnterForeground()`를 override한다.
5. Bootstrap prefab에 해당 컴포넌트를 부착한다.
6. app/contents layer가 Bootstrap prefab을 명시적으로 생성하고 `BootProc()`를 호출한다.
7. 샘플에서는 Firebase Functions region 같은 앱 설정값을 `MobileApplication`에 하드코딩하고, `MissionManager`/`PurchaseManager` 같은 하위 manager에 setter로 주입한다.

주의:
- Unity `OnApplicationPause` / `OnApplicationFocus`를 직접 override하지 않는다.
- lifecycle 처리는 `ApplicationManager`의 semantic hook을 사용한다.
- manager가 inspector/serialized field로 Firebase region 같은 앱 설정을 직접 소유하지 않는다. 설정 owner는 bootstrap/app layer다.

foreground 복귀 기준 동작:
- `MissionManager.RefreshClockAsync(...)`
- refresh 성공 시 `LeaderboardSeasonRewardManager.SyncSeasonTransitionRewardsAsync(...)` best-effort 호출


## Resource Prefab 생성 규칙

- Bootstrap prefab 경로: `Assets/Resources/Devian/Bootstrap.prefab`
- prefab에 `MobileApplication` 파생 컴포넌트를 **정확히 1개** 부착해야 한다.
- 프레임워크가 파생 컴포넌트를 자동 추가하지 않는다 — 개발자가 직접 추가해야 한다.
- `InputManager` 같은 `CompoSingleton` 의존성도 bootstrap prefab/object에 미리 부착해야 한다.
- prefab은 수동으로 생성한다 (자동 생성 코드 없음).


## VersionCheck

`MobileApplication`은 서버에서 받은 버전 정보와 `ApplicationManager.AppVersion`을 비교하여 업데이트 필요 여부를 판정한다.

### VersionCheckResult

```csharp
public enum VersionCheckResult
{
    Success,
    RecommendUpdate,
    ForceUpdate,
}
```

- `Success`: 현재 버전이 최신이거나 문제 없음
- `RecommendUpdate`: `currentVersion` 미만. 플레이 가능하지만 업데이트 권고
- `ForceUpdate`: `minVersion` 미만. 해당 버전 이상으로 업데이트 필수 (플레이 불가)

### VersionCheck() 메서드

```csharp
public VersionCheckResult VersionCheck()
```

동작:
1. `MissionManager.TryGet()`으로 MissionManager 접근
2. `MissionManager.Storage.clockSnapshot`에서 `minVersion`, `currentVersion` 읽기
3. `AppVersion < minVersion`이면 `ForceUpdate`
4. `AppVersion < currentVersion`이면 `RecommendUpdate`
5. 그 외 `Success`
6. MissionManager가 없거나 snapshot이 null이면 `Success` 반환

서버 버전 정보는 `getMissionClock` Firebase Callable 응답에 포함된다.
`MissionManager.InitializeAsync()` 또는 `RefreshClockAsync()` 이후에 호출해야 유효한 결과를 얻는다.

### Implementation Location (3-path mirror)

- UPM (정본): `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Bootstrap/VersionCheckResult.cs`
- Packages (sync): 위와 동일 경로 (Packages mirror)
- Assets/Samples (import): 위와 동일 경로 (Assets/Samples mirror)


## RequireComponent

MobileApplication에 부착된 RequireComponent:

- `AccountManager`
- `InventoryManager`
- `PurchaseManager`
- `AchieveManager`
- `MissionManager`
- `LeaderboardManager`
- `LeaderboardSeasonRewardManager`
- `GameMessageManager`
- `SaveDataManager`
- `InputManager` — [24-input-manager](../../20-domain-common-system/22-input-manager/SKILL.md)
- `FirebaseManager` — [23-firebase-manager](../23-firebase-manager/SKILL.md)


## Links
- [16-base-application](../../20-domain-common-system/14-application-manager/SKILL.md) — ApplicationManager 런타임 스펙
- [24-input-manager](../../20-domain-common-system/22-input-manager/SKILL.md) — InputManager 공용 입력 관리자
- [21-savedata-system/43-savedata-json-codec](../21-savedata-system/43-savedata-json-codec/SKILL.md) — SaveData JSON 직렬화 규약
- [50-leaderboard/13-leaderboard-season-reward-manager](../50-leaderboard/13-leaderboard-season-reward-manager/SKILL.md) — 시즌 보상 sync 흐름
- [50-mobile-system overview](../00-overview/SKILL.md) — MobileSystem (Devian Samples) 그룹 개요
