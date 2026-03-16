---
name: 03-ssot
description: Define the single source of truth for the purchase system across Unity IAP, backend verification, entitlements, restore rules, and routing. Use when deciding canonical purchase concepts and cross-document invariants for the purchase module.
---

# 03-ssot — 30-purchase-system (통합 SSOT)


Status: ACTIVE
AppliesTo: v10


## 이 문서가 정본이다 (SSOT)

이 문서는 30-purchase-system의 **단일 SSOT 허브**이다.
기존 04~08 분할 문서의 핵심 내용이 이 문서에 통합되었다.
운영/보안/테스트/DoD는 [09-ssot-operations](../09-ssot-operations/SKILL.md)를 참조한다.

## 관심사별 라우팅 (중복 금지)

- 운영/보안/테스트/DoD 체크리스트: `../09-ssot-operations/SKILL.md`
- PurchaseStorage(로컬/클라우드 저장용 최소 상태 스냅샷): `../33-purchase-storage/SKILL.md`
- Firebase Functions + Firestore 구현 상세: `../40-purchase-backend-firebase/SKILL.md`
- Firebase 레포 구성/CLI/배포 셋업: `../11-purchase-repo-firebase-functions-setup/SKILL.md`
- 고정 결정사항(Callable 이름/경로/시크릿/정책 결정): `../46-purchase-decisions/SKILL.md`

이 문서(03)는 통합 SSOT 허브/핵심 합의에 집중하고, 구현/운영 상세의 원문은 위 문서로 분리한다.


## 우선순위

- Root SSOT: `skills/devian/10-module/03-ssot/SKILL.md`
- Unity 관련 SSOT: `skills/devian-unity/03-ssot/SKILL.md`
- 결제/IAP 관련 SSOT: 이 문서

충돌 시 Root SSOT의 정의/플레이스홀더/규약이 우선한다.


---


## A. Unity IAP (from 04-ssot-unity-iap)


### 전제

- 결제 SDK는 Unity **In-App Purchasing (Unity IAP) 5.1.2** (`com.unity.purchasing@5.1.2`)를 사용한다.
- 스토어별 구현 차이는 존재하지만, 최종 지급/상태는 서버 검증 결과를 따른다.


### Unity IAP 5.x v5 API 정책

- 현재 구현은 **v5 신규 API** (`StoreController` + `UnityIAPServices` + 이벤트 기반)를 사용한다.
- `[Obsolete]` Legacy API (`IDetailedStoreListener`, `UnityPurchasing.Initialize`, `ConfigurationBuilder`, `IStoreController`, `Product.receipt`)는 **사용하지 않는다**.

#### v5 API 사용 패턴

| 기능 | v5 API |
|------|--------|
| 초기화 | `UnityIAPServices.StoreController()` → `await controller.Connect()` → `controller.FetchProducts(definitions)` |
| 제품 등록 | `List<ProductDefinition>` + `controller.FetchProducts(...)` (ProductCatalog에서 변환) |
| 구매 시작 | `controller.PurchaseProduct(productId)` |
| 구매 확인 | `controller.ConfirmPurchase(pendingOrder)` |
| Receipt 접근 | `PendingOrder.Info.Receipt` |
| Restore | `controller.RestoreTransactions(Action<bool, string?>)` (플랫폼 분기 불필요) |
| 구매 성공 콜백 | `controller.OnPurchasePending` (PendingOrder) |
| 구매 실패 콜백 | `controller.OnPurchaseFailed` (FailedOrder) |
| 제품 Fetch 콜백 | `controller.OnProductsFetched` / `OnProductsFetchFailed` |

#### 참고: 4.x → 5.x 주요 변경 사항

| 항목 | 변경 |
|------|------|
| Amazon/UWP/Facebook 스토어 | **제거됨** |
| Apple Receipt Validation | **Deprecated** (Google Play만 지원) |
| `IAppleExtensions.RestoreTransactions` | `Action<bool>` → `Action<bool, string>` |


### 플랫폼 차이

#### iOS: Restore(복원)

- iOS는 재설치/기기 변경 시 Restore 플로우가 필요하다.
- Restore는 "스토어 구매 이력 재동기화 트리거"이며,
  최종 Entitlement는 서버 상태(purchases/entitlements)를 기준으로 확정한다.

#### Android: 복원 UX

