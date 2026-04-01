---
name: 45-purchase-refund-processing
description: Define refund and revoke processing for the purchase backend. Use when implementing RTDN handling, purchase state transitions to REFUNDED or REVOKED, entitlement cleanup, and client refund adjustment workflows.
---

# 45-purchase-refund-processing — Refund Detection & Processing

Status: ACTIVE
AppliesTo: v10

> Purchase SSOT: `../03-ssot/SKILL.md` (C 섹션: verifyStatus enum)

> Backend 정본: `../40-purchase-backend-firebase/SKILL.md` (B 섹션: Firestore 스키마)


## 문서 경계 (Scope)

- 이 문서는 **환불 감지 → 상태 변경 → 클라이언트 회수** 전체 파이프라인의 구현/운영 정본이다.
- 포함: RTDN 핸들러, Callable(getPurchaseAdjustments/ackRefundApplied), 클라이언트 RefundAsync 흐름, 인프라 셋업 가이드
- 비포함: Firestore 스키마 정의(→ `40`), Callable 이름 고정(→ `46`), 로컬 저장 구조(→ `33`)


---


## A. 환불 처리 파이프라인 (Overview)

```
[Google Play] ─RTDN─→ [Pub/Sub] ─→ [handleGooglePlayNotification]
                                           │
                                    ┌──────┴──────┐
                                    │ Firestore    │
                                    │ verifyStatus │
                                    │ → REFUNDED   │
                                    └──────┬──────┘
                                           │
[Client App] ─RefundAsync()─→ [getPurchaseAdjustments] ─→ items[]
                    │
                    ├─→ ParseRefundSyncResult (REWARD 테이블 해석)
                    ├─→ RewardManager.RevokeRewardDatasPartial (로컬 인벤토리 회수)
                    └─→ [ackRefundApplied] ─→ clientRefundApplied=true
```

### A1. 단계별 역할

| 단계 | 위치 | 역할 |
|------|------|------|
| 1. 환불 감지 | Server (Pub/Sub) | Google Play RTDN 수신 → `verifyStatus` 변경 |
| 2. 환불 조회 | Server (Callable) | `getPurchaseAdjustments`: REFUNDED/REVOKED 문서 반환 |
| 3. 로컬 회수 | Client | `RefundAsync()`: REWARD 테이블 기반 인벤토리 회수 |
| 4. ACK | Server (Callable) | `ackRefundApplied`: `clientRefundApplied=true` 마킹 |


---


## B. Server: RTDN Handler (자동화)

### B1. 함수 정보

- 함수명: `handleGooglePlayNotification`
- 트리거: `onMessagePublished` (Pub/Sub)
- 토픽: `.env.{project}` 의 `RTDN_TOPIC` (예: `devian-play-rtdn`)
- 리전: `setGlobalOptions` 전역 설정 (index.ts)
- 시크릿: `GOOGLE_APPLICATION_CREDENTIALS_JSON`
- 소스: `functions/src/purchase/handleGooglePlayNotification.ts`

### B2. 처리할 RTDN 타입

| RTDN 필드 | 조건 | targetVerifyStatus | 설명 |
|-----------|------|-------------------|------|
| `voidedPurchaseNotification` | 존재 | `REFUNDED` | Play Console 환불 (가장 일반적) |
| `oneTimeProductNotification` | `notificationType == 2` | `REFUNDED` | 사용자 대기 구매 취소 |
| `subscriptionNotification` | `notificationType == 12` | `REVOKED` | 구독 취소/환불 |
| `testNotification` | 존재 | — | 연결 테스트 (로그만) |
| 기타 | — | — | 무시 (로그만) |

### B3. 처리 흐름

1. **메시지 파싱**: `event.data.message.json` 또는 base64 디코딩
2. **타입 필터**: 환불 관련 타입만 처리 (B2 참조)
3. **Firestore 조회**: collection group 쿼리 (`storePurchaseId == purchaseToken AND storeKey == "google"`)
4. **멱등성 검사**: 이미 REFUNDED/REVOKED → skip
5. **상태 검사**: GRANTED만 환불 가능 (그 외 → skip)
6. **Google Play API 재검증** (defense in depth):
   - `storeProductId` 확인 가능 시만 실행 (없으면 skip, RTDN 자체 신뢰)
   - products: `purchaseState === 0` (active) 이면 환불 중단
7. **Firestore 트랜잭션**:
   - purchase 문서: `verifyStatus → REFUNDED/REVOKED`, `updatedAt`, `refundedAt`, `refundSource: "RTDN"`
   - entitlements 정리:
     - Rental: `rentals` 맵에서 `rentalId` (fallback: `internal_product_id`) 키 삭제
     - SeasonPass: `ownedSeasonPasses[]` 에서 `seasonPassId` 제거
     - Consumable: entitlements 변경 없음

