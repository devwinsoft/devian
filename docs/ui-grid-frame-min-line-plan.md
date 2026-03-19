# UIGridFrame Minimum Line Plan

---

## 1. 목적

`UIGridFrame`에 "초기 line 수 floor"를 추가한다.

목표 동작:

- Inspector에서 minimum/initial line 수를 지정할 수 있어야 한다.
- 실제 데이터 line 수가 이 값보다 작아도, grid는 이 line 수만큼 기본 렌더 상태를 가져야 한다.
- line이 1개 늘어날 때마다 cell은 `columnCount`개 단위로 늘어나야 한다.
- 마지막 row를 포함해 부분 row 없이 full-row 단위로 렌더링해야 한다.
- effective line 수가 바뀌면 `RectTransform` size도 같이 갱신되어야 한다.

---

## 2. 현재 코드 분석

기준 파일:

- `/Users/maoshy/Documents/Projects/devian/framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/UIPackage/Runtime/Container/UIGridFrame.cs`

현재 `UIGridFrame`의 핵심 동작은 다음과 같다.

- `CellCount`만 단일 소스로 사용한다.
- `RowCount = CeilToInt(CellCount / columnCount)` 이다.
- `GetHeight()`는 `RowCount` 기준으로 계산한다.
- `GetLogicalRowCount()`도 `RowCount`를 그대로 사용한다.
- `BindRow(...)`는 마지막 row에서 `Mathf.Min(columnCount, CellCount - startIndex)`만큼만 cell을 생성한다.

즉 현재 구조는:

- minimum line 개념이 없다.
- 마지막 row가 partial row일 수 있다.
- `RectTransform` size는 `GetHeight()` 계산값과 느슨하게만 연결되어 있고, line floor 변경을 즉시 반영하는 코드가 없다.

---

## 3. 요구사항 해석

이번 요구는 단순히 `CellCount`를 늘리는 문제가 아니다.

실제로 필요한 모델은 3개다.

1. `DataCellCount`
2. `RenderRowCount`
3. `RenderCellCount`

권장 해석:

- `DataCellCount` = 실제 데이터 개수
- `RenderRowCount` = `max(minimumLineCount, ceil(DataCellCount / columnCount))`
- `RenderCellCount` = `RenderRowCount * columnCount`

이렇게 해야:

- minimum line floor를 만족할 수 있고
- row가 늘 때마다 항상 `columnCount`개씩 cell이 늘고
- partial row 없이 full-row grid를 유지할 수 있다

---

## 4. 목표 동작 모델

### 4.1 Count 모델

`UIGridFrame` 내부 계산을 아래처럼 분리한다.

```csharp
public int DataCellCount { get; private set; }
public int DataRowCount => _columnCount > 0 && DataCellCount > 0
    ? Mathf.CeilToInt(DataCellCount / (float)_columnCount)
    : 0;

public int MinimumLineCount => Mathf.Max(0, _minimumLineCount);
public int RowCount => _columnCount > 0
    ? Mathf.Max(MinimumLineCount, DataRowCount)
    : 0;
public int RenderCellCount => RowCount * _columnCount;
```

`CellCount`는 기존 public surface 호환을 위해 유지하되,
의미를 "실제 데이터 개수"로 고정하는 쪽이 안전하다.

즉:

- `CellCount` = data count
- `RowCount` = render row count

외부에 data row 수가 필요하면 `DataRowCount`를 별도로 노출한다.

문서에서는 설명 편의를 위해 `RenderRowCount`라는 용어를 쓰지만,
코드 레벨 public 프로퍼티명은 기존 호환을 위해 `RowCount`를 유지하는 쪽이 낫다.

### 4.2 Minimum Line 필드

Inspector 필드 추가:

```csharp
[SerializeField] private int _minimumLineCount = 0;
```

권장 공개 API:

```csharp
public int MinimumLineCount
{
    get => _minimumLineCount;
    set => _minimumLineCount = Mathf.Max(0, value);
}

public void SetMinimumLineCount(int lineCount);
```

