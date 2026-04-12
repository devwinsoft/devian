# 12-first-reward-settings

Status: ACTIVE
AppliesTo: v10

`FirstRewardSettings`는 초기 보상 지급 데이터(`RewardData[]`)를 보관하는 `ScriptableObject` 정본이다.
저장 시 payload는 Crypto(AES)로 암호화되며, `RewardManager.FirstInitAsync()`에서 복호화 후 적용한다.

---

## Class

- 클래스: `FirstRewardSettings : ScriptableObject`
- 네임스페이스: `Devian`
- 필드:
  - `InitialRewards: CString`
  - `SelectedHeroUnitId: UNIT_HERO_ID`
- 기본값:
  - 복호화 원문: `[{"type":"CURRENCY","id":"GOLD","amount":1000}]`

---

## Asset Path (SSOT)

- Resources 경로: `Devian/FirstRewardSettings`
- 프로젝트 에셋 경로: `Assets/Resources/Devian/FirstRewardSettings.asset`

`FirstInitAsync`는 `Resources.Load<FirstRewardSettings>(FirstRewardSettings.ResourcesPath)`로 로드한다.

---

## Storage Contract

`FirstRewardSettings`에는 아래 2개 데이터가 저장된다.

- `InitialRewards(CString)`
  - 원본 데이터는 `RewardData[]` JSON
  - `RewardManager`의 Crypto key/iv로 AES 암호화
  - 암호화 payload(string, base64)를 `CString`에 저장
- `SelectedHeroUnitId(UNIT_HERO_ID)`
  - AES payload에 포함하지 않고 ScriptableObject 직렬화 필드로 직접 저장
  - 값은 `UNIT_HERO.unit_id`를 의미한다
  - first-init 시 대응 `ITEM_HERO.item_id`를 hero reward `+1`로 자동 합성한다
  - 같은 `ITEM_HERO` row의 `initial_slot_##`, `initial_item_##`도 `ITEM_EQUIP +1` reward로 자동 합성되고 지정 slot에 장착된다

런타임 로딩 시 (`RewardManager.FirstInitAsync`):

1. `CString`에서 payload 문자열을 읽는다
2. `MobileApplication`에서 AES 복호화한다
3. 복호화된 JSON을 `RewardData[]` 규약으로 파싱/검증한다
4. `RewardManager.ApplyRewardDatas`로 적용한다
   - 이때 `SelectedHeroUnitId`가 설정되어 있으면 대응 hero reward `+1`도 함께 적용된다
   - 같은 hero row의 `initial_item_##`도 `EQUIP +1`로 함께 적용된다
5. `InventoryManager.LoadSettings()`로 InventorySettings를 로드한다
6. `InventoryManager.ApplyCurrency(CURRENCY_TYPE.STAMINA, MaxStamina)`로 초기 스태미나 지급
7. `SelectedHeroUnitId`를 읽어 `TB_ITEM_HERO.unit_id -> item_id`로 해석한다
8. `initial_slot_##`, `initial_item_##`로 방금 지급한 equip uid를 찾아 hero slot에 장착한다
9. 해석된 `item_id`를 `InventoryManager.SelectedHeroId`에 적용한다

soft-fail:
- `SelectedHeroUnitId`가 비어 있거나 `ITEM_HERO` 매핑이 없으면 선택 hero만 비운다
- 선택 hero 적용 실패 때문에 first-init 전체를 실패시키지 않는다

암복호화 key/iv 소유 위치:
- `MobileApplication` (정본, `[SerializeField] CString` + public property)
- 속성:
  - `CryptoKey: string` (get only)
  - `CryptoIv: string` (get only)

---

## Inspector (CustomEditor)

`FirstRewardSettingsEditor`는 `InitialRewards`를 행 단위로 편집한다.

- 별도 필드:
  - `SelectedHeroUnitId`는 `UNIT_HERO_ID` selector로 편집한다
  - 의미는 "초기 선택 hero의 unit_id"다
  - 설정된 hero는 first-init 때 자동으로 x1 지급된다
  - 해당 `ITEM_HERO.initial_item_##`도 자동 지급되고 `initial_slot_##`에 장착된다
