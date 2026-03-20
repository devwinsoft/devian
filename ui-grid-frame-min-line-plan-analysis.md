# UI Grid Frame Min Line 계획 분석 및 40-ui-container-id 설계

**작성일**: 2026-03-20
**상태**: 분석 완료 → 실행 대기

---

## 1. 현황 분석

### 1-1. 존재하는 자산

| 항목 | 위치 | 상태 | 용도 |
|------|------|------|------|
| **41-ui-cell-id** | `skills/devian-unity/23-ui-package/41-ui-cell-id/SKILL.md` | ✅ ACTIVE | UIGridCell 프리팹 ID 래퍼 |
| **23-ui-frame-grid** | `skills/devian-unity/23-ui-package/23-ui-frame-grid/SKILL.md` | ✅ ACTIVE | Grid 구현체, UI_CELL_ID 사용 중 |
| **21-container-simple** | `skills/devian-unity/23-ui-package/21-container-simple/SKILL.md` | ✅ ACTIVE | 기본 Container 구현 |
| **11-ui-canvas-system** | `skills/devian-unity/23-ui-package/11-ui-canvas-system/SKILL.md` | ✅ ACTIVE | UIBaseContainer 정의 |

### 1-2. 누락된 자산

| 항목 | 예상 위치 | 현황 | 중요도 |
|------|---------|------|--------|
| **40-ui-container-id** | `skills/devian-unity/23-ui-package/40-ui-container-id/SKILL.md` | ❌ 없음 | 🔴 높음 |
| **ui-grid-frame-min-line-plan.md** | (지정 위치 없음) | ❌ 없음 | 🟡 중간 |

---

## 2. 41-ui-cell-id 구조 분석

### 2-1. 핵심 설계

```
String Wrapper Pattern
  ├─ Runtime Type: UI_CELL_ID (Serializable, sealed)
  ├─ Value: string (prefab name)
  ├─ IsValid: !string.IsNullOrEmpty(Value)
  └─ implicit operator string for compatibility
```

### 2-2. 구현 계층

| 계층 | 클래스 | 파일 | 역할 |
|------|--------|------|------|
| **Runtime** | `UI_CELL_ID` | `Runtime/Container/UI_CELL_ID.cs` | 타입 정의 |
| **Editor Selector** | `UICellIdSelector` | `Editor/UICellIdSelector.cs` | 프리팹 선택 UI |
| **Editor Drawer** | `UI_CELL_ID_Drawer` | `Editor/UI_CELL_ID_Drawer.cs` | PropertyDrawer |

### 2-3. 선택 메커니즘 (구현 규약)

**기본 규칙 (12-asset-id SSOT 준수)**
- Apply/Create 버튼 금지
- ShowUtility() 필수 (클릭 즉시 적용, 창 자동 닫기)
- Selector 캐싱 금지 (매번 CreateInstance)

**스캔 대상**
- BundleSettings.GetEntry("UI_CELL_ID") → SearchDir 획득
- SearchDir에서 UIGridCell 컴포넌트 있는 Prefab 스캔
- prefab.name을 ID 값으로 사용
- @prefix 제외, case-insensitive 중복 에러 처리

### 2-4. 문제점 및 개선안

| 문제점 | 현재 상태 | 제안 |
|--------|---------|------|
| SearchDir 하드코딩 가능성 | BundleSettings 의존 | ✅ Good (재사용성 높음) |
| ID 명명 규칙 | prefab.name 사용 | ✅ Good (일관성) |
| 런타임 AssetDatabase 사용 | 금지됨 | ✅ Good (AssetManager 캐시만) |

---

## 3. 40-ui-container-id 설계 (41-ui-cell-id 참고)

### 3-1. 목표

**UI Container 프리팹을 참조하는 string wrapper ID 타입**

- UIBaseContainer를 상속한 컴포넌트 참조
- UISimpleContainer, UIScrollContainer 등 지원
- 41-ui-cell-id와 동일한 패턴 적용

### 3-2. 구조 설계

```
String Wrapper Pattern (41-ui-cell-id와 동일)
  ├─ Runtime Type: UI_CONTAINER_ID (Serializable, sealed)
  ├─ Value: string (container prefab name)
  ├─ IsValid: !string.IsNullOrEmpty(Value)
  └─ implicit operator string for compatibility
```

### 3-3. 파일 구조 및 위치

```
skills/devian-unity/23-ui-package/40-ui-container-id/
├── SKILL.md                              (이 스킬 정의)
└── (구현은 framework-cs에 위치)

framework-cs/upm/com.devian.foundation/Samples~/UIPackage/
├── Runtime/Container/
│   └── UI_CONTAINER_ID.cs               (타입 정의)
└── Editor/
    ├── UIContainerIdSelector.cs         (선택 UI)
    └── UI_CONTAINER_ID_Drawer.cs        (PropertyDrawer)
```

### 3-4. 핵심 구현 사항

#### Runtime (UI_CONTAINER_ID.cs)

