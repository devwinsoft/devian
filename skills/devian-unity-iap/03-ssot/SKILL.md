# 03-ssot — devian-unity-iap


Status: ACTIVE
AppliesTo: v10


## SSOT 범위


이 그룹에서 **03~09 문서는 모두 SSOT**이다.


- 03: 허브/우선순위/링크
- 04~09: SSOT 분할 문서(정본 규칙)


---


## Start Here (SSOT)


| Doc | Scope |
|-----|-------|
| [04-ssot-unity-iap](../04-ssot-unity-iap/SKILL.md) | Unity IAP 전제, 플랫폼 차이(restore/pending 등) |
| [05-ssot-product-catalog](../05-ssot-product-catalog/SKILL.md) | internalProductId ↔ Store SKU 매핑, 타입 규칙 |
| [06-ssot-verify-idempotency](../06-ssot-verify-idempotency/SKILL.md) | Cloud Functions 검증 서버 + Firestore 멱등 원장/트랜잭션 |
| [07-ssot-subscription-noads](../07-ssot-subscription-noads/SKILL.md) | 구독 기반 NoAds 상태 규칙 |
| [08-ssot-season-pass](../08-ssot-season-pass/SKILL.md) | 시즌 패스(시즌별 SKU) 운영 규칙 |
| [09-ssot-operations](../09-ssot-operations/SKILL.md) | 보안/로그/테스트/운영 체크 |


---


## 우선순위


- Root SSOT: `skills/devian-core/03-ssot/SKILL.md`
- Unity 관련 SSOT: `skills/devian-unity/03-ssot/SKILL.md`
- 결제/IAP 관련 SSOT: 이 문서 및 04~09


충돌 시 Root SSOT의 정의/플레이스홀더/규약이 우선한다.
