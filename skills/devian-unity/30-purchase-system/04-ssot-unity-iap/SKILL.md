# 04-ssot-unity-iap — Unity IAP


Status: ACTIVE
AppliesTo: v10


## 전제


- 결제 SDK는 Unity **In-App Purchasing (Unity IAP)**를 사용한다.
- 스토어별 구현 차이는 존재하지만, 최종 지급/상태는 서버 검증 결과를 따른다.


---


## 플랫폼 차이 (SSOT)


### iOS: Restore(복원)


- iOS는 재설치/기기 변경 시 Restore 플로우가 필요하다.
- Restore는 "스토어 구매 이력 재동기화 트리거"이며,
  최종 Entitlement는 서버 `getEntitlements` 결과로 확정한다.


### Android: 복원 UX


- Android는 보통 Restore 버튼을 직접 노출하지 않고,
  앱 시작/로그인 시 서버 상태 동기화로 복원을 처리해도 된다.


---


## Pending / Deferred 상태


- iOS: 승인 대기(deferred) 상태가 발생할 수 있다.
- Android: pending 상태가 발생할 수 있다.
- 공통 규칙: **PENDING/DEFERRED 상태에서는 지급 금지** (서버 검증 확정 전 지급 금지)


정본: [06-ssot-verify-idempotency](../06-ssot-verify-idempotency/SKILL.md)
