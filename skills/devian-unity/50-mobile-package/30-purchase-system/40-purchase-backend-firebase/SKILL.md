---
name: 40-purchase-backend-firebase
description: Define the Firebase Functions and Firestore implementation for the purchase backend. Use when implementing verifyPurchase, refund processing, entitlement projection, Firestore schema, and idempotent server-side purchase state transitions.
---

# 40-purchase-backend-firebase — Firebase Backend Implementation (Functions + Firestore)

Status: ACTIVE
AppliesTo: v10

> Root SSOT: `skills/devian/10-module/03-ssot/SKILL.md`

> Purchase SSOT: `skills/devian-unity/50-mobile-package/30-purchase-system/03-ssot/SKILL.md` (특히 C 섹션)

## 문서 경계 (Scope)

- 이 문서는 **Firebase Functions + Firestore 서버 구현 정본**이다.
- 포함: Callable 책임, Firestore 스키마, 멱등 규칙, 인덱스 요구사항
- 비포함: 레포 배치/`firebase.json`/`.firebaserc`/CLI 배포 셋업 상세 (→ `11`)
- 비포함: 운영 체크리스트/테스트 최소 시나리오/운영 DoD (→ `09`)
- 비포함: 최종 고정 결정값(Callable 이름/시크릿/경로)의 결정 관리 (→ `46`)

## 목적

`PurchaseManager`가 호출할 **검증 서버(Firebase Cloud Functions)** 와 **원장/entitlements 저장소(Firestore)** 를 "스텁 없이" 구현하기 위한 **구현 정본 스킬**이다.

이 스킬은 아래 Callable의 "프로젝트 구조/배포/Firestore 스키마/멱등 규칙"을 고정한다.

- `verifyPurchase`
- `ackPurchaseClientGrant`
- `ackPurchaseStoreConfirm`
- `getEntitlements`


---


## A. Functions 프로젝트 구조 (정본)

NEEDS CHECK: 레포 내 Functions 위치가 아직 고정돼 있지 않으면, 아래 중 하나로 고정해야 한다.
- Option A: `{repoRoot}/functions` (Firebase 표준)
- Option B: `{repoRoot}/server/firebase/functions` (서버 분리)

결정 후, 이 문서의 모든 경로는 결정된 위치로 통일한다.

레포에 실제로 어떤 파일을 어디에 둘지와 Firebase CLI 셋업 절차는 `11-purchase-repo-firebase-functions-setup` 문서를 우선한다.


### A1. 배포 명령 (정본)

NEEDS CHECK: Firebase CLI 사용 여부/버전이 레포에서 고정돼 있어야 한다.

- 예시 (결정 후 고정):
  - `firebase deploy --only functions:verifyPurchase,functions:ackPurchaseClientGrant,functions:ackPurchaseStoreConfirm,functions:getEntitlements`


---


## B. Firestore 스키마 (정본)

### B1. Purchases Ledger

- Path: `/users/{uid}/purchases/{purchaseId}`

필드 (최소):
- `purchaseId: string` (doc id와 동일)
- `storeKey: string` (`"apple" | "google"`)
- `internalProductId: string`
- `kind: string` (`"Consumable" | "Rental" | "Subscription" | "SeasonPass"`) (=`PurchaseKind` string)
- `verifyStatus: string` (SSOT의 resultStatus에 대응하는 대문자 저장: `"GRANTED" | "REJECTED" | "PENDING" | "REVOKED" | "REFUNDED"`)
- `clientGrantStatus: string` (`"PENDING" | "APPLIED_ACKED" | "FAILED_REPORTED"`) — 클라이언트 로컬 지급 결과 보고 상태
- `storeConfirmStatus: string` (`"PENDING" | "CONFIRMED"`) — 스토어 Confirm 처리 상태
- `clientGrantReportedAt: Timestamp` (optional)
- `storeConfirmedAt: Timestamp` (optional)
- `status: string` (legacy compatibility, temporary)
- `storePurchasedAt: Timestamp` — 영수증/스토어 검증 응답에서 추출한 구매 시각(서버에서만 생성, 클라 시간 사용 금지)
- `createdAt: Timestamp`
- `updatedAt: Timestamp`
- `store`: object (스토어별 원본/파싱값 저장; 최소 저장 필드만 유지)

### B2. Entitlements Snapshot

- Path: `/users/{uid}/entitlements/current`

