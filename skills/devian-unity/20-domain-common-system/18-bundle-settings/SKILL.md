# 18-bundle-settings

Status: ACTIVE
AppliesTo: v10
Type: Component Specification

## 1. 목적

Unity 프로젝트에서 번들 경로 설정을 `BundleSettings.asset` (ScriptableObject) 단일 정본으로 관리한다.

- BundleSettings.asset 단일 정본 (JSON 형태 금지)
- **정본 경로: `Assets/Resources/Devian/BundleSettings.asset`**
- deprecated/fallback 레이어를 만들지 않는다.

---

## 2. 네임스페이스

모든 C# 코드는 `namespace Devian`.

---

## 3. 파일 위치 (정본)

Runtime:

```
com.devian.foundation/Runtime/Unity/Settings/
└── BundleSettings.cs
```

Editor:

```
com.devian.foundation/Editor/Settings/
└── BundleSettingsMenu.cs
```

---

## 4. BundleSettings.asset 규약

### 경로 (Hard)

| 용도 | 경로 | 상수 |
|------|------|------|
| 정본 (Resources) | `Assets/Resources/Devian/BundleSettings.asset` | `BundleSettings.DefaultResourcesAssetPath` |
| Resources.Load 경로 | `Devian/BundleSettings` | `BundleSettings.ResourcesPath` |

### 기본값

- `entries[COMMON_EFFECT_ID]` = `"Assets/Bundles/CommonEffects"`
- `entries[MATERIAL_EFFECT_ID]` = `"Assets/Bundles/MaterialEffects"`

### 필드

- `SettingsEntry[] _entries`: 범용 Key → Value 매핑

---

## 5. Editor 메뉴

- `Devian/Create Settings` 메뉴
- BundleSettings를 생성/보수
- 기본값(`COMMON_EFFECT_ID`, `MATERIAL_EFFECT_ID`)은 `BundleSettingsMenu` 내부에서 직접 보수한다

---

## 6. 금지(Hard)

- BundleSettings.json 생성/동기화 기능 금지 (.asset 단일 정본)
- deprecated/fallback settings(예: AssetIdSearchSettings) 추가/유지 금지

---

## 7. Reference

- Parent: `skills/devian-unity/20-domain-common-system/00-overview/SKILL.md`
