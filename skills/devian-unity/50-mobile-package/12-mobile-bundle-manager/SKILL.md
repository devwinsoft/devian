# 12-mobile-bundle-manager

Status: ACTIVE
AppliesTo: v1

---

## 목적

MobilePackage 레이어의 BundleManager 중간 추상 클래스.
`BundleManager<T>`의 labels 파라미터를 `PatchLabels` abstract property로 캡슐화하고,
파라미터 없는 `InitializeAsync()` / `DownloadAsync()`를 제공한다.
`onLoadBundlesAsync()`에서 UI transition preset을 직접 사전 로드한다.

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
        protected abstract IReadOnlyList<string> PatchLabels { get; }

        public Task<CommonResult<PatchInfo>> InitializeAsync();
        public Task<CommonResult> DownloadAsync(Action<float>? onProgress = null);

        // BundleManager<T>.onLoadBundlesAsync override
        // UI transition preset preload 직접 수행
        protected override Task onLoadBundlesAsync(SystemLanguage language, Action<float>? onProgress = null);
    }
}
```

---

## API

### PatchLabels

concrete 서브클래스가 정의하는 패치/다운로드 대상 라벨 목록.
`InitializeAsync()` / `DownloadAsync()`는 이 목록을 `base.InitializeAsync(labels)` / `base.DownloadAsync(labels, ...)` 에 전달한다.

### InitializeAsync()

`PatchLabels` 기준으로 `base.InitializeAsync(PatchLabels)` 호출.
파라미터 없는 편의 메서드.

### DownloadAsync(onProgress)

`PatchLabels` 기준으로 `base.DownloadAsync(PatchLabels, onProgress)` 호출.

### onLoadBundlesAsync(language, onProgress)

`UIManager.Instance.LoadBundlesAsync()`를 호출하여 UI 번들 에셋(transition preset + UI GameObject)을 로드한다.
concrete 서브클래스는 `base.onLoadBundlesAsync()` 호출 후 테이블/에셋 로드를 수행한다.

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
}
```

---

## Reference

- Parent: `skills/devian-unity/50-mobile-package/SKILL.md`
- BundleManager base: `skills/devian-unity/20-common-package/19-bundle-manager/SKILL.md`
