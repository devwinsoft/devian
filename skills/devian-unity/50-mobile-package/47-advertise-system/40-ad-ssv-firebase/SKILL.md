# 40-ad-ssv-firebase — Rewarded SSV Firebase Server Implementation


Status: ACTIVE
AppliesTo: v10

> Root SSOT: `skills/devian/10-module/03-ssot/SKILL.md`

> Advertise SSOT: `skills/devian-unity/50-mobile-package/47-advertise-system/03-ssot/SKILL.md` (특히 H 섹션)


## 문서 경계 (Scope)

- 이 문서는 **AdMob Rewarded SSV Firebase 서버 구현 정본**이다.
- 포함: HTTP endpoint 구조, ECDSA 서명 검증, Firestore 감사 로그 스키마, custom_data 파싱, 멱등 규칙
- 비포함: 고정 결정값(endpoint 이름, Firestore path, custom_data 포맷) (→ `41`)
- 비포함: 클라이언트 보상 지급 흐름 (→ `03-ssot §D`, `30-ads-manager`)
- 비포함: 운영 체크리스트/테스트 시나리오 (→ `09-ssot-operations`)


## 목적

AdMob Rewarded SSV 콜백을 수신하여 **서명 검증 + 감사 로그 기록**을 수행하는 Firebase Cloud Functions HTTP endpoint의 구현 정본이다.

- 보상 지급 방식: **클라이언트 즉시 지급** (기존 흐름 유지). SSV는 감사/사후 검증 용도.
- AdMob이 광고 시청 완료 시 서버 콜백 URL로 GET 요청을 보내고, 서버는 서명 검증 후 Firestore에 기록한다.


---


## A) SSV 엔드포인트 구조 (정본)

- 타입: **HTTP function** (`onRequest`). Callable이 아니다.
  - AdMob이 외부 URL로 GET 요청을 보내므로, Firebase Callable(클라이언트 SDK 호출)이 아닌 HTTP endpoint를 사용한다.
- 소스 위치: `{repoRoot}/functions/src/ads/verifyAdReward.ts`
- export: `functions/src/index.ts`에서 export
- HTTP 메서드: **GET** (AdMob SSV 콜백은 GET 요청)

### A1) 엔드포인트 URL

- 배포 후 URL 형태: `https://{region}-{projectId}.cloudfunctions.net/verifyAdReward`
- 이 URL을 AdMob 콘솔 > 앱 설정 > Rewarded SSV 콜백 URL에 등록한다.


---


## B) ECDSA 서명 검증 절차 (정본)

### B1) 공개키 조회

- URL: `https://gstatic.com/admob/reward/verifier-keys.json`
- 응답: `{ "keys": [{ "keyId": number, "pem": string, "base64": string }, ...] }`
- 캐시: **최대 24시간**. 공개키는 정기적으로 회전된다.

### B2) 검증 흐름

1. 콜백 URL의 쿼리 파라미터에서 `signature`와 `key_id`를 추출한다.
2. `signature`와 `key_id`를 제외한 나머지 쿼리 파라미터를 알파벳 순으로 정렬하여 `&`로 연결한 문자열을 생성한다.
3. `key_id`에 해당하는 공개키를 조회한다.
4. ECDSA with SHA-256으로 서명을 검증한다.
5. 검증 성공 시 Firestore에 기록. 실패 시 에러 로깅만 수행.

### B3) 구현 방식

- Node.js `crypto` 모듈로 직접 ECDSA 검증을 구현한다. 외부 npm 패키지 불필요.
- 결정 정본: `41-ad-ssv-decisions §F`


---


## C) Firestore 스키마 — 감사 로그 (정본)

### C1) Ad Rewards Log

- Path: `/users/{uid}/adRewards/{transactionId}`

필드 (최소):
- `transactionId: string` (doc id와 동일, AdMob SSV의 `transaction_id`)
- `adUnit: string` (AdMob `ad_unit` 파라미터)
- `adNetwork: string` (AdMob `ad_network` 파라미터)
- `rewardAmount: number` (AdMob `reward_amount` 파라미터)
- `rewardItem: string` (AdMob `reward_item` 파라미터)
- `customData: string` (AdMob `custom_data` 파라미터, 원본 보존)
- `advertise_id: string` (custom_data에서 파싱)
- `reward_group_id: string` (custom_data에서 파싱)
- `verified: boolean` (서명 검증 성공 여부)
- `timestamp: number` (AdMob `timestamp` 파라미터, epoch ms)
- `verifiedAt: Timestamp` (Firestore serverTimestamp, 기록 시각)


---


## D) custom_data 포맷 (정본)

클라이언트가 `ServerSideVerificationOptions`에 설정하는 `customData` 문자열:

- 포맷: `{uid}:{advertise_id}:{reward_group_id}`
- 구분자: `:` (콜론)
- 예시: `abc123:ad_rewarded_001:reward_chest_001`

서버는 이 문자열을 파싱하여:
- `uid`로 Firestore 문서 경로의 `{uid}` 결정
- `advertise_id` / `reward_group_id`로 감사 로그 필드 기록

파싱 실패 시: 에러 로깅 + HTTP 200 응답 (재시도 방지)


---


## E) 멱등 처리 (정본)

- `transaction_id`를 Firestore 문서 ID로 사용한다.
- 동일 `transaction_id`로 재요청 시:
  - 이미 문서가 존재하면 **덮어쓰지 않는다** (중복 기록 방지)
  - HTTP 200 응답 (AdMob 재시도 중단)


---


## F) 에러 처리 (정본)

AdMob은 HTTP 200이 아닌 응답을 받으면 최대 5회 1초 간격으로 재시도한다.

규칙:
- **서명 검증 실패**: HTTP 200 반환 + 에러 로깅 (재시도해도 결과 동일)
- **custom_data 파싱 실패**: HTTP 200 반환 + 에러 로깅
- **Firestore 기록 실패**: HTTP 500 반환 (재시도 허용)
- **공개키 조회 실패**: HTTP 500 반환 (재시도 허용)


---


## 클라이언트 측 변경 (참고)

클라이언트 코드 변경이 필요하다 (구현은 `31-ad-provider-google-mobile-ads` 범위):

- Rewarded 광고 로드 후, Show 이전에 `ServerSideVerificationOptions` 설정:

```
var options = new ServerSideVerificationOptions.Builder()
    .SetUserId(uid)
    .SetCustomData($"{uid}:{advertise_id}:{reward_group_id}")
    .Build();
rewardedAd.SetServerSideVerificationOptions(options);
```


---


## DoD

Hard (must be 0)
- [ ] HTTP endpoint 구조(onRequest, GET)가 문서에 고정돼 있다.
- [ ] ECDSA 서명 검증 절차가 문서에 고정돼 있다.
- [ ] Firestore 감사 로그 스키마(path + 필드)가 문서에 고정돼 있다.
- [ ] custom_data 포맷이 문서에 고정돼 있다.
- [ ] 멱등 규칙(transaction_id 기반)이 문서에 고정돼 있다.

Soft
- [ ] Firebase 에뮬레이터에서 SSV 콜백 시뮬레이션 가이드 추가
