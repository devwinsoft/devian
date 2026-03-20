# UIToastSettings 계획

## 1. 전제

- 사용자 요청의 `UITweenService`는 현재 코드 기준 `UIToastService`로 해석한다.
- 이번 변경의 목표는 **toast group 설정의 전역화**다.
- 최소 변경 원칙:
  - `ToastGroupConfig[]`를 `UIToastSettings.asset`로 이동
  - group root 생성/배치는 계속 `UIToastPanel`이 담당

## 2. 목표 구조

```text
Assets/Resources/Devian/UIToastSettings.asset
  -> ToastGroupConfig[]

UIToastService
  -> Resources.Load<UIToastSettings>()
  -> global settings cache
  -> UIToastPanel이 settings를 조회할 수 있게 제공

UIToastPanel
  -> local _groupConfigs 제거
  -> UIToastService settings 기반으로 group 생성
```

## 3. 구현 항목

### 3.1 Runtime

- `UIPackage/Runtime/Toast/UIToastSettings.cs`
  - `ScriptableObject`
  - 고정 경로:
    - `ResourcesPath = "Devian/UIToastSettings"`
    - `DefaultResourcesAssetPath = "Assets/Resources/Devian/UIToastSettings.asset"`
  - 필드:
    - `[SerializeField] private ToastGroupConfig[] _groupConfigs;`
  - API:
    - `public ToastGroupConfig[] GroupConfigs => _groupConfigs;`

- `UIToastService.cs`
  - `UIToastSettings` lazy load/cache 추가
  - `public UIToastSettings Settings { get; }`
  - `GetGroupConfigs()` 추가
  - settings 미존재 시 warning + default config fallback 규약 제공

- `UIToastPanel.cs`
  - `_groupConfigs`, `_groupsRoot`, `_toastFrameId` 제거
  - group parent는 항상 `rectTransform`
  - `EnsureGroups()`는 `UIToastService.Instance`가 제공하는 global group config를 사용
  - `UIToastGroup` 생성 시 frame id는 `ToastGroupConfig.ToastFrameId`를 사용
  - settings null/empty면 기존처럼 default group 1개 생성

### 3.2 Example Asset

- `Assets/Resources/Devian/UIToastSettings.asset` 생성
- 기본값:
  - `GroupId = "System"`
  - `ToastFrameId = "ui_toast_frame"`
  - `AnchorPreset = TopCenter`
  - `AnchoredOffset = (0, -80)`
  - `MaxVisibleCount = 1`
  - `DefaultDuration = 2`
  - `DuplicatePolicy = Allow`

### 3.3 Skills

- 신규:
  - `skills/devian-unity/23-ui-package/52-ui-toast-system/16-ui-toast-settings/SKILL.md`
- 수정:
  - `00-overview`
  - `10-service`
- `11-canvas-panel`
- `14-data-model`
- `15-ui-toast-frame-id`
- `52-ui-toast-system` 인덱스

## 4. 책임 경계

- `UIToastService`
  - 전역 settings source
  - settings load/cache 책임
- `UIToastPanel`
  - panel root 아래 `RectTransform` root 생성
  - group runtime 인스턴스 생성
  - request enqueue 위임
- `UIToastGroup`
  - queue / duplicate / relayout 책임 유지

## 5. 변경하지 않는 것

- `UIToastFrame` 동작
- `UITransitionPlayer` / tween 경로
- `UI_TOAST_FRAME_ID` 구조
- `MobileApplication`의 toast canvas bootstrap

## 6. 구현 순서

1. `UIToastSettings.cs` 추가
2. `UIToastService`에 settings resolve/cache 추가
3. `UIToastPanel`에서 `_groupConfigs`, `_groupsRoot`, `_toastFrameId` 제거 후 service settings 사용으로 전환
4. example `UIToastSettings.asset` 생성
5. toast skill 문서 갱신
6. mirror 동기화 + build 검증

## 7. 검증 항목

- `UIToastSettings.asset`가 없을 때 default group fallback이 동작하는가
- asset에 group 2개 이상 설정 시 `GroupId`별 routing이 되는가
- duplicate `GroupId` warning이 유지되는가
- 기존 `UIToastService.Show("msg")` 기본 동작이 유지되는가
- `UIToastPanel` prefab에서 `_groupConfigs` 제거 후에도 NRE가 없는가
