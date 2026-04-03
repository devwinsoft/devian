# 11-ui-component-button

Status: ACTIVE
AppliesTo: v11

## Purpose

Button press visual feedback plugin with UnityEvent hooks, optional UI sound playback,
and `ScrollRect` drag bridge.

## Scope

### Includes
- PointerDown/Up 시각 피드백 (`EffectType.Scale`, `EffectType.AnchoredPosition`)
- 선택적 UI 사운드 재생 (`SoundDown`, `SoundUp`)
- UnityEvent hook (`onDown`, `onUp`)
- nested scroll 지원용 drag bridge
  - 부모 `ScrollRect` 자동 탐색
  - `SetScroll(ScrollRect)`로 수동 override 가능
- `EventTrigger` 자동 구성

### Sound Playback

On PointerDown: apply visual feedback + optional UI sound
On PointerUp: restore visual state + optional UI sound
On BeginDrag: restore visual state and cancel pending PointerUp action/sound for the current pointer sequence

사운드 재생 경로:
```
SOUND_ID -> TB_SOUND.Get(id.Value) -> row.Sound_id -> SoundManager.Instance.PlaySound(..., channelOverride: SoundChannelType.Ui)
```

- `SoundDown` / `SoundUp`이 invalid면 silent no-op
- `TB_SOUND.Get()` 결과가 null이거나 `row.Sound_id`가 비어 있으면 silent no-op

## Dependencies (Domain Sound)

| Symbol | Purpose |
|--------|---------|
| `SOUND_ID` | 사운드 테이블 키 |
| `TB_SOUND` | 사운드 테이블 조회 |
| `SoundManager` | 사운드 재생 |
| `SoundChannelType.Ui` | UI 채널 지정 |

## SSOT

### Code Path
```
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Component/UIComponentButton.cs
```

### Class
```csharp
namespace Devian
{
    [RequireComponent(typeof(EventTrigger))]
    public class UIComponentButton : UIComponentBase
}
```

### Serialized Fields
```csharp
[SerializeField] private EffectType _effectType = EffectType.Scale;
[SerializeField] private SOUND_ID SoundDown;
[SerializeField] private SOUND_ID SoundUp;
```

## Reference

- Parent: `../00-overview/SKILL.md`
- Base: [10-ui-component-base](../10-ui-component-base/SKILL.md)
- Index: `skills/devian-unity/23-ui-package/SKILL.md`
