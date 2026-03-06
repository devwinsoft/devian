# 03-ssot — 20-operation


Status: ACTIVE
AppliesTo: v10
ParentSSOT: skills/devian/80-tools/03-ssot/SKILL.md


## Scope

Operation 웹앱의 **기능(탭) 정의**를 관리한다.
각 탭의 입출력, 파이프라인, 참조 SSOT를 이 문서에서 정의한다.


---


## A) Version Config (구현됨)

앱 버전 설정 탭.

| 항목 | 값 |
|------|-----|
| Firestore 문서 | `/config/appVersion` |
| 입력 | `minVersion`, `currentVersion` (`#.#.#`) |
| 출력 | 저장/로드 상태 메시지 |
| 버튼 | [Load], [Save] |


---


## B) Initial Inventory (구현됨)

초기 지급 `RewardData[]`를 설정하는 탭.
**탭 순서상 2번째 위치**에 추가한다.

| 항목 | 값 |
|------|-----|
| Firestore 문서 | `/config/initialInventory` |
| 입력 | `rewards: RewardData[]` (UI row 입력) |
| 출력 | 저장/로드 상태 메시지 + 유효성 검증 결과 |
| 버튼 | row [ - ] / add [ + ] / [Import Reward IDs] / [Save] |
| ID 소스 | `/config/rewardIdCatalog` (`currencyIds/equipIds/cardIds/heroIds`) |

### RewardData[] 스키마

```json
{
  "rewards": [
    { "type": "CURRENCY", "id": "GOLD", "amount": 1000 },
    { "type": "CARD", "id": "card_fire_001", "amount": 1 }
  ]
}
```

필드 규칙:
- `type`: `REWARD_TYPE` enum 이름 문자열 (`CARD|CURRENCY|EQUIP|HERO|RENTAL|SEASON_PASS`)
- `id`: 비어있지 않은 문자열
- `amount`: 양의 정수

ID listbox 규칙:
- `CURRENCY` 선택 시 `/config/rewardIdCatalog.currencyIds`를 listbox 옵션으로 사용한다.
- `EQUIP`/`CARD`/`HERO` 선택 시 `/config/rewardIdCatalog`의 `equipIds/cardIds/heroIds`를 listbox 옵션으로 사용한다.
- `RENTAL`/`SEASON_PASS`는 Initial Inventory UI에서 추가 선택을 지원하지 않는다.

catalog 문서 정본:
- `/config/rewardIdCatalog`
- import 경로: [20-excel-reward-id-export](../20-excel-reward-id-export/SKILL.md) (`ENUM_TYPES.json` + xlsx)

서버 연동 정본:
- callable: `getInitialInventory`
- core: `functions/src/inventory/getInitialInventoryCore.ts`
- 1회 지급 marker: `/users/{uid}/meta/initialInventory`
- 서버 검증: invalid row가 있으면 `failed-precondition`으로 실패하고 marker를 기록하지 않는다.

UI 동작 정본:
- 탭(form) 생성 완료 직후 Firestore 문서를 자동 로드한다.
- 탭(form) 생성 완료 직후 `/config/rewardIdCatalog`도 함께 로드한다.
- 기존 reward row는 우측 `-` 버튼으로 제거한다.
- 하단 입력 row(`type/id-listbox/amount`)에서 `+` 버튼으로 신규 row를 append한다.
- `Import Reward IDs` 버튼은 local dev server endpoint(`POST /__operation/import-reward-id-catalog`)로 importer 스크립트를 실행한다.
- importer stdout/stderr는 status message로 표시한다.
- 하단 `Save` 버튼으로 전체 `rewards` 배열을 저장한다.


---


## C) Obfuscate

ComplexUtil byte-substitution. 평문 → 난독화.

| 항목 | 값 |
|------|-----|
| 입력 | 평문 바이트 |
| 출력 | 난독화된 바이트 |
| 버튼 | [Obfuscate] |

