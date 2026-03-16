# 52-push-system — Policy

Status: ACTIVE
AppliesTo: v10
Type: Policy / Entry Point

## Purpose

푸시 시스템(`PushManager`)의 공개 경계와 하드룰을 정의한다.

---

## Hard Rules

### 1) PushManager가 단일 진입점이다

- 외부 모듈은 `PushManager` API만 호출한다.
- 플랫폼 SDK(`FirebaseMessaging`, `UNUserNotificationCenter` 등)를 직접 호출하지 않는다.
- Provider는 PushManager 내부에서만 호출된다.

### 2) 토큰은 로컬 캐시만 한다 (서버 등록 없음)

- FCM 토큰 획득/캐시는 `PushManager`만 수행한다.
- 토큰 값을 외부에 직접 노출하지 않는다.
- 서버에 토큰을 등록하지 않는다 — 모든 원격 푸시는 토픽 기반 발송만 사용한다.
- FCM 클라이언트 SDK가 토픽 구독 시 토큰을 내부적으로 사용한다.

### 3) 토픽 구독/해제는 내부 토픽 ID를 사용한다

- 공개 API는 내부 `topicId`만 받는다.
- 플랫폼별 토픽 이름 변환은 Provider 내부에서 수행한다.

### 4) 로컬 알림은 PushManager가 스케줄링한다

- 로컬 알림 등록/취소는 `PushManager` API만 사용한다.
- 플랫폼별 알림 구현(`UNUserNotificationCenter`, `AlarmManager`/`NotificationChannel`)은 Provider가 담당한다.
- 알림 ID 관리는 `PushStorage`가 정본이다.

### 5) Initialize는 명시적 호출이다

- `InitializeAsync(ct)`는 명시적으로 호출한다.
- 초기화 전 토큰/토픽/로컬알림 API 호출은 실패를 반환한다.

### 6) 권한 요청은 PushManager가 orchestration 한다

- 푸시 알림 권한(iOS provisional/full, Android POST_NOTIFICATIONS)은 `PushManager`가 요청한다.
- 권한 거부 시 토큰 등록을 스킵하고 실패를 반환한다.

### 7) 플랫폼 안전 실패

- 지원하지 않는 플랫폼(Editor 등)에서는 즉시 안전 실패를 반환한다.
- Provider 미설정 시에도 안전 실패.

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [10-push-manager](../10-push-manager/SKILL.md)
- [11-push-provider-apple](../11-push-provider-apple/SKILL.md)
- [12-push-provider-google](../12-push-provider-google/SKILL.md)
