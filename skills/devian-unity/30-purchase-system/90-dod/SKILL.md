# 30-purchase-system — DoD


Status: ACTIVE
AppliesTo: v10


## Hard (0이어야 PASS)


- `skills/devian-unity/30-purchase-system/`에 다음 문서가 존재한다:
  - `00-overview`, `01-policy`
  - SSOT: `03-ssot` ~ `09-ssot-operations`
- SSOT(06)에 다음 합의가 명시되어 있다:
  - "Firebase Cloud Functions = verification server"
  - "Firestore = idempotent payment records / subscription status"
- Policy(01)에 다음 원칙이 명시되어 있다:
  - "클라 콜백만으로 지급 금지"
  - "internalProductId만 게임 로직에 노출"
  - "iOS Restore 제공"


## Soft


- Overview/SSOT 허브의 Start Here 링크 가독성 정리
