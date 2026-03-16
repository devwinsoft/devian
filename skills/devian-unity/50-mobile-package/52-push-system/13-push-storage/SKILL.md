# 13-push-storage

Status: ACTIVE
AppliesTo: v10

`PushStorage` 저장 모델 규약 문서다.

---

## Implementation Location (3-path mirror)

> 3-path mirror 정책: [devian-unity/04-package-policy](../../../../devian-unity/04-package-policy/SKILL.md), [devian-unity/01-policy](../../../../devian-unity/01-policy/SKILL.md) §SSOT 원칙

| 파일 | UPM (정본) | Packages (sync) | Assets/Samples (import) |
|---|---|---|---|
| `PushStorage.cs` | `upm/com.devian.foundation/Samples~/MobilePackage/Runtime/Push/` | `Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/Push/` | `Assets/Samples/Devian Foundation/{version}/MobilePackage/Runtime/Push/` |
| `LocalNotificationData.cs` | 동일 경로 | 동일 경로 | 동일 경로 |

---

## Ownership

- `PushManager`가 `PushStorage`를 소유한다.
- 정본 접근 경계: `PushManager.Storage`
- 외부 모듈은 `PushStorage`를 직접 생성/수정하지 않는다.

---

## Storage Model

필드 정의는 [03-ssot §B PushStorage](../03-ssot/SKILL.md)가 정본이다.

---

## SaveData Payload

루트 payload에서 Push 섹션은 `push` 키를 사용한다.

```json
{
  "version": 15,
  "push": {
    "schemaVersion": 1,
    "token": "fMcT0k3n...",
    "tokenUpdatedAt": "2025-06-01T12:00:00Z",
    "subscribedTopics": ["news", "event"],
    "scheduledNotifications": [
      {
        "notificationId": "daily-reward",
        "title": "보상 수령",
        "body": "일일 보상이 준비되었습니다!",
        "fireAt": "2025-06-02T09:00:00Z",
        "repeatInterval": "daily",
        "payload": "{\"type\":\"reward\"}"
      }
    ]
  }
}
```

핵심 규칙:
- push 저장 위치는 반드시 `push` 키
- `subscribedTopics`는 구독 중인 토픽 ID의 정본이다 (초기화 시 재구독 기반)
- `scheduledNotifications`는 등록된 로컬 알림의 정본이다 (중복 ID 방지)
- `token`은 디버깅/진단 용도의 로컬 캐시이며, 서버 등록에 사용하지 않는다

---

## Deserialize Rules

- `PushStorage.Clear()` 후 복원
- `schemaVersion <= 0`이면 1로 보정
- `subscribedTopics`가 null이면 빈 리스트로 초기화
- `scheduledNotifications`가 null이면 빈 리스트로 초기화
- `token`이 null/empty이면 빈 문자열 유지 (초기화 시 재획득)
- 누락 키는 안전 기본값 사용

---

## Clear() 규칙

- `schemaVersion`은 현재 버전(1)으로 리셋
- `token`, `tokenUpdatedAt`은 빈 문자열로 리셋
- `subscribedTopics`, `scheduledNotifications`는 빈 리스트로 초기화
- Clear 후 FCM 토큰/토픽 재등록은 다음 `InitializeAsync` 호출 시 수행

---

## Related

- [03-ssot §B PushStorage](../03-ssot/SKILL.md) — 필드 정의 정본
- [10-push-manager](../10-push-manager/SKILL.md) — Storage 소유자
- [01-policy](../01-policy/SKILL.md) — 모듈 경계
