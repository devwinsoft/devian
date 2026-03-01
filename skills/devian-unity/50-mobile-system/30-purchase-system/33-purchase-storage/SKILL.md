# 33-purchase-storage


PurchaseStorage(구매 상태 스냅샷)의 위치/역할/저장 규칙을 설명한다.

## 문서 경계 (Scope)

- 이 문서는 `PurchaseStorage`의 **역할/필드 범위/소유자(PurchaseManager)** 를 정의한다.
- 전체 구매 이력(ledger) 설계, 서버 검증/멱등, 환불/복구 정책은 이 문서의 범위가 아니다.
- 서버 정본은 `40/41/42/46`, 클라이언트 구매 흐름은 `30-samples-purchase-manager`를 참조한다.


---


## Purpose

- `PurchaseManager`의 **진행 중인 결제 상태(current)** 만 최소 정보로 저장한다.
- `PurchaseManager`가 소유하며 SaveData(local/cloud) 경로에 포함될 수 있게 한다.
- 앱 재시작/동기화 이후에도 미완료 구매 흐름(verify 성공 후 ACK/Confirm 이전 등)의 복구 보조용으로 사용할 수 있다.


---


## Ownership (SSOT)

- 소유자: `PurchaseManager`
- 사용처: `PurchaseManager` (구매 시작/스토어 pending/verify 성공/로컬지급 완료/ACK 완료 시점에 기록)
- 저장 경로: `SaveDataManager` JSON의 `purchase` 섹션

중요:
- `PurchaseStorage`는 **PurchaseManager 소유 필드**이다.
- `PurchaseStorage`는 **서버 구매 원장(Firestore purchases/grants/entitlements) 대체가 아니다**.


---


## Implementation Location (3-path mirror)

> 3-path mirror 정책: [devian-unity/07-samples-creation-guide](../../../../devian-unity/07-samples-creation-guide/SKILL.md), [devian-unity/03-ssot](../../../../devian-unity/03-ssot/SKILL.md) §UPM Packages Sync

- UPM (정본): `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Purchase/PurchaseStorage.cs`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/Purchase/PurchaseStorage.cs`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/Purchase/PurchaseStorage.cs`

- asmdef: `Devian.Samples.MobileSystem` (`Samples~/MobileSystem/Runtime/Devian.Samples.MobileSystem.asmdef`)


---


## Data Scope (Minimal Snapshot)

`PurchaseStorage`는 "전체 구매 기록 배열"을 저장하지 않는다.
저장 범위는 아래 3개로 제한한다.
- `current` (진행 중 결제 1건)
- `refundSupportLogs` (환불/지원 대응용 최소 로그, bounded)
- `refundSync` (환불 동기화 처리 완료 키, bounded)

### 1) current (진행 중 구매, 최대 1건)

- 코드 구조는 `PurchaseStorage.Current` (중첩 클래스 `CurrentPurchaseState`)로 묶어 관리한다.

- `isPurchaseInProgress`
- `internalProductId`
- `kind`
- `storeKey`
- `startedAtUtcMs`
- `isStorePending`
- `storePendingAtUtcMs`
- `purchaseId` (서버 verify 후 확정되는 멱등키)
- `verifyStatus` (`GRANTED` / `ALREADY_GRANTED` 등 verify 결과)
- `clientGrantApplied` (로컬 지급 완료 여부)
- `clientGrantReported` (로컬 지급 결과를 서버에 보고 완료했는지)
- `storeConfirmedLocal` (클라가 `ConfirmPurchase`를 로컬에서 성공 호출했는지)

### 2) refundSupportLogs (환불/지원 대응용 최소 로그, bounded)

- 목적: 환불/지원 대응 시 필요한 최소 구매 상태 스냅샷 보조 로그
- 정본 아님: 서버 purchases ledger가 정본
- bounded: 무제한 저장 금지 (현재 구현은 개수 상한 적용)
- 보관 정책(초기값, 구현됨):
  - 보관 기간(TTL): `30일` (`lastUpdatedAtUtcMs` 기준)
  - 개수 상한(Cap): `32개`
  - 정리 순서: `TTL prune` 후 `count cap trim`
- 정리 실행 시점(구현됨):
  - `UpsertRefundSupportLog(...)` 직후
  - `RestoreRefundSupportLogs(...)` 복원 직후
  - `SaveDataManager.ToJson()` 경로의 `PurchaseStorage.PruneRefundSupportLogs()` 호출 시
- 항목 예시:
  - `purchaseId`
  - `internalProductId`
  - `kind`
  - `storeKey`
  - `verifyStatus`
  - `clientGrantStatus`
  - `storeConfirmStatus`
  - `firstSeenAtUtcMs`
  - `lastUpdatedAtUtcMs`

### 2-1) 조회/삭제 API (구현됨)

- 조회:
  - `RefundSupportLogs` (read-only 프로퍼티)
  - `GetRefundSupportLogs()`
  - `TryGetRefundSupportLog(purchaseId, out entry)`
- 삭제:
  - `RemoveRefundSupportLog(purchaseId)` (개별 삭제)
  - `ClearRefundSupportLogs()` (전체 삭제)
