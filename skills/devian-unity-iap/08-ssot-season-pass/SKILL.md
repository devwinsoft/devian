# 08-ssot-season-pass — Season Pass (SSOT)


Status: ACTIVE
AppliesTo: v10


## SSOT 원칙


- 시즌 패스는 시즌별 구매 1회성 Entitlement로 운영한다.
- 시즌별 Store SKU를 분리한다(시즌별 SKU 1개 원칙).
- 소유 여부는 Firestore Entitlement 상태로 저장/복구한다.


정본(카탈로그): [05-ssot-product-catalog](../05-ssot-product-catalog/SKILL.md)
정본(검증/멱등): [06-ssot-verify-idempotency](../06-ssot-verify-idempotency/SKILL.md)


---


## 구매/지급 규칙


- `verifyPurchase` 결과가 `GRANTED`인 경우에만,
  해당 시즌 `owned=true`로 반영한다.
- 이미 소유(owned=true)인 시즌은 "추가 구매/중복 지급"이 발생하지 않도록 처리한다.
  - 중복 방지는 서버 멱등 원장과 Entitlement 상태가 최종 방어선이다.
