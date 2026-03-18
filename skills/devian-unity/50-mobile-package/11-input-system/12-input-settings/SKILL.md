# 12-input-settings

Status: ACTIVE
AppliesTo: v11
Type: Component Specification

## 목적

`InputSettings`는 **InputManager의 프로젝트 단위 설정**을 외부 ScriptableObject `.asset`으로 분리한 데이터 컨테이너이다.

- InputManager가 `Resources.Load<InputSettings>(InputSettings.ResourcesPath)`로 고정 경로에서 로드
- 프로젝트/빌드별 입력 설정을 에셋 단위로 관리
- `Assets/Resources/Devian/InputSettings.asset` 고정 위치

---

## 범위

### 포함

- `InputSettings` — ScriptableObject, 입력 설정 데이터 컨테이너

### 제외

- 입력 수집/정규화/발행 로직 (→ `10-input-manager`)
- 입력 소비/콜백 (→ `11-input-controller`)

---

## 네임스페이스

```csharp
namespace Devian
```

---

## 핵심 규약 (Hard Rule)

### 1. ScriptableObject 기반

- `CreateAssetMenu`로 프로젝트 내 `.asset` 파일 생성
- `menuName = "Devian/MobilePackage/Input Settings"`
- `ResourcesPath = "Devian/InputSettings"` (Resources.Load 경로)
- `DefaultResourcesAssetPath = "Assets/Resources/Devian/InputSettings.asset"`

### 2. 필드 목록

| 필드 | 타입 | 기본값 | 용도 |
|------|------|--------|------|
| `_asset` | `InputActionAsset` | null | Unity InputActionAsset 참조 |
| `_gameplayMapName` | `string` | `"Player"` | Gameplay ActionMap 이름 |
| `_uiMapName` | `string` | `"UI"` | UI ActionMap 이름 |
| `_moveKey` | `string` | `"Player/Move"` | Move 축 Action 키 |
| `_lookKey` | `string` | `"Player/Look"` | Look 축 Action 키 |
| `_expectedButtonKeys` | `string[]` | empty | 버튼 Action 키 배열 |

### 3. 읽기 전용 프로퍼티

모든 필드는 `public` 읽기 전용 프로퍼티로 노출한다. setter 없음.

### 4. InputManager 연동

- InputManager는 `Resources.Load`로 고정 경로에서 로드 (`ensureSettings()` 패턴)
- `_outputEnabled`는 인스턴스별 런타임 토글이므로 InputManager에 잔류

---

## API 시그니처

```csharp
[CreateAssetMenu(fileName = "InputSettings", menuName = "Devian/MobilePackage/Input Settings")]
public class InputSettings : ScriptableObject
{
    public const string ResourcesPath = "Devian/InputSettings";
    public const string DefaultResourcesAssetPath = "Assets/Resources/Devian/InputSettings.asset";

    public InputActionAsset Asset { get; }
    public string GameplayMapName { get; }
    public string UIMapName { get; }
    public string MoveKey { get; }
    public string LookKey { get; }
    public string[] ExpectedButtonKeys { get; }
}
```

---

## Editor 기능

### Refresh Expected Button Keys

`InputSettingsInspector` 커스텀 인스펙터에 **"Refresh Expected Button Keys"** 버튼을 제공한다.

**동작:**
1. `InputActionAsset`의 모든 ActionMap을 스캔
2. `action.expectedControlType == "Button"` 인 액션만 수집
3. key 포맷: `"Map/Action"` (예: `"Player/Attack"`)
4. 중복 제거 (`StringComparer.Ordinal`)
5. Ordinal 정렬
6. 64개 초과 시 64개까지만 적용 + 경고 로그
7. `_expectedButtonKeys` 배열을 완전 덮어쓰기
8. 내부 버튼 맵 재빌드는 InputManager.OnEnable() 초기화 시 자동 수행

**Play Mode:** Play Mode에서 비활성화된다.

### Install/Ensure VirtualGamepad Bindings

`InputSettingsInspector`에 **"Install/Ensure VirtualGamepad Bindings"** 버튼을 제공한다.

**동작:**
1. `MoveKey`, `LookKey` 값을 읽어 해당 Action을 해석
2. Action에 `<VirtualGamepad>/move`, `<VirtualGamepad>/look` 바인딩이 없으면 추가
3. 이미 존재하면 아무 것도 안 함
4. Undo 지원 + `AssetDatabase.SaveAssets()`

**Play Mode:** Play Mode에서 비활성화된다.

---

## 파일 경로

| 타입 | 경로 |
|------|------|
| InputSettings | `com.devian.foundation/Samples~/MobilePackage/Runtime/Input/InputSettings.cs` |
| InputSettingsInspector | `com.devian.foundation/Samples~/MobilePackage/Editor/Input/InputSettingsInspector.cs` |

---

## DoD (Definition of Done)

- [ ] `namespace Devian` 사용
- [ ] ScriptableObject 상속
- [ ] CreateAssetMenu 어트리뷰트 적용
- [ ] 6개 필드 + 읽기 전용 프로퍼티
- [ ] InputManager가 `Resources.Load<InputSettings>(InputSettings.ResourcesPath)`로 로드
- [ ] InputManager에서 기존 6개 SerializedField 제거됨
- [ ] `_outputEnabled`는 InputManager에 잔류
- [ ] `InputSettingsInspector` — Refresh, VirtualGamepad 버튼 제공
- [ ] UPM ↔ UnityExample 동일

---

## Reference

- Parent: `../00-overview/SKILL.md`
- InputManager: `../10-input-manager/SKILL.md`
