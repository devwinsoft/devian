# 41-ad-ssv-decisions — Rewarded SSV Decisions


Status: ACTIVE
AppliesTo: v10


## 목적

AdMob Rewarded SSV 구현이 안정적으로 개발/관리되도록,
서버 엔드포인트/Firestore 경로/custom_data 포맷/의존성의
**결정사항을 단일 정본으로 고정**한다.

이 문서의 값이 바뀌면:
- 서버(Firebase Functions) SSV 코드
- 클라(Unity GoogleMobileAdsProvider)
- Firestore schema
- AdMob 콘솔 설정

이 함께 변경되어야 한다.


---


## A. Repo 구조 (결정)

- Functions 프로젝트 위치: `{repoRoot}/functions` (purchase와 동일, 46-purchase-decisions §A 참조)
- SSV 소스 위치: `{repoRoot}/functions/src/ads/verifyAdReward.ts`
- export: `{repoRoot}/functions/src/index.ts`에서 export


---


## B. 엔드포인트 (결정)

- 이름: `verifyAdReward`
- 타입: HTTP function (`onRequest`), **not Callable**
- HTTP 메서드: GET
- AdMob 콘솔 등록: 앱 설정 > Rewarded 광고 단위 > SSV 콜백 URL에 등록
- 리전: `asia-northeast3` (purchase와 동일, 46-purchase-decisions §G 참조)


---


## C. custom_data 포맷 (결정)

- 포맷: `{uid}:{advertise_id}:{reward_group_id}`
- 구분자: `:` (콜론)
- 예시: `abc123:ad_rewarded_001:reward_chest_001`

클라이언트가 `ServerSideVerificationOptions.SetCustomData()`로 설정한다.


---


## D. Firestore path (결정)

- 감사 로그: `/users/{uid}/adRewards/{transactionId}`
- `transactionId`는 AdMob SSV 콜백의 `transaction_id` 파라미터


---


## E. 공개키 (결정)

- URL: `https://gstatic.com/admob/reward/verifier-keys.json`
- 캐시 TTL: 24시간
- 검증 알고리즘: ECDSA with SHA-256


---


## F. npm 의존성 (결정)

- Node.js `crypto` 모듈로 직접 ECDSA 검증 (외부 의존성 없음)
- 추가 npm 패키지 불필요


---


## G. 보상 지급 방식 (결정)

- **클라이언트 즉시 지급** (기존 RewardManager 흐름 유지)
- SSV는 **감사/사후 검증** 용도
- 서버가 클라이언트 지급을 차단하거나 대기시키지 않는다


---


## H. 관련 정본 링크

- SSV 서버 구현: `../40-ad-ssv-firebase/SKILL.md`
- Advertise SSOT: `../03-ssot/SKILL.md` (§H)
- Policy: `../01-policy/SKILL.md` (§8)
- Purchase decisions (공유 인프라 참조): `../../30-purchase-system/46-purchase-decisions/SKILL.md`


---


## DoD

Hard (must be 0)
- [ ] 엔드포인트 이름/타입/리전이 단일 결정으로 고정돼 있다.
- [ ] custom_data 포맷이 단일 결정으로 고정돼 있다.
- [ ] Firestore path가 단일 결정으로 고정돼 있다.
- [ ] 보상 지급 방식(클라 즉시 + 서버 감사)이 고정돼 있다.

Soft
- [x] npm 의존성 선택이 확정돼 있다. (Node.js `crypto` 직접 구현)
