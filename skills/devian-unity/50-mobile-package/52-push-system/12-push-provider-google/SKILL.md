# 12-push-provider-google — FCM

Status: ACTIVE
AppliesTo: v10

## 범위

- Android(FCM) 푸시 토큰 획득
- Android 푸시 알림 권한 요청 (Android 13+ POST_NOTIFICATIONS)
- Android 토픽 구독/해제 (Firebase Messaging Android SDK)
- Android 로컬 알림 스케줄/취소 (`AlarmManager` + `NotificationChannel`)

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

### Token

- `Firebase.Messaging.FirebaseMessaging.GetTokenAsync()`로 FCM 토큰 획득

### Topic

- `Firebase.Messaging.FirebaseMessaging.SubscribeAsync(topicId)`
- `Firebase.Messaging.FirebaseMessaging.UnsubscribeAsync(topicId)`

### Local Notification

- `NotificationChannel` 생성 (Android 8+, 앱 시작 시 1회)
- `AlarmManager.SetExactAndAllowWhileIdle` / `SetRepeating`으로 스케줄
- `AlarmManager.Cancel`로 취소
- `NotificationCompat.Builder`로 알림 빌드

---

## Related

- [10-push-manager](../10-push-manager/SKILL.md)
- [03-ssot](../03-ssot/SKILL.md)