- Android는 보통 Restore 버튼을 직접 노출하지 않고,
  앱 시작/로그인 시 서버 상태 동기화로 복원을 처리해도 된다.


### Pending / Deferred 상태

- iOS: 승인 대기(deferred) 상태가 발생할 수 있다.
- Android: pending 상태가 발생할 수 있다.
- 공통 규칙: **PENDING/DEFERRED 상태에서는 지급 금지** (서버 검증 확정 전 지급 금지)


---


## B. Product Catalog (from 05-ssot-product-catalog)


### SSOT 원칙

#### 1) 내부 표준 ID가 정본

- 상위 로직은 `internalProductId`를 정본으로 사용한다.
- Store SKU는 매핑 데이터로만 취급한다.

#### 2) 타입 규칙

- Consumable: 재화 등 반복 구매/즉시 지급
- Rental: 기간제 구매(예: NoAds 30일). 반복 구매 허용, 만료일 연장 방식
- Subscription: "구독 기반 NoAds" 등 상태 기반
- Season Pass: 시즌별 구매 1회성 Entitlement로 운영

- kind의 정본 enum은 컨텐츠 레이어가 정의한 `PurchaseKind`다.
- 테이블(`ShopTable.xlsx` PURCHASE.kind)의 타입은 `PurchaseKind`를 사용한다.

### 카탈로그 통합

PurchaseManager는 `TB_PURCHASE` 테이블을 **직접 참조**하여 카탈로그를 구성한다.
`Devian.Samples.MobilePackage.asmdef`에 `Devian.Domain.Game` 참조가 포함되어 있다.

Purchase 지급을 위해 `internalProductId -> rewardGroupId` 변환이 필요하다.
- `PurchaseManager`가 `ResolveRewardGroupId(internalProductId)` → `TB_PURCHASE.Get(internalProductId).RewardGroupId`로 직접 변환한다.
- `BuildProductDefinitions()` → `TB_PURCHASE.GetAll()`에서 `isActive` 필터링 후 ProductDefinition 목록을 생성한다.

### Unity IAP 5.x (v5) Catalog Notes

- 본 SSOT에서 "카탈로그"는 **내부 ID(`internalProductId`) ↔ 스토어 SKU(Apple/Google) 매핑 데이터**를 의미한다.
- Unity IAP 5.x(v5)에서는 과거 방식의 `ProductCatalog.LoadDefaultCatalog()` 같은 런타임 로딩 API에 의존하지 않는다. (obsolete 전제 제거)
- 런타임에서는 "등록된 제품 정의"를 사용해 구매를 시작하며, 상위 로직은 반드시 `internalProductId`만 사용한다.
- 스토어 SKU는 플랫폼별 매핑 데이터일 뿐이며, 운영/코드에서 직접 참조하지 않는다.

### NEEDS CHECK (형준 결정 필요)

#### Product Catalog (상품 요약/매핑) SSOT — ✅ 전체 결정됨
- [x] SSOT 저장 위치: **(A) Devian input 테이블(Excel) 기반** — 결정됨
  - PURCHASE 테이블 스키마를 이 문서(03-ssot)에서 SSOT로 정의한다.
  - `ShopTable.xlsx`의 PURCHASE 시트에서 데이터를 관리한다.
- [x] `{buildInputJson}` 도메인 등록: — 결정됨
  - 도메인 등록 정보는 컨텐츠 레이어 SSOT에서 관리
- [x] ShopTable.xlsx 경로: — 결정됨
  - 경로는 컨텐츠 레이어 SSOT에서 관리
- [x] PURCHASE 테이블 스키마/필드: — 결정됨
  - `internalProductId` (string, pk) — 내부 상품 ID (정본)
  - `rewardGroupId` (string) — 지급 Reward Key, `internalProductId -> rewardGroupId` 변환의 SSOT
  - `kind` (PurchaseKind) — 상품 타입 (`Consumable` / `Rental` / `Subscription` / `SeasonPass`)
  - `isActive` (bool) — 운영 활성 토글
  - `storeSkuApple` (string) — Apple Store SKU
  - `storeSkuGoogle` (string) — Google Play SKU
- [x] 시즌 매핑 필드 위치: — 결정됨
  - 시즌 구매 제한용 `seasonId`는 `SHOP_PURCHASE.seasonId`에서 관리한다. (`PURCHASE` 스키마에는 포함하지 않는다.)


---


## C. Verification Server + Idempotency + Firestore Ledger (from 06-ssot-verify-idempotency)


