# MATERIAL_EFFECT_ID

## 목적
MaterialEffectAsset을 참조하기 위한 string wrapper ID 타입

## 파일 위치 (SSOT)
- Runtime: `com.devian.foundation/Runtime/Unity/MaterialEffect/MATERIAL_EFFECT_ID.cs`
- Editor Selector: `com.devian.foundation/Editor/AssetId/Generated/MATERIAL_EFFECT_ID.Editor.cs`

## String Wrapper 패턴

COMMON_EFFECT_ID와 동일한 구조:
```csharp
[Serializable]
public sealed class MATERIAL_EFFECT_ID
{
    public string Value;
    public bool IsValid => !string.IsNullOrEmpty(Value);

    // implicit operators for string 호환
}
```

## Selector/Drawer 규약 (21-asset-id 준수)

### 필수 규칙
- **Apply/Create 버튼 금지**
- **ShowUtility() 필수**
- **Selector 캐싱 금지**
- **클릭 즉시 적용 + 창 자동 닫기**

### SearchDir 공급
- DevianSettings.asset의 AssetIdEntry에서 `GroupKey="MATERIAL_EFFECT"`로 조회
- 실패/폴더 없음이면 `"Assets"` fallback

### 스캔 대상
- MaterialEffectAsset(ScriptableObject) 목록을 SearchDir에서 스캔
- `asset.name`을 ID 값으로 사용
- `@` prefix 이름 제외
- case-insensitive 중복 name은 에러 로그 후 스킵

## DevianSettings 등록

```
assetId[MATERIAL_EFFECT] = "Assets/Bundles/MaterialEffects"
```

## Editor 구현

### Selector 클래스
```csharp
public sealed class MaterialEffectIdSelector : BaseEditorScriptableAssetIdSelector<MaterialEffectAsset>
{
    protected override string GroupKey => "MATERIAL_EFFECT";
    protected override string DisplayTypeName => "MATERIAL_EFFECT_ID";
}
```

### Drawer 클래스
```csharp
[CustomPropertyDrawer(typeof(MATERIAL_EFFECT_ID))]
public sealed class MATERIAL_EFFECT_ID_Drawer : BaseEditorID_Drawer<MaterialEffectIdSelector>
{
    // ShowUtility()로 창 표시
    // title: "Select MATERIAL_EFFECT_ID"
}
```

## 금지 사항

- Selector 캐싱 금지 (항상 CreateInstance)
- Apply 버튼 금지 (SelectionGrid 클릭 즉시 적용/닫기)
- 런타임에서 AssetDatabase/Resources.Load 금지 (AssetManager 캐시만)
