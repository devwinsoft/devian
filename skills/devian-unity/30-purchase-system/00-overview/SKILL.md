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
| [03-ssot](../03-ssot/SKILL.md) | 30-purchase-system SSOT 허브(03~09) |
| [06-ssot-verify-idempotency](../06-ssot-verify-idempotency/SKILL.md) | Functions 검증 서버 + Firestore 멱등 원장(핵심) |
| [07-ssot-subscription-noads](../07-ssot-subscription-noads/SKILL.md) | 구독 기반 NoAds 상태 규칙 |
| [08-ssot-season-pass](../08-ssot-season-pass/SKILL.md) | 시즌 패스(시즌별 SKU) 규칙 |


---


## Related


- Sample Implementation: `com.devian.samples` — `Samples~/PurchaseManager/Runtime/PurchaseManager.cs`
- [Root SSOT](../../../devian-core/03-ssot/SKILL.md)
- [Unity SSOT](../../03-ssot/SKILL.md)
- [Devian Index](../../../devian/SKILL.md)