```csharp
[Serializable]
public sealed class UI_CONTAINER_ID
{
    public string Value;
    public bool IsValid => !string.IsNullOrEmpty(Value);

    // implicit operator string for compatibility
    public static implicit operator string(UI_CONTAINER_ID id)
        => id?.Value ?? string.Empty;
    public static implicit operator UI_CONTAINER_ID(string value)
        => new() { Value = value };
}
```

#### Editor Selector (UIContainerIdSelector.cs)

```csharp
public sealed class UIContainerIdSelector : BaseEditorAssetIdSelector<UIBaseContainer>
{
    protected override string GroupKey => "UI_CONTAINER_ID";
    protected override string DisplayTypeName => "UI_CONTAINER_ID";
}
```

#### Editor Drawer (UI_CONTAINER_ID_Drawer.cs)

```csharp
[CustomPropertyDrawer(typeof(UI_CONTAINER_ID))]
public sealed class UI_CONTAINER_ID_Drawer : BaseEditorID_Drawer<UIContainerIdSelector>
{
    // ShowUtility()로 창 표시
}
```

### 3-5. 선택 메커니즘 (41-ui-cell-id와 동일)

**BundleSettings 등록**
```
entries[UI_CONTAINER_ID] = "Assets/Bundles/UIContainers"
```

**스캔 대상**
- BundleSettings.GetEntry("UI_CONTAINER_ID") → SearchDir
- SearchDir에서 **UIBaseContainer 상속** 컴포넌트 있는 Prefab 스캔
  - ✅ UISimpleContainer (21-container-simple)
  - ✅ UIScrollContainer (22-ui-container-scroll)
  - ✅ 향후 추가 Container 구현체
- prefab.name을 ID 값으로 사용
- @prefix 제외, case-insensitive 중복 에러 처리

### 3-6. UIBaseContainer 참조의 의미

| 항목 | 설명 | 효과 |
|------|------|------|
| **Generic Type** | `BaseEditorAssetIdSelector<UIBaseContainer>` | UIBaseContainer 상속 모든 구현체 자동 인식 |
| **참조 대상** | UISimpleContainer, UIScrollContainer 등 | 타입 안전성 + 확장성 |
| **검증 로직** | GetComponent<UIBaseContainer>() | 런타임에 정확한 컴포넌트 획득 |

---

## 4. UIGridFrame 내에서의 활용

### 4-1. 현재 상태

```csharp
public class UIGridFrame : UIBaseFrame, IUIScrollSection
{
    [SerializeField] private UI_CELL_ID _cellPrefabId;      // ✅ 사용 중
    // UI_CONTAINER_ID는 아직 사용되지 않음
}
```

### 4-2. 향후 활용 시나리오

**시나리오 1**: Container 참조 필요 시
```csharp
[SerializeField] private UI_CONTAINER_ID _parentContainerId;  // 새로 추가 가능
```

**시나리오 2**: 동적 Container 생성/접근
```csharp
// 런타임에 container prefab 이름으로 접근
public void SetContainerId(UI_CONTAINER_ID containerId) { ... }
```

### 4-3. MinimumLineCount 계획 (ui-grid-frame-min-line-plan)

**목적**: UIGridFrame의 `MinimumLineCount` 기능 문서화

**현재 구현**
- 속성: `MinimumLineCount` (직렬화)
- 계산: `RowCount = max(MinimumLineCount, DataRowCount)`
- 크기 동기화: setter 변경 시 RectTransform 업데이트

**계획 내용**
- `SetMinimumLineCount()` 메서드 규약
- 런타임 변경 시 Rebuild() 자동 요청
- 빈 행(placeholder) 처리 방식
- 레이아웃 재계산 타이밍

---

## 5. 실행 계획 (To-Do)

### Phase 1: 40-ui-container-id 스킬 생성 (우선순위: 🔴 높음)

- [ ] **5-1-1**: `40-ui-container-id/SKILL.md` 생성
  - 목적 정의
  - 파일 위치 (SSOT)
  - String Wrapper 패턴
  - Selector/Drawer 규약
  - BundleSettings 등록
  - 금지 사항

- [ ] **5-1-2**: `UI_CONTAINER_ID.cs` 구현
  - 타입 정의 (41-ui-cell-id 패턴 복사)
  - implicit operator 구현
  - Serializable 어트리뷰트

- [ ] **5-1-3**: `UIContainerIdSelector.cs` 구현
  - BaseEditorAssetIdSelector<UIBaseContainer> 상속
  - GroupKey, DisplayTypeName 설정

- [ ] **5-1-4**: `UI_CONTAINER_ID_Drawer.cs` 구현
  - CustomPropertyDrawer 어트리뷰트
  - BaseEditorID_Drawer<UIContainerIdSelector> 상속

- [ ] **5-1-5**: 빌드/테스트
  - 컴파일 확인 (0 errors)
  - BundleSettings 등록 확인

### Phase 2: ui-grid-frame-min-line-plan 문서 (우선순위: 🟡 중간)

