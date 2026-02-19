# 11-leaderboard-platform-apple — Game Center (설계)


Status: ACTIVE
AppliesTo: v10


## 범위

- iOS(Game Center) 기반:
  - Leaderboard 점수 보고
  - Achievements(업적) 달성 보고
  - (선택) 플랫폼 UI 표시


---


## 핵심 정책


- iOS 런타임 외에서는 안전 실패(미지원)로 처리한다.
- 상위 로직은 내부 ID만 사용하고, Apple 플랫폼 ID는 SSOT 매핑으로만 취급한다.

정본: [03-ssot](../03-ssot/SKILL.md)


---


## 인증(설계)


- Game Center 기능은 "인증된 사용자" 전제가 필요하다.
- (구현 단계) 인증 실패 시 Report/Unlock/Sync는 실패 결과로 반환한다.
- 인증은 별도 로그인(Account) 흐름과 결합될 수 있다(제품 요구에 따라 결정).

연관: [34-account-login-apple](../../20-account-system/34-account-login-apple/SKILL.md)


---


## 기능(설계)


### 1) Report Score

- 입력: `leaderboardId`, `score`
- 처리:
  - SSOT 매핑으로 `appleLeaderboardId` 변환
  - Game Center에 점수 보고


### 2) Unlock Achievement

- 입력: `achievementId`
- 처리:
  - SSOT 매핑으로 `appleAchievementId` 변환
  - 업적 달성 보고
- 성공 시 Reward 시스템이 소비할 수 있는 이벤트 신호가 발생할 수 있다(정본 규칙은 Manager/Operations 문서 참조).


### 3) Sync Achievements

- 플랫폼의 업적 상태를 읽어 "신규 달성"만 판별한다.
- 이벤트/중복 방지 규칙은 [09-ssot-operations](../09-ssot-operations/SKILL.md)를 따른다.
