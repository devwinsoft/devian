# 13-inventory-settings

Status: ACTIVE
AppliesTo: v10

`InventorySettings`는 인벤토리 시스템의 설정 데이터를 보관하는 `ScriptableObject` 정본이다.
저장 시 payload는 MobileApplication의 Crypto(AES)로 암호화되며, 런타임에서 복호화 후 사용한다.

---

## Class

- 클래스: `InventorySettings : ScriptableObject`
- 네임스페이스: `Devian`
- 필드:
  - `_settingsPayload: CString` — AES 암호화된 JSON payload
- 기본값:
  - 복호화 원문: `{"maxStamina":30,"staminaIntervalSeconds":300}`

---

## Asset Path (SSOT)

- Resources 경로: `Devian/InventorySettings`
- 프로젝트 에셋 경로: `Assets/Resources/Devian/InventorySettings.asset`

런타임 로드: `Resources.Load<InventorySettings>(InventorySettings.ResourcesPath)`

---

## Settings Fields

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `maxStamina` | int | 30 | 스태미나 최대치 |
| `staminaIntervalSeconds` | int | 300 | 스태미나 1 회복 주기 (초) |

런타임에서 InventoryManager는 설정 값을 `CInt`로 보관한다.

---

## Storage Contract

`_settingsPayload(CString)`에는 아래 순서로 저장된다.

1. 원본 데이터는 설정 JSON (`{"maxStamina":30,"staminaIntervalSeconds":300}`)
2. `MobileApplication`의 Crypto key/iv로 AES 암호화
3. 암호화 payload(string, base64)를 `CString`에 저장

런타임 로딩 시:

1. `CString`에서 payload 문자열을 읽는다
2. `MobileApplication`에서 AES 복호화한다
3. 복호화된 JSON을 파싱하여 설정 값을 읽는다

---

## Inspector (CustomEditor)

`InventorySettingsEditor`는 설정 값을 편집한다.

- `MaxStamina` — IntField (기본 30)
- `StaminaIntervalSeconds` — IntField (기본 300)
- 하단 `Save` 버튼으로 JSON 직렬화 → AES 암호화 → CString에 저장 → 에셋 디스크 저장
- 암호화 key/iv는 `Assets/Resources/Devian/Application.prefab`의 `MobileApplication` 컴포넌트에서 읽는다
- key/iv 미설정 시 경고 메시지 표시 + 암호화 없이 저장

---

## Implementation Location (3-path mirror)

> 3-path mirror 정책: [devian-unity/07-samples-creation-guide](../../../07-samples-creation-guide/SKILL.md)

- InventorySettings:
  - UPM (정본): `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Inventory/InventorySettings.cs`
  - Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/Inventory/InventorySettings.cs`
  - Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/Inventory/InventorySettings.cs`
- InventorySettingsEditor:
  - UPM (정본): `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Editor/InventorySettingsEditor.cs`
  - Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Editor/InventorySettingsEditor.cs`
  - Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Editor/InventorySettingsEditor.cs`

---

## Related

- [10-inventory-manager](../10-inventory-manager/SKILL.md) — InventoryManager (설정 소비자)
- [14-inventory-stamina-controller](../14-inventory-stamina-controller/SKILL.md) — InventoryStaminaController (MaxStamina, StaminaInterval 사용)
- [49-reward-system/12-first-reward-settings](../../49-reward-system/12-first-reward-settings/SKILL.md) — 동일 패턴 (AES + CString)
