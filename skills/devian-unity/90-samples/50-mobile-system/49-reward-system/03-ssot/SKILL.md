# 03-ssot — 49-reward-system (통합 SSOT)


Status: ACTIVE
AppliesTo: v10


## 이 문서가 정본이다 (SSOT)

Reward 관련 규칙의 단일 SSOT는 이 문서다.

- RewardData(보상 적용 입력) 규약
- rewardId 해석 책임(컨텐츠 레이어)
- RewardManager의 "지급 실행기" 규약

비정본(이 문서에서 다루지 않음):
- 멱등/기록/복구(ledger/pending/서버 확정)
  - Mission: `48-mission-system`
  - Purchase: `30-purchase-system`


---


## A) Core Terms (정본)

- `RewardData`: 보상 적용 입력 단위 (`{ type, id, amount }`)
- `RewardData[]`: 보상 적용 입력 payload (array)
- `rewardId`: 컨텐츠 레이어가 정의/관리하는 보상 키(string, 선택 API에서만 사용)


---


## B) RewardData Schema (정본)

`RewardData`는 `{ type, id, amount }` 형태다.

- `type: RewardType(enum)` — `RewardType.Item` | `RewardType.Currency` (고정)
- `id: string`
- `amount: long` (`>= 0`만 허용)

이 스키마 문단이 `RewardData`의 단일 정본이다.
`52-inventory-system`에서는 스키마를 재정의하지 않고 본 문서를 참조한다.

NOTE:
- `type=RewardType.Item`의 `id`는 `itemId(pk)`를 의미한다(`itemUid` 없음).
- Reward/Purchase grants에는 `options`가 없다. `options`는 Inventory 내부 속성으로만 관리된다.

정합:
- Purchase `verifyPurchase` 응답 `grants[]`(지급 내역)과 동일 형태를 사용한다.
- `grants[]`는 inventory 지급 전용이며, 권한/플래그 변화는 `entitlementsSnapshot`으로 처리한다.
  - 연관: `30-purchase-system/42-purchase-entitlements-grants`

NEEDS CHECK (형준 결정 필요):
- `id` 네이밍 규칙(예: `gem`, `gold`, `item_sword_01`)


---


## C) RewardId 해석 (정본)

RewardManager가 `TB_REWARD` 테이블을 직접 참조하여 `rewardId`를 `RewardData[]`로 변환한다.
`Devian.Samples.MobileSystem.asmdef`에 `Devian.Domain.Game` 참조가 포함되어 있다.

- lookup key: `rewardId` (TB_REWARD 테이블 키)
- 결과: `RewardData[]`

NOTE:
- RewardManager는 변환된 `RewardData[]`를 "적용(지급 실행)"만 한다(멱등/기록/복구 금지).


---


## D) RewardManager 적용 규약 (정본)

- RewardManager는 입력 `RewardData[]`를 받아 로컬 인벤토리에 적용한다.
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


---


## F) RewardData Runtime Type Location (reference)

- UPM original file:  
  `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/RewardManager/RewardData.cs`
- UnityExample Packages mirror:  
  `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/RewardManager/RewardData.cs`
- UnityExample Assets mirror:  
  `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/0.1.0/MobileSystem/Runtime/RewardManager/RewardData.cs`
- 정본/미러링 하드룰은 상위 정책 [devian-unity/01-policy](../../../../01-policy/SKILL.md)를 따른다.
