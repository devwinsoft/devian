# 13-ui-component-safe-area

Status: ACTIVE
AppliesTo: v11

## Purpose

모바일 디바이스의 safe area에 맞춰 특정 `RectTransform`의 레이아웃을 자동 보정하는
독립형 UI 컴포넌트를 정의한다.

`UIComponentSafeArea`는 전역 UI manager 의존 없이 자기 own target만 수정해야 한다.

## Scope

### Includes
- `UIComponentSafeArea : UIComponentBase`
- target 지정 (`RectTransform`)
- 상/하/좌/우 개별 적용 플래그
- 추가 padding (`Vector4`: left, bottom, right, top)
- `SafeAreaApplyMode` (`Anchor`, `Offset`)
- `OnEnable` / 화면 크기 변경 / orientation 변경 시 `Refresh()`
- disable 시 baseline layout 복원
- parent 변경 시 baseline 재캡처 후 재적용
- `Screen.safeArea` 기반 safe area 보정
- baseline layout cache 기반 재적용
- baseline 필드 직렬화(`[SerializeField, HideInInspector]`)로 도메인 리로드 시 유실 방지
- 에디터 도메인 리로드 시 `beforeAssemblyReload` 핸들러로 RectTransform 사전 복원
- `Force Reset Baseline` ContextMenu로 오염된 baseline 수동 리셋
- editor simulation profile
- 내부 safe area source 추상화
- 마지막 적용 상태 조회 프로퍼티

### Excludes
- device-specific override
- debug gizmo

## SSOT

### Code Path

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Component/UIComponentSafeArea.cs
```

### Class

```csharp
namespace Devian
{
    public sealed class UIComponentSafeArea : UIComponentBase
    {
        public enum SafeAreaApplyMode
        {
            Anchor,
            Offset,
        }

        public void Refresh();

        [ContextMenu("Force Reset Baseline")]
        private void ForceResetBaseline();

        [ContextMenu("Refresh Safe Area")]
        // Refresh() is the public entry point above
    }
}
```

### Serialized Fields

```csharp
// Inspector 노출
[SerializeField] private RectTransform _target;
[SerializeField] private bool _applyTop = true;
[SerializeField] private bool _applyBottom = true;
[SerializeField] private bool _applyLeft = true;
[SerializeField] private bool _applyRight = true;
[SerializeField] private Vector4 _extraPadding;
[SerializeField] private SafeAreaApplyMode _applyMode = SafeAreaApplyMode.Anchor;
[SerializeField] private bool _refreshOnEnable = true;
[SerializeField] private bool _refreshOnResolutionChange = true;
[SerializeField] private bool _refreshOnOrientationChange = true;
[SerializeField] private bool _useEditorSimulation = true;
[SerializeField] private SafeAreaEditorSimulationProfile _editorSimulationProfile =
    SafeAreaEditorSimulationProfile.IPhone14Pro;

// Baseline 직렬화 (HideInInspector — 도메인 리로드 시 유실 방지용)
[SerializeField, HideInInspector] private RectTransform _baselineTarget;
[SerializeField, HideInInspector] private Vector2 _baselineAnchorMin;
[SerializeField, HideInInspector] private Vector2 _baselineAnchorMax;
[SerializeField, HideInInspector] private Vector2 _baselineOffsetMin;
[SerializeField, HideInInspector] private Vector2 _baselineOffsetMax;
[SerializeField, HideInInspector] private bool _hasBaseline;

