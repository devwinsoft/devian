# 14-data-model — ToastRequest / ToastGroupConfig / Enums / Defaults

Status: ACTIVE
AppliesTo: v11

---

## 목적

toast system 전체가 공유하는 **데이터 타입 정의**.
비즈니스 로직 없음. 타입/상수/기본값만 포함한다.

---

## SSOT

### Code Path

```
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Toast/
├── ToastRequest.cs
├── ToastGroupConfig.cs
├── ToastEnums.cs
└── UIToastDefaults.cs
```

---

## ToastRequest

```csharp
public readonly struct ToastRequest
{
    public readonly string GroupId;           // null/empty → DefaultGroupId
    public readonly string Message;           // null → ""
    public readonly float? DurationOverride;  // null → config.DefaultDuration 사용
    public readonly ToastType ToastType;

    public ToastRequest(
        string groupId,
        string message,
        float? durationOverride = null,
        ToastType toastType = ToastType.Info);
}
```

---

## ToastGroupConfig

```csharp
[Serializable]
public sealed class ToastGroupConfig
{
    public string GroupId                 = UIToastDefaults.DefaultGroupId;      // "System"
    public UI_TOAST_FRAME_ID ToastFrameId = UIToastDefaults.DefaultFramePrefabName; // "ui_toast_frame"
    public ToastAnchorPreset AnchorPreset = ToastAnchorPreset.TopCenter;
    public Vector2 AnchoredOffset         = UIToastDefaults.DefaultAnchoredOffset; // (0, -80)
    public int MaxVisibleCount            = UIToastDefaults.DefaultMaxVisibleCount; // 1
    public float DefaultDuration          = UIToastDefaults.DefaultDuration;        // 2f
    public ToastDuplicatePolicy DuplicatePolicy = ToastDuplicatePolicy.Allow;
}
```

## UISettings (Toast 부분)

Toast group 설정은 통합 `UISettings` asset에 포함된다. 상세: `../../13-ui-settings/SKILL.md`

- `ToastGroupConfig[]`의 전역 저장 위치다.
- `UIToastService`가 `Resources.Load<UISettings>(UISettings.ResourcesPath)`로 로드하고 cache한다.
- `UIToastPanel`은 local serialize field 대신 이 asset의 group 설정을 사용한다.

---

## Enums

### ToastType
| 값 | 설명 |
|----|------|
| `Info` | 기본 정보성 메시지 |
| `Success` | 성공 |
| `Warning` | 경고 |
| `Error` | 오류 |

### ToastDuplicatePolicy
| 값 | 동작 |
|----|------|
| `Allow` | 중복 허용 |
| `IgnoreIfVisible` | 동일 message가 visible이면 skip |
| `RefreshDurationIfVisible` | 동일 message가 visible이면 duration 갱신 |

### ToastAnchorPreset
`TopLeft / TopCenter / TopRight / MiddleLeft / MiddleCenter / MiddleRight / BottomLeft / BottomCenter / BottomRight`

---

## UIToastDefaults (internal)

```csharp
internal static class UIToastDefaults
{
    public const string DefaultGroupId          = "System";
    public const string DefaultFramePrefabName  = "ui_toast_frame";
    public const int    DefaultMaxVisibleCount   = 1;
    public const float  DefaultDuration          = 2f;
    public const float  DefaultSpacing           = 8f;
    public static readonly Vector2 DefaultAnchoredOffset = new Vector2(0f, -80f);
}
```

---

## Reference

- Overview: `../00-overview/SKILL.md`
- Service: `../10-service/SKILL.md`
- Group: `../12-group/SKILL.md`
