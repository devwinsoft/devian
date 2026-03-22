# 12-mobile-bundle-manager

Status: ACTIVE
AppliesTo: v1

---

## 목적

MobilePackage 레이어의 BundleManager 중간 추상 클래스.
`LoadBundlesAsync()`에서 `UIManager.Instance.LoadBundlesAsync()`를 호출하여 UI 번들 에셋을 로드한다.

---

## 상속 구조

```
BundleManager<T> : CompoSingleton<T>                        (CommonPackage, abstract)
    └── MobileBundleManager<T> : BundleManager<T>            (MobilePackage, abstract)
            └── TestBundleManager : MobileBundleManager<TestBundleManager>  (App layer, concrete)
```

---

## 소스 경로

| 위치 | 경로 |
|------|------|
| UPM 소스 | `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/BundleManager/MobileBundleManager.cs` |

---

## Class Shape

```csharp
namespace Devian
{
    public abstract class MobileBundleManager<T> : BundleManager<T> where T : MobileBundleManager<T>
    {
        // BundleManager<T>.LoadBundlesAsync override
        // UIManager.Instance.LoadBundlesAsync() 호출
        public override Task LoadBundlesAsync(SystemLanguage language, Action<float>? onProgress = null);
    }
}
```

PatchLabels, InitializeAsync, DownloadAsync는 `BundleManager<T>`가 담당한다.

---

## API

### LoadBundlesAsync(language, onProgress)

`UIManager.Instance.LoadBundlesAsync()`를 호출하여 UI 번들 에셋(transition preset + UI GameObject)을 로드한다.
concrete 서브클래스는 `base.LoadBundlesAsync()` 호출 후 테이블/에셋 로드를 수행한다.

---

## Concrete 서브클래스 예시

```csharp
public class TestBundleManager : MobileBundleManager<TestBundleManager>
{
    protected override IReadOnlyList<string> PatchLabels => new string[]
    {
        "common-effects", "prefabs", "scenes", "sounds", "ui",
        // editor/build 분기
    };

    public override async Task LoadBundlesAsync(SystemLanguage language, Action<float>? onProgress = null)
    {
        await base.LoadBundlesAsync(language, onProgress);
        // 테이블/에셋 로드 ...
    }
}
```

---

## Reference

- Parent: `skills/devian-unity/50-mobile-package/SKILL.md`
- BundleManager base: `skills/devian-unity/20-common-package/19-bundle-manager/SKILL.md`
