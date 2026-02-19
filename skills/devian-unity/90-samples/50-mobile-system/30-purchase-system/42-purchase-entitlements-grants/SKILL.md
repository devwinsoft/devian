# 42-purchase-entitlements-grants — Grants & Entitlements Rules (Server-side)

Status: ACTIVE
AppliesTo: v10

> Purchase SSOT: `skills/devian-unity/90-samples/50-mobile-system/30-purchase-system/03-ssot/SKILL.md` (C, D 섹션)

## 목적

`verifyPurchase` 결과의 `grants[]`와 `entitlements/current` 스냅샷을 서버에서 **어떤 규칙으로 계산/갱신하는지**를 정본으로 고정한다.

이 문서가 없으면 "지급" 정책이 구현 중 임의로 바뀔 수 있다.


---


## A. 입력 정본

- `internalProductId`가 정본이다.
- 상품 메타는 SSOT에서 합의된 PRODUCT 테이블을 따른다:
  - `input/Domains/Game/PurchaseTable.xlsx` (PRODUCT 시트)


---


## B. Grants 형식 (정본)

`grants[]` 각 항목은 `{ type, id, amount }` 형태다.

- `type: string` — `"item"` | `"currency"` (고정)
- `id: string`
- `amount: long` (`>= 0`만 허용)

Hard Rule:
- `grants[]`는 inventory 지급 전용이다.
- 권한/플래그 변화는 `grants[]`로 표현하지 않고, `entitlements/current` 스냅샷(`entitlementsSnapshot`)으로만 처리한다.

NOTE:
- `type=item`의 `id`는 `itemId(pk)`를 의미한다(`itemUid` 없음).
- `grants[]`에는 `options`가 포함되지 않는다. 아이템 `options`는 Inventory 내부 속성으로만 관리된다.

NEEDS CHECK:
- `id` 규칙(예: `gem`, `gold`, `item_sword_01`)을 확정해야 한다.


---


## C. Entitlements Snapshot (정본)

서버는 `verifyPurchase` 처리 후 entitlements를 재계산하여 `/users/{uid}/entitlements/current` 에 upsert 한다.

최소 필드:
- `noAds: bool`
- `subscriptions: object`
- `seasonPass: object`
- `updatedAt: Timestamp`


---


## D. 상태별 처리 (정본)

- `GRANTED`
  - grants 생성
  - entitlements 재계산 및 저장
- `ALREADY_GRANTED`
  - grants는 비우거나(권장) "이미 지급"을 나타내는 최소 응답
  - entitlements는 저장된 값 반환 가능
- `PENDING`
  - 지급 금지 (grants 없음)
  - entitlements 변경 금지
- `REJECTED`
  - 지급 금지
- `REFUNDED` / `REVOKED`
  - 지급 금지 (grants 없음)
  - entitlements 재계산 및 저장(철회 반영)


---


## DoD

Hard (must be 0)
- [ ] `internalProductId` → grants/entitlements 매핑이 "정본 데이터(테이블)" 기준임이 명시됐다.
- [ ] `PENDING/REJECTED`에서 지급 금지가 하드룰로 명시됐다.
- [ ] entitlements/current upsert 규칙이 명시됐다.

Soft
- [ ] 환불/철회 시나리오(REFUNDED/REVOKED) 재계산 예시 추가
