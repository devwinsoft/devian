# 33-time-manager

Status: ACTIVE
AppliesTo: v10
Type: Component Specification

## 목적

`TimeManager`는 서버 기준 UTC 시간을 클라이언트에서 시뮬레이션하는 공통 컴포넌트다.

- 기준 입력: `InitServerTime(long serverNowUtcMs)`
- 출력: `serverNowUtcMs`, `serverNowUtcDate`
- 시뮬레이션: `serverAnchor + (clientNowUtcMs - clientAnchorUtcMs)`

---

## 타입/상속

```csharp
public sealed class TimeManager : CompoSingleton<TimeManager>
```

규칙:

- `MobileApplication`이 `RequireComponent(typeof(TimeManager))`로 소유한다.
- 외부는 `TimeManager.Instance` 또는 `TryGet`으로 조회한다.

---

## API

```csharp
public void InitServerTime(long serverNowUtcMs);
public bool TryGetServerNowUtcMs(out long value);
public bool TryGetServerNowUtcDate(out DateTime value);

public bool IsInitialized { get; }
public long serverNowUtcMs { get; }
public DateTime serverNowUtcDate { get; }
```

---

## Hard Rules

1. `InitServerTime` 전에는 미초기화 상태다.
   - `serverNowUtcMs = 0`
   - `serverNowUtcDate = DateTime.MinValue`
2. `serverNowUtcMs <= 0`으로 초기화 호출 시 미초기화 상태로 리셋한다.
3. UTC milliseconds는 DateTime 범위를 벗어나지 않도록 clamp한다.
4. 서버 시간 정본 입력은 `MissionManager` clock snapshot이다.

---

## 연동 규칙

- `MissionManager`는 `TimeManager`를 직접 초기화하지 않는다.
- 외부 bootstrap/application 계층에서 `MissionClockSnapshot.serverNowUtcMs`를 받아 `TimeManager.InitServerTime(...)`를 호출한다.
- season 조건 평가(`ACHIEVE_PASS.reqSeasonId`)는 `TB_SEASON` + `TimeManager.serverNowUtcMs` 기반으로 판정한다.

---

## 파일 경로

- `framework-cs/upm/com.devian.domain.common/Runtime/Unity/Time/TimeManager.cs`
- `framework-cs/apps/UnityExample/Packages/com.devian.domain.common/Runtime/Unity/Time/TimeManager.cs`
