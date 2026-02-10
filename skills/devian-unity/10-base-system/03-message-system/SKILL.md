# 03-message-system

Status: ACTIVE
AppliesTo: v11
Type: Component Specification

## 목적

`MessageSystem<TOwnerKey, TMsgKey>`는 **ownerKey + msgKey 기반 메시지/트리거 라우팅** 시스템이다.

- 소유자 키(TOwnerKey) 단위로 메시지 핸들러 등록/해제
- enum 키(TMsgKey) 기반 메시지 분기
- 1회성 핸들러 지원 (SubcribeOnce)

---

## 범위

### 포함

- `Subcribe` / `UnSubcribe` — ownerKey 기반 핸들러 등록/해제
- `SubcribeOnce` — 1회 호출 후 자동 해제
- `Notify` — key 매칭 핸들러 일괄 호출
- `ClearAll` — 전체 초기화

### 제외

- **시간 기반 이벤트 (타이머/틱/스케줄러)** — Unity `Invoke` / `Coroutine` 사용 권장
- 백그라운드 스레드 지원
- Unity 이벤트 루프 (Update) 연동
- `UnityEngine.Object` owner 오버로드 (삭제됨)

---

## 네임스페이스

```csharp
namespace Devian
```

---

## 제약 조건

```csharp
public class MessageSystem<TOwnerKey, TMsgKey>
    where TOwnerKey : IEquatable<TOwnerKey>, IComparable<TOwnerKey>
    where TMsgKey : unmanaged, Enum
```

| 파라미터 | 제약 | 설명 |
|----------|------|------|
| `TOwnerKey` | `IEquatable<TOwnerKey>, IComparable<TOwnerKey>` | 소유자 식별 키 (int, string 등) |
| `TMsgKey` | `unmanaged, Enum` | 메시지 키 (enum 타입 필수) |

---

## 핵심 규약 (Hard Rule)

### 1. Notify reverse 순회

`Notify` 호출 시 등록 목록을 **뒤에서 앞으로(reverse)** 순회한다.
이는 순회 중 핸들러 제거 시 컬렉션 안전성을 보장한다.

### 2. handler true 반환 시 자동 해제

`Handler` delegate가 `true`를 반환하면 해당 핸들러는 즉시 제거된다.

```csharp
public delegate bool Handler(object[] args);
```

### 3. SubcribeOnce는 Action 래핑

`SubcribeOnce`는 전달받은 `Action<object[]>`를 내부에서 래핑하여, 한 번 호출 후 `true`를 반환하도록 구현한다.

### 4. 키 비교

```csharp
_keyComparer = EqualityComparer<TMsgKey>.Default
```

### 6. 내부 저장소

```csharp
private readonly Dictionary<TOwnerKey, List<Entry>> _byInstanceId = new();
```

---

## API 시그니처

```csharp
#nullable enable
using System;
using System.Collections.Generic;

namespace Devian
{
    public class MessageSystem<TOwnerKey, TMsgKey>
        where TOwnerKey : IEquatable<TOwnerKey>, IComparable<TOwnerKey>
        where TMsgKey : unmanaged, Enum
    {
        public delegate bool Handler(object[] args);

        public void ClearAll();

        public void Subcribe(TOwnerKey ownerKey, TMsgKey key, Handler handler);
        public void SubcribeOnce(TOwnerKey ownerKey, TMsgKey key, Action<object[]> handler);
        public void UnSubcribe(TOwnerKey ownerKey);

        public void Notify(TMsgKey key, params object[] args);
    }
}
```

---

## 사용 예시

### 1. enum 키 정의

```csharp
public enum GameMessage
{
    PlayerDied,
    LevelComplete,
    ItemPickup,
    EnemySpawned
}
```

### 2. 인스턴스 생성 + MonoBehaviour에서 사용

```csharp
using UnityEngine;
using Devian;

public class GameEventListener : MonoBehaviour
{
    private readonly MessageSystem<int, GameMessage> _ms = new();
    private int _ownerKey = 1;

    private void Start()
    {
        _ms.Subcribe(_ownerKey, GameMessage.PlayerDied, OnPlayerDied);
        _ms.Subcribe(_ownerKey, GameMessage.LevelComplete, OnLevelComplete);

        // 1회성 핸들러
        _ms.SubcribeOnce(_ownerKey, GameMessage.ItemPickup, args =>
        {
            Debug.Log($"First item picked up: {args[0]}");
        });
    }

    private void OnDestroy()
    {
        // owner의 모든 핸들러 해제
        _ms.UnSubcribe(_ownerKey);
    }

    private bool OnPlayerDied(object[] args)
    {
        Debug.Log("Player died!");
        return false; // 계속 유지
    }

    private bool OnLevelComplete(object[] args)
    {
        int level = (int)args[0];
        Debug.Log($"Level {level} complete!");
        return false; // 계속 유지
    }
}
```

### 3. 메시지 발송

```csharp
// 인스턴스를 통해 Notify 호출
_ms.Notify(GameMessage.PlayerDied);
_ms.Notify(GameMessage.LevelComplete, 5);
_ms.Notify(GameMessage.ItemPickup, "HealthPotion");
```

### 4. 시간 기반 호출

**MessageSystem은 시간 기반 이벤트를 지원하지 않는다.**
타이머/지연 호출이 필요하면 Unity의 `Invoke` 또는 `Coroutine`을 사용한다.

```csharp
// 3초 후 메시지 발송 (Unity Invoke 사용)
Invoke(nameof(SendDelayedMessage), 3f);

private void SendDelayedMessage()
{
    _ms.Notify(GameMessage.EnemySpawned);
}

// 또는 Coroutine 사용
IEnumerator DelayedNotify()
{
    yield return new WaitForSeconds(3f);
    _ms.Notify(GameMessage.EnemySpawned);
}
```

---

## 파일 경로

| 타입 | 경로 |
|------|------|
| Runtime | `com.devian.foundation/Runtime/Unity/MessageSystem/MessageSystem.cs` |

---

## Reference

- 인덱스: `10-base-system/skill.md`
