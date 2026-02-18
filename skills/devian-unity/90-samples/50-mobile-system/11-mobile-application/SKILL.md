# 11-mobile-application — MobileApplication (Bootstrap Sample)


Status: ACTIVE
AppliesTo: v10


## Purpose
MobileApplication 기반 부트스트랩 샘플.
`BaseBootstrap`을 상속한 추상 클래스 `MobileApplication`을 제공하여, 앱별 초기화 로직의 진입점을 정의한다.


## Sample SSOT
- `com.devian.samples/Samples~/MobileSystem`


## Locations (mirrored)
- UPM: `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Bootstrap/MobileApplication.cs`
- Packages: `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/Bootstrap/MobileApplication.cs`
- Assets: `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/0.1.0/MobileSystem/Runtime/Bootstrap/MobileApplication.cs`


## Usage

```csharp
namespace MyApp
{
    public sealed class MyApp : MobileApplication
    {
        protected override System.Collections.IEnumerator OnBootProc()
        {
            // MobileSystem common initialization (Log, GPGS Activate, AccountManager)
            yield return base.OnBootProc();

            // App-specific initialization here.
            yield break;
        }
    }
}
```

1. `MobileApplication`을 상속한 클래스를 만든다.
2. `OnBootProc()`을 override하고, `yield return base.OnBootProc();`을 호출하여 공통 초기화를 수행한다.
3. `base.OnBootProc()` 이후에 앱별 초기화 로직을 구현한다.
4. Bootstrap prefab에 해당 컴포넌트를 부착한다.


## Resource Prefab 생성 규칙

- Bootstrap prefab 경로: `Assets/Resources/Devian/Bootstrap.prefab`
- prefab에 `MobileApplication` 파생 컴포넌트를 **정확히 1개** 부착해야 한다.
- 프레임워크가 파생 컴포넌트를 자동 추가하지 않는다 — 개발자가 직접 추가해야 한다.
- prefab은 수동으로 생성한다 (자동 생성 코드 없음).


## Links
- [16-bootstrap](../../10-foundation/16-bootstrap/SKILL.md) — BaseBootstrap 런타임 스펙
- [50-mobile-system overview](../00-overview/SKILL.md) — MobileSystem (Devian Samples) 그룹 개요