필드 (최소):
- `updatedAt: Timestamp`
- `ownedSeasonPasses: string[]` (SeasonPass 복원 projection)
- `rentals: object` (`rentalId -> expiresAtServerUtcMs`, Rental 복원 projection; simple map)
- `currencyBalances: object` (필요 시; 현재 구현에서 사용)
- `subscriptions: object` (구독 상태 요약, 필요 시)
- `seasonPass: object` (legacy/프로젝트별 상태 요약, 필요 시)
- `consumables: object` (필요 시)


---


## C. Security Rules (정본)

원칙:
- 클라이언트는 ledger/entitlements에 **직접 write 금지**
- Functions(서버)만 write

NEEDS CHECK:
- 레포에 Firestore rules 파일 위치/적용 경로가 고정되어야 한다.


---


## D. Idempotency (정본)

### D1. purchaseId 생성 규칙

`purchaseId`는 "스토어 트랜잭션 식별자" 기반의 멱등 키로 생성한다.

- `purchaseId = "{storeKey}_{storePurchaseId}"`

NEEDS CHECK:
- Apple/Google 각각의 `storePurchaseId`로 삼을 필드를 41 스킬(스토어 검증)에서 확정한다.

### D2. 멱등 처리 규칙

- 동일 `purchaseId`로 `verifyPurchase`가 재호출되면:
  - 이미 `verifyStatus=GRANTED`이면 `ALREADY_GRANTED`를 반환한다.
  - 응답에 `clientGrantStatus`를 포함하여 클라 로컬 지급 복구 여부를 판단할 수 있게 한다.
  - `Rental` 만료일 projection은 멱등 재시도에서 다시 증가시키지 않는다. (`ALREADY_GRANTED` 시 연장 금지)
  - `verifyStatus=PENDING`이면 `PENDING`을 유지한다.
  - `verifyStatus=REJECTED`인 경우 정책에 따라 재검증 허용 여부를 SSOT 기준으로 결정한다. (NEEDS CHECK)

### D3. Rental 만료일 projection 계산 (결정 반영, 구현됨)

- `verifyPurchase`의 신규 `GRANTED` 처리 트랜잭션에서 서버 UTC 기준으로 계산한다.
- 정책: 연장 방식
  - `newExpiry = max(existingExpiry, serverNow) + 30일`
- 저장 위치(정본): `/users/{uid}/entitlements/current.rentals[internalProductId] = expiresAtServerUtcMs`


---


## E. Callable API 계약 (정본)

SSOT의 "C# ↔ Callable 필드 매핑"을 그대로 따른다. (SSOT: 03-ssot 문서 C 섹션)

Callable 이름/요청·응답 키의 최종 고정값은 `46-purchase-decisions`를 우선한다. 이 문서는 구현 관점의 구조/책임/스키마 설명에 집중한다.

NEEDS CHECK:
- 실제 TS/JS Functions 구현 시 요청/응답 타입 파일을 어디에 둘지(예: `functions/src/types`)를 레포 구조에 맞게 고정한다.


---


## F. Firestore Index (정본)

`getRecentPurchases30d` 쿼리는 복합 인덱스를 요구한다:
- `kind` ASC
- `storePurchasedAt` DESC
- `__name__` DESC (docId tie-break)

쿼리 조건:
- `where(kind == <kind 파라미터>)` (PurchaseKind 값)
- `where(storePurchasedAt >= threshold)` (서버 now − 30일)
- `orderBy(storePurchasedAt, desc)`
- `orderBy(documentId(), desc)`

인덱스는 `firestore.indexes.json`으로 레포에서 관리하고 `firebase deploy --only firestore:indexes`로 반영한다.


---


## DoD

Hard (must be 0)
- [ ] `verifyPurchase`, `ackPurchaseClientGrant`, `ackPurchaseStoreConfirm`, `getEntitlements` Callable의 프로젝트 구조/배포 경로가 모호하지 않다.
- [ ] Firestore 스키마(2개 Path)가 문서에 고정돼 있다.
- [ ] "클라 write 금지 / 서버 write only" 규칙이 명시돼 있다.
- [ ] purchaseId 규칙이 문서에 고정돼 있다.

Soft
- [ ] 로컬 에뮬레이터 실행 커맨드 예시 추가
- `noAds`는 게임 로직 전용 상태이며, Purchase Functions 서버(entitlements/current)에서 관리하지 않는다.
