# 46-purchase-decisions — Purchase Verification Decisions (Firebase Callable)


Status: ACTIVE
AppliesTo: v10


## 목적


Firebase Callable 기반 결제 검증 구현이 "안정적으로 개발 관리"되도록,
레포/서버/스토어검증/멱등키/시크릿/Callable 계약/지급 포맷의
**결정사항을 단일 정본으로 고정**한다.

이 문서의 값이 바뀌면:
- 서버(Firebase Functions) 코드
- 클라(Unity PurchaseManager)
- Firestore schema/rules

이 함께 변경되어야 한다.


---


## A. Repo 구조 (결정)


- Functions 프로젝트 위치: `{repoRoot}/functions`
- Firestore rules 위치: `{repoRoot}/firestore.rules`
- Firebase 설정 파일 커밋: `firebase.json`, `.firebaserc` **레포에 포함(커밋)**


---


## B. Callable 계약 (결정)


- Callable 이름(고정):
  - `verifyPurchase`
  - `ackPurchaseClientGrant`
  - `ackPurchaseStoreConfirm`
  - `getEntitlements`
  - `getRecentPurchases30d`

- 인증 정책:
  - Callable은 `context.auth.uid` 필수(unauthenticated 거부)

- `kind`의 정본 enum은 컨텐츠 레이어 SSOT의 `ProductKind`이며, Callable에는 그 string 값을 그대로 보낸다.

- 요청 스키마(고정 키):
  - `storeKey` (`"apple" | "google"`)
  - `internalProductId` (string)
  - `kind` (`"Consumable" | "Rental" | "Subscription" | "SeasonPass"`) (=`ProductKind` string)
  - `payload` (string, Unity IAP receipt raw)

- 응답 스키마(고정 키):
  - `resultStatus` (`GRANTED | ALREADY_GRANTED | REJECTED | PENDING | REVOKED | REFUNDED`)
  - `purchaseId` (string, verify 멱등키)
  - `verifyStatus` (`GRANTED | REJECTED | PENDING | REVOKED | REFUNDED`) — Firestore purchase field와 정합
  - `clientGrantStatus` (`PENDING | APPLIED_ACKED | FAILED_REPORTED`) — 클라이언트 로컬 지급 결과 보고 상태
  - `storeConfirmStatus` (`PENDING | CONFIRMED`) — 스토어 Confirm 처리 상태
  - `grants` (array)
  - `entitlementsSnapshot` (optional)

- `ackPurchaseClientGrant` 요청/응답(고정 키):
  - 요청: `purchaseId`, `clientGrantStatus` (`APPLIED_ACKED | FAILED_REPORTED`)
  - 응답: `purchaseId`, `verifyStatus`, `clientGrantStatus`

- `ackPurchaseStoreConfirm` 요청/응답(고정 키):
  - 요청: `purchaseId`
  - 응답: `purchaseId`, `verifyStatus`, `clientGrantStatus`, `storeConfirmStatus`

- 클라이언트 구매 상태 전이 순서(결정):
  - 계정/SKU 잠김(`already owned`) 방지를 위해 `ConfirmPurchase` + `ackPurchaseStoreConfirm`를 로컬 지급/`ackPurchaseClientGrant`보다 앞에 둔다.
  - `RetryInterruptedPurchaseAsync()`는 새 구매를 시작하지 않고 `CurrentPurchase` 상태 전이를 재개한다.

- getEntitlements 응답 스냅샷 키(고정):
  - `ownedSeasonPasses` (string[])
  - `rentals` (object map; `internalProductId -> expiresAtServerUtcMs`)
  - `currencyBalances` (object map)
  - `serverNowUtcMs` (number)

> NOTE:
> `PurchaseManager`는 `SeasonPass ownership`를 local/cloud cache(PurchaseStorage)에 저장할 수 있다.
> `Rental` 복원 정본은 서버 `rentals` projection이며, `GetRentalRemainingMsAsync(internalProductId)`는 서버 질의 결과(남은 시간 ms)로 클라이언트 cache(`noAdsExpireAtClientUtcMs`)를 갱신한다.
> `noAds`는 서버가 아니라 클라이언트 게임 로직 상태(local/cloud cache)로만 관리한다.


---


## C. 멱등키 규칙 (결정)


- `purchaseId = "{storeKey}_{storePurchaseId}"` 를 Firestore 문서 ID로 사용한다.
- 동일 `purchaseId` 재요청 시:
  - 이미 지급 완료면 `ALREADY_GRANTED` 반환(중복 지급 금지)
  - 응답에 `clientGrantStatus`를 포함해 클라 로컬 지급 복구 가능 여부를 전달한다

### C1. Firestore purchase 상태 필드 명명 (결정)

- `purchases/{purchaseId}.status` → legacy compatibility field (temporary)
- 정본 필드:
  - `verifyStatus`
  - `clientGrantStatus`
  - `storeConfirmStatus`


---


## D. Google Play 검증 방식 (결정)


- 서버 검증 방식: Google Play Developer API(androidpublisher v3)
- storePurchaseId 규칙(고정): `purchaseToken`
- 제품/구독 분기:
  - `kind == Subscription` → `purchases.subscriptions.get`
  - `kind == Consumable` / `Rental` / `SeasonPass` → `purchases.products.get` (one-time 검증 경로)


---


## E. Apple 검증 방식 (결정)