### SSOT (핵심 합의)

- 별도 VM 서버 없이 **Firebase Cloud Functions**를 "검증 서버"로 사용한다.
- 결제/구매 정보는 **Firestore Cloud에 저장**한다. (원장/entitlements)
- 클라이언트는 결제 원장에 직접 write 하지 않는다. (서버 write only)
- 멱등 처리(Idempotency)로 중복 verify/중복 지급을 방지한다.

구현 상세(함수 구조/스키마/인덱스)는 `40`, 레포/배포 셋업은 `44`, 최종 고정 결정값은 `46`을 참조한다.


### resultStatus (정본 enum)

클라이언트·서버 모두 아래 값만 사용한다. (문자열 비교, 대문자)

| 값 | 의미 |
|----|------|
| `GRANTED` | 신규 검증 성공 — 지급 실행 |
| `ALREADY_GRANTED` | 동일 구매 재요청 — 이미 지급됨(멱등) |
| `REJECTED` | 스토어 검증 실패 / 위변조 |
| `PENDING` | 스토어가 아직 확정하지 않은 상태 (deferred/pending) — 지급 금지 |
| `REVOKED` | 환불/취소 등으로 사후 철회 |
| `REFUNDED` | 스토어 환불 확인 |

- Firestore purchase 문서의 정본 상태 필드는 `verifyStatus`다. (`resultStatus`와 같은 의미의 서버 상태)
- `clientGrantStatus`는 클라이언트 로컬 지급 결과 보고 상태를 별도로 저장한다. (`PENDING` / `APPLIED_ACKED` / `FAILED_REPORTED`)
- `storeConfirmStatus`는 스토어 `ConfirmPurchase` 처리 상태를 별도로 저장한다. (`PENDING` / `CONFIRMED`)
- 01-policy의 기존 표기(`GRANTED|ALREADY_GRANTED|REJECTED|PENDING`)와 정합.


### 엔드포인트(최소 세트)

#### 1) verifyPurchase (Callable 권장)

- 입력
  - `uid` — Auth context에서 확보 (클라가 보내지 않음)
  - `storeKey: string` — "apple" | "google"
  - `internalProductId: string`
  - `kind: string` — "Consumable" | "Rental" | "Subscription" | "SeasonPass" (=`PurchaseKind` string)
  - `payload: string` — 스토어 영수증/검증 데이터 (클라에서 `BuildVerifyPayload(receipt)` 결과)
- 처리
  1) 스토어 서버 검증(Apple/Google)
  2) Firestore 원장 기록 upsert (멱등)
  3) 결과 반환 (entitlements 갱신은 별도 `getEntitlements` 호출로 분리)
- 출력
  - `resultStatus: string` — 위 enum 중 하나
  - `purchaseId: string` — `{storeKey}_{storePurchaseId}`
  - `verifyStatus: string` — 서버 purchase 상태 필드(`verifyStatus`)와 정합
  - `clientGrantStatus: string` — `"PENDING" | "APPLIED_ACKED" | "FAILED_REPORTED"`
  - `storeConfirmStatus: string` — `"PENDING" | "CONFIRMED"`
  - `grants: array` — (server informational, 클라 지급 입력으로 미사용) 지급 내역 참고용. 보상은 `rewardGroupId` 경로로 지급
  - `entitlementsSnapshot: object?` — (optional) 갱신된 entitlements 스냅샷. GRANTED/ALREADY_GRANTED 시 포함. 클라는 `ParseEntitlementsSnapshot`으로 파싱 + `cacheEntitlementsSnapshot`으로 InventoryStorage에 동기화

##### C# ↔ Callable 필드 매핑

| C# (`VerifyPurchaseRequest`) | Callable JSON key | 비고 |
|------------------------------|-------------------|------|
| `InternalProductId` | `internalProductId` | |
| `Kind` (enum → string) | `kind` | `"Consumable"` / `"Rental"` / `"Subscription"` / `"SeasonPass"` |
| `Store` | `storeKey` | |
| `Payload` | `payload` | |

