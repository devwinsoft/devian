# 15-ui-toast-frame-id

Status: ACTIVE
AppliesTo: v1
Type: Component Specification

## 목적

UIToastFrame 프리팹을 참조하기 위한 string wrapper ID 타입.
현재는 `ToastGroupConfig.ToastFrameId`가 이 타입을 사용한다.

## 파일 위치 (SSOT)

- Runtime: `com.devian.foundation/Samples~/UIPackage/Runtime/Toast/UI_TOAST_FRAME_ID.cs`
- Editor Selector: `com.devian.foundation/Samples~/UIPackage/Editor/UIToastFrameIdSelector.cs`
- Editor Drawer: `com.devian.foundation/Samples~/UIPackage/Editor/UI_TOAST_FRAME_ID_Drawer.cs`

## String Wrapper 패턴

UI_SCROLL_CELL_ID / UI_CONTAINER_ID와 동일한 구조:
```csharp
[Serializable]
public sealed class UI_TOAST_FRAME_ID
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
- UISettings.GetSearchDir(`"UI_TOAST_FRAME_ID"`)로 조회
- 실패/폴더 없음이면 `"Assets"` fallback

### 스캔 대상
- UIToastFrame 컴포넌트가 있는 Prefab 목록을 SearchDir에서 스캔
- `BaseEditorAssetIdSelector<UIToastFrame>` 재활용 (AssetManager.FindPrefabs)
- `prefab.name`을 ID 값으로 사용
- `@` prefix 이름 제외
- case-insensitive 중복 name은 에러 로그 후 스킵

## UISettings 등록

```
entries[UI_TOAST_FRAME_ID] = "Assets/Bundles/UIToastFrames"
```

## Editor 구현

### Selector 클래스
```csharp
public sealed class UIToastFrameIdSelector : BaseEditorAssetIdSelector<UIToastFrame>
{
    protected override string GroupKey => "UI_TOAST_FRAME_ID";
    protected override string DisplayTypeName => "UI_TOAST_FRAME_ID";
}
```

### Drawer 클래스
```csharp
[CustomPropertyDrawer(typeof(UI_TOAST_FRAME_ID))]
public sealed class UI_TOAST_FRAME_ID_Drawer : BaseEditorID_Drawer<UIToastFrameIdSelector>
{
    // ShowUtility()로 창 표시
}
```

## 금지 사항

- Selector 캐싱 금지 (항상 CreateInstance)
- Apply 버튼 금지 (SelectionGrid 클릭 즉시 적용/닫기)
- 런타임에서 AssetDatabase/Resources.Load 금지 (AssetManager 캐시만)

## Reference

- Parent: `skills/devian-unity/23-ui-package/13-ui-toast-system/00-overview/SKILL.md`
- AssetId Base: `skills/devian-unity/20-common-package/12-asset-id/SKILL.md`
- UIToastFrame: `skills/devian-unity/23-ui-package/13-ui-toast-system/13-frame/SKILL.md`