- 서버 검증 방식: `verifyReceipt` (Apple receipt validation)
- storePurchaseId 규칙(고정): `transaction_id`
- sandbox 재시도 규칙:
  - status `21007`이면 sandbox endpoint로 재시도


---


## F. Grants / Entitlements 최소 정책 (결정)


- 초기 구현 단계에서는 `grants`는 "빈 배열"을 허용한다.
- 단, `resultStatus`가 `GRANTED/ALREADY_GRANTED`일 때는
  `entitlementsSnapshot`을 항상 반환하도록 한다(클라 동기화 안정성).

> NOTE: 추후 42 스킬(Grants/Entitlements 정본)이 완성되면,
> 이 문서의 최소 정책을 42의 규칙으로 교체한다.

### F1. 상품 종류별 구매 제한 정책 (결정)

- `Consumable`: 반복 구매 허용
- `Rental`: 반복 구매 허용 (30일 재구매 제한 없음)
- `Subscription`: 스토어 검증/상태 기준 (별도 서버 상태 계산)
- `SeasonPass`: 동일 `internalProductId` 1회만 허용 (기존 구매 기록 있으면 `REJECTED`)

### F1-1. SeasonPass / Rental 복원용 서버 projection (결정)

- `SeasonPass` 복원용 정본 정보는 서버(Firestore) `internalProductId` 단위 구매 이력/소유 projection을 사용한다.
- `Rental` 복원용 정본 정보는 서버(Firestore) `rentals` map(`internalProductId -> expiresAtServerUtcMs`)을 사용한다.
- `PurchaseManager`는 `noAds`를 별도 구매 복원 타입으로 취급하지 않는다. (`noAds` 해석/적용은 게임 로직 영역)

### F1-2. Rental 만료일 계산 정책 (결정)

- 기준 시간은 반드시 서버 시간(UTC)이다.
- 재구매 시 만료일 계산은 "연장" 방식으로 고정한다:
  - `newExpiry = max(existingExpiry, serverNow) + 30일`
- 의미:
  - 만료 전 재구매: 기존 만료일 + 30일
  - 만료 후 재구매: 현재 서버시간 + 30일
- 동일 `purchaseId` 멱등 재시도(`ALREADY_GRANTED`)에서는 만료일을 다시 증가시키지 않는다.


---


## F2. storePurchasedAt / 최근 30일 조회 (결정)


- "최근 30일 구매 내역"의 기준 시각은 `storePurchasedAt`(영수증 날짜)이다.
- 서버 기준 `now`로 threshold(`now − 30일`)를 계산하며, 클라이언트/디바이스 시간은 사용 금지.
- `kind` 값은 ProductKind SSOT(PascalCase) 기준이며, 호출 시 파라미터로 전달한다.
- Callable 이름은 `getRecentPurchases30d`로 고정한다.
- `storePurchasedAt` 값은 스토어 검증 응답에서 추출한 구매 시각이며, 서버에서만 생성한다:
  - Google: `purchaseTimeMillis` (products) / `startTimeMillis` (subscriptions)
  - Apple: `purchase_date_ms` (in_app 트랜잭션 중 `purchase_date_ms` 최댓값)
- `storePurchasedAt`(영수증 날짜)은 필수이며, 누락 시 serverTimestamp로 대체하지 않는다.
  - `purchasedAtMs`를 확보하지 못하면 `verifyPurchase`는 REJECTED 처리한다.
- 최신(latest) 정의:
  - 최신 = `storePurchasedAt`가 가장 큰 항목
  - 동률이면 문서 ID(desc)로 tie-break
- `PurchaseManager`는 page 없이 최신 1건만 필요하므로:
  - `getRecentPurchases30d`를 `kind="Consumable"` 또는 `kind="Rental"`, `pageSize=1`로 호출하고 `items[0]`만 사용한다.
- `getRecentPurchases30d` 페이지네이션:
  - `nextCursor`는 `"storePurchasedAtMs|docId"` 문자열 토큰 형식이다.
  - `storePurchasedAtMs`는 `storePurchasedAt`의 `toMillis` 값이다.


---


## G. 시크릿/운영 결정 (결정)


- Node 런타임: Node 20
- 시크릿 키 이름(고정):
  - `GOOGLE_APPLICATION_CREDENTIALS_JSON`
  - `APPLE_SHARED_SECRET`
- 리전: `asia-northeast3` (Seoul)

*(서버 리전 고정 필요 시 적용)*


---


## H. 관련 정본 링크


- Purchase SSOT: `../03-ssot/SKILL.md`
- Repo setup(44): `../44-purchase-repo-firebase-functions-setup/SKILL.md`
- Backend(40): `../40-purchase-backend-firebase/SKILL.md`
- Store verification(41): `../41-purchase-store-verification/SKILL.md`
- Grants(42): `../42-purchase-entitlements-grants/SKILL.md`
- Client integration(43): `../43-purchase-client-server-integration/SKILL.md`


---


## DoD


Hard (must be 0)
- [ ] Functions 위치 / rules 위치 / 설정파일 커밋 여부가 단일 결정으로 고정돼 있다.
- [ ] Callable 이름/스키마/인증 정책이 단일 결정으로 고정돼 있다.
- [ ] storePurchaseId 규칙(Apple/Google)과 purchaseId 멱등 규칙이 고정돼 있다.
- [ ] 시크릿 키 이름/Node 런타임이 고정돼 있다.

Soft
- [ ] 42 스킬이 확정되면, grants/entitlements 최소 정책을 교체하는 작업 항목을 별도 이슈로 등록