| Callable JSON response | C# (`VerifyPurchaseResponse`) | 비고 |
|------------------------|-------------------------------|------|
| `resultStatus` | `ResultStatus` | 위 enum 값 |
| `purchaseId` | `PurchaseId` | verify 멱등키 |
| `verifyStatus` | `VerifyStatus` | Firestore purchase 상태 필드와 정합 |
| `clientGrantStatus` | `ClientGrantStatus` | 로컬 지급 결과 보고 상태 |
| `storeConfirmStatus` | `StoreConfirmStatus` | 스토어 Confirm 처리 상태 |
| `grants[]` | (클라이언트 미사용 — 무시) | 서버 informational. 보상은 `rewardGroupId` 경로로 지급 |
| `entitlementsSnapshot` | `Snapshot` (`EntitlementsSnapshot?`) | optional |

#### 1-1) ackPurchaseClientGrant (Callable)

- 입력
  - `uid` — Auth context에서 확보 (클라가 보내지 않음)
  - `purchaseId: string`
  - `clientGrantStatus: string` — `"APPLIED_ACKED"` | `"FAILED_REPORTED"`
- 처리
  1) purchase 문서 소유자/상태 확인 (`verifyStatus == GRANTED`)
  2) `clientGrantStatus = APPLIED_ACKED | FAILED_REPORTED` 멱등 업데이트
- 출력
  - `purchaseId`
  - `verifyStatus`
  - `clientGrantStatus`

#### 1-2) ackPurchaseStoreConfirm (Callable)

- 입력
  - `uid` — Auth context에서 확보 (클라가 보내지 않음)
  - `purchaseId: string`
- 처리
  1) purchase 문서 소유자/상태 확인 (`verifyStatus == GRANTED`)
  2) `storeConfirmStatus = CONFIRMED` 멱등 업데이트
- 출력
  - `purchaseId`
  - `verifyStatus`
  - `clientGrantStatus`
  - `storeConfirmStatus`

#### 2) getEntitlements (Callable/HTTPS 중 택1)

- 입력: `uid` (Auth context)
- 출력:

| Callable JSON response | C# (`EntitlementsSnapshot`) | 비고 |
|------------------------|------------------------------|------|
| `ownedSeasonPasses` | `OwnedSeasonPasses: IReadOnlyList<string>` | |
| `rentals` | `Rentals: IReadOnlyDictionary<string, long>` | key=`internalProductId`, value=`expiresAtServerUtcMs` |
| `currencyBalances` | `CurrencyBalances: IReadOnlyDictionary<string, long>` | key=재화ID, value=잔고 |
| `serverNowUtcMs` | `ServerNowUtcMs: long` | Rental 남은시간 계산 기준 |

#### 3) getRecentPurchases30d (Callable)

- 목적: "최근 30일 이내" 구매 내역 조회 (kind 파라미터로 필터)
- 기준 시각: `storePurchasedAt`(영수증 날짜)
- 30일 기준: 서버 now − 30일 (클라/기기 시간 금지)
- 최신(latest): `storePurchasedAt` desc, 동률이면 docId desc
- 인증: `context.auth.uid` 필수
- 입력: `kind` (string, 필수 — PurchaseKind 값. `Consumable`/`Rental` 등), `pageSize` (optional, 기본 20)
- 출력:
  - `items: array` — 각 원소: `{ purchaseId, internalProductId, storePurchasedAt, verifyStatus, status }`
    - `status`는 legacy response compatibility alias (temporary)
  - `nextCursor: string | null` — 형식: `"storePurchasedAtMs|docId"` (페이지네이션 토큰)

#### PurchaseManager 정식 API

- `GetLatestConsumablePurchase30dAsync()`
  - 서버 Callable: `getRecentPurchases30d` (`kind="Consumable"`, `pageSize=1`로 호출, `items[0]`만 사용)
  - 최근 30일 내 해당 kind 내역이 없으면 실패(`COMMON_ERROR_TYPE.COMMON_SERVER` + 메시지)로 처리
  - 페이지네이션 없이 최신 1건만 반환하는 단일 API


### Client-Side Purchase Flow (정본)

#### ConfirmPurchase 타이밍 정책