public enum SafeAreaEditorSimulationProfile
{
    None,
    IPhone14Pro,
    IPad,
    AndroidTall,
}
```

## Behavior

### Target Resolution

- `_target`이 있으면 그 `RectTransform`을 사용
- `_target`이 비어 있으면 자기 자신의 `RectTransform`을 사용

### Safe Area Source

- 런타임 source는 `Screen.safeArea`
- 에디터 simulation이 켜져 있으면 profile 기반 simulated safe area를 사용
- 화면 기준값은 `Screen.width`, `Screen.height`, `Screen.orientation`

### Apply Rules

- left 미적용: `xMin = 0`
- bottom 미적용: `yMin = 0`
- right 미적용: `xMax = Screen.width`
- top 미적용: `yMax = Screen.height`
- `_extraPadding`은 pixel 기준으로 safe area rect에 더한다
- `_extraPadding`의 양수값은 safe rect를 안쪽으로 더 줄인다
- padding 적용 후 screen bounds 안으로 clamp 한다
- `Anchor` 모드는 최종 rect를 0..1 정규화 anchor 값으로 변환해 `anchorMin/anchorMax`에 적용한다
- `Anchor` 모드는 baseline `offsetMin/offsetMax`를 복원한 뒤 적용한다
- `Offset` 모드는 baseline `anchorMin/anchorMax`를 유지한다
- `Offset` 모드는 계산된 safe inset을 `offsetMin/offsetMax`에 반영한다
- `Offset` 모드에서 fixed axis는 한쪽만 적용 시 size를 유지한 채 위치만 이동한다

### Baseline Rules

- 첫 적용 시 target의 `anchorMin`, `anchorMax`, `offsetMin`, `offsetMax`를 baseline으로 캡처한다
- baseline 필드는 `[SerializeField, HideInInspector]`로 직렬화하여 에디터 도메인 리로드 시 유실을 방지한다
- 이후 모든 `Refresh()`는 baseline 기준으로 재계산한다
- target이 바뀌면 이전 target을 baseline으로 복원한 뒤 baseline을 다시 캡처한다
- component가 disable되면 target을 baseline으로 복원한다

### Domain Reload Protection (Editor Only)

- `[ExecuteAlways]` 컴포넌트가 에디터에서 RectTransform을 수정한 상태에서 도메인 리로드가 발생하면 baseline 오염이 생길 수 있다
- `beforeAssemblyReload` 핸들러가 리로드 전에 모든 활성 인스턴스의 RectTransform을 baseline으로 복원한다
- 이미 오염된 baseline은 `Force Reset Baseline` ContextMenu로 수동 리셋한다: 컴포넌트 비활성화 → RectTransform 수동 수정 → 재활성화

### Refresh Triggers

- `onInit(Canvas)`에서 첫 `Refresh()`
- `OnEnable()`에서 `_refreshOnEnable == true`면 `Refresh()`
- `Update()`에서 아래 변화 감지 시 `Refresh()`
- `OnTransformParentChanged()`에서 baseline 복원 후 버리고 다시 `Refresh()`
- `Screen.safeArea`
- `Screen.width` / `Screen.height`
- `Screen.orientation`

### Public State

- `Target`
- `LastAppliedSafeArea`
- `LastOrientation`
- `IsApplied`

## Policy

### Supported Targets

- 전체 safe root
- 상단 HUD
- 하단 메뉴 바
- 화면 모서리 고정 버튼
- safe area 영향을 직접 받는 frame/panel

### Unsupported Targets

- 배경
- dim
- 풀스크린 이펙트
- 중앙 고정 팝업 전체 루트

`Anchor`는 전체 stretch root나 viewport 보정에 적합하고,
top/bottom bar 같은 고정형 레이아웃은 `Offset`을 우선 권장한다.

### Dependency Rules

- 허용: `RectTransform`, `Screen.safeArea`, `Screen.width`, `Screen.height`, `Screen.orientation`
- 비권장: `UIManager`, canvas manager, 특정 panel 타입 직접 참조, 전역 UI stack 참조

### Canvas Assumption

screen-space UI 기준 컴포넌트로 본다.
`WorldSpace` canvas는 no-op 처리한다.

## Reference

- Parent: `../00-overview/SKILL.md`
- Base: [10-ui-component-base](../10-ui-component-base/SKILL.md)
- UI Canvas System: [11-ui-canvas-system](../../10-base-system/11-ui-canvas-system/SKILL.md)
