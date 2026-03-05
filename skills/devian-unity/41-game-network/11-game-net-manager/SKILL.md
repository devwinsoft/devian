# 11-game-net-manager

> **패키지:** com.devian.samples
> **샘플명:** GameNetwork
> **도메인:** devian-upm-samples
> **문서 버전:** v21

---

## 1. 개요

`Devian.Protocol.Game` 프로토콜을 사용하는 Unity 클라이언트의 **실사용 예시** 샘플.

**표준 구조 (session host + send-only proxy 기반):**
- **`GameNetManager`는 `CompoSingleton<GameNetManager>`를 상속** (중복 인스턴스 방지, 씬 1개 인스턴스 보장)
- **`GameNetManager`가 `_stub`, `_proxy`, `_sessionHost`를 소유**
- **`_stub`은 `Game2CStub` 타입** — 서버→클라이언트 수신/디스패치용 stub
- **Generated Proxy는 send-only** — `AttachSession()`, `DetachSession()`, `SendXxx()`만 담당
- **generated `ClientSessionHost`가 protocol pairing을 제공하고, foundation `NetClientSessionHost`가 lifecycle/state/event를 소유**
- **Awake:** `base.Awake()` 호출 + sessionHost 생성 + host 이벤트 구독
- **Connect:** `SessionHost.Connect(url)` 호출
- **Update:** `sessionHost.Tick()` 위임
- **OnDestroy:** host dispose 후 `base.OnDestroy()` 호출
- **`Game2CStub`는 partial 클래스로, inbound 메시지 처리 내부 구현**

**용도:**
- WebSocket 기반 네트워크 클라이언트 사용 예시
- Generated Proxy가 인터페이스에만 의존하는 DI 패턴 예시
- partial 클래스 확장 패턴 예시
- CompoSingleton 싱글톤 패턴 예시

---

## 2. 경로

### 2.1 원본 (upm)

```
framework-cs/upm/com.devian.samples/Samples~/GameNetwork/
```

### 2.2 설치 후 위치 (Unity 프로젝트)

```
Assets/Samples/Devian Samples/{version}/GameNetwork/
```

> 현재 session-host 리팩토링은 UnityExample의 설치된 샘플 경로에서 우선 검증되었고, `Samples~` 원본도 동일 구조로 유지되어야 한다.

---

## 3. 폴더 구조 (Hard Rule)

```
Samples~/GameNetwork/
├── README.md
└── Runtime/
    ├── Devian.Samples.GameNetwork.asmdef
    └── Game/
        ├── GameNetManager.cs          (CompoSingleton, Stub/Proxy/generated session host 소유)
        └── Game2CStub.cs              (partial, inbound 메시지 처리)
```

`ClientSessionHost.g.cs`는 샘플 폴더가 아니라 `com.devian.protocol.game/Runtime/Generated/`에서 제공된다.

---

## 4. asmdef 정보

### 4.1 Runtime asmdef

**파일명:** `Devian.Samples.GameNetwork.asmdef`

```json
{
  "name": "Devian.Samples.GameNetwork",
  "rootNamespace": "Devian",
  "references": [
    "Devian.Core",
    "Devian.Domain.Common",
    "Devian.Protocol.Game"
  ]
}
```

---

## 5. 의존성

이 샘플을 사용하려면 다음 패키지가 프로젝트에 설치되어 있어야 함:

| 패키지 | 필수 | 용도 |
|--------|------|------|
| `com.devian.foundation` | ✅ | Core 네트워크 인프라 (INetSession, INetConnector, NetWsConnector 등) |
| `com.devian.protocol.game` | ✅ | Game 프로토콜 (Game2C.Stub, C2Game.Proxy 등) |

---

## 6. 포함 파일

### 6.1 GameNetManager.cs

**CompoSingleton<GameNetManager>**. Stub, Proxy, SessionHost를 소유. **Proxy는 send-only**.

- **중복 인스턴스 금지**: CompoSingleton이 씬 1개 인스턴스를 보장
- **DontDestroyOnLoad**: 기본 활성화