- `verifyPurchase` 응답의 `resultStatus`에 따라:
  - `GRANTED`
    - `controller.ConfirmPurchase(pendingOrder)` → `ackPurchaseStoreConfirm` **먼저 실행** (스토어 잠김 방지 우선)
    - 이후 로컬 지급 → `ackPurchaseClientGrant(APPLIED_ACKED)` 수행
    - `PurchaseStorage.current`는 `Confirm + storeConfirm ACK + clientGrant report` 종결 후 clear
    - 로컬 지급 실패 → `ackPurchaseClientGrant(FAILED_REPORTED)` 보고 가능, 단 `storeConfirmStatus != CONFIRMED`이면 clear 금지
  - `ALREADY_GRANTED`
    - `storeConfirmStatus == PENDING`이고 로컬에서 이미 Confirm 완료(`storeConfirmedLocal`) 상태면 `ackPurchaseStoreConfirm` 복구 먼저 수행
    - `clientGrantStatus == APPLIED_ACKED` → 중복 지급 없이 Confirm/ACK 복구만 진행
    - `clientGrantStatus == PENDING` 또는 `FAILED_REPORTED` → 로컬 지급 복구 재시도 허용 → APPLIED_ACKED 보고
    - `PurchaseStorage.current`는 복구 단계가 모두 종결될 때만 clear
  - `REJECTED` → Confirm **하지 않음** (Unity가 자동 환불 처리)
  - `PENDING` → Confirm **하지 않음** (스토어 확정 대기)
- Confirm을 호출하지 않으면 Unity IAP가 다음 앱 실행 시 `OnPurchasePending`을 재전달한다.

#### Client-side local snapshot (PurchaseStorage)

- 클라이언트는 `PurchaseManager`가 소유하는 `PurchaseStorage`에 **최소 상태 스냅샷**만 기록할 수 있다.
- 목적: 구매 진행 중 상태(current)의 local/cloud 저장 (복구 보조) + 환불/지원 대응용 최소 로그(refundSupportLogs)
- `current`는 복구 워크아이템이며, `Confirm + storeConfirm ACK + clientGrant report`가 종결되기 전에는 clear하지 않는다.
- 금지: 전체 구매 이력 정본 저장, 구매 실패 상세(코드/메시지) 저장, raw receipt 저장, 서버 원장/멱등 대체
- Rental 만료 시각(`expiresAtClientUtcMs`) / SeasonPass 소유권은 **InventoryStorage**에서 관리한다 (PurchaseStorage 범위 아님).
  - InventoryStorage: `Rentals` (rentalTypeId → expiresAtClientUtcMs), `SeasonPasses` (seasonPassTypeId → owned)
  - Reward 파이프라인(`REWARD_TYPE.RENTAL` / `REWARD_TYPE.PASS`)을 통해 설정하고, 만료 시각은 서버 데이터 Sync에서 갱신한다.
- `RetryInterruptedPurchaseAsync()`는 "재구매"가 아니라 `PurchaseStorage.current` + 서버 상태를 바탕으로 **중단된 상태 전이를 재개**하는 경로다.
- 정본 문서: `33-purchase-storage`

#### Reward 적용(클라) 규칙

- `GRANTED`이면 컨텐츠 레이어 매핑(`internalProductId -> rewardGroupId`) 후 RewardManager의 `ApplyRewardGroup(rewardGroupId)`를 호출한다. (`AppliedRewards` 수집 가능)
- `ALREADY_GRANTED`는 기본적으로 재지급 금지이나, `clientGrantStatus == PENDING` 또는 `FAILED_REPORTED`인 경우에는 로컬 지급 복구 경로를 허용한다.
- `grants[]`는 응답 스키마에 존재할 수 있으나, **클라 지급 입력으로 사용하지 않는다**(지급 호출은 rewardGroupId 기반).
- RewardManager는 지급 실행만 담당하며, 멱등/기록/복구는 PurchaseManager(+서버)가 책임진다.

연관:
- [49-reward-system/01-policy](../../49-reward-system/01-policy/SKILL.md)
- [49-reward-system/03-ssot](../../49-reward-system/03-ssot/SKILL.md)

#### Pending / Deferred 처리 규칙 (v5)

- v5 `OnPurchasePending`은 **정상 구매와 deferred/pending을 모두 포함**한다.
- 클라이언트 처리 흐름:
  1) `OnPurchasePending` 수신 → 무조건 서버 `verifyPurchase` 호출
  2) 서버가 스토어 검증 시 pending/deferred 상태 감지 → `PENDING` 반환
  3) 클라: `PENDING` 수신 → Confirm 하지 않음, 지급 하지 않음
  4) 스토어 확정 후 다음 앱 실행 시 `OnPurchasePending` 재전달 → 1)부터 재시도
- **PENDING 상태에서는 절대 지급/Confirm 금지**


### Firestore SSOT Schema (Canonical)