- reward row 편집:
  - `InitialRewards`는 기존처럼 행 단위로 편집한다

- 기존 행: Read-only TextBox(single line) + `삭제` 버튼
  - 표시 형식: `{Type}  |  {Id}  |  {Amount}`
  - `EditorGUI.DisabledScope(true)` + `EditorGUILayout.TextField`로 구현
- `id` 컬럼은 `type`에 따라 드롭다운 목록이 달라진다:
  - `CURRENCY` → `CURRENCY_TYPE` enum 팝업
  - `CARD` → `ITEM_CARD_ID` — `TB_ITEM_CARD` PK(`Item_id`) 목록
  - `EQUIP` → `ITEM_EQUIP_ID` — `TB_ITEM_EQUIP` PK(`Item_id`) 목록
  - `HERO` → `ITEM_HERO_ID` — `TB_ITEM_HERO` PK(`Item_id`) 목록
  - `RENTAL` → `ITEM_RENTAL_ID` — `TB_ITEM_RENTAL` PK(`Item_id`) 목록
  - `PASS` → `ITEM_PASS_ID` — `TB_ITEM_PASS` PK(`Item_id`) 목록
  - `TREASURE` → `ITEM_GRADE_TYPE` enum 팝업 (`NONE` 제외)
  - 테이블 미로드 시 `AssetManager.FindAssets` → `LoadFromNdjson`으로 자동 로드 (Selector 패턴)
  - 로드 후에도 빈 경우 텍스트 입력 폴백
- 각 줄의 오른쪽 `삭제` 버튼으로 해당 행 제거
- 맨 아래 입력 1줄(`type`, `id`, `amount`) + 오른쪽 `추가` 버튼으로 행 추가
- 변경 사항 저장 시 `RewardData[]` JSON으로 직렬화 후 AES 암호화하여 `InitialRewards`에 기록
- 암호화 key/iv는 `Assets/Resources/Devian/Application.prefab`의 `MobileApplication` 컴포넌트에서 읽는다
- 하단 `저장` 버튼으로 에셋 디스크 저장 (`AssetDatabase.SaveAssets()`)

제약:
- `id` 공백 금지
- `amount > 0`

---

## MobileApplication Inspector

`MobileApplicationEditor`는 key/iv 편의 기능을 제공한다.

- `Generate key iv` 버튼 제공
- 클릭 시 AES key(32 bytes) + iv(16 bytes)를 생성하고 base64 문자열로 저장
- 저장 위치: `MobileApplication._cryptoKey`, `MobileApplication._cryptoIv`

---

## JSON Parse Contract

JSON 원문은 아래 형식을 허용한다.

- `RewardData[]`
- `{ "rewards": RewardData[] }`

검증 규칙은 `RewardManager.parseFirstRewardSettings` 정본을 따른다.

---

## Implementation Location (3-path mirror)

> 3-path mirror 정책: [devian-unity/04-package-policy](../../../04-package-policy/SKILL.md)

- FirstRewardSettings:
  - UPM (정본): `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/Reward/FirstRewardSettings.cs`
  - Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/Reward/FirstRewardSettings.cs`
  - Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/Reward/FirstRewardSettings.cs`
- FirstRewardSettingsEditor:
  - UPM (정본): `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Editor/FirstRewardSettingsEditor.cs`
  - Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Editor/FirstRewardSettingsEditor.cs`
  - Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Editor/FirstRewardSettingsEditor.cs`
- MobileApplicationEditor:
  - UPM (정본): `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Editor/MobileApplicationEditor.cs`
  - Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Editor/MobileApplicationEditor.cs`
  - Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Editor/MobileApplicationEditor.cs`

---

## Related

- [10-reward-manager](../10-reward-manager/SKILL.md) — FirstInitAsync 흐름
- [03-ssot](../03-ssot/SKILL.md) — RewardData 규약 정본
- [11-rewarddata-interpretation](../11-rewarddata-interpretation/SKILL.md) — RewardData 해석