### B4. 에러 처리

| 상황 | 동작 | 이유 |
|------|------|------|
| 파싱 불가 | `return` (재시도 안함) | 메시지 형식 오류 |
| purchase 문서 미발견 | `return` (재시도 안함) | 문서 없음 |
| 이미 REFUNDED/REVOKED | `return` | 멱등 |
| 재검증 실패 (transient) | `throw` | Pub/Sub 재시도 |
| 재검증 결과 active | `return` | 환불 아님 |


---


## C. Server: Callable (자동화)

### C1. getPurchaseAdjustments

- 쿼리: `verifyStatus in ["REFUNDED", "REVOKED"]` AND `clientRefundApplied !== true`
- 응답: `{ items[], nextCursor, hasMore }`
- 커서: `"updatedAtMs|docId"` 형식
- 페이지: 빈 페이지 방지를 위해 `clientRefundApplied` 필터 후 추가 라운드 실행 (최대 5회)

### C2. ackRefundApplied

- 요청: `{ purchaseId }`
- 동작: purchase 문서에 `clientRefundApplied: true` (merge update)
- 문서 삭제 없음 (영구 감사 추적)


---


## D. Client: RefundAsync (자동화)

- 호출 시점: `initPurchase()` — IAP 초기화 후 앱 시작 시
- 흐름:
  1. `getPurchaseAdjustments` 호출 → 환불 항목 조회
  2. 각 항목의 `internal_product_id` → `ResolveRewardGroupId` → `ResolveRewardDatas`
  3. `RewardManager.RevokeRewardDatasPartial` 로컬 인벤토리 회수
  4. `ackRefundApplied` 호출 → 처리 완료 마킹
- 소스: `PurchaseManager.cs` (`RefundAsync` 메서드)


---


## E. Firestore 인덱스 (자동화)

`firestore.indexes.json` 에 포함:

### E1. RTDN 핸들러용 (collection group)

```json
{
  "collectionGroup": "purchases",
  "queryScope": "COLLECTION_GROUP",
  "fields": [
    { "fieldPath": "storeKey", "order": "ASCENDING" },
    { "fieldPath": "storePurchaseId", "order": "ASCENDING" },
    { "fieldPath": "__name__", "order": "ASCENDING" }
  ]
}
```

### E2. getPurchaseAdjustments용 (collection)

```json
{
  "collectionGroup": "purchases",
  "queryScope": "COLLECTION",
  "fields": [
    { "fieldPath": "verifyStatus", "order": "ASCENDING" },
    { "fieldPath": "updatedAt", "order": "DESCENDING" },
    { "fieldPath": "__name__", "order": "DESCENDING" }
  ]
}
```

### E3. storePurchaseId 단일 필드 (collection group)

```json
"fieldOverrides": [{
  "collectionGroup": "purchases",
  "fieldPath": "storePurchaseId",
  "indexes": [
    { "order": "ASCENDING", "queryScope": "COLLECTION_GROUP" }
  ]
}]
```


---


## F. 환경 변수 / 시크릿 (자동화)

| 키 | 위치 | 용도 |
|----|------|------|
| `RTDN_TOPIC` | `.env.{project}` | Pub/Sub 토픽명 |
| `GOOGLE_APPLICATION_CREDENTIALS_JSON` | Secret Manager | Google Play API 인증 |

리전은 `index.ts`의 `setGlobalOptions({region: "asia-northeast3"})` 로 전역 설정.


---


## G. 수동 설정 가이드 (Manual Setup)

### G1. Pub/Sub 토픽 생성

```bash
gcloud pubsub topics create {RTDN_TOPIC} --project={PROJECT_ID}
```

> Firebase deploy 시 Eventarc가 자동으로 구독을 생성하므로, 토픽만 미리 생성하면 된다.

### G2. Google Play Console RTDN 설정