- 식별/표시:
  - 환불 로그 항목에는 `internalProductId`가 포함되어 있어 상품 식별에 사용할 수 있다.

### 3) refundSync (환불 동기화 상태, 구현됨)

- 목적: `RefundAsync()`가 서버의 환불/조정 내역을 페이지네이션 조회할 때, 처리 완료 위치와 이미 처리한 항목을 저장하여 중복 처리를 방지
- 필드:
  - `cursor` (string) — 서버 페이지네이션 커서. `RefundAsync()` 호출마다 newest부터 재스캔하므로 **런타임에서 사용하지 않는다**. JSON codec에서 persist되지만 `RefundAsync()`는 항상 빈 커서로 시작한다. (dead field, 향후 정리 대상)
  - `processedKeys` (string[]) — 이미 처리 완료한 refund adjustment 키 목록. 중복 처리 방지용으로 persist
- 개수 상한(Cap): `128개` (`MaxProcessedRefundSyncKeys`)
  - 초과 시 가장 오래된 키부터 FIFO 제거
- API (구현됨):
  - `RefundSyncCursor` (read-only 프로퍼티)
  - `ProcessedRefundSyncKeys` (read-only 프로퍼티)
  - `SetRefundSyncCursor(cursor)`
  - `IsRefundAdjustmentProcessed(key)`
  - `MarkRefundAdjustmentProcessed(key)`
  - `ClearRefundSyncState()` (커서 + 처리 키 전체 초기화)
  - `RestoreRefundSyncState(cursor, processedKeys)` (SaveData 복원 시)

### Future (지금 구현 안 함)

- `lastSyncedServerUtcMs`

위 항목은 필요 시 추가할 수 있으나, 현재 구현 범위에 포함되지 않는다.

NOTE:
- `noAdsExpireAtClientUtcMs`, `seasonPassOwnership`, `Rental 로컬 캐시 상태`는 **InventoryStorage로 이동됨** (PurchaseStorage 범위 아님).
  - InventoryStorage: `Rentals` (rentalTypeId → expiresAtClientUtcMs), `SeasonPasses` (seasonPassTypeId → owned)
  - Reward 파이프라인(`REWARD_TYPE.RENTAL` / `REWARD_TYPE.SEASON_PASS`)을 통해 관리된다.


---


## Hard Rules

### 1) 전체 구매 이력 저장 금지

- `PurchaseStorage`는 구매 이력 리스트/배열(정본 로그)을 저장하지 않는다.
- 전체 구매 내역의 정본은 서버(Firestore purchases/entitlements/grants)다.

### 1-1) 구매 실패 내역 저장 금지

- 실패 코드/실패 메시지/최근 실패 요약을 `PurchaseStorage`에 저장하지 않는다.
- 사용하지 않는 진단 데이터는 클라이언트에 저장하지 않는다.

### 2) 민감정보 저장 금지

- raw receipt / 영수증 payload 저장 금지
- Firebase/Auth token 저장 금지
- Store token/서명 원문 저장 금지

### 3) 서버 검증/멱등 대체 금지

- `PurchaseStorage`의 값으로 지급 결정/환불 판정/중복 지급 방지를 수행하지 않는다.
- 지급/엔타이틀먼트 최종 판정은 서버 `verifyPurchase` / `getEntitlements` 결과만 사용한다.

### 4) PurchaseManager 소유 유지

- 로컬/클라우드 저장 연동을 위해 `PurchaseManager`가 `PurchaseStorage`를 직접 소유한다.
- `SaveDataManager`는 직렬화/역직렬화 시 `PurchaseManager.Instance.Storage`를 사용한다.


---


## Integration Notes

- `PurchaseManager`는 자신의 `Storage`에 기록한다.
- `SaveDataManager.ClearGameState()`는 `PurchaseStorage.ClearAll()`을 호출한다.
- SaveData(local/cloud)는 `SaveDataManager` JSON 전체를 저장하므로 `purchase` 섹션도 함께 저장/복구된다.
- `PurchaseStorage`는 `current`, `refundSupportLogs`, `refundSync`만 저장하며, 실패 이력/전체 구매 정본 로그는 저장하지 않는다.
- `current`는 복구 워크아이템이며, `Confirm + storeConfirm ACK + clientGrant report`가 종결되기 전에는 clear하지 않는다.
- `storeConfirmedLocal=true` + `storeConfirmStatus=PENDING`인 경우 `RetryInterruptedPurchaseAsync()`는 `ackPurchaseStoreConfirm`부터 재개할 수 있어야 한다.
- `refundSupportLogs`는 지원/환불 대응 보조용이며, TTL/Cap 정책에 따라 자동 정리된다.


---


## Related

- [00-overview](../00-overview/SKILL.md) — Purchase System 개요
- [03-ssot](../03-ssot/SKILL.md) — Purchase 통합 SSOT
- [30-samples-purchase-manager](../30-samples-purchase-manager/SKILL.md) — PurchaseManager 샘플
- [21-savedata-system/43-savedata-json-codec](../../21-savedata-system/43-savedata-json-codec/SKILL.md) — SaveData JSON 직렬화 정본
- [21-savedata-system/00-overview](../../21-savedata-system/00-overview/SKILL.md) — local/cloud 저장 시스템 개요
