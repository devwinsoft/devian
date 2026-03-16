---
name: 48-purchase-audit-google-sheets
description: Define and implement Google Sheets based purchase and refund audit logging for PurchaseManager server flows. Use when adding or changing external purchase/refund logs outside Firestore, deciding append row schema, spreadsheet configuration, or server write points in Firebase Functions.
---

# 48-purchase-audit-google-sheets — Google Sheets Audit

Status: ACTIVE
AppliesTo: v10

> Purchase SSOT: `../03-ssot/SKILL.md`

> Backend 정본: `../40-purchase-backend-firebase/SKILL.md`

> Refund 파이프라인: `../45-purchase-refund-processing/SKILL.md`


## 문서 경계 (Scope)

- 이 문서는 **Google Sheets 기반 감사 로그** 구현/운영 정본이다.
- 포함: 서버 write 포인트, purchase/refund event 타입, 월별 테이블 명세, spreadsheet/tab 구성, 멱등/보안 규칙
- 비포함: Firestore 원장 스키마 (→ `40`), 환불 상태 전이 자체 (→ `45`), Callable 이름/결정값 고정 (→ `46`), grants/entitlements 계산 (→ `42`)


## 목적

`PurchaseManager` 구매/환불 흐름에서 Firestore와 별도로 **Google Sheets에 append-only 감사 로그**를 남기기 위한 기준을 고정한다.

이 로그는 집계/감사/장애 추적용 **secondary sink** 이며,
구매 승인/환불/권한 회수의 **source of truth가 아니다**.


---

## A. Hard Rules

- 클라이언트(`PurchaseManager`)가 Google Sheets에 직접 write 하지 않는다. **Firebase Functions 서버만 write** 한다.
- 감사 로그 write 실패는 **non-fatal** 로 취급한다. 구매 승인/환불 상태 변경 자체를 rollback 하지 않는다.
- Google Sheets 로그는 **append-only audit sink** 로 사용한다. entitlements 계산, 재구매 판정, 환불 처리 여부를 Sheet에서 읽지 않는다.
- 원본 영수증(raw receipt), Apple receipt base64, 전체 `storeResponse` JSON은 Sheet row에 저장하지 않는다.
- 모든 시각은 **서버 UTC** 기준으로 기록한다. 클라이언트/디바이스 시간은 사용하지 않는다.
- Spreadsheet는 월별 탭(`YYYY-MM`) 구조를 사용한다.
- 이벤트 row는 월별 탭에만 append 한다.
- 월별 탭 컬럼은 아래 11개로 고정한다.
  - `loggedAtUtcIso`
  - `purchaseId`
  - `storeKey`
  - `internalProductId`
  - `kind`
  - `storeProductId`
  - `storePurchaseId`
  - `verifyStatus`
  - `region`
  - `storePurchasedAtUtcIso`
  - `eventOccurredAtUtcIso`
- Sheet 컬럼 변경은 정본 문서와 코드가 동시에 수정될 때만 허용한다.


---

## B. 최소 로그 이벤트 (정본)

최소 필수 이벤트:

| eventType | 서버 write 포인트 | 기록 조건 |
|----------|-------------------|-----------|
| `PURCHASE_GRANTED` | `verifyPurchase` | Firestore 트랜잭션 결과가 신규 `GRANTED` 일 때만 |
| `PURCHASE_REFUNDED` | `handleGooglePlayNotification` | `verifyStatus -> REFUNDED` 상태 변경이 실제로 반영됐을 때 |
| `PURCHASE_REVOKED` | `handleGooglePlayNotification` | `verifyStatus -> REVOKED` 상태 변경이 실제로 반영됐을 때 |

현재 11컬럼 스키마에서는 `verifyStatus` 로 구분 가능한 상태 변화만 기록한다.
즉, `clientGrant` / `storeConfirm` 같은 선택 lifecycle 이벤트는 이 정본 범위에 포함하지 않는다.


---

## C. Spreadsheet Layout (정본)

| 시트 이름 | 역할 | 쓰기 방식 |
|-----------|------|-----------|
| `YYYY-MM` | 월별 purchase/refund event log | 서버가 append-only |

월별 탭 규칙:
- 탭 이름은 서버 UTC 기준 `YYYY-MM`
- 예: `2026-03`
- 이벤트는 `loggedAtUtcIso` 기준 월 탭으로 들어간다


---

## D. 테이블 명세

### D1. `YYYY-MM`

헤더 순서(고정):

```text
loggedAtUtcIso,purchaseId,storeKey,internalProductId,kind,storeProductId,storePurchaseId,verifyStatus,region,storePurchasedAtUtcIso,eventOccurredAtUtcIso
```