**필드 (non-nullable, 선언 즉시 초기화, readonly):**
- `private readonly Game2CStub _stub = new()` — 서버→클라 수신용
- `private readonly C2Game.Proxy _proxy = new()` — 클라→서버 송신용
- `private ClientSessionHost? _sessionHost` — generated session host

**Static 접근:**
- `public static C2Game.Proxy Proxy => Instance._proxy` — 메시지 전송용 static 접근

**프로퍼티 (SessionHost에 위임):**
- `public bool IsConnected` — _sessionHost.IsConnected 기반
- `public string Url` — _sessionHost.Url 기반
- `public string LastError` — _sessionHost.LastError 기반

**싱글톤 접근:**
- `GameNetManager.Instance` — 인스턴스 조회 (없으면 예외)
- `GameNetManager.TryGet(out var manager)` — 안전한 조회

**이벤트:**
- `public event Action? OnOpen`
- `public event Action<ushort, string>? OnClose`
- `public event Action<Exception>? OnError`

**Unity 라이프사이클:**
- `Awake()` — `base.Awake()` 호출 후 sessionHost 생성 + host 이벤트 구독
- `Update()` — `_sessionHost?.Tick()` 호출
- `OnDestroy()` — host 이벤트 언구독 + host.Dispose() 후 `base.OnDestroy()` 호출

**Public API:**
- `Connect(string url)` — `SessionHost.Connect(url)` 호출
- `Disconnect()` — `_sessionHost?.Disconnect()` 호출

**Connect(url) 내부:**
```csharp
public void Connect(string url)
{
    if (string.IsNullOrEmpty(url))
    {
        Debug.LogError("[GameNetManager] URL cannot be empty");
        return;
    }

    SessionHost.Connect(url);
}
```

**Awake():**
```csharp
protected override void Awake()
{
    base.Awake();
    _sessionHost = new ClientSessionHost(_stub, _proxy);
    _sessionHost.OnOpen += HandleOpen;
    _sessionHost.OnClose += HandleClose;
    _sessionHost.OnError += HandleError;
}
```

**OnDestroy():**
```csharp
protected override void OnDestroy()
{
    if (_sessionHost != null)
    {
        _sessionHost.OnOpen -= HandleOpen;
        _sessionHost.OnClose -= HandleClose;
        _sessionHost.OnError -= HandleError;
        _sessionHost.Dispose();
        _sessionHost = null;
    }

    base.OnDestroy();
}
```

**namespace:** `Devian`

### 6.2 ClientSessionHost.g.cs

**generated session host wrapper**. protocol pairing만 담당하고 lifecycle은 foundation `NetClientSessionHost`로 위임한다.

**포함:**
- `ClientSessionHost(Game2C.Stub, C2Game.Proxy, INetConnector? connector = null)`
- `new Game2C.Runtime(stub)` pairing
- `new INetSessionBindable[] { proxy }` binding 구성

### 6.3 NetClientSessionHost

**foundation lifecycle owner**. session 생성, 상태 노출, event forwarding, proxy attach/detach를 담당.

### 6.4 Game2CStub.cs

**partial** concrete stub 클래스. inbound 메시지 처리.

**기본 빌드에서 로그 없음 (zero GC):**
- `DEVIAN_NET_DEBUG` 심볼이 정의되지 않으면 `Debug.Log` 호출 없음
- 문자열 보간/할당 없음
- 디버그가 필요하면 파일 상단의 `#define DEVIAN_NET_DEBUG` 주석 해제

**포함:**
- `OnPong()` — `OnPongImpl()` 호출 (디버그 모드에서만 로그)
- `OnEchoReply()` — `OnEchoReplyImpl()` 호출 (디버그 모드에서만 로그)
- `partial void OnPongImpl(...)` — 확장 훅
- `partial void OnEchoReplyImpl(...)` — 확장 훅

**namespace:** `Devian`

---

## 7. 사용 흐름 (표준)

### Step 1: GameNetManager 배치

```csharp
// 방법 1: 에디터 메뉴
// Devian → Samples → Network → Create GameNetManager

// 방법 2: Bootstrap prefab 또는 scene object에
// GameNetManager 컴포넌트를 미리 부착
```

### Step 2: Connect

```csharp
var manager = GetComponent<GameNetManager>();
manager.Connect("ws://localhost:8080");
```

