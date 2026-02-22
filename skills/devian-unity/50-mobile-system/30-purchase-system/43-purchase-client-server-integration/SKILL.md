# 43-purchase-client-server-integration — Client ↔ Server Calls (Verify/Sync)

Status: ACTIVE
AppliesTo: v10

> Purchase SSOT: `skills/devian-unity/50-mobile-system/30-purchase-system/03-ssot/SKILL.md` (C 섹션 "Callable 권장", 필드 매핑)

## 목적

`PurchaseManager`의 스텁:
- `VerifyPurchaseAsync`
- `AckPurchaseClientGrantAsync`
- `SyncEntitlementsAsync`
를 어떤 호출 방식으로 서버(Functions)에 연결할지 "정본"으로 고정한다.

또한 `ConfirmPurchase` 호출 타이밍을 SSOT 기준으로 하드룰로 고정한다.


---


## A. 호출 방식 (정본) — ✅ 확정: Firebase Functions Callable

- **Firebase Functions Callable**을 사용한다.
- SDK: `Firebase.Functions` (Firebase Unity SDK 13.7.0, `Firebase.Functions.dll`)
- 호출 패턴:
  - `FirebaseFunctions.DefaultInstance.GetHttpsCallable(functionName)`
  - `.CallAsync(data)` → `HttpsCallableResult` → `.Data` (Dictionary)
- `uid`는 Firebase Auth context에서 자동 전달된다 (클라가 명시적으로 보내지 않음).


---


## B. 요청/응답 매핑 (정본)

SSOT의 "C# ↔ Callable 필드 매핑"을 그대로 따른다.

- 요청 키:
  - `storeKey`, `internalProductId`, `kind` (`"Consumable" | "Rental" | "Subscription" | "SeasonPass"`), `payload`
- 응답 키:
  - `resultStatus`, `purchaseId`, `verifyStatus`, `clientGrantStatus`, `storeConfirmStatus`, `grants`, `entitlementsSnapshot`

- ACK(로컬 지급 결과 보고) 요청 키:
  - `purchaseId`, `clientGrantStatus`
- ACK 응답 키:
  - `purchaseId`, `verifyStatus`, `clientGrantStatus`, `storeConfirmStatus`
- Confirm ACK 요청/응답 키:
  - 요청: `purchaseId`
  - 응답: `purchaseId`, `verifyStatus`, `clientGrantStatus`, `storeConfirmStatus`


---


## C. ConfirmPurchase 타이밍 (하드룰)

클라이언트는 서버 응답의 `resultStatus`와 `clientGrantStatus`에 따라
로컬 지급 복구/ACK/Confirm 순서를 결정한다.

- `GRANTED` → (로컬 지급 성공) → `ackPurchaseClientGrant(APPLIED_ACKED)` 성공 → `PurchaseStorage.current` clear → `ConfirmPurchase(pendingOrder)` → `ackPurchaseStoreConfirm`
- `GRANTED` 로컬 지급 실패 → `ackPurchaseClientGrant(FAILED_REPORTED)` 성공 → `PurchaseStorage.current` clear → Confirm 하지 않음
- `ALREADY_GRANTED`
  - `clientGrantStatus == APPLIED_ACKED` → 중복 지급 없이 `ConfirmPurchase(pendingOrder)` (필요 시 `ackPurchaseStoreConfirm`)
  - `clientGrantStatus == PENDING` → 로컬 지급 복구 후 `ackPurchaseClientGrant(APPLIED_ACKED)` 성공 → `PurchaseStorage.current` clear → `ConfirmPurchase(pendingOrder)` → `ackPurchaseStoreConfirm`
  - `clientGrantStatus == FAILED_REPORTED` → 로컬 지급 복구 재시도 허용 (성공 시 APPLIED_ACKED 보고 후 Confirm 경로)
- `REJECTED` / `PENDING` / `REVOKED` / `REFUNDED` → Confirm 하지 않음

(SSOT의 resultStatus 규칙과 불일치하면 이 문서/코드가 아니라 SSOT를 기준으로 수정한다.)

> ~~**현재 코드 위반**~~ → ✅ **수정됨**: `resultStatus` 확인 후 `GRANTED`/`ALREADY_GRANTED`만 Confirm, 나머지는 Confirm 하지 않음.

또한 로컬 지급은 `GRANTED` 또는 `ALREADY_GRANTED + clientGrantStatus == PENDING/FAILED_REPORTED` 복구 경로에서만 수행한다.
Reward는 지급 실행만 담당하며, 멱등/복구 판단은 PurchaseManager(+서버 상태) 기준으로 한다.


---


## C2. purchaseAndVerifyAsync 반환값 규칙 (하드룰)

`purchaseAndVerifyAsync`는 서버 `resultStatus`에 따라 반환 타입을 구분해야 한다.

- `GRANTED` / `ALREADY_GRANTED` → `CommonResult.Success(PurchaseFinalResult)` 반환
- `REJECTED` / `PENDING` / `REVOKED` / `REFUNDED` → `CommonResult.Failure(...)` 반환

호출자가 `IsSuccess`만으로 지급 여부를 판단할 수 있어야 한다.

> ~~**현재 코드 위반**~~ → ✅ **수정됨**: `GRANTED`/`ALREADY_GRANTED`만 `Success`, 나머지는 `Failure` 반환.


---


## C3. 최근 구매 조회 (kind 파라미터)

- Client: `PurchaseManager.GetLatestConsumablePurchase30dAsync()`
- Server: `getRecentPurchases30d` (`kind="Consumable"`, `pageSize=1`)
- Rental latest 조회도 별도 callable을 만들지 않고 동일하게 `getRecentPurchases30d`를 `kind="Rental"`로 호출한다.
- 서버가 "최근 30일"을 계산한다. 클라/기기 시간 사용 금지.
- 최근 30일 내 해당 kind 내역이 없으면 `CommonResult.Failure(COMMON_SERVER, ...)` 반환.


---


## D. Entitlements 동기화 (정본)

- 앱 시작/로그인/복원 트리거 시 `getEntitlements`를 호출하여
  클라 표시/UI 상태를 서버 스냅샷으로 맞춘다.


---


## DoD

Hard (must be 0)
- [x] Verify/Sync 호출 방식이 단일 옵션으로 확정됐다. → Firebase Functions Callable
- [x] SSOT 필드 매핑과 동일하다. → 코드에서 verify/ack 요청·응답 키 사용 확인
- [x] ConfirmPurchase 하드룰이 문서에 명시돼 있다.

Soft
- [ ] 호출 실패 시 에러 매핑 규칙(예: CommonErrorType) 링크 추가
