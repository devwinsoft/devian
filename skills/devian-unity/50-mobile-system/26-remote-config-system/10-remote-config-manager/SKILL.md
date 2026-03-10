# 10-remote-config-manager

Status: ACTIVE
AppliesTo: v10

RemoteConfigManager 설계 문서다.

---

## Implementation Location (3-path mirror)

| 파일 | UPM (정본) | Packages (sync) | Assets/Samples (import) |
|---|---|---|---|
| `RemoteConfigManager.cs` | `upm/com.devian.samples/Samples~/MobileSystem/Runtime/RemoteConfig/` | `Packages/com.devian.samples/Samples~/MobileSystem/Runtime/RemoteConfig/` | `Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/RemoteConfig/` |
| `RemoteConfigSnapshot.cs` | 동일 경로 | 동일 경로 | 동일 경로 |
| `RemoteConfigStorage.cs` | 동일 경로 | 동일 경로 | 동일 경로 |
| `SaveDataJsonCodecRemoteConfig.cs` | `.../Runtime/SaveData/JsonCodec/` | 동일 경로 | 동일 경로 |

---

## Responsibilities

- `getRemoteConfig` callable 호출과 응답 파싱(`FirebaseCallableManager` 경유)
- server UTC 시각 시뮬레이션
- 버전 정보(`minVersion/currentVersion`) 저장
- SaveData `remoteConfig` 섹션 직렬화/역직렬화
- legacy `mission.clockSnapshot` 마이그레이션

---

## Public API

- `InitializeAsync(RemoteConfigSnapshot preloadedSnapshot = null, CancellationToken ct = default)`
- `RefreshAsync(CancellationToken ct = default)`
- `TryGetServerNowUtcMs(out long value)`
- `TryGetServerNowUtcDate(out DateTime value)`
- `ClearStorage()`
- `Storage`, `IsInitialized`, `serverNowUtcMs`, `serverNowUtcDate`

---

## Hard Rules

- `RemoteConfigManager` 초기화 전에 서버시간 의존 로직을 실행하지 않는다.
- 외부에서 클라이언트 로컬 시각으로 시즌 판정을 하지 않는다.
- 버전 판정은 `RemoteConfigSnapshot`만 사용한다.
- `MissionManager`는 clock snapshot을 소유하지 않는다.
- `TimeManager`는 사용/참조하지 않는다.

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [11-mobile-application](../../11-mobile-application/SKILL.md)
- [23-firebase-callable-manager](../../23-firebase-callable-manager/SKILL.md)
- [48-mission-system/10-mission-manager](../../48-mission-system/10-mission-manager/SKILL.md)
