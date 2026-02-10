# 21-asset-id

Status: ACTIVE
AppliesTo: v10
Type: Component Specification

## 1. 목적

Unity Editor에서 특정 폴더의 prefab 목록을 스캔하여,
Inspector에서 string 기반 ID를 선택할 수 있도록 하는 **AssetId 패턴**을 정의한다.

- TableId 패턴과 동일한 UX (Select 버튼 + 검색 + 그리드 선택, 클릭 즉시 적용)
- 폴더 경로는 **DevianSettings(`Assets/Resources/Devian/DevianSettings.asset`)** 에서 공급받는다.
- deprecated/fallback 레이어는 만들지 않는다.

---

## 2. 네임스페이스

모든 코드는 `namespace Devian`.

---

## 3. 생성 대상 패키지

- `com.devian.foundation`

---

## 4. 파일 위치 (정본)

Editor:

```
com.devian.foundation/Editor/AssetId/
├── EditorAssetIdSelectorBase.cs
└── Generated/
    └── {ASSET}_ID.Editor.cs
```

Settings (단일 정본):

- `Assets/Resources/Devian/DevianSettings.asset` (JSON 형태 금지)

Runtime ID 타입(예: COMMON_EFFECT_ID):

```
com.devian.foundation/Runtime/Unity/Effects/
└── COMMON_EFFECT_ID.cs
```

---

## 5. SearchDir 공급 규약(Hard)

Selector는 `DevianSettings.asset`에서 GroupKey로 SearchDir를 찾는다.

- `DevianSettings.AssetId`에서 `{GroupKey, SearchDir}` 매칭
- 매칭 실패 시 `"Assets"` fallback (최후 수단)

AssetId 그룹은 **단일 SearchDir(string)** 만 지원한다.

---

## 6. Selector Window 표시 규약 (Hard)

- Drawer는 Select 버튼 클릭 시 **반드시 Selector 창을 화면에 표시**해야 한다.
- Selector 창에는 **Apply/Create 버튼이 존재하면 안 된다**. (SSOT: skills/devian-core/03-ssot/SKILL.md)
- 따라서 Selector는 **EditorWindow(Utility) 기반**으로 생성/표시한다.
- Selector 캐싱은 금지한다. (창을 닫았다가 다시 Select 시 창이 안 뜨는 버그 방지)
- `CreateInstance` 후 **반드시 ShowUtility()** 로 표시 상태로 만들어야 한다.

권장 패턴(정본):

```csharp
protected override TSelector GetSelector()
{
    var w = ScriptableObject.CreateInstance<TSelector>();
    w.ShowUtility();
    return w;
}
```

> NOTE: 기존 "ScriptableWizard 기반/DisplayWizard 강제" 문구는 SSOT의 "Apply 버튼 금지"와 충돌하므로 제거한다.

---

## 7. 금지(Hard)

- deprecated/fallback settings 추가/유지 금지 (예: AssetIdSearchSettings)
- DevianSettings.json 형태 금지 (.asset 단일 정본)
- Selector 캐싱 금지 (창을 닫았다가 다시 Select 시 창이 안 뜨는 버그 방지)
- `@`로 시작하는 prefab name은 목록에서 제외한다. (AssetManager 정책과 일치)
- ID 값은 prefab.name 그대로 저장한다.
- Apply 버튼 금지: 선택 리스트(SelectionGrid)에서 항목을 클릭하는 즉시 Value가 적용되고, Selector 창은 자동으로 닫혀야 한다.
