# 20-push-setup-apple — Apple Push (APNs + FCM iOS) Setup

Status: ACTIVE
AppliesTo: v10

## 문서 경계 (Scope)

- 이 문서는 **iOS 푸시 알림 인프라를 새 프로젝트에 셋업하는 실행 가이드 정본**이다.
- 포함: APNs Key 생성, Firebase 프로젝트 iOS 앱 등록, Xcode Capability, GoogleService-Info.plist, CocoaPods/SPM 설정
- 비포함: PushManager 런타임 구현(→ `10`), Provider 구현 상세(→ `11`)

---

## A. 사전 요구사항 (Prerequisites)

### A1. 필요 도구

| 도구 | 용도 |
|------|------|
| Xcode | iOS 빌드 + Capability 설정 |
| Apple Developer Account | APNs Key 생성 |
| Firebase Console | iOS 앱 등록 + APNs Key 업로드 |

### A2. 필요 정보

| 항목 | 예시 | 설명 |
|------|------|------|
| `{BUNDLE_ID}` | `com.devian.framework` | iOS 번들 ID |
| `{FIREBASE_PROJECT_ID}` | `devian-framework-example` | Firebase 프로젝트 ID |
| `{TEAM_ID}` | `ABC123DEF4` | Apple Developer Team ID |
| `{APNS_KEY_ID}` | `XYZ789` | APNs Auth Key ID |

---

## B. Apple Developer — APNs Key 생성

1. [Apple Developer](https://developer.apple.com/account/resources/authkeys/list) → Keys → **+** 버튼
2. Key Name: `FCM APNs Key` (자유)
3. **Apple Push Notifications service (APNs)** 체크
4. **Continue** → **Register**
5. `.p8` 파일 다운로드 (1회만 가능, 안전 보관)
6. **Key ID** 기록 → `{APNS_KEY_ID}`

> APNs Key는 Certificate 방식이 아닌 **Token 기반(.p8)** 을 사용한다. Certificate는 만료 관리가 필요하므로 금지.

---

## C. Firebase Console — iOS 앱 등록 + APNs Key 업로드

### C1. iOS 앱 등록

1. [Firebase Console](https://console.firebase.google.com) → 프로젝트 → **앱 추가** → iOS
2. Bundle ID: `{BUNDLE_ID}`
3. `GoogleService-Info.plist` 다운로드

### C2. APNs Key 업로드

1. Firebase Console → 프로젝트 설정 → **Cloud Messaging** 탭
2. iOS 앱 섹션 → **APNs Authentication Key** → **Upload**
3. `.p8` 파일 업로드
4. Key ID: `{APNS_KEY_ID}`
5. Team ID: `{TEAM_ID}`

---

## D. Xcode 프로젝트 설정

### D1. Capability 추가

1. Xcode → Target → **Signing & Capabilities**
2. **+ Capability** → **Push Notifications** 추가
3. **+ Capability** → **Background Modes** 추가 → **Remote notifications** 체크

### D2. GoogleService-Info.plist

1. C1에서 다운로드한 `GoogleService-Info.plist`를 Unity 프로젝트에 배치
2. 경로: `Assets/Plugins/iOS/GoogleService-Info.plist`
3. Unity 빌드 시 Xcode 프로젝트에 자동 복사됨

### D3. Unity iOS 빌드 후처리 (PostProcessBuild)

- `Push Notifications` Capability는 Unity 에디터에서 직접 추가할 수 없음
- `PostProcessBuild` 스크립트로 자동 추가:

```csharp
// Editor/PushNotificationPostProcess.cs
#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace Devian
{
    public static class PushNotificationPostProcess
    {
        [PostProcessBuild(100)]
        public static void OnPostProcessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;

            var projPath = PBXProject.GetPBXProjectPath(path);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);

            var mainTarget = proj.GetUnityMainTargetGuid();
            var manager = new ProjectCapabilityManager(
                projPath, "Entitlements.entitlements", null, mainTarget);

            manager.AddPushNotifications(false);
            manager.AddBackgroundModes(BackgroundModesOptions.RemoteNotifications);
            manager.WriteToFile();
        }
    }
}
#endif
```

---

## E. Firebase iOS SDK (Unity)

- `Firebase Unity SDK` 패키지에서 `FirebaseMessaging.unitypackage` 임포트
- 또는 UPM: `com.google.firebase.messaging`

---

## F. 검증 체크리스트

| # | 항목 | 방법 | 기대 결과 |
|---|------|------|----------|
| 1 | APNs Key 등록 | Firebase Console → Cloud Messaging | Key ID 표시 |
| 2 | GoogleService-Info.plist | Xcode 프로젝트 내 파일 존재 | 빌드 성공 |
| 3 | Push Capability | Xcode → Signing & Capabilities | Push Notifications 존재 |
| 4 | Background Modes | Xcode → Signing & Capabilities | Remote notifications 체크 |
| 5 | 토큰 획득 | 기기에서 앱 실행, `PushManager.InitializeAsync` | FCM 토큰 로그 출력 |
| 6 | 원격 푸시 수신 | Firebase Console → Messaging → 테스트 메시지 전송 | 기기에서 알림 수신 |

---

## G. 트러블슈팅

### G1. 토큰 획득 실패

원인: APNs Key 미업로드 또는 Push Capability 미설정
해결: C2 + D1 재확인

### G2. 원격 푸시 미수신 (foreground)

원인: iOS는 기본적으로 foreground에서 알림 배너를 표시하지 않음
해결: `UNUserNotificationCenter.willPresentNotification`에서 표시 옵션 설정 필요 (Provider 책임)

### G3. Provisional 권한 후 알림 미표시

원인: Provisional은 알림 센터에만 조용히 전달됨
해결: 정상 동작. Full 권한 요청은 사용자 인터랙션 후 수행

---

## Related

- [11-push-provider-apple](../11-push-provider-apple/SKILL.md) — 런타임 Provider 구현
- [10-push-manager](../10-push-manager/SKILL.md) — PushManager 설계
- [03-ssot](../03-ssot/SKILL.md) — Push 시스템 SSOT
