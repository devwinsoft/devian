# 09-ssot-operations — 47-advertise-system


Status: ACTIVE
AppliesTo: v10


이 문서는 47-advertise-system의 운영/테스트/DoD 정본이다.
테이블/보상/매니저 경계는 [03-ssot](../03-ssot/SKILL.md)가 정본이다.


---


## 운영 시나리오(정본)


### 1) 기본 개발/CI 경로

- Editor, CI, 로컬 개발 기본 provider는 `MockAdProvider`
- 실제 네트워크 광고 요청 없이 load/show/reward/fail 시나리오를 scripted callback으로 재현
- UI/scene 전환/버튼 흐름 검증은 mock 결과로 수행


### 2) 실기기 smoke test 경로

- 실 SDK 검증은 test ad unit 또는 test device 기반 수동 smoke test로만 수행
- placement별 최소 체크만 한다: initialize, load success, show success/fail, close, rewarded callback
- 한 세션에서 과도하게 반복 호출하지 않는다


### 3) 라이브 광고 운영 경로

- 라이브 ad unit은 QA 자동화/반복 회귀 테스트에 사용하지 않는다
- 무효 트래픽으로 오해될 수 있는 반복 노출/클릭/스크립트 호출을 금지
- 배포 전 검증은 mock + test unit + 제한된 실제 smoke test를 조합한다


### 4) Rewarded 지급 검증

- rewarded 완료 전에는 보상이 지급되지 않아야 한다
- reward callback 발생 시 `RewardManager.ApplyRewardGroup(rewardGroupId)`가 1회만 호출되어야 한다
- close-only / fail / no fill 경로에서는 보상 지급이 없어야 한다


### 5) SSV 콜백 검증

- Firebase Functions 에뮬레이터에서 SSV 콜백 엔드포인트가 정상 응답(HTTP 200)하는지 확인
- ECDSA 서명 검증 실패 시에도 HTTP 200 반환 + 에러 로깅 확인
- 동일 transaction_id 재요청 시 Firestore 중복 기록이 발생하지 않는지 확인
- custom_data 파싱(`{uid}:{advertiseId}:{rewardGroupId}`)이 정상 동작하는지 확인


---


## 테스트 체크리스트(정본)

- Editor에서 mock provider로 initialize/load/show/reward/fail이 모두 재현된다
- `REWARDED` placement는 `rewardGroupId` 누락 시 안전 실패한다
- `BANNER` / `INTERSTITIAL` / `APP_OPEN`은 `rewardGroupId` 없이 동작한다
- 동일 rewarded show cycle에서 reward callback이 2번 와도 보상 지급은 1회다
- NoAds 활성 시 banner/interstitial/app-open이 차단된다
- unsupported platform / SDK 미초기화 / no fill에서 예외 폭발 없이 실패 결과를 반환한다
- 실제 SDK smoke test는 test ad unit 또는 test device로만 수행한다
- SSV 콜백 엔드포인트가 Firebase 에뮬레이터에서 HTTP 200 응답한다
- SSV 서명 검증 실패 시에도 앱 크래시 없이 에러 로깅만 수행한다


---


## DoD (구현 단계 기준)


### Hard (반드시 0)

- live ad unit 기반 반복 자동 테스트 0건
- rewarded 중복 지급(동일 show cycle) 0건
- 미지원 플랫폼/Editor에서 크래시 0건
- reward callback 이전 선지급 0건
- `ADVERTISE` 정본 외 설정 하드코딩 0건


### Soft

- provider 전환이 AdsManager 상위 API 변경 없이 가능
- mock scenario 스위칭이 간단한 enum/설정 수준으로 가능
