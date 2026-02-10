# 06-ssot-verify-idempotency — Verification Server (Functions) + Idempotency (Firestore)


Status: ACTIVE
AppliesTo: v10


## SSOT (핵심 합의)


- 별도 VM 서버 없이 **Firebase Cloud Functions**를 "검증 서버"로 사용한다.
- **Firestore**에 멱등 지급 원장 및 구독 상태를 저장한다.


---


## 엔드포인트(최소 세트)


### 1) verifyPurchase (Callable 권장)


- 입력(개념):
  - `uid`(Auth에서 확보)
  - `store`(google/apple)
  - `internalProductId`
  - 스토어 검증에 필요한 payload(토큰/트랜잭션 정보)
- 출력:
  - `resultStatus`: `GRANTED | ALREADY_GRANTED | REJECTED | PENDING`
  - `entitlementsDelta`(지급된 변화량/플래그)
  - `serverTime`


### verifyPurchase 응답(SSOT)


- `resultStatus`: `GRANTED | ALREADY_GRANTED | REJECTED | PENDING`
- `grants[]` 또는 `currencyDelta{}`를 포함한다.
  - Consumable(보물상자 등)은 여기의 지급 결과를 기준으로 클라가 반영한다.
- `ALREADY_GRANTED`는 재시도/중복 콜백 상황에서 정상일 수 있으며,
  이 경우 클라는 `getEntitlements`로 상태를 동기화한다.


### 2) getEntitlements (Callable)


- 앱 시작/포그라운드/로그인 시 호출
- 출력:
  - `noAdsActive`
  - `ownedSeasonPasses[]`
  - `currencyBalances{}` 등


### 3) (iOS) apple server notifications (HTTP)


- 구독 상태 갱신을 위해 서버 알림을 수신하는 HTTPS endpoint를 제공한다.
- 알림 수신 시 Firestore의 구독 상태를 업데이트한다.


### 4) (선택) scheduled recheck


- 운영 안정화를 위해 구독 상태 재검증/정리 작업을 스케줄로 둘 수 있다.


---


## Firestore 데이터(멱등 원장 + 상태)


### A) purchaseRecords (멱등 원장)


문서 키: `{store}:{purchaseKey}`


- `store`: `google` | `apple`
- `purchaseKey`:
  - Google: purchaseToken 등 "유일키"
  - Apple: transaction 기반 유일키


필드(개념):
- `uid`
- `internalProductId`
- `storeSku`
- `productType`
- `state`: `VERIFIED | GRANTED | REVOKED | PENDING | REJECTED`
- `verifiedAt`, `grantedAt`


### B) userEntitlements (유저 상태)


- `noAdsActive`(+ 만료 정보가 있으면 함께)
- `ownedSeasonPass`(시즌별 소유)
- `currencyBalances`(재화 잔고)


### C) subscriptionState (구독 전용 상태)


- `status`: `ACTIVE/EXPIRED/...`
- `expireAt`
- `lastStoreSyncAt`


---


## 멱등 규칙 (Hard)


- 동일 `{store}:{purchaseKey}`가 이미 `GRANTED`면 **추가 지급 금지**
- 원장 기록 + Entitlement 반영은 Firestore **Transaction**으로 묶는다.
- 클라에서 온 `storeSku/productType`는 신뢰하지 말고,
  서버 카탈로그/매핑을 기준으로 최종 판단한다(카탈로그 위치는 05에서 결정).


---


## 보안 규칙 (Hard)


- verifyPurchase/getEntitlements는 **Firebase Auth 필수**
- 영수증 원문/민감 데이터는 로그/저장에 주의(필요 최소 저장, 원문 그대로 로그 금지)
