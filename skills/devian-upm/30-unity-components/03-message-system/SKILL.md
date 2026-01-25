# 03-message-system

Status: ACTIVE  
AppliesTo: v11  
Type: Component Specification

## 목적

`MessageSystem<TKey>`는 **instanceId + key 기반 메시지/트리거 라우팅** 시스템이다.

- GameObject/MonoBehaviour 단위로 메시지 핸들러 등록/해제
- enum 키 기반 메시지 분기
- 1회성 핸들러 지원 (RegisterOnce)

---

## 범위

### 포함

- `Register` / `Unregister` — instanceId 기반 핸들러 등록/해제
- `RegisterOnce` — 1회 호출 후 자동 해제
- `Notify` — key 매칭 핸들러 일괄 호출
- `ClearAll` — 전체 초기화

### 제외

- **시간 기반 이벤트 (타이머/틱/스케줄러)** — Unity `Invoke` / `Coroutine` 사용 권장
- 백그라운드 스레드 지원
- Unity 이벤트 루프 (Update) 연동

---

## 네임스페이스

```csharp
namespace Devian
```

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

### 3. RegisterOnce는 Action 래핑

`RegisterOnce`는 전달받은 `Action<object[]>`를 내부에서 래핑하여, 한 번 호출 후 `true`를 반환하도록 구현한다.

### 4. owner overload는 null-safe + GetInstanceID

`UnityEngine.Object owner` 오버로드는:
- `owner == null`이면 즉시 return (예외 없음)
- 내부에서 `owner.GetInstanceID()` 사용

### 5. 키 비교

```csharp
EqualityComparer<TKey>.Default.Equals(a, b)
```

---

## API 시그니처

```csharp
#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Devian
{
    public static class MessageSystem<TKey> where TKey : unmanaged, Enum
    {
        public delegate bool Handler(object[] args);

        public static void ClearAll();

        public static void Register(int instanceId, TKey key, Handler handler);
        public static void Register(UnityEngine.Object owner, TKey key, Handler handler);

        public static void RegisterOnce(int instanceId, TKey key, Action<object[]> handler);
        public static void RegisterOnce(UnityEngine.Object owner, TKey key, Action<object[]> handler);

        public static void Unregister(int instanceId);
        public static void Unregister(UnityEngine.Object owner);

        public static void Notify(TKey key, params object[] args);
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

### 2. MonoBehaviour에서 사용

```csharp
using UnityEngine;
using Devian;

public class GameEventListener : MonoBehaviour
{
    private void Start()
    {
        // this(owner)로 등록
        MessageSystem<GameMessage>.Register(this, GameMessage.PlayerDied, OnPlayerDied);
        MessageSystem<GameMessage>.Register(this, GameMessage.LevelComplete, OnLevelComplete);

        // 1회성 핸들러
        MessageSystem<GameMessage>.RegisterOnce(this, GameMessage.ItemPickup, args =>
        {
            Debug.Log($"First item picked up: {args[0]}");
        });
    }

    private void OnDestroy()
    {
        // owner의 모든 핸들러 해제
        MessageSystem<GameMessage>.Unregister(this);
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
// 어디서든 Notify 호출
MessageSystem<GameMessage>.Notify(GameMessage.PlayerDied);
MessageSystem<GameMessage>.Notify(GameMessage.LevelComplete, 5);
MessageSystem<GameMessage>.Notify(GameMessage.ItemPickup, "HealthPotion");
```

### 4. 시간 기반 호출

**MessageSystem은 시간 기반 이벤트를 지원하지 않는다.**  
타이머/지연 호출이 필요하면 Unity의 `Invoke` 또는 `Coroutine`을 사용한다.

```csharp
// 3초 후 메시지 발송 (Unity Invoke 사용)
Invoke(nameof(SendDelayedMessage), 3f);

private void SendDelayedMessage()
{
    MessageSystem<GameMessage>.Notify(GameMessage.EnemySpawned);
}

// 또는 Coroutine 사용
IEnumerator DelayedNotify()
{
    yield return new WaitForSeconds(3f);
    MessageSystem<GameMessage>.Notify(GameMessage.EnemySpawned);
}
```

---

## 파일 경로

| 타입 | 경로 |
|------|------|
| Runtime | `com.devian.unity/Runtime/Message/MessageSystem.cs` |

---

## Reference

- 인덱스: `30-unity-components/SKILL.md`
