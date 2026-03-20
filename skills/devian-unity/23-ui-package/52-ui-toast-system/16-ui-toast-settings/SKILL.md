# 16-ui-toast-settings — UIToastSettings

Status: ACTIVE
AppliesTo: v12

---

## 목적

toast group 설정을 `Resources` 기반 전역 asset으로 보관한다.
`UIToastService`가 이 asset을 load/cache하고, `UIToastPanel`은 여기서 읽은 group 설정으로 `UIToastGroup`을 생성한다.

---

## SSOT

### Code Path

```
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Toast/UIToastSettings.cs
```

### Asset Path

```
framework-cs/apps/UnityExample/Assets/Resources/Devian/UIToastSettings.asset
```

### Class Signature

```csharp
namespace Devian
{
    [CreateAssetMenu(fileName = "UIToastSettings", menuName = "Devian/UIPackage/UIToast Settings")]
    public sealed class UIToastSettings : ScriptableObject
    {
        public const string ResourcesPath = "Devian/UIToastSettings";
        public const string DefaultResourcesAssetPath = "Assets/Resources/Devian/UIToastSettings.asset";

        public ToastGroupConfig[] GroupConfigs { get; }
    }
}
```

---

## Rules

- group 설정의 단일 전역 source다.
- 저장 내용은 `ToastGroupConfig[]`다.
- `UIToastService`가 `Resources.Load<UIToastSettings>(ResourcesPath)`로 load/cache한다.
- asset이 없거나 `GroupConfigs`가 비어 있으면 `UIToastPanel`이 default group 1개로 fallback한다.

---

## Default Asset

권장 기본값:

```text
GroupId         = "System"
ToastFrameId    = "ui_toast_frame"
AnchorPreset    = TopCenter
AnchoredOffset  = (0, 300)
MaxVisibleCount = 1
DefaultDuration = 2
DuplicatePolicy = Allow
```

---

## Reference

- Overview: `../00-overview/SKILL.md`
- Service: `../10-service/SKILL.md`
- Canvas/Panel: `../11-canvas-panel/SKILL.md`
- Data Model: `../14-data-model/SKILL.md`
