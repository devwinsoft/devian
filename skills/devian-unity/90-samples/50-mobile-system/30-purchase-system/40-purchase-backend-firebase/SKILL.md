# 40-purchase-backend-firebase — Firebase Backend Implementation (Functions + Firestore)

Status: ACTIVE
AppliesTo: v10

> Root SSOT: `skills/devian/10-module/03-ssot/SKILL.md`

> Purchase SSOT: `skills/devian-unity/90-samples/50-mobile-system/30-purchase-system/03-ssot/SKILL.md` (특히 C 섹션)

## 목적

`PurchaseManager`가 호출할 **검증 서버(Firebase Cloud Functions)** 와 **원장/entitlements 저장소(Firestore)** 를 "스텁 없이" 구현하기 위한 **구현 정본 스킬**이다.

이 스킬은 아래 Callable의 "프로젝트 구조/배포/Firestore 스키마/멱등 규칙"을 고정한다.

- `verifyPurchase`
- `getEntitlements`
- `deleteMyPurchases` (개발/테스트 전용 — 46 스킬 F3 참조)


---


## A. Functions 프로젝트 구조 (정본)

NEEDS CHECK: 레포 내 Functions 위치가 아직 고정돼 있지 않으면, 아래 중 하나로 고정해야 한다.
- Option A: `{repoRoot}/functions` (Firebase 표준)
- Option B: `{repoRoot}/server/firebase/functions` (서버 분리)

결정 후, 이 문서의 모든 경로는 결정된 위치로 통일한다.


### A1. 배포 명령 (정본)

NEEDS CHECK: Firebase CLI 사용 여부/버전이 레포에서 고정돼 있어야 한다.

- 예시 (결정 후 고정):
  - `firebase deploy --only functions:verifyPurchase,functions:getEntitlements`


---


## B. Firestore 스키마 (정본)

### B1. Purchases Ledger

- Path: `/users/{uid}/purchases/{purchaseId}`

필드 (최소):
- `purchaseId: string` (doc id와 동일)
- `storeKey: string` (`"apple" | "google"`)
- `internalProductId: string`
- `kind: string` (`"Consumable" | "Subscription" | "SeasonPass"`) (=`ProductKind` string)
- `status: string` (SSOT의 resultStatus에 대응하는 소문자 저장: `"granted" | "already_granted" | "rejected" | "pending" | "revoked" | "refunded"`)
- `storePurchasedAt: Timestamp` — 영수증/스토어 검증 응답에서 추출한 구매 시각(서버에서만 생성, 클라 시간 사용 금지)
- `createdAt: Timestamp`
- `updatedAt: Timestamp`
- `store`: object (스토어별 원본/파싱값 저장; 최소 저장 필드만 유지)

### B2. Entitlements Snapshot

- Path: `/users/{uid}/entitlements/current`

필드 (최소):
- `updatedAt: Timestamp`
- `noAds: bool`
- `subscriptions: object` (구독 상태 요약)
- `seasonPass: object` (시즌 패스 상태 요약)
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
  - 이미 `granted` 상태면 `ALREADY_GRANTED`를 반환한다.
  - 상태가 `pending`이면 `PENDING`을 유지한다.
  - `rejected`인 경우 정책에 따라 재검증 허용 여부를 SSOT 기준으로 결정한다. (NEEDS CHECK)


---


## E. Callable API 계약 (정본)

SSOT의 "C# ↔ Callable 필드 매핑"을 그대로 따른다. (SSOT: 03-ssot 문서 C 섹션)

`deleteMyPurchases` Callable은 개발/테스트 전용이며, 환경 변수 `ALLOW_PURCHASE_DELETE=true`일 때만 동작한다. 상세 계약은 46 스킬 F3 참조.

NEEDS CHECK:
- 실제 TS/JS Functions 구현 시 요청/응답 타입 파일을 어디에 둘지(예: `functions/src/types`)를 레포 구조에 맞게 고정한다.


---


## F. Firestore Index (정본)

`getRecentPurchases30d` 쿼리는 복합 인덱스를 요구한다:
- `kind` ASC
- `storePurchasedAt` DESC
- `__name__` DESC (docId tie-break)

쿼리 조건:
- `where(kind == <kind 파라미터>)` (ProductKind 값)
- `where(storePurchasedAt >= threshold)` (서버 now − 30일)
- `orderBy(storePurchasedAt, desc)`
- `orderBy(documentId(), desc)`

인덱스는 `firestore.indexes.json`으로 레포에서 관리하고 `firebase deploy --only firestore:indexes`로 반영한다.


---


## DoD

Hard (must be 0)
- [ ] `verifyPurchase`, `getEntitlements` 2개 Callable의 프로젝트 구조/배포 경로가 모호하지 않다.
- [ ] Firestore 스키마(2개 Path)가 문서에 고정돼 있다.
- [ ] "클라 write 금지 / 서버 write only" 규칙이 명시돼 있다.
- [ ] purchaseId 규칙이 문서에 고정돼 있다.

Soft
- [ ] 로컬 에뮬레이터 실행 커맨드 예시 추가
