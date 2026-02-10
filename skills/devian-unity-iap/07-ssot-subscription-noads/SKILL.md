# 07-ssot-subscription-noads — Subscription NoAds (SSOT)


Status: ACTIVE
AppliesTo: v10


## SSOT 원칙


- NoAds는 "구독 Active 상태"로만 판정한다.
- 클라이언트 Unity IAP 콜백만으로 NoAds를 영구 적용하지 않는다.
- 최종 상태는 `getEntitlements` 결과로 확정한다.


정본(검증/저장): [06-ssot-verify-idempotency](../06-ssot-verify-idempotency/SKILL.md)


---


## 상태 갱신


### iOS


- 서버 알림(Apple Server Notifications)을 수신하여 구독 상태를 갱신하는 구성을 권장한다.
- 알림 기반 갱신이 불가할 경우, scheduled recheck로 보조할 수 있다.


### Android


- 운영 초기에는 scheduled recheck로 시작할 수 있다(선택).
- 최종 목표는 서버에서 "현재 유효 구독"을 신뢰 가능한 방식으로 유지하는 것이다.


---


## 클라이언트 적용 규칙


- 앱 시작/포그라운드/로그인 시 `getEntitlements`로 NoAds 상태를 갱신한다.
- NoAds는 광고 표시 로직의 단일 입력값으로 사용한다(여러 군데 중복 판정 금지).
