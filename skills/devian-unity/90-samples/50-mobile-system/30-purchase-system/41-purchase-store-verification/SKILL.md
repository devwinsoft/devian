# 41-purchase-store-verification — Store Verification (Apple + Google)

Status: ACTIVE
AppliesTo: v10

> Purchase SSOT: `skills/devian-unity/90-samples/50-mobile-system/30-purchase-system/03-ssot/SKILL.md` (C 섹션)

## 목적

서버(Cloud Functions)에서 Apple/Google 구매를 검증하기 위해 필요한:
- 어떤 검증 API를 쓸지
- 어떤 식별자를 `storePurchaseId`로 고정할지
- 원장/멱등 키(purchaseId)에 어떤 값을 사용할지
를 **정본으로 고정**한다.

이 스킬이 확정되지 않으면 40 스킬의 `purchaseId`가 결정될 수 없다.


---


## A. 공통 규칙 (정본)

- 검증은 반드시 서버에서 수행한다.
- 클라 payload는 "서버 검증에 필요한 최소 데이터"만 전달한다.
- 서버는 검증 결과로부터 `storePurchaseId`를 추출하고, 이를 기반으로 `purchaseId`를 만든다:
  - `purchaseId = "{storeKey}_{storePurchaseId}"`


---


## B. Google Play 검증 (정본)

NEEDS CHECK:
- 어떤 Google API(경로/버전)를 사용할지 레포 정책으로 확정해야 한다.

고정해야 할 항목(결정 후 문서에 값 채우기):
- 인증 방식: 서비스 계정 사용 여부 / 키 관리 방식(Secret Manager 등)
- `storePurchaseId`로 삼을 필드:
  - 예: orderId / purchaseToken 기반 등 (정확한 선택 필요)

검증 엔드포인트 분기 (kind 기준):
- `kind == Subscription` → `purchases.subscriptions.get`
- `kind == Rental` / `Consumable` / `SeasonPass` → `purchases.products.get` (one-time 검증 경로)

서버가 최소로 저장해야 할 필드(원장):
- `internalProductId`
- `storePurchaseId`
- `purchaseTime`
- `acknowledged` 또는 동등 상태(가능한 경우)
- `raw`(필요 시 일부만)


---


## C. Apple 검증 (정본)

NEEDS CHECK:
- Apple 검증 방식(App Store Server API / receipt 검증)의 선택을 확정해야 한다.

고정해야 할 항목(결정 후 문서에 값 채우기):
- `storePurchaseId`로 삼을 필드:
  - 예: originalTransactionId / transactionId 등 (정확한 선택 필요)

검증 엔드포인트:
- Apple `verifyReceipt`는 kind에 관계없이 동일 엔드포인트를 사용한다.
- `Rental`은 Apple에서도 one-time 구매로 처리된다.

서버가 최소로 저장해야 할 필드(원장):
- `internalProductId`
- `storePurchaseId`
- `purchaseTime`
- `revocation/refund` 관련 상태(가능한 경우)
- `raw`(필요 시 일부만)


---


## D. 상태 매핑 (정본)

서버는 최종적으로 SSOT의 `resultStatus` enum으로 반환한다:
- `GRANTED`, `ALREADY_GRANTED`, `REJECTED`, `PENDING`, `REVOKED`, `REFUNDED`

Firestore에는 소문자 상태로 저장한다:
- `granted`, `already_granted`, `rejected`, `pending`, `revoked`, `refunded`


---


## DoD

Hard (must be 0)
- [ ] Google 검증에서 `storePurchaseId`가 단일 규칙으로 확정됐다.
- [ ] Apple 검증에서 `storePurchaseId`가 단일 규칙으로 확정됐다.
- [ ] `purchaseId = "{storeKey}_{storePurchaseId}"` 규칙이 흔들리지 않는다.
- [ ] SSOT resultStatus ↔ Firestore status 소문자 규칙이 명시돼 있다.

Soft
- [ ] 저장 필드 최소화 원칙(PII/과다 저장 방지) 문구 추가