이 값은 "초기값"이면서 동시에 "항상 유지되는 row floor"로 동작해야 한다.
즉 one-shot bootstrap 값이 아니라 persistent floor다.

추가 유효성 규칙:

- `_minimumLineCount`는 0 미만으로 내려가지 않게 clamp
- `_columnCount <= 0`이면 `RowCount = 0`, `RenderCellCount = 0`

---

## 5. Render 계약

### 5.1 BindRow

`BindRow(...)`는 local row 하나당 항상 `_columnCount`개의 cell을 생성한다.

다만 invalid row에 대한 방어 코드는 추가해야 한다.

```csharp
if (_columnCount <= 0) return;
if (localRow < 0 || localRow >= RowCount) return;
```

현재 코드의:

```csharp
int cellsInRow = Mathf.Min(_columnCount, CellCount - startIndex);
```

는 제거 대상이다.

대신:

```csharp
int cellsInRow = _columnCount;
```

로 고정한다.

### 5.2 Placeholder Cell 계약

full-row 렌더링을 하면 일부 cell은 실제 데이터가 없는 placeholder가 된다.

판정 규칙:

```csharp
bool hasData = cellIndex < DataCellCount;
```

Phase 1 권장안은 public callback 시그니처를 유지하는 것이다.

즉:

- `cell.Show(cellIndex)`는 그대로 호출
- `onBindCell?.Invoke(cell, cellIndex)`도 그대로 호출
- consumer는 `cellIndex >= CellCount`를 placeholder로 해석

이 구분은 변경과 동시에 바로 필요하므로 helper를 Phase 1에 포함한다.

예:

```csharp
public bool HasDataAt(int cellIndex) => cellIndex >= 0 && cellIndex < CellCount;
```

이 방식이 현재 API 변경을 최소화한다.

### 5.3 RefreshRow / Invalid Row 방어

`RefreshRow(localRowIndex)`는 full-row bind 방식으로도 기존 구조와 호환된다.
이미 active row에 대해 `rowCells.Count` 기준으로 다시 bind하기 때문이다.

다만 row count가 줄어든 뒤 container가 아직 `Rebuild()`되지 않은 상태를 방어해야 한다.

권장 동작:

```csharp
if (localRowIndex < 0 || localRowIndex >= RowCount)
{
    UnbindRow(localRowIndex);
    return;
}
```

중요:

- 이 가드는 stale active row를 정리하기 위한 방어 코드다
- row count가 바뀌었으면 여전히 `UIScrollContainer.Rebuild()`가 정식 경로다
- `Refresh()`만으로 logical row 재계산을 대체할 수는 없다

---

## 6. Size 계약

### 6.1 Intrinsic Size

`GetHeight()`는 render row count, 즉 `RowCount` 기준으로 계산해야 한다.

```csharp
height = RowCount * cellSize.y
       + (RowCount - 1) * spacing.y
```

`GetWidth()`는 기존처럼 `columnCount` 기준으로 유지한다.

### 6.2 RectTransform 동기화

이번 요구는 계산값만이 아니라 실제 `RectTransform` size 변경까지 포함한다.

따라서 `UIGridFrame` 내부에 size 동기화 helper를 추가하는 것이 좋다.

권장 helper:

```csharp
private void SyncRectTransformSize();
```

최소 동작:

- 현재 `columnCount`
- `cellSize`
- `spacing`
- `RowCount`

를 기준으로 `rectTransform.sizeDelta`를 갱신한다.

helper 내부에서도 기본 방어를 둔다.

```csharp
if (rectTransform == null) return;
```

### 6.3 호출 시점

다음 시점마다 `SyncRectTransformSize()`가 호출되어야 한다.

1. `onInitComplete()`
2. `SetCellCount(...)`에서 `rectTransform`이 준비된 경우
3. `SetMinimumLineCount(...)`에서 `rectTransform`이 준비된 경우
4. `ColumnCount`, `CellSize`, `Spacing` 변경 메서드가 생기면 그 안에서

권장 정리:

