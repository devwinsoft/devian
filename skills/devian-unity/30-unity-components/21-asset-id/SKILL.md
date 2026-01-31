# 21-asset-id

Status: ACTIVE
AppliesTo: v10
Type: Component Specification

## 1. 목적

Unity Editor에서 특정 폴더의 prefab 목록을 스캔하여,
Inspector에서 string 기반 ID를 선택할 수 있도록 하는 **AssetId 패턴**을 정의한다.

- TableId 패턴과 동일한 UX (Select 버튼 + 검색 + 그리드 선택)
- 폴더 경로는 **DevianSettings(Assets/Settings/DevianSettings.asset)** 에서 공급받는다.
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

- `Assets/Settings/DevianSettings.asset` (JSON 형태 금지)

Runtime ID 타입(예: EFFECT_ID):

```
com.devian.foundation/Runtime/Unity/Effects/
└── EFFECT_ID.cs
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
- Selector는 ScriptableWizard 기반이므로 `ScriptableWizard.DisplayWizard<TSelector>(...)`를 사용해 **표시 상태로 생성**해야 한다.
- `CreateInstance`만 하고 Show 호출이 없는 구현은 금지한다.

권장 패턴(정본):

```csharp
protected override TSelector GetSelector()
{
    return ScriptableWizard.DisplayWizard<TSelector>("Select ...");
}
```

---

## 7. 금지(Hard)

- deprecated/fallback settings 추가/유지 금지 (예: AssetIdSearchSettings)
- DevianSettings.json 형태 금지 (.asset 단일 정본)
- Selector 캐싱 금지 (창을 닫았다가 다시 Select 시 창이 안 뜨는 버그 방지)
- `@`로 시작하는 prefab name은 목록에서 제외한다. (AssetManager 정책과 일치)
- ID 값은 prefab.name 그대로 저장한다.
