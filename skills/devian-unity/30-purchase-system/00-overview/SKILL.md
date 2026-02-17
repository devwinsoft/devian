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


---


## Related


- [PurchaseManager Sample](../30-samples-purchase-manager/SKILL.md) — `com.devian.samples` / `Samples~/PurchaseManager/Runtime/PurchaseManager.cs`
- [Root SSOT](../../../devian-core/03-ssot/SKILL.md)
- [Unity SSOT](../../03-ssot/SKILL.md)
- [Devian Index](../../../devian/SKILL.md)