- setter에서 size sync를 막을 필요는 없다
- 대신 `UIScrollContainer` 내부에서는 row count 변경 후 `Rebuild()`가 별도로 필요하다는 계약을 명확히 한다
- `ApplySectionLayout(...)`는 container layout 경계이므로 여기서 `SyncRectTransformSize()`를 호출하지 않는다

주의:

- `UIScrollContainer`는 section layout/visible row를 별도로 계산하므로,
  row count가 바뀌면 `UIScrollContainer.Rebuild()`도 같이 필요하다.
- `SyncRectTransformSize()`는 frame 자체 size를 맞추는 일이고,
  container의 logical row 재계산까지 대신하지는 않는다.

---

## 7. 런타임 수정 대상

### Runtime

- `/Users/maoshy/Documents/Projects/devian/framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/UIPackage/Runtime/Container/UIGridFrame.cs`

필요 시 mirror sync:

- `/Users/maoshy/Documents/Projects/devian/framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Container/UIGridFrame.cs`
- `/Users/maoshy/Documents/Projects/devian/framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/UIPackage/Runtime/Container/UIGridFrame.cs`

### Docs

- `/Users/maoshy/Documents/Projects/devian/skills/devian-unity/23-ui-package/23-ui-frame-grid/SKILL.md`

---

## 8. 구현 순서

1. `UIGridFrame`에 `_minimumLineCount`와 계산 프로퍼티 추가
2. `CellCount` 의미를 data count로 고정
3. `RowCount` 이름은 유지한 채 render row count 의미로 전환
4. `GetHeight()` / `GetLogicalRowCount()`를 render row 기준으로 수정
5. `HasDataAt(int cellIndex)` helper 추가
6. `BindRow(...)`를 full-row spawn + range guard 방식으로 변경
7. `RefreshRow(...)`에 invalid row 방어를 추가
8. `SyncRectTransformSize()` 추가 및 setter/init 경로에 연결
9. `ApplySectionLayout(...)`는 size sync 없이 유지
10. `23-ui-frame-grid` 스킬 문서 갱신

---

## 9. 검증 체크리스트

### Case A

- `columnCount = 4`
- `CellCount = 0`
- `MinimumLineCount = 2`

기대:

- `RenderRowCount = 2`
- `RowCount = 2`
- `RenderCellCount = 8`
- `GetHeight()` = 2 row 기준
- row bind 시 각 row마다 4개 cell 생성

### Case B

- `columnCount = 4`
- `CellCount = 3`
- `MinimumLineCount = 0`

기대:

- `RowCount = 1`
- `RenderCellCount = 4`
- partial row 없이 4개 cell 렌더
- `cellIndex == 3`은 placeholder slot

### Case C

- `columnCount = 4`
- `CellCount = 10`
- `MinimumLineCount = 2`

기대:

- `DataRowCount = 3`
- `RowCount = 3`
- `RenderCellCount = 12`
- 마지막 row도 4개 cell 렌더

### Case D

runtime에서 `SetMinimumLineCount()` 또는 `SetCellCount()` 후:

- `rectTransform.sizeDelta`가 즉시 갱신되는지
- `UIScrollContainer.Rebuild()` 후 visible row와 content size가 맞게 재계산되는지

### Case E

- `columnCount = 0`
- `CellCount = 10`
- `MinimumLineCount = 2`

기대:

- `RowCount = 0`
- `RenderCellCount = 0`
- `BindRow(...)` / `RefreshRow(...)`는 안전하게 return
- `GetWidth()` / `GetHeight()`가 음수 없이 안전하게 처리

---

## 10. 주의점

이번 변경은 `UIGridFrame`의 의미를 "데이터 수만큼만 생성하는 grid"에서
"full-row footprint를 유지하는 grid"로 넓힌다.

따라서 consumer는 다음 사실을 알아야 한다.

- `cellIndex < CellCount`인 cell만 실제 데이터 slot이다.
- 그 외 cell은 placeholder slot이다.

문서 업데이트 없이 코드만 바꾸면 사용자가 마지막 row placeholder를 버그로 오해할 가능성이 크다.