필드 의미:

| 컬럼 | 의미 |
|------|------|
| `loggedAtUtcIso` | Sheet row를 기록한 서버 시각 (`new Date().toISOString()`) |
| `purchaseId` | 정본 멱등키 (`{storeKey}_{storePurchaseId}`) |
| `storeKey` | `apple` / `google` |
| `internalProductId` | Devian 내부 상품 ID |
| `kind` | `Consumable` / `Rental` / `Subscription` / `SeasonPass` |
| `storeProductId` | 스토어 SKU |
| `storePurchaseId` | Google `purchaseToken`, Apple `transaction_id` |
| `verifyStatus` | purchase 문서의 최종 verify 상태 |
| `region` | Functions 리전 |
| `storePurchasedAtUtcIso` | 스토어 영수증 기준 구매 시각 |
| `eventOccurredAtUtcIso` | 상태 전이 실제 발생 시각. 소스 함수의 server time 사용 |

규칙:
- UTF-8, 헤더 1회만 기록
- 숫자/상태 enum도 문자열로 저장해 파서 의존성을 줄인다.
- 필요 시 Google Sheets의 export 기능으로 외부 전달용 CSV를 내려받는다.


---

## E. Spreadsheet 저장 규칙

- 감사 로그 산출물은 **공유된 단일 Google Spreadsheet** 로 관리한다.
- 서버는 해당 월 탭을 보장한 뒤 row append 한다.
- 새 파일/새 spreadsheet를 서버에서 자동 생성하는 것을 기본값으로 삼지 않는다.
- 월 구분은 파일이 아니라 탭 이름(`YYYY-MM`)으로 처리한다.
- 헤더는 탭이 비어 있을 때 서버가 자동으로 1회 생성한다.

구현 우선순위:

1. **정본**: Google Sheets API `append row`
2. **비권장**: raw `.csv` 파일을 Google Drive API로 직접 갱신

주의:
- raw CSV rewrite는 원자적 append API가 없으므로, 동시 write 충돌 위험이 크다.
- 구매 hot path에서는 spreadsheet append를 우선한다.


---

## F. 서버 write 포인트

### E1. purchase

- `verifyPurchase`의 Firestore 트랜잭션이 끝난 뒤, `txResult.resultStatus === "GRANTED"` 인 경우에만 `PURCHASE_GRANTED`를 append 한다.
- `ALREADY_GRANTED` 에서는 append 하지 않는다. 중복 결제 로그를 만들지 않기 위함이다.

### E2. refund / revoke

- `handleGooglePlayNotification`에서 purchase 문서 상태가 실제로 `REFUNDED` 또는 `REVOKED`로 바뀐 뒤에만 append 한다.
- 이미 동일 상태였다면 skip 한다. duplicate row를 만들지 않는다.

---

## G. 멱등 / 중복 방지

- Sheet에는 별도 dedupe 컬럼을 두지 않는다.
- 동일 `purchaseId` 재검증에서 `ALREADY_GRANTED`가 반환되면 `PURCHASE_GRANTED`를 다시 남기지 않는다.
- 환불 RTDN 재수신으로 이미 `REFUNDED`/`REVOKED` 상태이면 row를 추가하지 않는다.
- Sheet sink의 dedupe는 **서버 상태 전이의 멱등성** 을 우선 활용한다. Spreadsheet 자체를 source of truth로 삼아 중복 여부를 판단하지 않는다.


---

## H. 보안 / 운영

- Google Sheets 접근용 서비스 계정은 Google Play 검증용 계정과 분리하는 것을 우선한다.
- 대상 Spreadsheet는 서비스 계정에 `Editor` 권한으로 공유한다.
- 로그 실패는 `logger.error(...)` 로 남기되, purchase/refund function 응답은 가능한 한 계속 진행한다.
- Sheet에는 민감도가 높은 원문 payload를 넣지 않는다.


---

## DoD

Hard (must be 0)
- [ ] purchase/refund Sheet 로그가 **서버 write only** 로 문서에 고정돼 있다.
- [ ] 최소 필수 이벤트 3개(`PURCHASE_GRANTED`, `PURCHASE_REFUNDED`, `PURCHASE_REVOKED`)가 문서에 고정돼 있다.
- [ ] `YYYY-MM` 월별 테이블 명세가 11개 컬럼으로 문서에 고정돼 있다.
- [ ] 월별 탭 이름 규칙(`YYYY-MM`)이 문서에 고정돼 있다.
- [ ] `ALREADY_GRANTED`/중복 환불에서 duplicate row를 만들지 않는 규칙이 명시돼 있다.

Soft
- [ ] 대상 Spreadsheet ID와 공유 권한이 준비됐다.