> 결제 운영(조회/감사/복구)을 위해 "구매 상세 조회"가 가능한 최소 필드를 유지한다.

#### `/users/{uid}/purchases/{purchaseId}`

- `purchaseId` 권장 규칙: `{storeKey}_{storePurchaseId}`
  - `storePurchaseId`는 Apple transaction id / Google orderId 또는 purchaseToken 기반 식별자 등 "스토어에서 유일한 값"
- 최소 필드
  - `storeKey: string`
  - `storePurchaseId: string`
  - `internalProductId: string`
  - `kind: string`
  - `verifyStatus: string`  // "GRANTED" | "REJECTED" | "PENDING" | "REVOKED" | "REFUNDED"
  - `clientGrantStatus: string` // "PENDING" | "APPLIED_ACKED" | "FAILED_REPORTED"
  - `storeConfirmStatus: string` // "PENDING" | "CONFIRMED"
  - `status: string`  // legacy compatibility field (temporary)
  - `lastStatusChangeAt: timestamp` // server timestamp (상태 변경 시각)
  - `statusReason: string`          // "refund" | "refund_reversed" | "chargeback" | "manual" 등
  - `payloadHash: string` // 원문 저장 대신 해시 권장
  - `environment: string` // "sandbox" | "production" (가능하면 기록)
  - `storePurchasedAt: timestamp` // 스토어(영수증/서버 검증 응답)에서 추출한 "구매 시각" — 클라이언트/디바이스 시간 사용 금지(서버에서만 생성)
  - `createdAt: timestamp` (server timestamp)
  - `updatedAt: timestamp` (server timestamp)
  - `raw: map` (optional; 민감정보 제외한 최소 subset)

#### `/users/{uid}/entitlements/current`

- 최소 필드(프로젝트 확장 가능)
  - `updatedAt: timestamp` (server timestamp)
  - (예시) `ownedSeasonPasses: array<string>`
  - (예시) `rentals: map<string, number>` // `internalProductId -> expiresAtServerUtcMs`
  - (예시) `currencyBalances: map<string, number>`

### SeasonPass / Rental Restore Projection (design fixed, server-side)

- `SeasonPass`/`Rental` 복원 정본은 서버 Firestore projection(`/users/{uid}/entitlements/current`)을 사용한다.
- `SyncEntitlementsAsync()`는 `getEntitlements`를 호출해 `InventoryStorage.Passes/Rentals`를 서버 값으로 덮어쓴 뒤 snapshot을 반환한다.
- `RestoreAsync()`(스토어 복원)는 스토어 복원 트리거 후 동일한 entitlements 복원 규칙을 적용한다.
- `SeasonPass` 복원은 시즌 유효 구간(`TB_SEASON.start <= now < end`)일 때만 반영한다.
- Rental/SeasonPass 조회: `GetRentalRemainingMsAsync`는 로컬 저장값 기준 남은 시간(ms)을 조회한다.
- `Rental` 재구매 만료일 계산 정책(클라이언트):
  - `newExpiry = max(existingExpiry, now) + 30일` (연장 방식)
- Rental 복원 정책(클라이언트):
  - `expiresAtServerUtcMs > serverNowUtcMs`만 반영
  - 누적 연장을 허용하므로 복원 시 상한(30일 cap)을 두지 않는다.


### Idempotency (멱등) 규칙

- 멱등 키는 `purchaseId`(권장: `{storeKey}_{storePurchaseId}`)를 사용한다.
- `verifyPurchase`는 같은 `purchaseId`로 여러 번 호출되어도:
  - purchases 문서는 upsert로 유지
  - entitlements는 "원장 기반 재계산" 또는 트랜잭션으로 갱신
  - **중복 지급이 발생하지 않아야 한다**


### Write / Read Policy

- Firestore write는 **서버(Functions/Admin SDK)** 만 수행한다.
- 클라이언트는 verify + `getEntitlements` 동기화 + SaveData(local/cloud) 저장/복원으로 상태를 갱신/조회한다.
- (정책 선택) 클라이언트 Firestore direct read를 허용한다면:
  - `/users/{uid}/entitlements/current`만 제한적으로 허용을 권장
  - purchases 원장 direct read는 기본 비권장
