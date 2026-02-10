# 05-ssot-product-catalog — Product Catalog (SSOT)


Status: ACTIVE
AppliesTo: v10


## SSOT 원칙


### 1) 내부 표준 ID가 정본


- 게임/Devian 로직은 `internalProductId`가 정본이다.
- Store SKU는 매핑 데이터로만 취급한다.


### 2) 타입 규칙


- Consumable: 재화 등 반복 구매/즉시 지급
- Subscription: "구독 기반 NoAds" 등 상태 기반
- Season Pass: 시즌별 구매 1회성 Entitlement로 운영(아래 규칙)


---


## 시즌 패스 규칙 (SSOT)


- 시즌 패스는 **시즌별 Store SKU 1개**를 원칙으로 한다.
  - 예: `season_pass_s2026_01`, `season_pass_s2026_02`
- 시즌 패스 구매 결과는 Entitlement(`SeasonPass(seasonId) owned`)로 저장한다.


정본: [08-ssot-season-pass](../08-ssot-season-pass/SKILL.md)


---


## NEEDS CHECK (형준 결정 필요)


- 카탈로그 SSOT 저장 위치:
  - (A) Devian input 테이블(Excel) 기반
  - (B) Firestore/JSON 기반
위 위치 결정 전까지 "내부 ID ↔ SKU 매핑이 필요"만 확정한다.
