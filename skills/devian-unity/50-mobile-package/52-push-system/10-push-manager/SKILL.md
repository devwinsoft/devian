# 10-push-manager

Status: ACTIVE
AppliesTo: v10

MobilePackage 샘플의 `PushManager` 설계 문서다.

---

## 문서 경계 (Scope)

- 이 문서는 **PushManager 클라이언트 샘플 코드의 위치/흐름/규약**을 설명한다.
- Provider 구현 상세는 `11`(Apple), `12`(Google) 문서를 참조한다.

PushManager는 **단일 concrete 클래스**이다.
플랫폼별 분기는 `IPushPlatformProvider` 인터페이스를 통해 위임한다.

---

## Implementation Location (3-path mirror)

> 3-path mirror 정책: [devian-unity/04-package-policy](../../../../devian-unity/04-package-policy/SKILL.md), [devian-unity/01-policy](../../../../devian-unity/01-policy/SKILL.md) §SSOT 원칙

| 파일 | UPM (정본) | Packages (sync) | Assets/Samples (import) |
|---|---|---|---|
| `PushManager.cs` | `upm/com.devian.foundation/Samples~/MobilePackage/Runtime/Push/` | `Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/Push/` | `Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/Push/` |
| `IPushPlatformProvider.cs` | 동일 경로 | 동일 경로 | 동일 경로 |
| `PushStorage.cs` | 동일 경로 | 동일 경로 | 동일 경로 |
| `LocalNotificationData.cs` | 동일 경로 | 동일 경로 | 동일 경로 |
| `PushProviderApple.cs` | 동일 경로 | 동일 경로 | 동일 경로 |
| `PushProviderGoogle.cs` | 동일 경로 | 동일 경로 | 동일 경로 |
| `PushProviderUnsupported.cs` | 동일 경로 | 동일 경로 | 동일 경로 |

- asmdef:
  - `Devian.Samples.MobilePackage` (`Samples~/MobilePackage/Runtime/Devian.Samples.MobilePackage.asmdef`)

---

## Singleton

```csharp
CompoSingleton<PushManager>.Instance
```

- Registry key: `PushManager`
- 다른 매니저에서 접근: `Singleton.Get<PushManager>()`

---

## Public API (Sample)

- `InitializeAsync(ct)` → `Task<CommonResult>`
  - 권한 요청 → 토큰 획득 → 저장 토픽 재구독
  - Idempotent: 여러 번 호출해도 동일 Task 반환
  - Editor에서는 즉시 `PUSH_UNSUPPORTED_PLATFORM` 반환
- `SubscribeTopicAsync(topicId, ct)` → `Task<CommonResult>`
  - 토픽 구독 + `PushStorage.subscribedTopics` 동기화
- `UnsubscribeTopicAsync(topicId, ct)` → `Task<CommonResult>`
  - 토픽 해제 + `PushStorage.subscribedTopics` 동기화
- `ScheduleLocalNotificationAsync(data, ct)` → `Task<CommonResult>`
  - 로컬 알림 등록 + `PushStorage.scheduledNotifications` 동기화
- `CancelLocalNotificationAsync(notificationId, ct)` → `Task<CommonResult>`
  - 로컬 알림 취소 + `PushStorage.scheduledNotifications`에서 제거
- `CancelAllLocalNotificationsAsync(ct)` → `Task<CommonResult>`
  - 모든 로컬 알림 취소 + `PushStorage.scheduledNotifications` 초기화
- `ClearStorage()`
  - 저장 데이터 초기화

Events:
- `OnPermissionResult(bool granted)` — 권한 요청 결과

---

## Internal Responsibilities

- 플랫폼 분기: `#if UNITY_IOS` / `#elif UNITY_ANDROID` / `else` → Provider 인스턴스 선택
- 토큰 획득: 초기화 시 `GetTokenAsync`로 FCM 토큰 획득, `PushStorage.token`에 저장 (로컬 캐시 용도)
- 토픽 복원: 초기화 시 `PushStorage.subscribedTopics` 기반 재구독
- 로컬 알림 Storage 동기화: 등록/취소마다 `PushStorage` 갱신

> **서버 의존 없음**: 토큰 서버 등록/타겟 발송은 사용하지 않는다. 모든 원격 푸시는 토픽 기반 발송만 사용한다. FCM 클라이언트 SDK가 토픽 구독/해제를 직접 처리한다.

---

## Hard Rules (Sample must follow)

- 플랫폼 SDK 직접 호출 금지 — `IPushPlatformProvider`를 통해서만 접근
- 토큰 값을 외부(다른 매니저)에 직접 노출하지 않는다
- 초기화 전 API 호출은 `PUSH_NOT_INITIALIZED` 반환
- 권한 미획득 시 토큰 등록을 스킵하고 `PUSH_PERMISSION_DENIED` 반환
- 로컬 알림 ID는 `PushStorage`가 정본이다 (중복 등록 방지)

---

## Related SSOT

- `skills/devian-unity/50-mobile-package/52-push-system/03-ssot/SKILL.md`
- `skills/devian-unity/50-mobile-package/52-push-system/01-policy/SKILL.md`
