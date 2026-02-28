# 32-ad-format-banner


Banner 광고 포맷 규약을 정의한다.


---


## Rules

- `Format=BANNER`
- `RewardGroupId`는 비워둔다
- show/hide 가능한 지속형 placement로 취급한다
- NoAds 활성 시 표시하지 않는다
- safe area / UI overlay 충돌을 고려해 위치를 제어한다


---


## AdManager 기대 동작

- `PreloadAsync(advertiseId, ct)`로 준비 가능
- `ShowAsync(advertiseId, ct)`는 banner 표시로 해석할 수 있다
- `HideBanner(advertiseId)` 또는 동등 API로 숨김 처리
- banner load fail은 non-fatal
