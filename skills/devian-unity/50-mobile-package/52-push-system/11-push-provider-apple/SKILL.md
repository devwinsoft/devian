# 11-push-provider-apple — APNs

Status: ACTIVE
AppliesTo: v10

## 범위

- iOS(APNs) 푸시 토큰 획득
- iOS 푸시 알림 권한 요청
- iOS 토픽 구독/해제 (Firebase Messaging iOS SDK 경유)
- iOS 로컬 알림 스케줄/취소 (Unity Mobile Notifications 패키지)

---

## 정책

- iOS 런타임 외에는 안전 실패
- `#if UNITY_IOS` 컴파일 가드 사용
- 상위 API에는 Apple SDK 타입을 노출하지 않는다
- 권한 요청은 provisional → full 순서를 지원한다

---

## 동작

### Permission

- `UNUserNotificationCenter.RequestAuthorization`으로 권한 요청
- provisional 권한 지원 (iOS 12+): 최초에는 provisional, 사용자 인터랙션 시 full 요청

### Token

- `Firebase.Messaging.FirebaseMessaging.GetTokenAsync()`로 FCM 토큰 획득
- APNs 토큰은 Firebase SDK가 내부에서 매핑

### Topic

- `Firebase.Messaging.FirebaseMessaging.SubscribeAsync(topicId)`
- `Firebase.Messaging.FirebaseMessaging.UnsubscribeAsync(topicId)`

### Local Notification

- **Unity Mobile Notifications** 패키지(`com.unity.mobile.notifications`) 사용
- `iOSNotificationCenter.ScheduleNotification(notification)`로 스케줄
- `iOSNotificationCenter.RemoveScheduledNotification(notificationId)`로 개별 취소
- `iOSNotificationCenter.RemoveAllScheduledNotifications()`로 전체 취소
- `iOSNotificationTimeIntervalTrigger`로 발화 시각 지정

### Icon 정책

- iOS는 시스템이 앱 아이콘을 강제 사용한다. 커스텀 아이콘 설정 불가.
- 코드에서 별도 아이콘 설정을 하지 않는다.

---

## Related

- [10-push-manager](../10-push-manager/SKILL.md)
- [03-ssot](../03-ssot/SKILL.md)