> **내부 동작:**
> - `GameNetManager.Awake()` → `base.Awake()` + stub/proxy/sessionHost 연결 + host 이벤트 구독
> - `manager.Connect(url)`:
>   - `SessionHost.Connect(url)` 호출
>   - host 내부에서: `var runtime = new Game2C.Runtime(_stub);` — **수신 방향 런타임**
>   - Connector가 세션 생성: `connector.CreateSession(runtime, url)`
>   - Connector 내부에서: NetClient → NetWsTransport → NetClientBase
>   - host가 `proxy.AttachSession(session)` 호출
> - 연결 성공 시 host.HandleOpen() → OnOpen 이벤트
> - `GameNetManager.Update()` → `_sessionHost?.Tick()`

### Step 3: Tick (GameNetManager가 처리)

```csharp
// GameNetManager.Update()에서 자동 호출
// 사용자 코드에서 직접 Tick 호출 불필요
```

### Step 4: 메시지 전송

```csharp
// Use static Proxy to send messages (recommended)
GameNetManager.Proxy.SendPing(new C2Game.Ping { Timestamp = Time.time });
```

### Step 5: 커스텀 핸들러 (partial 확장)

```csharp
// Game2CStub.Partial.cs (사용자가 추가하는 파일)
namespace Devian
{
    public partial class Game2CStub
    {
        partial void OnPongImpl(Game2C.EnvelopeMeta meta, Game2C.Pong message)
        {
            // Custom Pong handling
            Debug.Log($"Custom Pong: timestamp={message.Timestamp}");
        }
    }
}
```

---

## 8. partial 확장 규칙 (Hard Rule)

### 8.1 Game2CStub 확장 훅

```csharp
// Game2CStub.Partial.cs
namespace Devian
{
    public partial class Game2CStub
    {
        partial void OnPongImpl(Game2C.EnvelopeMeta meta, Game2C.Pong message)
        {
            // Handle Pong message
        }

        partial void OnEchoReplyImpl(Game2C.EnvelopeMeta meta, Game2C.EchoReply message)
        {
            // Handle EchoReply message
        }
    }
}
```

---

## 9. 금지 패턴 (재발 방지)

### 9.1 Generated Proxy에서 구체 타입 참조 금지

- ❌ Generated Proxy가 `NetWsTransport`, `NetClientBase` 참조
- ✅ Generated Proxy는 `INetSession`, `INetConnector` 인터페이스에만 의존

### 9.2 Proxy 생성 시 sender/client ctor 주입 금지

- ❌ `new C2Game.Proxy(sender)` — ctor 주입 금지
- ✅ `new C2Game.Proxy()` — 기본 생성자만 사용

### 9.3 외부 핸들러 등록 금지

- ❌ `stub.RegisterHandler(...)` — 금지
- ❌ `Register*Handler(...)` — 금지
- ✅ partial 메서드로 내부 확장

### 9.4 자동 연결 금지

- ❌ `_autoConnect` 필드 — 금지
- ❌ `Start()`에서 자동 Connect — 금지
- ✅ 외부에서 명시적으로 `Connect()` 호출

### 9.5 편의 Send 메서드 금지

- ❌ `SendPing()`, `SendEcho()` — 금지
- ✅ `manager.Proxy`를 통해 직접 전송

### 9.6 Stub 외부 노출 금지

- ❌ `public Game2CStub Stub { get; }` — 금지 (내부 처리 원칙)
- ✅ Stub은 내부에서만 사용

### 9.7 역방향 Stub 사용 금지 (Hard Rule)

- ❌ `C2GameStub` 같은 역방향 stub 생성 금지
- ❌ send-only `C2Game.Proxy`에 stub/runtime/connect 로직을 다시 넣는 패턴 금지
- ✅ generated `ClientSessionHost`가 `Game2C.Stub`로 `Game2C.Runtime`를 조립
- ✅ 프로토콜 pairing 지식은 session owner에만 둔다

---

## 10. 설치 방법

1. Unity 프로젝트 열기
2. Window → Package Manager
3. `Devian Samples` 패키지 선택
4. Samples 섹션에서 "GameNetwork" → "Import" 클릭

---

## 11. 테스트 실행

