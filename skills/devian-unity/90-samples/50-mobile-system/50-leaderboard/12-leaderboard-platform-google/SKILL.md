# 12-leaderboard-platform-google — GPGS v2 (설계)


Status: ACTIVE
AppliesTo: v10


## 범위

- Android(GPGS v2) 기반:
  - Leaderboard 점수 보고
  - Achievements(업적) 달성 보고
  - (선택) 플랫폼 UI 표시


---


## 핵심 정책


- **GPGS v2 전용**(연관 스킬과 동일 정책):
  - `Google.Play.Games` 어셈블리 기반
  - 플러그인 미설치 환경에서도 컴파일 가능해야 한다(Reflection 기반 설계)
- Android 런타임 외에서는 안전 실패(미지원)로 처리한다.
- 상위 로직은 내부 ID만 사용하고, Google 플랫폼 ID는 SSOT 매핑으로만 취급한다.

정본: [03-ssot](../03-ssot/SKILL.md)
연관: [36-account-login-gpgs](../../20-account-system/36-account-login-gpgs/SKILL.md)


---


## 인증(설계)


- Report/Unlock/Sync 전제: GPGS Sign-in 완료
- (구현 단계) 인증 실패 시 Report/Unlock/Sync는 실패 결과로 반환한다.
- Sign-in 흐름은 `AccountLoginGpgs`와 결합한다(중복 구현 금지).


---


## 기능(설계)


### 1) Report Score

- 입력: `leaderboardId`, `score`
- 처리:
  - SSOT 매핑으로 `googleLeaderboardId` 변환
  - GPGS에 점수 보고


### 2) Unlock Achievement

- 입력: `achievementId`
- 처리:
  - SSOT 매핑으로 `googleAchievementId` 변환
  - 업적 달성/진행도 보고(kind에 따라 구현 방식이 달라질 수 있음)


### 3) Sync Achievements

- 플랫폼 업적 상태를 읽어 "신규 달성"만 판별한다.
- 이벤트/중복 방지 규칙은 [09-ssot-operations](../09-ssot-operations/SKILL.md)를 따른다.


---


## NEEDS CHECK (구현 단계)

- GPGS v2에서 "업적 상태 조회(Sync)"에 사용할 API 표면 확정
- (선택) 리더보드/업적 UI를 제공할지 여부(제품 요구에 따름)
