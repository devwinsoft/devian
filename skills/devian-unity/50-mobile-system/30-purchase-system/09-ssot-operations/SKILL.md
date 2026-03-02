# 09-ssot-operations — Operations (SSOT)


Status: ACTIVE
AppliesTo: v10

## 문서 경계 (Scope)

- 이 문서는 **운영/보안/테스트/DoD 체크리스트** 정본이다.
- Firebase Functions 구현 상세(함수 내부 로직/Firestore 스키마/멱등 구현)는 이 문서에 두지 않는다.
- 레포 구성/배포 명령/설정 파일 위치는 이 문서에 두지 않는다.
- 구현/셋업/결정사항은 아래 "Functions 관련 정본 링크"로 라우팅한다.


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


### 5) Reward 지급 연동

- `resultStatus == GRANTED`일 때만 컨텐츠 레이어 매핑(`internalProductId -> rewardGroupId`) 후 `RewardManager.ApplyRewardGroup(rewardGroupId)`로 적용한다.
- 멱등/복구/원장 정본은 Purchase 쪽이다. Reward는 지급 실행만 담당한다.

연관:
- [49-reward-system/01-policy](../../49-reward-system/01-policy/SKILL.md)


---

## Functions 관련 정본 링크 (중복 금지)

- 구현 정본(Functions + Firestore 스키마/멱등): `../40-purchase-backend-firebase/SKILL.md`
- 레포/배포/CLI/설정 파일 정본: `../11-purchase-repo-firebase-functions-setup/SKILL.md`
- 클라-서버 호출/ConfirmPurchase 규칙 정본: `../43-purchase-client-server-integration/SKILL.md`
- 고정 결정사항(Callable 이름/스키마/시크릿/경로): `../46-purchase-decisions/SKILL.md`

운영 문서(09)에는 체크리스트/테스트/DoD만 유지하고, 구현 상세는 위 문서들에만 기록한다.


---


## DoD (from 90-dod)


### Hard (0이어야 PASS)


- `skills/devian-unity/50-mobile-system/30-purchase-system/`에 다음 문서가 존재한다:
  - `00-overview`, `01-policy`
  - SSOT: `03-ssot` (통합 SSOT 허브)
  - Operations: `09-ssot-operations` (이 문서)
- SSOT(03)에 다음 합의가 명시되어 있다:
  - "Firebase Cloud Functions = verification server"
  - "Firestore = idempotent payment records / subscription status"
- Policy(01)에 다음 원칙이 명시되어 있다:
  - "클라 콜백만으로 지급 금지"
  - "internalProductId만 상위 로직에 노출"
  - "iOS Restore 제공"


### Soft


- Overview/SSOT 허브의 Start Here 링크 가독성 정리
