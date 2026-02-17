# 09-ssot-operations — Operations (SSOT)


Status: ACTIVE
AppliesTo: v10


## 보안/운영 체크 (SSOT)


### 1) 서버 권한/인증


- verifyPurchase/getEntitlements는 Auth 필수
- uid 없이 지급/상태 변경 금지


### 2) 로그/개인정보


- 영수증 원문을 그대로 로그에 남기지 않는다.
- 저장은 필요 최소(원장에 필요한 키/상태/시각 중심)로 한다.


### 3) 멱등/재시도


- 클라이언트는 네트워크 실패 시 재시도할 수 있다.
- 서버는 동일 purchaseKey에 대해 **항상 멱등**으로 동작해야 한다.


### 4) 테스트 시나리오(최소)


- Consumable: 재시도/중복 콜백에도 1회만 지급
- Season Pass: 소유 후 재구매 방지/재설치 후 복구
- Subscription NoAds: 활성/만료 상태 변경이 NoAds에 반영됨(서버 기준)
- Consumable(보물상자): 네트워크 재시도/중복 콜백에도 1회만 지급(서버 멱등 원장 기준)
- Pending/Deferred: 지급되지 않음


---


## DoD (from 90-dod)


### Hard (0이어야 PASS)


- `skills/devian-unity/30-purchase-system/`에 다음 문서가 존재한다:
  - `00-overview`, `01-policy`
  - SSOT: `03-ssot` (통합 SSOT 허브)
  - Operations: `09-ssot-operations` (이 문서)
- SSOT(03)에 다음 합의가 명시되어 있다:
  - "Firebase Cloud Functions = verification server"
  - "Firestore = idempotent payment records / subscription status"
- Policy(01)에 다음 원칙이 명시되어 있다:
  - "클라 콜백만으로 지급 금지"
  - "internalProductId만 게임 로직에 노출"
  - "iOS Restore 제공"


### Soft


- Overview/SSOT 허브의 Start Here 링크 가독성 정리
