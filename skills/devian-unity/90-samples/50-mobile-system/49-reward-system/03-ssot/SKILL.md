# 03-ssot — 49-reward-system (통합 SSOT)


Status: ACTIVE
AppliesTo: v10


## 이 문서가 정본이다 (SSOT)

Reward 관련 규칙의 단일 SSOT는 이 문서다.

- InventoryDelta(보상 적용 입력) 규약
- rewardId 해석 책임(컨텐츠 레이어)
- RewardManager의 "지급 실행기" 규약

비정본(이 문서에서 다루지 않음):
- 멱등/기록/복구(ledger/pending/서버 확정)
  - Mission: `48-mission-system`
  - Purchase: `30-purchase-system`


---


## A) Core Terms (정본)

- `InventoryDelta`: 보상 적용 입력 단위 (`{ type, id, amount }`)
- `InventoryDelta[]`: 보상 적용 입력 payload (array)
- `rewardId`: Game 도메인 `REWARD.rewardId(pk)` (선택 API에서만 사용)
  - source file: `input/Domains/Game/RewardTable.xlsx`
  - source table: `REWARD`


---


## B) InventoryDelta Schema (정본)

`InventoryDelta`는 `{ type, id, amount }` 형태다.

- `type: string` — `"item"` | `"currency"` (고정)
- `id: string`
- `amount: long` (`>= 0`만 허용)

NOTE:
- `type=item`의 `id`는 `itemId(pk)`를 의미한다(`itemUid` 없음).
- Reward/Purchase grants에는 `options`가 없다. `options`는 Inventory 내부 속성으로만 관리된다.

정합:
- Purchase `verifyPurchase` 응답 `grants[]`(지급 내역)과 동일 형태를 사용한다.
- `grants[]`는 inventory 지급 전용이며, 권한/플래그 변화는 `entitlementsSnapshot`으로 처리한다.
  - 연관: `30-purchase-system/42-purchase-entitlements-grants`

NEEDS CHECK (형준 결정 필요):
- `id` 네이밍 규칙(예: `gem`, `gold`, `item_sword_01`)


---


## C) RewardId 해석 책임 (정본)

rewardId는 컨텐츠(Game) 레이어에서 해석한다.
시스템 레이어(RewardManager)는 `REWARD` 테이블 타입/컬럼을 직접 참조하지 않는다.

컨텐츠 레이어는 아래를 만족하는 방식으로 rewardId를 `InventoryDelta[]`로 변환해야 한다:

- lookup key: `REWARD.rewardId == rewardId`
- 결과: `InventoryDelta[]`

NOTE:
- RewardManager는 변환된 `InventoryDelta[]`를 "적용(지급 실행)"만 한다(멱등/기록/복구 금지).


---


## D) RewardManager 적용 규약 (정본)

- RewardManager는 입력 `InventoryDelta[]`를 받아 로컬 인벤토리에 적용한다.
- RewardManager는 다음을 하지 않는다:
  - 멱등 처리(`grantId` 사용 금지)
  - 지급 기록(ledger) 저장
  - 서버 확정/조회


---


## E) 책임 분리 링크(정본)

- Mission(무료): 지급 조회/기록/개별 지급 기록/리셋 정본
  - [48-mission-system/01-policy](../../48-mission-system/01-policy/SKILL.md)
- Purchase(유료): 멱등/기록/복구(restore 포함) 정본
  - [30-purchase-system/03-ssot](../../30-purchase-system/03-ssot/SKILL.md)
