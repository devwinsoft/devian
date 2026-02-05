# 30-ui-plugin-button

Status: ACTIVE
AppliesTo: v10

## Purpose

Button press visual feedback plugin with UnityEvent hooks and ScrollRect drag bridge.
Provides scale/position animation on pointer down/up without sound or domain dependencies.

## Scope

### Includes
- PointerDown/Up 시각 피드백 (Scale 1.1x 또는 AnchoredPosition -10)
- UnityEvent hook (`onDown`, `onUp`)
- ScrollRect drag bridge (`SetScroll`)
- EventTrigger 자동 설정

### Excludes
- 사운드 재생 (외부에서 `onDown`/`onUp` 이벤트로 연결)
- 도메인 의존성 (SOUND_ID 등)

## SSOT

### Code Path
```
framework-cs/upm/com.devian.foundation/Runtime/Unity/UI/Plugins/UIPlugInButton.cs
```

### Class
```csharp
namespace Devian
{
    [RequireComponent(typeof(EventTrigger))]
    public class UIPlugInButton : MonoBehaviour
}
```

## Reference

- Parent: `skills/devian-unity/40-ui-components/skill.md`
