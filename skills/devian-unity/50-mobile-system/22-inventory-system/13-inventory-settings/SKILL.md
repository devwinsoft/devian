# 13-inventory-settings

Status: ACTIVE
AppliesTo: v10

`InventorySetting`은 초기 인벤토리 지급 데이터(`RewardData[]`)를 보관하는 `ScriptableObject` 정본이다.
저장 시 payload는 Crypto(AES)로 암호화되며, `InventoryManager.FirstInitAsync()`에서 복호화 후 적용한다.

---

## Class

- 클래스: `InventorySetting : ScriptableObject`
- 네임스페이스: `Devian`
- 필드:
  - `InitialInventory: CString`
- 기본값:
  - payload(base64): `y7oR9tQzvr+hQd9BFh2hd6U0/B5itdaLwmVEboGI3xa7sJzLx9Fhlr1IYGqCCU6w`
  - 복호화 원문: `[{\"type\":\"CURRENCY\",\"id\":\"GOLD\",\"amount\":1000}]`

---

## Asset Path (SSOT)

- Resources 경로: `Devian/InventorySettings`
- 프로젝트 에셋 경로: `Assets/Resources/Devian/InventorySettings.asset`

`FirstInitAsync`는 `Resources.Load<InventorySetting>(InventorySetting.ResourcesPath)`로 로드한다.

---

## Storage Contract

`InitialInventory(CString)`에는 아래 순서로 저장된다.

1. 원본 데이터는 `RewardData[]` JSON
2. `InventoryManager`의 Crypto key/iv로 AES 암호화
3. 암호화 payload(string, base64)를 `CString`에 저장

런타임 로딩 시:

1. `CString`에서 payload 문자열을 읽는다
2. `InventoryManager`에서 AES 복호화한다
3. 복호화된 JSON을 `RewardData[]` 규약으로 파싱/검증한다
4. `InventoryManager.AddRewards`로 적용한다

암복호화 key/iv 소유 위치:
- `InventoryManager` (정본, serializable field + public property)
- 속성:
  - `InitialInventoryCryptoKey`
  - `InitialInventoryCryptoIv`

---

## Inspector (CustomEditor)

`InventorySettingEditor`는 `InitialInventory`를 행 단위로 편집한다.

- 기존 행: Read-only TextBox(single line) + `삭제` 버튼
  - 표시 형식: `{Type}  |  {Id}  |  {Amount}`
  - `EditorGUI.DisabledScope(true)` + `EditorGUILayout.TextField`로 구현
- `id` 컬럼은 `type`에 따라 드롭다운 목록이 달라진다:
  - `CURRENCY` → `CURRENCY_TYPE` enum 팝업
  - `CARD` → `ITEM_CARD_ID` — `TB_ITEM_CARD` PK(`CardId`) 목록
  - `EQUIP` → `ITEM_EQUIP_ID` — `TB_ITEM_EQUIP` PK(`EquipId`) 목록
  - `HERO` → `UNIT_HERO_ID` — `TB_UNIT_HERO` PK(`UnitId`) 목록
  - `RENTAL` → `ITEM_RENTAL_ID` — `TB_ITEM_RENTAL` PK(`RentalId`) 목록
  - `PASS` → `ITEM_PASS_ID` — `TB_ITEM_PASS` PK(`PassId`) 목록
  - `TREASURE` → `ITEM_TREASURE_ID` — `TB_ITEM_TREASURE.GetGroupKeys()` (group key = `ChestId`) 목록
  - 테이블 미로드 시 `AssetManager.FindAssets` → `LoadFromNdjson`으로 자동 로드 (Selector 패턴)
  - 로드 후에도 빈 경우 텍스트 입력 폴백
- 각 줄의 오른쪽 `삭제` 버튼으로 해당 행 제거
- 맨 아래 입력 1줄(`type`, `id`, `amount`) + 오른쪽 `추가` 버튼으로 행 추가
- 변경 사항 저장 시 `RewardData[]` JSON으로 직렬화 후 AES 암호화하여 `InitialInventory`에 기록
- 암호화 key/iv는 `Assets/Resources/Devian/Application.prefab`의 `InventoryManager` 컴포넌트에서 읽는다
- 하단 `저장` 버튼으로 에셋 디스크 저장 (`AssetDatabase.SaveAssets()`)

제약:
- `id` 공백 금지
- `amount > 0`

---

## InventoryManager Inspector

`InventoryManagerEditor`는 key/iv 편의 기능을 제공한다.

- `Generate key iv` 버튼 제공
- 클릭 시 AES key(32 bytes) + iv(16 bytes)를 생성하고 base64 문자열로 저장
- 저장 위치: `InventoryManager._initialInventoryCryptoKey`, `InventoryManager._initialInventoryCryptoIv`

---

## JSON Parse Contract

복호화된 JSON 원문은 아래 형식을 허용한다.

- `RewardData[]`
- `{ "rewards": RewardData[] }`

검증 규칙은 `InventoryManager.ParseInitialInventoryRewardsJson` 정본을 따른다.

---

## Related

- [10-inventory-manager](../10-inventory-manager/SKILL.md) — FirstInit 흐름
- [03-ssot](../03-ssot/SKILL.md) — Inventory 적용 규칙 정본
- [49-reward-system/11-rewarddata-interpretation](../../49-reward-system/11-rewarddata-interpretation/SKILL.md) — RewardData 해석
