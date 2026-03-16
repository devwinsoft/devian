# 11-savedata-settings

Status: ACTIVE
AppliesTo: v10

`SaveDataSettings`는 SaveData 시스템의 설정 데이터를 보관하는 `ScriptableObject` 정본이다.
SaveDataManager의 Inspector `[SerializeField]`에 있던 설정을 분리하여 독립 에셋으로 관리한다.
민감하지 않은 설정이므로 AES 암호화 없이 평문으로 저장한다.

---

## Class

- 클래스: `SaveDataSettings : ScriptableObject`
- 네임스페이스: `Devian`
- 접근자: `public sealed`

### Constants

- `ResourcesPath` = `"Devian/SaveDataSettings"`
- `DefaultResourcesAssetPath` = `"Assets/Resources/Devian/SaveDataSettings.asset"`

### Fields (SerializeField)

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `_localRoot` | `SaveLocalRoot` | `PersistentData` | 로컬 저장 루트 경로 |
| `_primaryLocalFilename` | `string` | `"save/main.json"` | 프라이머리 로컬 파일명 |
| `_primaryCloudSlot` | `string` | `"main"` | 프라이머리 클라우드 슬롯 |

### Properties (public, read-only)

- `LocalRoot: SaveLocalRoot` (get)
- `PrimaryLocalFilename: string` (get)
- `PrimaryCloudSlot: string` (get)

---

## Asset Path (SSOT)

- Resources 경로: `Devian/SaveDataSettings`
- 프로젝트 에셋 경로: `Assets/Resources/Devian/SaveDataSettings.asset`

런타임 로드: `Resources.Load<SaveDataSettings>(SaveDataSettings.ResourcesPath)`

에셋 생성: Unity Editor → Project 패널 → Create → Devian → MobilePackage → SaveData Settings

---

## SaveDataManager Integration

SaveDataManager는 `_settings` 캐시 필드를 보유하며, `ensureSettings()`로 최초 접근 시 로드한다.

```
private SaveDataSettings _settings;

private SaveDataSettings ensureSettings()
{
    if (_settings == null)
        _settings = Resources.Load<SaveDataSettings>(SaveDataSettings.ResourcesPath);
    return _settings;
}
```

기존 Inspector `[SerializeField]`는 제거되었다:
- ~~`[SerializeField] private SaveLocalRoot _localRoot`~~
- ~~`[SerializeField] private string _primaryLocalFilename`~~
- ~~`[SerializeField] private string _primaryCloudSlot`~~

설정 접근 메서드:
- `getRootPath()`: `ensureSettings()?.LocalRoot ?? SaveLocalRoot.PersistentData`
- `getPrimaryLocalFilename()`: `ensureSettings()?.PrimaryLocalFilename ?? "save/main.json"`
- `getPrimaryCloudSlot()`: `ensureSettings()?.PrimaryCloudSlot ?? "main"`

설정 에셋이 없으면 기본값을 사용하고 LogWarning을 출력한다.

---

## Implementation Location (3-path mirror)

> 3-path mirror 정책: [devian-unity/04-package-policy](../../../04-package-policy/SKILL.md)

- SaveDataSettings.cs:
  - UPM (정본): `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/SaveData/SaveDataSettings.cs`
  - Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/SaveData/SaveDataSettings.cs`
  - Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/SaveData/SaveDataSettings.cs`

---

## Related

- [10-savedata-manager](../10-savedata-manager/SKILL.md) — SaveDataManager (설정 소비자)
- [41-savedata-savelocal](../41-savedata-savelocal/SKILL.md) — SaveLocal (LocalRoot 사용)
- [42-savedata-savecloud](../42-savedata-savecloud/SKILL.md) — SaveCloud (CloudSlot 사용)