> ComplexUtil 정본: [31-variable-complex](../../../10-module/20-core/31-variable-complex/SKILL.md)


---


## D) Deobfuscate

ComplexUtil byte-substitution. 난독화 → 평문.

| 항목 | 값 |
|------|-----|
| 입력 | 난독화된 바이트 |
| 출력 | 평문 바이트 |
| 버튼 | [Deobfuscate] |

> ComplexUtil 정본: [31-variable-complex](../../../10-module/20-core/31-variable-complex/SKILL.md)


---


## E) Save Data

Save Data 편집 워크플로우. 파일 확장자(`.json` / `.dvn`)에 따라 자동 분기.


### 입력 형식

| 확장자 | 출처 | 외부 래핑 |
|--------|------|-----------|
| `.json` | Unity Editor 로컬 저장 | 없음 (SaveLocalPayload JSON 그대로) |
| `.dvn` | 모바일 Export | DVN 인코딩 (version byte + ComplexUtil + HMAC) |

두 형식 모두 내부 `payload` 필드는 `ComplexUtil.Encrypt_Base64(게임 상태 JSON)`.
`.dvn` v2는 HMAC 무결성 검증을 포함한다 (§DVN 파이프라인 참조).


### 양방향 프로세스

```
[SaveDataManager]                    [Operation]
  ExportDvnAsync  ── .dvn ──→  Import (decode)
  로컬 저장        ── .json ──→  Import (parse)
                                   ↓ JSON 편집
  ImportDvnAsync  ←── .dvn ──  Export (encode)
  RestoreFromPlainJsonAsync ←── .json ──  Export (serialize)
```

### DVN 파이프라인

v2 (현재, HMAC 포함):
```
Export (.dvn):
  1. JSON에서 account.socialUserId 추출
  2. HMAC-SHA256(JSON, APP_SECRET + socialUserId) → hmac_hex
  3. signedPayload = JSON + "\n" + hmac_hex
  4. ComplexUtil.Encrypt_Base64(signedPayload) → obfuscated
  5. (char)0x02 + obfuscated → .dvn

Import (.dvn):
  1. version parse (0x02)
  2. ComplexUtil.Decrypt_Base64 → signedPayload
  3. signedPayload 분리 → JSON + hmac_hex
  4. HMAC 검증 (APP_SECRET + socialUserId)
  5. Return JSON
```

v1 (레거시, HMAC 없음):
```
Import (.dvn): .dvn → version parse (0x01) → ComplexUtil.Decrypt_Base64 → JSON
```

- Export는 항상 v2로 생성한다.
- Import는 v1/v2 모두 허용한다 (v1은 HMAC 검증 없이 통과).
- Operation 웹앱은 APP_SECRET을 코드 내 상수로 보유한다 (dvn-codec.ts).

정본: [25-recovery-system/03-ssot](../../../../devian-unity/50-mobile-system/25-recovery-system/03-ssot/SKILL.md)
- DVN 포맷: §A
- 인코딩 파이프라인: §B
- HMAC Integrity: §E


---


## Login Server Flow (정본)

로그인 시 서버 호출은 아래 순서를 따른다:

1. `initSession` callable (`getMissionClock + getEntitlements + getPurchaseAdjustments`)
2. `SyncGameStorageAsync`로 local/cloud 동기화
3. `SyncState.Initial`일 때만 `getInitialInventory` callable 호출
4. 서버는 transaction으로 marker를 기록하여 1회 지급을 보장

`initSession`은 초기 인벤토리를 포함하지 않는다.
초기 지급은 별도 callable(`getInitialInventory`)로 분리한다.


---


## Related

- [00-overview](../00-overview/SKILL.md) — Operation 개요
- [01-policy](../01-policy/SKILL.md) — 정책
- [20-excel-reward-id-export](../20-excel-reward-id-export/SKILL.md) — xlsx id catalog import
- [Tools SSOT](../../03-ssot/SKILL.md) — 상위 SSOT
