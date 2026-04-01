# 33-ad-format-interstitial


Interstitial 광고 포맷 규약을 정의한다.


---


## Rules

- `Format=INTERSTITIAL`
- `Reward_group_id`는 비워둔다
- gameplay safe point에서만 표시한다
- `Cooldown_sec`을 사용해 동일 placement 반복 노출을 제한한다
- NoAds 활성 시 표시하지 않는다


---


## AdsManager 기대 동작

- 표시 전 readiness 확인
- show fail / no fill은 non-fatal
- 닫힘 후 재노출은 cooldown과 preload 상태를 다시 따른다