### 11.1 서버 실행 (로컬)

```bash
# framework-ts 루트에서
npm install
npm -w GameServer run start
```

- **기본 포트:** `ws://localhost:8080`
- **코덱:** Protobuf (기본)

### 11.2 Unity에서 테스트

1. 에디터 메뉴: Devian → Samples → Network → Create GameNetManager
2. (선택) Game2CStub.Partial.cs 파일을 추가해 핸들러 구현
3. Play 모드 진입
4. `manager.Connect("ws://localhost:8080")` 호출

### 11.3 예상 로그

**서버 측:**
```
Session connected
```

**Unity 측:**
```
[GameNetManager] Connected!
[Game2CStub] OnPong received: timestamp=...
```

---

## 12. 참고

- 정책 문서: `skills/devian-examples/01-policy/SKILL.md`
- **Core/Net 정본**: `com.devian.foundation/Runtime/Module/Net/`
- NetClient/NetWsClient: `skills/devian/10-module/60-net/72-network-ws-client/SKILL.md`
- Protocol 코드젠: `skills/devian/80-tools/11-builder/34-protocol-gen-policy/SKILL.md`

---

## 13. INetSession/INetConnector 원칙 (재발 방지)

**Generated Proxy는 send path에 필요한 인터페이스만 안다:**
- `INetSession` — 세션 인터페이스 (`SendTo` 대상)
- `C2Game.Proxy`는 `AttachSession()`, `DetachSession()`, `SendXxx()`만 가진다

**SessionHost가 foundation lifecycle 인터페이스를 사용한다:**
- `INetConnector` — 세션 팩토리 인터페이스 (CreateSession)
- `ClientSessionHost`는 `Game2C.Runtime` pairing만 제공하고, 실제 `Connect(url)`는 `NetClientSessionHost`에서 처리한다:
  - `var runtime = new Game2C.Runtime(_stub);` — **수신 방향 프로토콜 런타임 생성**
  - `var session = connector.CreateSession(runtime, url);` — 세션 생성
  - 이벤트 핸들러 연결 + `proxy.AttachSession(session)` + `ConnectAsync` 시작

**프로토콜 방향 규칙 (Hard Rule):**
- send-only Proxy는 runtime을 만들지 않는다
- C2Game (클라→서버 송신) 경로의 runtime 조립은 generated `ClientSessionHost`가 `Game2C.Runtime`로 수행
- Game2C (서버→클라 송신) 경로도 같은 원칙으로 session assembly 레이어에서 조립한다

**Foundation에서 제공하는 구현:**
- `NetWsConnector : INetConnector` — WebSocket 세션 생성
- `NetClientBase : INetSession` — 세션 구현

**GameNetManager가 소유하는 것:**
- `_stub: Game2CStub = new()` — 서버→클라 수신용
- `_proxy: C2Game.Proxy = new()` — 클라→서버 송신용
- `_sessionHost: ClientSessionHost?` — generated session host

**Static 접근:**
- `public static C2Game.Proxy Proxy => Instance._proxy` — 메시지 전송용

**라이프사이클:**
- `Awake()` — `base.Awake()` + sessionHost 생성 + host 이벤트 구독
- `Connect(url)` — `SessionHost.Connect(url)` 호출
- `Update()` — `_sessionHost?.Tick()` 호출
- `OnDestroy()` — 이벤트 언구독 + host.Dispose() + `base.OnDestroy()`

**Sample에서 금지:**
- Generated Proxy가 구체 타입(NetWsTransport, NetClientBase) 참조
- 중간 레이어 클래스 추가 (NetworkClientImpl 등)
- 편의 Send 메서드 구현
- 역방향 stub (C2GameStub) 생성
- generated Proxy에 `Connect/Tick/Disconnect/Dispose`를 다시 추가

**Sample에서 허용:**
- GameNetManager가 Stub/Proxy/SessionHost 소유
- Game2CStub partial 확장
- Proxy static send 접근

**사용자 코드에서 수행:**
- `OnPongImpl()`, `OnEchoReplyImpl()` 구현 (partial 확장)
- 메시지 전송 (`GameNetManager.Proxy.SendXxx(...)` — static 접근 권장)
