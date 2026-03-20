# 10-service — UIToastService

Status: ACTIVE
AppliesTo: v11

---

## 목적

외부에서 toast를 요청하는 **단일 진입점**.
`AutoSingleton`으로 등록되며, 전역 `UIToastSettings`를 load/cache하고,
canvas 상태를 직접 관리하지 않고 `UIToastPanel`에 위임한다.

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
        public UIToastSettings Settings { get; }
        public ToastGroupConfig[] GetGroupConfigs();

        // 편의 오버로드 — 내부에서 ToastRequest 생성 후 Show(request) 위임
        public void Show(
            string message,
            string groupId = UIToastDefaults.DefaultGroupId,
            float? durationOverride = null,
            ToastType toastType = ToastType.Info);

        // 실제 처리 진입점
        public void Show(ToastRequest request);
    }
}
```

## Settings 동작

```
1. Resources.Load<UIToastSettings>(UIToastSettings.ResourcesPath)
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
3. canvas가 null이면 → LogWarning, return null
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
