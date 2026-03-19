# 41-ui-cell-id 계획

---

## 1. 목적

UIGridCell 프리팹을 참조하기 위한 string wrapper ID 타입 `UI_CELL_ID`를 도입한다.
`MATERIAL_EFFECT_ID` 패턴과 동일한 구조를 따른다.

---

## 2. 참조 패턴 (24-material-effect-id)

| 항목 | MATERIAL_EFFECT_ID | UI_CELL_ID |
|------|-------------------|------------|
| Wrapper class | `MATERIAL_EFFECT_ID` | `UI_CELL_ID` |
| Asset type | `MaterialEffectAsset` (ScriptableObject) | UIGridCell **Prefab** (GameObject with UIGridCell) |
| BundleSettings key | `"MATERIAL_EFFECT_ID"` | `"UI_CELL_ID"` |
| Selector base | `BaseEditorScriptableAssetIdSelector<MaterialEffectAsset>` | **신규 필요** — Prefab(GameObject) 스캔 |
| GroupKey | `"MATERIAL_EFFECT"` | `"UI_CELL"` |

### 핵심 차이

`BaseEditorScriptableAssetIdSelector<TAsset>`는 `where TAsset : ScriptableObject` 제약.
UIGridCell은 MonoBehaviour이므로 이 base를 직접 사용할 수 없다.

**해결: Prefab 기반 selector 작성**
- `t:Prefab`으로 SearchDir를 스캔
- 각 prefab에서 `GetComponent<UIGridCell>()` 확인
- prefab.name을 ID로 사용

---

## 3. 생성 파일

### Runtime

| 파일 | 위치 | 설명 |
|------|------|------|
| `UI_CELL_ID.cs` | `UIPackage/Runtime/Container/UI_CELL_ID.cs` | String wrapper |

### Editor

| 파일 | 위치 | 설명 |
|------|------|------|
| `UICellIdSelector.cs` | `UIPackage/Editor/UICellIdSelector.cs` | Prefab 기반 selector |
| `UI_CELL_ID_Drawer.cs` | `UIPackage/Editor/UI_CELL_ID_Drawer.cs` | PropertyDrawer |

---

## 4. UI_CELL_ID Runtime 클래스

```csharp
namespace Devian
{
    [Serializable]
    public sealed class UI_CELL_ID
    {
        public string Value;
        public bool IsValid => !string.IsNullOrEmpty(Value);

        public static implicit operator string(UI_CELL_ID obj)
            => obj == null ? string.Empty : (obj.Value ?? string.Empty);

        public static implicit operator UI_CELL_ID(string value)
            => new UI_CELL_ID { Value = value };
    }
}
```

MATERIAL_EFFECT_ID와 완전 동일한 구조.

---

## 5. Editor — Prefab 기반 Selector

`BaseEditorScriptableAssetIdSelector`를 사용할 수 없으므로,
`BaseEditorID_Selector`를 직접 상속하여 Prefab 스캔 로직을 구현한다.

```csharp
public sealed class UICellIdSelector : BaseEditorID_Selector
{
    protected override string GetDisplayTypeName() => "UI_CELL_ID";

    public override void Reload()
    {
        ClearItems();
        var searchDir = ResolveSearchDir("UI_CELL_ID");

        // Prefab 스캔 — UIGridCell 컴포넌트를 가진 것만
        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { searchDir });
        var normalizedSet = new HashSet<string>();

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            if (prefab.GetComponent<UIGridCell>() == null) continue;

            var name = prefab.name;
            if (string.IsNullOrEmpty(name) || name.StartsWith("@")) continue;

            var normalized = name.Trim().ToLowerInvariant();
            if (normalizedSet.Contains(normalized))
            {
                Debug.LogError($"[UI_CELL_ID] Duplicate prefab name: '{name}'. Skipping.");
                continue;
            }
            normalizedSet.Add(normalized);
            AddItem(name, name);
        }
    }

    private static string ResolveSearchDir(string key)
    {
        var settings = AssetDatabase.LoadAssetAtPath<BundleSettings>(
            BundleSettings.DefaultResourcesAssetPath);
        if (settings == null) return "Assets";

        var dir = settings.GetEntry(key);
        if (string.IsNullOrWhiteSpace(dir) || !AssetDatabase.IsValidFolder(dir))
            return "Assets";
        return dir;
    }
}
```

### Drawer

```csharp
[CustomPropertyDrawer(typeof(UI_CELL_ID))]
public sealed class UI_CELL_ID_Drawer : BaseEditorID_Drawer<UICellIdSelector>
{
    // title: "Select UI_CELL_ID"
    // ShowUtility() 필수
}
```

---

## 6. BundleSettings 등록

```
entries[UI_CELL_ID] = "Assets/Bundles/UICells"
```

---

## 7. UIGridFrame에서 사용

현재 UIGridFrame의 `_cellPrefabName`을 `UI_CELL_ID`로 교체 가능:

```csharp
// 변경 전
[SerializeField] private string _cellPrefabName;

// 변경 후
[SerializeField] private UI_CELL_ID _cellPrefabId;
public string CellPrefabName => _cellPrefabId.Value;
```

이 교체는 별도 단계로 수행. 이번 스킬은 ID 타입 정의만.

---

## 8. 스킬 문서

### 디렉토리

```
skills/devian-unity/23-ui-package/41-ui-cell-id/
└── SKILL.md
```

### 인덱스

`23-ui-package/SKILL.md` Components 테이블에 41번 추가.

---

## 9. 구현 순서

| 단계 | 작업 |
|------|------|
| 1 | `UI_CELL_ID.cs` Runtime 생성 |
| 2 | `UICellIdSelector.cs` Editor 생성 |
| 3 | `UI_CELL_ID_Drawer.cs` Editor 생성 |
| 4 | `41-ui-cell-id/SKILL.md` 생성 |
| 5 | `23-ui-package/SKILL.md` 인덱스 추가 |
| 6 | 3-path sync |

---

## 10. 규약 준수

- [ ] Apply/Create 버튼 금지
- [ ] ShowUtility() 필수
- [ ] Selector 캐싱 금지 (항상 CreateInstance)
- [ ] 클릭 즉시 적용 + 창 자동 닫기
- [ ] `@` prefix 이름 제외
- [ ] case-insensitive 중복 검사
- [ ] BundleSettings fallback: "Assets"
