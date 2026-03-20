# 40-ui-container-id

Status: ACTIVE
AppliesTo: v1
Type: Component Specification

## 목적

UIBaseContainer 프리팹을 참조하기 위한 string wrapper ID 타입.

## 파일 위치 (SSOT)

- Runtime: `com.devian.foundation/Samples~/UIPackage/Runtime/Container/UI_CONTAINER_ID.cs`
- Editor Selector: `com.devian.foundation/Samples~/UIPackage/Editor/UIContainerIdSelector.cs`
- Editor Drawer: `com.devian.foundation/Samples~/UIPackage/Editor/UI_CONTAINER_ID_Drawer.cs`

## String Wrapper 패턴

UI_CELL_ID와 동일한 구조:
```csharp
[Serializable]
public sealed class UI_CONTAINER_ID
{
    public string Value;
    public bool IsValid => !string.IsNullOrEmpty(Value);

    // implicit operators for string 호환
}
```

## Selector/Drawer 규약 (12-asset-id 준수)

### 필수 규칙
- **Apply/Create 버튼 금지**
- **ShowUtility() 필수**
- **Selector 캐싱 금지**
- **클릭 즉시 적용 + 창 자동 닫기**

### SearchDir 공급
- BundleSettings.GetEntry(`"UI_CONTAINER_ID"`)로 조회
- 실패/폴더 없음이면 `"Assets"` fallback

### 스캔 대상
- UIBaseContainer 컴포넌트가 있는 Prefab 목록을 SearchDir에서 스캔
- `BaseEditorAssetIdSelector<UIBaseContainer>` 재활용 (AssetManager.FindPrefabs)
- `prefab.name`을 ID 값으로 사용
- `@` prefix 이름 제외
- case-insensitive 중복 name은 에러 로그 후 스킵

## BundleSettings 등록

```
entries[UI_CONTAINER_ID] = "Assets/Bundles/UIContainers"
```

## Editor 구현

### Selector 클래스
```csharp
public sealed class UIContainerIdSelector : BaseEditorAssetIdSelector<UIBaseContainer>
{
    protected override string GroupKey => "UI_CONTAINER_ID";
    protected override string DisplayTypeName => "UI_CONTAINER_ID";
}
```

### Drawer 클래스
```csharp
[CustomPropertyDrawer(typeof(UI_CONTAINER_ID))]
public sealed class UI_CONTAINER_ID_Drawer : BaseEditorID_Drawer<UIContainerIdSelector>
{
    // ShowUtility()로 창 표시
}
```

## 금지 사항

- Selector 캐싱 금지 (항상 CreateInstance)
- Apply 버튼 금지 (SelectionGrid 클릭 즉시 적용/닫기)
- 런타임에서 AssetDatabase/Resources.Load 금지 (AssetManager 캐시만)

## Reference

- Parent: `skills/devian-unity/23-ui-package/SKILL.md`
- AssetId Base: `skills/devian-unity/20-common-package/12-asset-id/SKILL.md`
- UIBaseContainer: `skills/devian-unity/23-ui-package/11-ui-canvas-system/SKILL.md`
