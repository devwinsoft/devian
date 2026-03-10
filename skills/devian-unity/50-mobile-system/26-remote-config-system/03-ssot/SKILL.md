# 03-ssot — 26-remote-config-system

Status: ACTIVE
AppliesTo: v10

## SSOT Scope

이 문서는 아래 항목의 정본이다.

- `RemoteConfigSnapshot` 필드
- `RemoteConfigStorage` 저장 구조
- `RemoteConfigManager` 초기화/갱신/서버시간 계산 규칙
- Firebase callable 이름 (`getRemoteConfig`)과 `initSession.remoteConfig` 응답 키

---

## A) Snapshot

```csharp
public sealed class RemoteConfigSnapshot
{
    public long serverNowUtcMs;
    public string minVersion;
    public string currentVersion;
}
```

규칙:
- `serverNowUtcMs <= 0`이면 시간 소스로 사용할 수 없다.
- 버전 문자열은 비어있을 수 있다.

---

## B) Storage

```csharp
public sealed class RemoteConfigStorage
{
    public int schemaVersion; // default: 1
    public RemoteConfigSnapshot snapshot;
    public long snapshotReceivedAtClientUtcMs;
}
```

규칙:
- SaveData root `remoteConfig` 섹션에 저장한다.
- 구버전 payload(`mission.clockSnapshot`)은 deserialize 시 자동 마이그레이션한다.

---

## C) Manager Contract

- `InitializeAsync(preloadedSnapshot, ct)`
- `RefreshAsync(ct)`
- `TryGetServerNowUtcMs(out long)`
- `TryGetServerNowUtcDate(out DateTime)`
- `ClearStorage()`

시간 계산:
- `estimated = snapshot.serverNowUtcMs + max(0, clientNowUtcMs - snapshotReceivedAtClientUtcMs)`
- 반환 범위는 UnixTime(DateTime min~max)로 clamp 한다.

---

## D) Integration Rules

- `MobileApplication.OnEnterForeground()`는 `RemoteConfigManager.RefreshAsync()`를 호출한다.
- `LoginManager.VersionCheckAsync(clientVersion, ct)`는 런타임에서 `FirebaseCallableManager.GetRemoteConfigAsync()` 응답의 `minVersion/currentVersion`으로 판정한다.
- Unity Editor에서는 `RemoteConfigManager.RefreshAsync()` 후 snapshot 기준으로 버전 판정을 수행한다.
- `MissionManager`, `AchieveManager`, `PurchaseManager`, `LeaderboardManager`는 서버시간을 `RemoteConfigManager`에서만 읽는다.
- `SessionInitSnapshot`의 원격설정 필드는 `RemoteConfig`다.

---

## E) Server Contract

- callable: `getRemoteConfig`
- initSession 응답 키: `remoteConfig`
- payload shape:
  - `serverNowUtcMs: number`
  - `minVersion: string`
  - `currentVersion: string`
