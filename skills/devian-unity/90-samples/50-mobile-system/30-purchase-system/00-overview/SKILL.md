# 30-purchase-system — Overview


Status: ACTIVE
AppliesTo: v10


> Routing(키워드→스킬)은 중앙 정본을 따른다: `skills/devian/00-overview/SKILL.md`


Unity **In-App Purchasing (Unity IAP)** 기반의 인앱 결제 모듈(클라)과,
Firebase Cloud Functions + Firestore 기반의 **결제 검증/멱등 지급/구독 상태** 운영 규칙을 다룬다.


---


## Start Here


| Document | Description |
|----------|-------------|
| [01-policy](../01-policy/SKILL.md) | Devian 결제 모듈 경계/클라 API 규약(Policy) |
| [03-ssot](../03-ssot/SKILL.md) | 통합 SSOT (Unity IAP, Product Catalog, Verify/Idempotency, NoAds, Season Pass) |
| [09-ssot-operations](../09-ssot-operations/SKILL.md) | 운영/보안/테스트/DoD |
| [30-samples-purchase-manager](../30-samples-purchase-manager/SKILL.md) | PurchaseManager(구매) 샘플 |
| [40-purchase-backend-firebase](../40-purchase-backend-firebase/SKILL.md) | Firebase Backend(Functions+Firestore) 구현 정본 |
| [41-purchase-store-verification](../41-purchase-store-verification/SKILL.md) | Apple/Google 스토어 검증 및 storePurchaseId 정본 |
| [42-purchase-entitlements-grants](../42-purchase-entitlements-grants/SKILL.md) | grants/entitlements 계산 및 저장 규칙 정본 |
| [43-purchase-client-server-integration](../43-purchase-client-server-integration/SKILL.md) | Verify/Sync 호출 방식 및 ConfirmPurchase 하드룰 |
| [44-purchase-repo-firebase-functions-setup](../44-purchase-repo-firebase-functions-setup/SKILL.md) | Firebase Functions를 레포에 추가하기 위한 구성 정본(파일/폴더/배포) |
| [46-purchase-decisions](../46-purchase-decisions/SKILL.md) | 결제 검증(Firebase Callable) 결정사항 정본(경로/검증방식/멱등/시크릿/스키마) |


---


## Related


- [PurchaseManager Sample](../30-samples-purchase-manager/SKILL.md) — `com.devian.samples` / `Samples~/MobileSystem/Runtime/PurchaseManager/PurchaseManager.cs`
- [Root SSOT](../../../../../devian-core/03-ssot/SKILL.md)
- [Unity SSOT](../../../../03-ssot/SKILL.md)
- [Devian Index](../../../../../devian/SKILL.md)