1. [Google Play Console](https://play.google.com/console) 접속
2. 설정 → 수익 창출 설정 (Monetization setup)
3. **Real-time developer notifications** 섹션
4. 토픽 입력: `projects/{PROJECT_ID}/topics/{RTDN_TOPIC}`
5. **저장** 클릭
6. **Send test notification** 버튼으로 연결 확인

### G3. IAM 설정

#### G3-1. Google Play → Pub/Sub (Publisher)

Google Play가 RTDN 메시지를 토픽에 발행할 수 있도록:

```bash
gcloud pubsub topics add-iam-policy-binding {RTDN_TOPIC} \
  --member="serviceAccount:google-play-developer-notifications@system.gserviceaccount.com" \
  --role="roles/pubsub.publisher" \
  --project={PROJECT_ID}
```

> `google-play-developer-notifications@system.gserviceaccount.com` 은 Google 관리 시스템 계정이다. 프로젝트에 생성할 필요 없음.

#### G3-2. Pub/Sub → Cloud Run (Invoker)

Pub/Sub 구독이 Cloud Run(Gen 2 함수)을 호출할 수 있도록:

```bash
# 1) Compute 서비스 계정 확인
gcloud iam service-accounts list --project={PROJECT_ID} --filter="email:compute@developer.gserviceaccount.com"

# 2) Cloud Run invoker 역할 부여
gcloud run services add-iam-policy-binding handlegoogleplaynotification \
  --region={REGION} \
  --member="serviceAccount:{PROJECT_NUMBER}-compute@developer.gserviceaccount.com" \
  --role="roles/run.invoker" \
  --project={PROJECT_ID}
```

### G4. 배포

```bash
# Functions + Firestore indexes 동시 배포
firebase deploy --only functions,firestore:indexes --project={PROJECT_ID}
```

### G5. 연결 확인 체크리스트

| 단계 | 확인 방법 | 기대 결과 |
|------|----------|----------|
| 1. 토픽 존재 | `gcloud pubsub topics describe {RTDN_TOPIC}` | 토픽 정보 출력 |
| 2. 구독 연결 | `gcloud pubsub topics list-subscriptions {RTDN_TOPIC}` | eventarc 구독 1개 |
| 3. 연결 테스트 | Play Console "Send test notification" | Cloud Run 로그: `Test notification received` |
| 4. 로그 확인 | 아래 명령 참조 | 함수 실행 로그 |

```bash
# Cloud Run 로그 확인 (gcloud functions logs 보다 상세)
gcloud logging read \
  'resource.type="cloud_run_revision" AND resource.labels.service_name="handlegoogleplaynotification"' \
  --project={PROJECT_ID} \
  --limit=20 \
  --format="table(timestamp,severity,textPayload)" \
  --freshness=30m
```


---


## H. 제한사항

1. **라이선스 테스터 구매**: Google Play는 테스트 구매에 대해 `voidedPurchaseNotification` RTDN을 보내지 않는다. 실제 금전 거래가 있는 프로덕션 구매만 자동 감지된다.
2. **Apple**: 현재 Apple RTDN (App Store Server Notifications v2) 미구현. 향후 별도 스킬로 추가.
3. **수동 시뮬레이션**: 테스트 환경에서는 Pub/Sub 메시지를 수동 발행하여 핸들러를 검증할 수 있다:

```bash
gcloud pubsub topics publish projects/{PROJECT_ID}/topics/{RTDN_TOPIC} \
  --message='{"version":"1.0","packageName":"{PACKAGE_NAME}","eventTimeMillis":"...","voidedPurchaseNotification":{"purchaseToken":"{TOKEN}","orderId":"{ORDER_ID}","productType":1,"refundType":0}}'
```


---


## I. 관련 정본 링크

- Purchase SSOT: `../03-ssot/SKILL.md`
- Backend(40): `../40-purchase-backend-firebase/SKILL.md`
- Decisions(46): `../46-purchase-decisions/SKILL.md`
- PurchaseStorage(33): `../33-purchase-storage/SKILL.md`
- Client-Server Integration(43): `../43-purchase-client-server-integration/SKILL.md`
- Operations(09): `../09-ssot-operations/SKILL.md`


---


## DoD

Hard (must be 0)
- [x] RTDN 핸들러가 `voidedPurchaseNotification` / `oneTimeProductNotification(2)` / `subscriptionNotification(12)` 를 처리한다.
- [x] purchase 문서 `verifyStatus` → `REFUNDED`/`REVOKED` 변경이 Firestore 트랜잭션으로 수행된다.
- [x] entitlements 정리(Rental/SeasonPass)가 동일 트랜잭션에서 수행된다.
- [x] 멱등성: 이미 REFUNDED/REVOKED인 문서 재처리 방지.
- [x] Firestore collection group 인덱스(`storeKey` + `storePurchaseId`)가 배포되어 있다.
- [x] IAM 설정(Publisher + Invoker)이 완료되어 있다.
- [x] 클라이언트 `RefundAsync` → `getPurchaseAdjustments` → `RewardManager.RevokeRewardDatasPartial` → `ackRefundApplied` 흐름이 동작한다.

Soft
- [ ] Apple App Store Server Notifications v2 구현
- [ ] Voided Purchases API 폴링 (보조 경로) 구현
