# 03-ssot — 52-push-system

Status: ACTIVE
AppliesTo: v10

## SSOT 범위

이 문서는 Push 시스템의 정본이다.

- 토큰 저장 구조: `PushStorage`
- 토픽 목록/구독 상태
- 로컬 알림 스케줄 스키마
- Provider 인터페이스 계약: `IPushPlatformProvider`

---

## A. Provider Interface

```csharp
public interface IPushPlatformProvider
{
    Task<CommonResult> RequestPermissionAsync(CancellationToken ct);
    Task<CommonResult<string>> GetTokenAsync(CancellationToken ct);
    Task<CommonResult> SubscribeTopicAsync(string topicId, CancellationToken ct);
    Task<CommonResult> UnsubscribeTopicAsync(string topicId, CancellationToken ct);
    Task<CommonResult> ScheduleLocalNotificationAsync(LocalNotificationData data, CancellationToken ct);
    Task<CommonResult> CancelLocalNotificationAsync(string notificationId, CancellationToken ct);
    Task<CommonResult> CancelAllLocalNotificationsAsync(CancellationToken ct);
}
```

- `#if UNITY_IOS` → `PushProviderApple`
- `#elif UNITY_ANDROID` → `PushProviderGoogle`
- `else` → `PushProviderUnsupported` (즉시 `PUSH_UNSUPPORTED_PLATFORM` 반환)

---

## B. PushStorage

`PushStorage`는 `PushManager`가 소유하며, SaveData 로컬/클라우드 저장 대상이다.

저장 필드:

| field | type | note |
|-------|------|------|
| `schemaVersion` | int | 스키마 버전 |
| `token` | string | 마지막 등록 성공 토큰 |
| `tokenUpdatedAt` | string | 토큰 갱신 시각 (ISO 8601) |
| `subscribedTopics` | `List<string>` | 구독 중인 토픽 ID 목록 |
| `scheduledNotifications` | `List<ScheduledNotificationEntry>` | 등록된 로컬 알림 목록 |

`ScheduledNotificationEntry`:

| field | type | note |
|-------|------|------|
| `notificationId` | string | 고유 알림 ID |
| `title` | string | 알림 제목 |
| `body` | string | 알림 본문 |
| `fireAt` | string | 발화 시각 (ISO 8601) |
| `repeatInterval` | string | 반복 주기 (none/daily/weekly) |
| `payload` | string | 커스텀 페이로드 (JSON) |

---

## C. LocalNotificationData (DTO)

```csharp
public class LocalNotificationData
{
    public string NotificationId;
    public string Title;
    public string Body;
    public DateTime FireAt;
    public RepeatInterval Repeat;   // None, Daily, Weekly
    public string Payload;          // custom JSON string
}
```

---

## D. Topic Rules

- FCM 토픽 ID = `PUSH_REMOTE.PushId` (테이블 pk). 예: `"event_korean"`, `"test_english"`.
- 구독/해제 성공 시 `PushStorage.subscribedTopics`를 즉시 동기화한다.
- 초기화 시 `PushStorage.subscribedTopics`를 기반으로 재구독을 시도한다.

---

## E. Token Rules

- 초기화 시 `GetTokenAsync`로 FCM 토큰을 획득한다.
- 획득한 토큰은 `PushStorage.token`에 로컬 캐시한다 (디버깅/진단 용도).
- 서버 토큰 등록은 하지 않는다 — 모든 원격 푸시는 토픽 기반 발송만 사용한다.
- FCM 토큰은 토픽 구독/해제의 내부 식별자로 FCM 클라이언트 SDK가 자동 사용한다.

---

## F. Table-driven Topic Subscription

- `InitializeAsync` 성공 시, `TB_PUSH_REMOTE` 테이블에서 `MobileApplication.Instance.DefaultLanguage`에 해당하는 토픽을 자동 구독한다.
- `TB_PUSH_REMOTE`의 GroupKey = `Language` (OperationTable.xlsx PUSH_REMOTE 시트, Operation 도메인, `group:true`).
- `TB_PUSH_REMOTE.GetByGroup(DefaultLanguage.ToString())` → 반환된 행의 `PushId`를 FCM 토픽으로 구독한다.
- **IsTest 필터링:**
  - `Debug.isDebugBuild == true` (Development Build): 모든 행 구독 (IsTest 무관)
  - `Debug.isDebugBuild == false` (Release Build): `IsTest == false` 인 행만 구독
- 이미 `PushStorage.subscribedTopics`에 존재하는 토픽은 중복 구독하지 않는다.
- `TB_PUSH_REMOTE`가 로드되지 않았으면 (`TB_PUSH_REMOTE.IsLoaded == false`) skip한다.

---

## G. Table-driven Local Notification

- `ScheduleLocalNotificationAsync(string pushId, CDateTime fireAt, ct)` — pushId로 `TB_PUSH_LOCAL` 조회 후 로컬 알림 등록.
- `TB_PUSH_LOCAL`은 OperationTable.xlsx PUSH_LOCAL 시트, Operation 도메인. group 없음.
- 테이블 스키마:

| 컬럼 | 타입 | 옵션 | 설명 |
|------|------|------|------|
| `PushId` | string | pk | 알림 고유 ID (= `LocalNotificationData.NotificationId`) |
| `TitleTextId` | string | | `ST_TEXT.Get(TitleTextId)` → 로컬라이즈 제목 |
| `BodyTextId` | string | | `ST_TEXT.Get(BodyTextId)` → 로컬라이즈 본문 |
| `IsTest` | bool | | dev/release 필터 |

- **외부에서 `LocalNotificationData`를 직접 전달하지 않는다.** pushId를 받아 테이블에서 조회한다.
- `FireAt`은 호출자가 `CDateTime`(서버 시간 기준)으로 전달한다. 테이블 컬럼이 아니다.
- `Repeat`은 `RepeatInterval.None` 고정. 테이블 컬럼이 아니다.
- **IsTest 필터링:** §F와 동일 규칙. `!Debug.isDebugBuild && row.IsTest` → `PUSH_TEST_BLOCKED` 반환.
- `TB_PUSH_LOCAL`이 로드되지 않았으면 `PUSH_TABLE_NOT_LOADED` 반환.
- pushId 조회 실패 시 `PUSH_LOCAL_NOT_FOUND` 반환.
- 중복 notificationId 시 `PUSH_DUPLICATE_NOTIFICATION` 반환.

---

## H. 초기화 시 Local Push Clear

- `InitializeAsync` 시작 시(권한 요청 전) 기존 로컬 알림을 모두 취소한다.
- `_provider.CancelAllLocalNotificationsAsync(ct)` 호출 + `_storage.scheduledNotifications.Clear()`.
- 앱 재시작마다 깨끗한 상태에서 시작하기 위한 정책이다.

---

## Related

- [10-push-manager](../10-push-manager/SKILL.md)
- [11-push-provider-apple](../11-push-provider-apple/SKILL.md)
- [12-push-provider-google](../12-push-provider-google/SKILL.md)
