# 10-service — UIToastService

Status: ACTIVE
AppliesTo: v11

---

## 목적

Toast system의 **단일 진입점**.
`AutoSingleton`으로 등록되며, canvas bootstrap과 show 요청을 모두 담당한다.

---

## SSOT

### Code Path

```
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Toast/UIToastService.cs
```

### Class Signature

```csharp
namespace Devian
{
    public sealed class UIToastService : AutoSingleton<UIToastService>
    {
        public UISettings Settings { get; }
        public ToastGroupConfig[] GetGroupConfigs();

        /// UISettings에서 canvas ID를 읽어 UIToastCanvas를 spawn/init한다.
        /// MobileApplication.onLoadCompletedAsync()에서 1회 호출.
        public void Initialize();

        public void Show(
            string message,
            string groupId = UIToastDefaults.DefaultGroupId,
            float? durationOverride = null,
            ToastType toastType = ToastType.Info);

        public void Show(ToastRequest request);
    }
}
```

---

## Initialize 동작

`MobileApplication.onLoadCompletedAsync()` → `UIToastService.Instance.Initialize()` 호출.

```
1. UIToastCanvas.Instance 조회
2. null이면 FindAnyObjectByType<UIToastCanvas>(FindObjectsInactive.Include)
3. 여전히 null이면:
   a. ResolveSettings() → settings.ToastCanvasId 읽기
   b. IsValid 검사 (invalid이면 return)
   c. BundlePool.Spawn<UIToastCanvas>(canvasId)
4. DontDestroyOnLoad(canvas)
5. canvas.isInitialized가 false이면 canvas.Init()
```

---

## Settings 동작

```
1. Resources.Load<UISettings>(UISettings.ResourcesPath)
2. 없으면 warning 1회 출력
3. service 내부에 cache
4. panel은 GetGroupConfigs()를 통해 전역 group 설정을 조회
5. settings null 또는 empty면 panel 쪽 default group fallback 사용
```

---

## ResolvePanel 동작

```
1. UIToastCanvas.Instance 조회
2. null이면 FindAnyObjectByType<UIToastCanvas>(FindObjectsInactive.Include)
3. canvas가 null이면 → return null
4. canvas.isInitialized && canvas.isInitComplete 모두 true인지 확인
5. canvas.panel이 null이면 → LogWarning, return null
6. return canvas.panel
```

panel이 null이면 toast 요청은 **silent skip** (LogWarning만 출력).

---

## Reference

- Overview: `../00-overview/SKILL.md`
- Canvas/Panel: `../11-canvas-panel/SKILL.md`
- Data Model: `../14-data-model/SKILL.md`
