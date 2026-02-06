# 30-ui-plugin-button

Status: ACTIVE
AppliesTo: v10

## Purpose

Button press visual feedback plugin with UnityEvent hooks, optional UI sound playback,
and ScrollRect drag bridge.

## Scope

### Includes
- PointerDown/Up 시각 피드백 (useScaling=true → Scale 1.1x, false → EffectType 기반)
- (선택) PointerDown/Up UI 사운드 재생 (`SoundDown`, `SoundUp`)
- UnityEvent hook (`onDown`, `onUp`)
- ScrollRect drag bridge (`SetScroll`)
- EventTrigger 자동 설정

### Sound Playback

On PointerDown: apply visual feedback + (optional) play UI sound
On PointerUp: restore visual state + (optional) play UI sound

사운드 재생 경로:
```
SOUND_ID → TB_SOUND.Get(id.Value) → row.Sound_id → SoundManager.Instance.PlaySound(..., channelOverride: SoundChannelType.Ui)
```

- `SoundDown` / `SoundUp`이 유효하지 않으면 무음 (silent no-op)
- `TB_SOUND.Get()` null 반환 또는 `row.Sound_id` 빈 문자열 시 무음 (null-safe)

### Dependencies (Domain Sound)

사용하는 `Devian.Domain.Sound` 심볼:

| 심볼 | 용도 |
|------|------|
| `SOUND_ID` | 사운드 테이블 키 (SerializeField) |
| `TB_SOUND` | 사운드 테이블 조회 |
| `SoundManager` | 사운드 재생 (`PlaySound`) |
| `SoundChannelType.Ui` | UI 채널 지정 |

## SSOT

### Code Path
```
framework-cs/upm/com.devian.ui/Runtime/Plugins/UIPlugInButton.cs
```

### Class
```csharp
namespace Devian
{
    [RequireComponent(typeof(EventTrigger))]
    public class UIPlugInButton : MonoBehaviour
}
```

### Serialized Fields
```csharp
[SerializeField] private EffectType _effectType = EffectType.Scale;
[SerializeField] private bool useScaling = true;
[SerializeField] private SOUND_ID SoundDown;
[SerializeField] private SOUND_ID SoundUp;
```

## Reference

- Parent: `skills/devian-unity/40-ui-components/skill.md`
