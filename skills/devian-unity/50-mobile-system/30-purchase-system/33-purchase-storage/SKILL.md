# 33-purchase-storage


PurchaseStorage(구매 상태 스냅샷)의 위치/역할/저장 규칙을 설명한다.

## 문서 경계 (Scope)

- 이 문서는 `PurchaseStorage`의 **역할/필드 범위/소유자(GameStorageManager)** 를 정의한다.
- 전체 구매 이력(ledger) 설계, 서버 검증/멱등, 환불/복구 정책은 이 문서의 범위가 아니다.
- 서버 정본은 `40/41/42/46`, 클라이언트 구매 흐름은 `30-samples-purchase-manager`를 참조한다.


---


## Purpose

- `PurchaseManager`의 구매 진행 상태와 최근 결과 요약을 **최소 정보만** 저장한다.
- `GameStorageManager`가 소유하여 SaveData(local/cloud) 경로에 포함될 수 있게 한다.
- 앱 재시작/동기화 이후에도 구매 진행 중 상태/최근 실패 코드 등을 진단/UX 보조용으로 사용할 수 있다.


---


## Ownership (SSOT)

- 소유자: `GameStorageManager`
- 사용처: `PurchaseManager` (구매 시작/스토어 pending/verify 성공·실패 시점에 기록)
- 저장 경로: `GameStorageManager.ToJson()` / `LoadFromJson()`의 `purchase` 섹션

중요:
- `PurchaseStorage`는 **PurchaseManager 소유 필드가 아니다**.
- `PurchaseStorage`는 **서버 구매 원장(Firestore purchases/grants/entitlements) 대체가 아니다**.


---


## Implementation Location (3-path mirror)

- UPM: `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Purchase/PurchaseStorage.cs`
- UnityExample/Packages: `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/Purchase/PurchaseStorage.cs`
- Assets/Samples: `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/0.1.0/MobileSystem/Runtime/Purchase/PurchaseStorage.cs`

- asmdef: `Devian.Samples.MobileSystem` (`Samples~/MobileSystem/Runtime/Devian.Samples.MobileSystem.asmdef`)


---


## Data Scope (Minimal Snapshot)

`PurchaseStorage`는 "전체 구매 기록 배열"을 저장하지 않는다.
저장 범위는 아래 2개 스냅샷으로 제한한다.

### 1) current (진행 중 구매, 최대 1건)

- `isPurchaseInProgress`
- `internalProductId`
- `kind`
- `storeKey`
- `startedAtUtcMs`
- `isStorePending`
- `storePendingAtUtcMs`

### 2) last (최근 결과 요약, 최대 1건)

- `internalProductId`
- `kind`
- `storeKey`
- `resultStatus`
- `errorCode`
- `errorMessage`
- `updatedAtUtcMs`


---


## Hard Rules

### 1) 전체 구매 이력 저장 금지

- `PurchaseStorage`는 구매 이력 리스트/배열을 저장하지 않는다.
- 전체 구매 내역의 정본은 서버(Firestore purchases/entitlements/grants)다.

### 2) 민감정보 저장 금지

- raw receipt / 영수증 payload 저장 금지
- Firebase/Auth token 저장 금지
- Store token/서명 원문 저장 금지

### 3) 서버 검증/멱등 대체 금지

- `PurchaseStorage`의 값으로 지급 결정/환불 판정/중복 지급 방지를 수행하지 않는다.
- 지급/엔타이틀먼트 최종 판정은 서버 `verifyPurchase` / `getEntitlements` 결과만 사용한다.

### 4) GameStorageManager 소유 유지

- 로컬/클라우드 저장 연동을 위해 `GameStorageManager`가 `PurchaseStorage`를 소유한다.
- `PurchaseManager`는 상태 기록만 수행한다.


---


## Integration Notes

- `PurchaseManager`는 `GameStorageManager.Instance.Purchase`에 기록한다.
- `GameStorageManager.Clear()`는 `PurchaseStorage.ClearAll()`을 호출한다.
- SaveData(local/cloud)는 `GameStorageManager` JSON 전체를 저장하므로 `purchase` 섹션도 함께 저장/복구된다.


---


## Related

- [00-overview](../00-overview/SKILL.md) — Purchase System 개요
- [03-ssot](../03-ssot/SKILL.md) — Purchase 통합 SSOT
- [30-samples-purchase-manager](../30-samples-purchase-manager/SKILL.md) — PurchaseManager 샘플
- [95-game-storage-manager](../../95-game-storage-manager/SKILL.md) — GameStorageManager 정본
- [21-savedata-system/00-overview](../../21-savedata-system/00-overview/SKILL.md) — local/cloud 저장 시스템 개요

