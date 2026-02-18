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
  - `getEntitlements`
  - `getRecentRentalPurchases30d`

- 인증 정책:
  - Callable은 `context.auth.uid` 필수(unauthenticated 거부)

- `kind`의 정본 enum은 `input/Domains/Purchase/contracts/ProductKind.json`의 `ProductKind`이며, Callable에는 그 string 값을 그대로 보낸다.

- 요청 스키마(고정 키):
  - `storeKey` (`"apple" | "google"`)
  - `internalProductId` (string)
  - `kind` (`"Consumable" | "Rental" | "Subscription" | "SeasonPass"`) (=`ProductKind` string)
  - `payload` (string, Unity IAP receipt raw)

- 응답 스키마(고정 키):
  - `resultStatus` (`GRANTED | ALREADY_GRANTED | REJECTED | PENDING | REVOKED | REFUNDED`)
  - `grants` (array)
  - `entitlementsSnapshot` (optional)

- getEntitlements 응답 스냅샷 키(고정):
  - `noAdsActive` (bool)
  - `ownedSeasonPasses` (string[])
  - `currencyBalances` (object map)


---


## C. 멱등키 규칙 (결정)


- `purchaseId = "{storeKey}_{storePurchaseId}"` 를 Firestore 문서 ID로 사용한다.
- 동일 `purchaseId` 재요청 시:
  - 이미 지급 완료면 `ALREADY_GRANTED` 반환(중복 지급 금지)


---


## D. Google Play 검증 방식 (결정)


- 서버 검증 방식: Google Play Developer API(androidpublisher v3)
- storePurchaseId 규칙(고정): `purchaseToken`
- 제품/구독 분기:
  - `kind == Subscription` → `purchases.subscriptions.get`
  - `kind == Rental` / `Consumable` / `SeasonPass` → `purchases.products.get` (one-time 검증 경로)


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


---


## F2. storePurchasedAt / Rental 30일 조회 (결정)


- "최근 30일 Rental 구매 내역"의 기준 시각은 `storePurchasedAt`(영수증 날짜)이다.
- 서버 기준 `now`로 threshold(`now − 30일`)를 계산하며, 클라이언트/디바이스 시간은 사용 금지.
- `kind` 값은 ProductKind SSOT(PascalCase) 기준으로 `"Rental"`을 사용한다.
- Callable 이름은 `getRecentRentalPurchases30d`로 고정한다.
- `storePurchasedAt` 값은 스토어 검증 응답에서 추출한 구매 시각이며, 서버에서만 생성한다:
  - Google: `purchaseTimeMillis` (products) / `startTimeMillis` (subscriptions)
  - Apple: `purchase_date_ms` (in_app 트랜잭션 중 `purchase_date_ms` 최댓값)
- `storePurchasedAt`(영수증 날짜)은 필수이며, 누락 시 serverTimestamp로 대체하지 않는다.
  - `purchasedAtMs`를 확보하지 못하면 `verifyPurchase`는 REJECTED 처리한다.
- 최신(latest) 정의:
  - 최신 = `storePurchasedAt`가 가장 큰 항목
  - 동률이면 문서 ID(desc)로 tie-break
- `PurchaseManager`는 page 없이 최신 1건만 필요하므로:
  - `getRecentRentalPurchases30d`를 `pageSize=1`로 호출하고 `items[0]`만 사용한다.
- `getRecentRentalPurchases30d` 페이지네이션:
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