- [ ] **5-2-1**: 문서 위치 결정
  - 스킬 문서: 23-ui-frame-grid/SKILL.md 섹션 추가
  - 또는: `docs/ui-grid-frame-min-line-plan.md` (별도 계획 문서)

- [ ] **5-2-2**: MinimumLineCount 기능 문서화
  - 현재 구현 분석
  - RowCount 계산식 명확화
  - Rebuild 타이밍 설명
  - placeholder 처리 방식

- [ ] **5-2-3**: 예제 코드 작성
  - SetMinimumLineCount() 사용 예
  - 런타임 변경 시나리오

### Phase 3: 통합 및 검증

- [ ] **5-3-1**: 폴더 구조 최종 확인
  ```
  skills/devian-unity/23-ui-package/
  ├── 40-ui-container-id/
  │   └── SKILL.md
  ├── 41-ui-cell-id/
  │   └── SKILL.md
  └── 23-ui-frame-grid/
      └── SKILL.md
  ```

- [ ] **5-3-2**: 교차 참조 확인
  - 23-ui-package/SKILL.md에 40-ui-container-id 추가
  - 40-ui-container-id → UIBaseContainer 참조
  - 23-ui-frame-grid → 40-ui-container-id 링크 (향후 사용 시)

- [ ] **5-3-3**: 규칙 준수 검증
  - 23-ui-package/01-policy: No "Usage" Section 확인
  - 스킬 읽기 순서 (CLAUDE.md 6-1) 준수

---

## 6. 보완 점 (Gap Analysis)

### 6-1. 설계 단계에서 확인해야 할 점

| 항목 | 현황 | 조치 |
|------|------|------|
| **BundleSettings 키** | 예정된 이름 미정 | ✅ "UI_CONTAINER_ID" 사용 예정 |
| **SearchDir 기본값** | 폴더 없을 시 fallback | ✅ "Assets" fallback 사용 |
| **에러 처리** | case-insensitive 중복 | ✅ 에러 로그 후 스킵 |
| **UIBaseContainer 위치** | 정확한 파일 경로 필요 | ⚠️ 확인 필요 |

### 6-2. 구현 단계에서 주의할 점

| 항목 | 주의사항 | 영향 |
|------|---------|------|
| **Generic Type** | `BaseEditorAssetIdSelector<T>` 제너릭 | 타입 안전성 확보 |
| **Implicit Operator** | string 호환성 | 기존 코드 손상 없음 |
| **ShowUtility() 규약** | Apply 버튼 금지 | UX 일관성 |
| **AssetDatabase 런타임** | 금지 (AssetManager만) | 성능 보장 |

### 6-3. 문서화 단계에서 추가할 점

| 항목 | 현황 | 필요 |
|------|------|------|
| **UIBaseContainer 의존성** | 링크만 있음 | ✅ 명확한 상속 관계 표시 |
| **구현 예제** | 코드 스펙만 있음 | ✅ 간단한 사용 패턴 |
| **에러 시나리오** | 미기술 | ✅ 검증 실패 케이스 |

---

## 7. 정리 및 다음 단계

### 7-1. 현재 상황 요약

| 항목 | 상태 | 의존성 |
|------|------|--------|
| **41-ui-cell-id** | ✅ 완성 | 독립 |
| **40-ui-container-id** | 🔴 미생성 | 11-ui-canvas-system (UIBaseContainer) |
| **ui-grid-frame-min-line-plan** | 🟡 계획 단계 | 23-ui-frame-grid |

### 7-2. 즉시 실행 항목

**Phase 1-1**: `40-ui-container-id/SKILL.md` 생성
**Phase 1-2**: `UI_CONTAINER_ID.cs` 구현
**Phase 1-3**: Editor Selector/Drawer 구현

### 7-3. 스킬 구조 검증

상위 정책 확인 (CLAUDE.md 6-1 규칙):
1. ✅ `devian-unity/01-policy/SKILL.md` — 상위 정책 읽음
2. ⚠️ `devian-unity/03-ssot/SKILL.md` — 미확인 (필요 시 읽기)
3. ✅ `23-ui-package/00-overview/SKILL.md` — 읽음
4. ✅ `23-ui-package/01-policy/SKILL.md` — 읽음 (No "Usage" Section)
5. ⚠️ `23-ui-package/03-ssot/SKILL.md` — 미확인 (없을 가능성)

---

## 참고 자료

- `skills/devian-unity/23-ui-package/41-ui-cell-id/SKILL.md` — 참고 패턴
- `skills/devian-unity/23-ui-package/23-ui-frame-grid/SKILL.md` — MinimumLineCount 사용
- `skills/devian-unity/23-ui-package/21-container-simple/SKILL.md` — UIBaseContainer 상속 예
- `skills/devian-unity/20-common-package/12-asset-id/SKILL.md` — Selector/Drawer 기본 규약
- `/mnt/devian/CLAUDE.md` — 프로젝트 정책