- Refund visibility (minimum)
  - 환불 발생 시: `verifyStatus="REFUNDED"`, `statusReason="refund"`, `lastStatusChangeAt=serverTimestamp`
  - 환불 취소(철회) 시: `verifyStatus`를 정상 상태로 되돌리고, `statusReason="refund_reversed"`, `lastStatusChangeAt=serverTimestamp`
  - 이 2개 필드로 "환불/환불취소가 실제로 있었는지"를 `purchaseId` 단건 조회에서 확인 가능하다.

### Privacy (Raw 데이터)

- receipt/payload 원문 저장은 피한다.
- `payloadHash` 저장을 기본으로 하고,
  raw 저장이 필요하면 "민감정보 제외 + 최소 subset"만 저장한다.

### End-to-End Flow

1) 클라: Unity IAP로 구매 진행
2) 클라: 영수증/receipt 확보 후 서버 `verifyPurchase` 호출
3) 서버: 스토어 검증(Apple/Google) 수행
4) 서버: Firestore에 purchases 기록 저장 + entitlements/current 갱신 (필수)
5) 클라: 서버 verify 성공 응답 수신 후에만 ConfirmPendingPurchase / 지급 처리
6) 클라: `SyncEntitlementsAsync`로 entitlements/current를 반영해 `mPasses/mRentals`를 복원
7) 클라: 반영된 상태를 SaveData(local/cloud) payload에 저장/복원

### Security Rules (High-level)

- purchases 컬렉션은 클라이언트 write 금지.
- entitlements/current는 (정책에 따라) 클라 read 허용 가능, write 금지.
- UID 스코프는 반드시 `request.auth.uid == uid`로 제한.

### NEEDS CHECK (code alignment)

- 현재 저장소 코드가 실제로 어떤 Firestore 경로/필드를 사용 중인지 별도 점검 필요.
  (본 작업은 문서-only이며 코드 변경 금지)


---


## D. Subscription NoAds (from 07-ssot-subscription-noads)


### SSOT 원칙

- NoAds는 "구독 Active 상태"로만 판정한다.
- 클라이언트 Unity IAP 콜백만으로 NoAds를 영구 적용하지 않는다.
- 최종 상태는 verifyPurchase + `SyncEntitlementsAsync`(server restore) + SaveData(local/cloud) 저장/복원으로 확정한다.


### 상태 갱신

#### iOS

- 서버 알림(Apple Server Notifications)을 수신하여 구독 상태를 갱신하는 구성을 권장한다.
- 알림 기반 갱신이 불가할 경우, scheduled recheck로 보조할 수 있다.

#### Android

- 운영 초기에는 scheduled recheck로 시작할 수 있다(선택).
- 최종 목표는 서버에서 "현재 유효 구독"을 신뢰 가능한 방식으로 유지하는 것이다.


### 클라이언트 적용 규칙

- NoAds 상태의 정본은 서버 entitlements 기반으로 복원된 `InventoryStorage` Rental(`NO_ADS`) 값이다.
- 동기화 방식: `PurchaseManager.SyncEntitlementsAsync(ct)`가 서버 `getEntitlements`를 호출해 Rental/SeasonPass를 복원(덮어쓰기)한다.
- NoAds는 광고 표시 로직의 단일 입력값으로 사용한다(여러 군데 중복 판정 금지).


---


## E. Season Pass (from 08-ssot-season-pass)


### SSOT 원칙

- 시즌 패스는 시즌별 구매 1회성 Entitlement로 운영한다.
- 시즌별 Store SKU를 분리한다(시즌별 SKU 1개 원칙).
  - 예: `season_pass_s2026_01`, `season_pass_s2026_02`
- 소유 여부는 Firestore Entitlement 상태로 저장/복구한다.
- 시즌 패스 구매 결과는 Entitlement(`SeasonPass(seasonId) owned`)로 저장한다.


### 구매/지급 규칙

- `verifyPurchase` 결과가 `GRANTED`인 경우에만,
  해당 시즌 `owned=true`로 반영한다.
- 이미 소유(owned=true)인 시즌은 "추가 구매/중복 지급"이 발생하지 않도록 처리한다.
  - 중복 방지는 서버 멱등 원장과 Entitlement 상태가 최종 방어선이다.


---


## Legacy / Deprecated Notes

- 과거 문서에서 `purchaseRecords`, `userEntitlements`, `subscriptionState` 같은 루트 컬렉션 명칭이 등장할 수 있다.
- SSOT는 본 문서의 `/users/{uid}/...` 스키마를 기준으로 한다.
