# 35-ad-format-app-open


App Open 광고 포맷 규약을 정의한다.


---


## Rules

- `Format=APP_OPEN`
- `Reward_group_id`는 비워둔다
- cold start / resume 진입 지점에서만 사용한다
- gameplay 도중 임의 표시하지 않는다
- NoAds 활성 시 표시하지 않는다


---


## AdsManager 기대 동작

- foreground 진입 직후 gating 검사
- 최근 표시 이력과 `Cooldown_sec` 반영
- 표시 실패는 non-fatal
