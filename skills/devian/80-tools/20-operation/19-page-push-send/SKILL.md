# 20-operation/19-page-push-send — Push Send 탭


Status: ACTIVE
AppliesTo: v10


## UI

```
Pipeline: PUSH_REMOTE.json → Firebase Function → FCM topic send

┌──────────────────────────────────────────────────┐
│  [Load Data]  ← PUSH_REMOTE.json 파일 선택       │
│  Status: "Loaded 4 entries (2 topics)"           │
├──────────────────────────────────────────────────┤
│  Topic: [▼ event / test / ...]                   │
├──────────────────────────────────────────────────┤
│  Korean   [body 입력 텍스트]                      │
│  English  [body 입력 텍스트]                      │
│  Japanese [body 입력 텍스트]                      │
├──────────────────────────────────────────────────┤
│                                     [Send]        │
├──────────────────────────────────────────────────┤
│  Result:                                          │
│    event_korean: ✓ sent                           │
│    event_english: ✓ sent                          │
│    event_japanese: ✗ no matching entry            │
└──────────────────────────────────────────────────┘
```


## 동작 상세

### [Load Data]

- 파일 picker로 `PUSH_REMOTE.json` (NDJSON) 선택
- 한 줄씩 `JSON.parse` → `PushRemoteEntry[]` 배열 구성
- 고유 `Topic` 목록 추출 → 드롭다운 옵션 갱신
- 상태 텍스트: 로드된 entry 수, topic 수 표시

### Topic 드롭다운

- Load Data 후 활성화
- 고유 Topic 값 (예: `event`, `test`) 목록

### Body 입력

- Korean / English / Japanese 3개 텍스트 입력
- 빈 값은 발송 시 skip

### [Send]

- 선택된 Topic으로 PUSH_REMOTE 행 필터링
- 각 행의 Language와 Body 입력을 매칭:
  - `event_korean` (PushId) → Korean body
  - `event_english` (PushId) → English body
- Body가 비어있는 언어는 skip
- `httpsCallable("sendPushNotification")` 호출
- 결과를 Result 영역에 표시

### Result 영역

- 각 PushId별 발송 결과 (✓ sent / ✗ error message / skip)


---


## 파일 위치

```
framework-ts/apps/Operation/src/tabs/push-send.ts
```


## Related

- [00-overview](../00-overview/SKILL.md) — Operation 개요
- [03-ssot](../03-ssot/SKILL.md) §D — Push Send 기능 정의
- [15-layout](../15-layout/SKILL.md) — 레이아웃, 스타일 가이드
