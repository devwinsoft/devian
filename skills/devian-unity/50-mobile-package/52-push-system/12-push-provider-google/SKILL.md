# 12-push-provider-google — FCM

Status: ACTIVE
AppliesTo: v10

## 범위

- Android(FCM) 푸시 토큰 획득
- Android 푸시 알림 권한 요청 (Android 13+ POST_NOTIFICATIONS)
- Android 토픽 구독/해제 (Firebase Messaging Android SDK)
- Android 로컬 알림 스케줄/취소 (Unity Mobile Notifications 패키지)

---

## 정책

- Android 런타임 외에는 안전 실패
- `#elif UNITY_ANDROID` 컴파일 가드 사용
- 상위 API에는 Android SDK 타입을 노출하지 않는다
- Android 13+(API 33) `POST_NOTIFICATIONS` 런타임 권한 요청 필수

---

## 동작

### Permission

- Android 13+: `Permission.RequestUserPermission("android.permission.POST_NOTIFICATIONS")`
- Android 12 이하: 권한 요청 불필요 (즉시 granted)
- 권한 획득 후 `ensureNotificationChannel()` 호출

### Token

- `Firebase.Messaging.FirebaseMessaging.GetTokenAsync()`로 FCM 토큰 획득

### Topic

- `Firebase.Messaging.FirebaseMessaging.SubscribeAsync(topicId)`
- `Firebase.Messaging.FirebaseMessaging.UnsubscribeAsync(topicId)`

### Local Notification

- **Unity Mobile Notifications** 패키지(`com.unity.mobile.notifications`) 사용
- `AndroidNotificationChannel` 생성 (Id: `"devian_push_default"`, Importance: High, 앱 시작 시 1회)
- `AndroidNotificationCenter.SendNotification(notification, channelId)`로 스케줄
- `AndroidNotificationCenter.CancelAllNotifications()`로 전체 취소

### Icon 정책

- 모든 로컬 알림에 **단일 기본 아이콘**을 사용한다.
- `DefaultSmallIcon = "icon_0"` — 상태바/알림 헤더 표시용 (모노크롬 drawable).
- `DefaultLargeIcon = "icon_1"` — 알림 확장 시 우측 큰 이미지 (풀컬러 drawable).
- `AndroidNotification.SmallIcon = DefaultSmallIcon`, `AndroidNotification.LargeIcon = DefaultLargeIcon` 설정.
- 두 아이콘 모두 Unity Mobile Notifications 패키지의 **NotificationsSettings.asset**에 등록 필요.
- 설정 경로: Unity Editor → Project Settings → Mobile Notifications → Android → Icons → Add
  - Identifier: `icon_0`, Type: Small
  - Identifier: `icon_1`, Type: Large

---

## Related

- [10-push-manager](../10-push-manager/SKILL.md)
- [03-ssot](../03-ssot/SKILL.md)
