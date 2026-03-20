# UI_CANVAS_ID 계획

## 1. 목표

`23-ui-package` 하위에 `12-ui-canvas-id` 스킬을 추가하고,
`UICanvas` 프리팹을 참조하는 `UI_CANVAS_ID`를 도입한다.

목적:

- canvas prefab 참조를 string wrapper ID로 통일
- inspector에서 selector/drawer로 concrete `UICanvas` prefab 선택
- 기존 `UI_CONTAINER_ID`, `UI_CELL_ID`, `UI_TRANSITION_PRESET_ID` 패턴과 정합성 유지

---

## 2. 구현 대상

### Runtime

- `UIPackage/Runtime/UI_CANVAS_ID.cs`

### Editor

- `UIPackage/Editor/UICanvasIdSelector.cs`
- `UIPackage/Editor/UI_CANVAS_ID_Drawer.cs`

### Settings

- `CommonPackage/Editor/Unity/Settings/BundleSettingsMenu.cs`

### Skill

- `skills/devian-unity/23-ui-package/12-ui-canvas-id/SKILL.md`
- `skills/devian-unity/23-ui-package/SKILL.md`

---

## 3. 데이터 모델

`UI_CANVAS_ID`는 기존 string wrapper 패턴을 그대로 따른다.

```csharp
[Serializable]
public sealed class UI_CANVAS_ID
{
    public string Value;
    public bool IsValid => !string.IsNullOrEmpty(Value);

    public static implicit operator string(UI_CANVAS_ID obj) { ... }
    public static implicit operator UI_CANVAS_ID(string value) { ... }
}
```

규칙:

- 런타임에서 `AssetDatabase` 직접 접근 금지
- 런타임 해상도는 `AssetManager.GetAsset<T>(id.Value)` 또는 `UIManager.CreateCanvas/EnsureCanvas` 호출자가 담당
- ID는 prefab name 기준이다

---

## 4. Editor 패턴

### 4.1 Selector

1차 권장안:

```csharp
public sealed class UICanvasIdSelector : BaseEditorAssetIdSelector<UICanvas>
{
    protected override string GroupKey => "UI_CANVAS_ID";
    protected override string DisplayTypeName => "UI_CANVAS_ID";
}
```

의도:

- concrete subclass (`UIGameCanvas`, `UILobbyCanvas`, `UILoginCanvas`, `UIToastCanvas`)가 붙은 prefab을 search dir에서 스캔
- prefab name을 ID 값으로 사용

### 4.2 Drawer

```csharp
[CustomPropertyDrawer(typeof(UI_CANVAS_ID))]
public sealed class UI_CANVAS_ID_Drawer : BaseEditorID_Drawer<UICanvasIdSelector>
{
    protected override UICanvasIdSelector GetSelector()
    {
        var w = ScriptableObject.CreateInstance<UICanvasIdSelector>();
        w.ShowUtility();
        return w;
    }
}
```

### 4.3 BundleSettings

기본 키 추가:

```text
entries[UI_CANVAS_ID] = "Assets/Bundles/UICanvases"
```

---

## 5. 리스크 / 확인 포인트

### 5.1 Abstract `UICanvas` 스캔

`UICanvas`는 abstract base다.
따라서 `BaseEditorAssetIdSelector<UICanvas>`가 concrete subclass prefab을 정상적으로 찾는지 확인이 필요하다.

검증 기준:

- prefab에 `UIGameCanvas : UICanvas<UIGameCanvas>` 같은 concrete 타입이 붙어 있을 때 selector 목록에 노출되는지

### 5.2 Fallback 계획

만약 `BaseEditorAssetIdSelector<UICanvas>`가 abstract base 때문에 정상 동작하지 않으면,
selector는 custom scan으로 내린다.

fallback 방향:

- `AssetManager.FindPrefabs<Component>(searchDir)` 같은 우회는 쓰지 않는다
- `AssetDatabase.FindAssets("t:Prefab")`로 prefab을 찾고,
  prefab root에서 `GetComponent<UICanvas>() != null` 필터를 적용하는 custom selector를 만든다

즉 구현 순서는:

1. 기존 base selector 재활용 시도
2. 동작 불가 시 custom selector로 전환

---

## 6. 스킬 문서 계획

`12-ui-canvas-id/SKILL.md`에는 다음을 정리한다.

- `UI_CANVAS_ID` 목적
- runtime/editor 파일 위치
- string wrapper 패턴
- selector/drawer 규약
- `BundleSettings` 키
- abstract `UICanvas` 스캔 규칙 또는 fallback 설명

`23-ui-package/SKILL.md` 인덱스에는 다음 entry를 추가한다.

```text
12 | UI_CANVAS_ID | UICanvas prefab 참조 ID (AssetId 패턴)
```

---

## 7. 구현 순서

### Phase 1. Runtime ID

- `UI_CANVAS_ID.cs`

### Phase 2. Editor Selector / Drawer

- `UICanvasIdSelector.cs`
- `UI_CANVAS_ID_Drawer.cs`

### Phase 3. Settings

- `BundleSettingsMenu.cs`에 `UI_CANVAS_ID` 기본 entry 추가

### Phase 4. Skills

- `12-ui-canvas-id/SKILL.md`
- `23-ui-package/SKILL.md` 인덱스 갱신

### Phase 5. 검증

- `git diff --check`
- `rg "UI_CANVAS_ID|UICanvasIdSelector"` 확인
- 가능하면 `Devian.Samples.UIPackage.csproj`, `Devian.Samples.UIPackage.Editor.csproj` 빌드

---

## 8. 완료 기준

1. `UI_CANVAS_ID`가 runtime string wrapper로 추가된다
2. inspector에서 concrete `UICanvas` prefab을 선택할 수 있다
3. `BundleSettings`에 `UI_CANVAS_ID` search dir 키가 기본 등록된다
4. `12-ui-canvas-id` 스킬과 `23-ui-package` 인덱스가 갱신된다
