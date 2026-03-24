# 31-ui-canvas-id

Status: ACTIVE
AppliesTo: v1
Type: Component Specification

## 목적

`UIBaseCanvas` 프리팹을 참조하기 위한 string wrapper ID 타입.

## 파일 위치 (SSOT)

- Runtime: `com.devian.foundation/Samples~/UIPackage/Runtime/Base/UI_CANVAS_ID.cs`
- Editor Selector: `com.devian.foundation/Samples~/UIPackage/Editor/UICanvasIdSelector.cs`
- Editor Drawer: `com.devian.foundation/Samples~/UIPackage/Editor/UI_CANVAS_ID_Drawer.cs`

## String Wrapper 패턴

`UI_CONTAINER_ID`, `UI_SCROLL_CELL_ID`와 동일한 구조:

```csharp
[Serializable]
public sealed class UI_CANVAS_ID
{
    public string Value;
    public bool IsValid => !string.IsNullOrEmpty(Value);

    // implicit operators for string 호환
}
```

## Selector/Drawer 규약 (AssetId Base 준수)

### 필수 규칙

- **Apply/Create 버튼 금지**
- **ShowUtility() 필수**
- **Selector 캐싱 금지**
- **클릭 즉시 적용 + 창 자동 닫기**

### SearchDir 공급

- `UISettings.GetSearchDir("UI_CANVAS_ID")`로 조회
- 실패/폴더 없음이면 `"Assets"` fallback

### 스캔 대상

- `UIBaseCanvas` 또는 concrete subclass가 있는 Prefab 목록을 SearchDir에서 스캔
- `BaseEditorAssetIdSelector<UIBaseCanvas>` 재활용 (`AssetManager.FindPrefabs`)
- `prefab.name`을 ID 값으로 사용
- `@` prefix 이름 제외
- case-insensitive 중복 name은 에러 로그 후 스킵

## UISettings 등록

```text
entries[UI_CANVAS_ID] = "Assets/Bundles/UICanvases"
```

## Editor 구현

### Selector 클래스

```csharp
public sealed class UICanvasIdSelector : BaseEditorAssetIdSelector<UIBaseCanvas>
{
    protected override string GroupKey => "UI_CANVAS_ID";
    protected override string DisplayTypeName => "UI_CANVAS_ID";
}
```

### Drawer 클래스

```csharp
[CustomPropertyDrawer(typeof(UI_CANVAS_ID))]
public sealed class UI_CANVAS_ID_Drawer : BaseEditorID_Drawer<UICanvasIdSelector>
{
    // ShowUtility()로 창 표시
}
```

## 주의 사항

- `UIBaseCanvas`는 abstract base지만, selector는 prefab root의 `GetComponent<UIBaseCanvas>()`로 concrete subclass를 잡는다
- selector 캐싱 금지 (항상 `CreateInstance`)
- 런타임에서 `AssetDatabase`/`Resources.Load` 금지 (`AssetManager` 캐시만)

## Reference

- Parent: `skills/devian-unity/23-ui-package/SKILL.md`
- AssetId Base: `skills/devian-unity/20-common-package/12-asset-id/SKILL.md`
- UICanvas System: `skills/devian-unity/23-ui-package/10-base-system/11-ui-canvas-system/SKILL.md`
