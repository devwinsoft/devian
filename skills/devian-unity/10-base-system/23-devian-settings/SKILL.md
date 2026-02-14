# 23-devian-settings

Status: ACTIVE
AppliesTo: v10
Type: Component Specification

## 1. 목적

Unity 프로젝트에서 사용하는 설정을 `DevianSettings.asset` (ScriptableObject) 단일 정본으로 관리한다.

- DevianSettings.asset 단일 정본 (JSON 형태 금지)
- **정본 경로: `Assets/Resources/Devian/DevianSettings.asset`**
- deprecated/fallback 레이어를 만들지 않는다.

---

## 2. 네임스페이스

모든 C# 코드는 `namespace Devian`.

---

## 3. 파일 위치 (정본)

Runtime:

```
com.devian.foundation/Runtime/Unity/Settings/
└── DevianSettings.cs
```

Editor:

```
com.devian.foundation/Editor/Settings/
└── DevianSettingsMenu.cs
```

---

## 4. DevianSettings.asset 규약

### 경로 (Hard)

| 용도 | 경로 | 상수 |
|------|------|------|
| 정본 (Resources) | `Assets/Resources/Devian/DevianSettings.asset` | `DevianSettings.DefaultResourcesAssetPath` |
| Resources.Load 경로 | `Devian/DevianSettings` | `DevianSettings.ResourcesPath` |
| 레거시 (마이그레이션용) | `Assets/Settings/DevianSettings.asset` | `DevianSettings.LegacyAssetPath` |

### 기본값

- `assetId[EFFECT]` = `"Assets/Bundles/Effects"`
- `playerPrefsPrefix` = `"devian.game."`

### 필드

- `AssetIdEntry[] _assetId`: GroupKey → SearchDir 매핑
- `string _playerPrefsPrefix`: PlayerPrefs Key Prefix (dot suffix 포함)

### 규약 (Hard)

- `playerPrefsPrefix`는 공백 불가
- `playerPrefsPrefix`는 반드시 `'.'`로 끝나야 한다 (예: `devian.game.`)
- PlayerPrefs 키는 `DevianSettings.playerPrefsPrefix`로 시작해야 한다 (SSOT)

---

## 5. Editor 메뉴

- `Devian/Create Settings` 메뉴
- DevianSettings를 생성/보수
- 기존 `Assets/Settings/DevianSettings.asset`가 있으면 `Assets/Resources/Devian/`로 자동 마이그레이션

---

## 6. Runtime 접근

DevianSettings는 Resources에서 직접 로드하여 접근한다.

```csharp
var settings = Resources.Load<DevianSettings>(DevianSettings.ResourcesPath);
```

---

## 7. 금지(Hard)

- DevianSettings.json 생성/동기화 기능 금지 (.asset 단일 정본)
- deprecated/fallback settings(예: AssetIdSearchSettings) 추가/유지 금지
- 기존 레거시 호환 레이어로 남겨두지 말고, 같은 작업에서 즉시 삭제/정리한다.
- `Assets/Settings/` 경로에 새 에셋 생성 금지 (레거시 경로)

---

## 8. Reference

- Parent: `skills/devian-unity/10-base-system/skill.md`
- Bootstrap: `skills/devian-unity/10-base-system/27-bootstrap/SKILL.md`
