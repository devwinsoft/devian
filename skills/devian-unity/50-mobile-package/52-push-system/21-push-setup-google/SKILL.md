# 21-push-setup-google — Google Push (FCM Android) Setup

Status: ACTIVE
AppliesTo: v10

## 문서 경계 (Scope)

- 이 문서는 **Android 푸시 알림 인프라를 새 프로젝트에 셋업하는 실행 가이드 정본**이다.
- 포함: Firebase 프로젝트 Android 앱 등록, google-services.json, Gradle 설정, AndroidManifest, NotificationChannel
- 비포함: PushManager 런타임 구현(→ `10`), Provider 구현 상세(→ `12`)

---

## A. 사전 요구사항 (Prerequisites)

### A1. 필요 도구

| 도구 | 용도 |
|------|------|
| Firebase Console | Android 앱 등록 + FCM 활성화 |
| Unity Editor | Android 빌드 설정 |

### A2. 필요 정보

| 항목 | 예시 | 설명 |
|------|------|------|
| `{PACKAGE_NAME}` | `com.devian.framework` | Android 패키지명 |
| `{FIREBASE_PROJECT_ID}` | `devian-framework-example` | Firebase 프로젝트 ID |

---

## B. Firebase Console — Android 앱 등록

1. [Firebase Console](https://console.firebase.google.com) → 프로젝트 → **앱 추가** → Android
2. Package name: `{PACKAGE_NAME}`
3. `google-services.json` 다운로드

---

## C. Unity 프로젝트 설정

### C1. google-services.json

1. B에서 다운로드한 `google-services.json`을 Unity 프로젝트에 배치
2. 경로: `Assets/Plugins/Android/google-services.json`
3. Unity 빌드 시 Android 프로젝트에 자동 복사됨

### C2. Firebase Unity SDK

- `Firebase Unity SDK` 패키지에서 `FirebaseMessaging.unitypackage` 임포트
- 또는 UPM: `com.google.firebase.messaging`

### C3. Custom Gradle 설정 (Unity)

Unity → Player Settings → Android → Publishing Settings:
- **Custom Main Gradle Template** 활성화
- **Custom Launcher Gradle Template** 활성화

> Firebase Unity SDK가 자동으로 google-services 플러그인과 dependencies를 주입한다.
> 수동 Gradle 수정이 필요한 경우에만 아래를 참조.

### C4. AndroidManifest — POST_NOTIFICATIONS 권한 (Android 13+)

```xml
<!-- Assets/Plugins/Android/AndroidManifest.xml -->
<uses-permission android:name="android.permission.POST_NOTIFICATIONS" />
```

> Android 12 이하에서는 이 권한이 자동 부여된다. 선언해도 무해.

### C5. NotificationChannel (Android 8+)

Android 8(API 26)부터 알림 채널이 필수다.
`PushProviderGoogle`에서 앱 시작 시 기본 채널을 생성한다.

채널 설정:

| 항목 | 값 |
|------|-----|
| Channel ID | `devian_push_default` |
| Channel Name | `Push Notifications` |
| Importance | `HIGH` |

> 채널 ID/Name은 프로젝트별로 변경 가능. Provider 내부에서 하드코딩하지 않고 설정 가능하게 구성 권장.

---

## D. FCM 토픽 발송 (Firebase Console)

> 서버 토큰 등록/타겟 발송은 사용하지 않는다. 모든 원격 푸시는 **토픽 기반 발송**만 사용한다.

### D1. FCM v1 API 활성화

1. [Google Cloud Console](https://console.cloud.google.com/apis/library/fcm.googleapis.com) → Firebase Cloud Messaging API → **사용 설정**
2. 이미 Firebase 프로젝트에서 활성화된 경우 생략

### D2. 토픽 발송 방법

- **Firebase Console**: Messaging → 새 캠페인 → 토픽 대상 지정 → 발송
- **Firebase Admin SDK** (기존 Functions에서 필요 시):

```typescript
import { getMessaging } from "firebase-admin/messaging";

await getMessaging().send({
  topic: topicId,
  notification: { title: "...", body: "..." },
  data: { /* custom payload */ }
});
```

> 토큰 기반 발송(`token: userFcmToken`)은 사용하지 않는다. Firebase Functions/Firestore에 토큰을 저장하지 않는다.

---

## E. 검증 체크리스트

| # | 항목 | 방법 | 기대 결과 |
|---|------|------|----------|
| 1 | google-services.json | Unity 빌드 성공 | 에러 없음 |
| 2 | FCM API 활성화 | GCP Console → API 목록 | Firebase Cloud Messaging API 활성 |
| 3 | 토큰 획득 | 기기에서 앱 실행, `PushManager.InitializeAsync` | FCM 토큰 로그 출력 |
| 4 | 원격 푸시 수신 | Firebase Console → Messaging → 테스트 메시지 전송 | 기기에서 알림 수신 |
| 5 | 토픽 푸시 수신 | 토픽 구독 후 토픽 발송 | 기기에서 알림 수신 |
| 6 | POST_NOTIFICATIONS | Android 13 기기에서 권한 팝업 | 권한 요청 다이얼로그 표시 |

---

## F. 트러블슈팅

### F1. 토큰 획득 실패

원인: `google-services.json` 누락 또는 패키지명 불일치
해결: B에서 올바른 패키지명으로 재다운로드

### F2. 알림 미수신 (Android 13+)

원인: `POST_NOTIFICATIONS` 런타임 권한 미요청 또는 거부
해결: `PushProviderGoogle`에서 권한 요청 로직 확인 (→ `12`)

### F3. 알림 미표시 (Android 8+)

원인: NotificationChannel 미생성
해결: 앱 시작 시 `devian_push_default` 채널 생성 확인 (→ `12`)

### F4. FCM v1 API 비활성

원인: 레거시 서버 키 사용 또는 API 미활성화
해결: D1 실행. FCM v1 HTTP API만 사용한다 (레거시 서버 키 금지).

---

## Related

- [12-push-provider-google](../12-push-provider-google/SKILL.md) — 런타임 Provider 구현
- [10-push-manager](../10-push-manager/SKILL.md) — PushManager 설계
- [03-ssot](../03-ssot/SKILL.md) — Push 시스템 SSOT
