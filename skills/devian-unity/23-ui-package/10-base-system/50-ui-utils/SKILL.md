# 50-ui-utils

Status: ACTIVE
AppliesTo: v1

---

## Overview

### Purpose

UIPackage 전반에서 사용되는 순수 유틸리티 함수를 모은 static class.
인스턴스 상태에 의존하지 않는 좌표 변환, 회전 계산, 커서 제어 등을 제공한다.

---

## SSOT

### Code Path

```
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Base/UIUtils.cs
```

### Class Signature

```csharp
namespace Devian
{
    public static class UIUtils
    {
        // ─── Coordinate Conversion ───
        public static bool TryWorldToOverlayLocal(
            Camera worldCamera,
            RectTransform overlaySpace,
            Vector3 worldPos,
            out Vector2 overlayLocal);

        // ─── Billboard ───
        public static Quaternion ComputeBillboardRotation(
            Camera camera,
            Vector3 targetWorldPos,
            BillboardMode mode = BillboardMode.Full);

        public static void ApplyBillboard(
            Camera camera,
            Transform target,
            BillboardMode mode = BillboardMode.Full);

        // ─── Cursor ───
        public static void SetCursor(bool visible, CursorLockMode lockMode);

        // ─── RectTransform Size ───
        public static float GetWidth(RectTransform rt);
        public static float GetHeight(RectTransform rt);
        public static Vector2 GetSize(RectTransform rt);
    }
}
```

### 이전 위치

| 메서드 | 이전 위치 | 비고 |
|--------|-----------|------|
| `TryWorldToOverlayLocal` | `UIBaseCanvas<T>` | `canvas.worldCamera` → 파라미터 `worldCamera` |
| `ComputeBillboardRotation` | `UIBaseCanvas<T>` | `canvas.worldCamera` → 파라미터 `camera` |
| `ApplyBillboard` | `UIBaseCanvas<T>` | `canvas.worldCamera` → 파라미터 `camera` |
| `SetCursor` | `UIManager` | 인스턴스 상태 없음, 그대로 이동 |

---

## Dependencies

| Dependency | Location |
|------------|----------|
| `BillboardMode` | `UIBaseCanvas.cs` (같은 Runtime/Base 폴더) |

---

## Reference

- Parent: `skills/devian-unity/23-ui-package/SKILL.md`
