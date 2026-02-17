# 10-samples-network

> **패키지:** com.devian.samples
> **샘플명:** Network
> **도메인:** devian-upm-samples
> **문서 버전:** v21

---

## 1. 개요

`Devian.Protocol.Game` 프로토콜을 사용하는 Unity 클라이언트의 **실사용 예시** 샘플.

**표준 구조 (INetSession/INetConnector 기반):**
- **`GameNetManager`는 `CompoSingleton<GameNetManager>`를 상속** (중복 인스턴스 방지, 씬 1개 인스턴스 보장)
- **`GameNetManager`가 `_stub`, `_proxy`, `_connector`를 소유**
- **`_stub`은 `Game2CStub` 타입** — 서버→클라이언트 수신/디스패치용 stub
- **Generated Proxy는 INetSession/INetConnector 인터페이스에만 의존** (구체 타입 참조 없음)
- **Awake:** `base.Awake()` 호출 + stub/proxy/connector 생성 + proxy 이벤트 구독
- **Connect:** `proxy.Connect(Game2C.Stub stub, url, connector)` 호출
- **Update:** `proxy.Tick()` 호출
- **OnDestroy:** 정리 후 `base.OnDestroy()` 호출
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
framework-cs/upm/com.devian.samples/Samples~/Network/
```

### 2.2 설치 후 위치 (Unity 프로젝트)

```
Assets/Samples/Devian Samples/0.1.0/Network/
```

---

## 3. 폴더 구조 (Hard Rule)

```
Samples~/Network/
├── README.md
├── Editor/
│   └── NetworkSampleMenu.cs           (에디터 메뉴 - 생성/사용법 안내)
└── Runtime/
    ├── Devian.Samples.Network.asmdef
    ├── GameNetManager.cs              (CompoSingleton, Stub/Proxy/Connector 소유)
    └── Game2CStub.cs                  (partial, inbound 메시지 처리)
```

---

## 4. asmdef 정보

### 4.1 Runtime asmdef

**파일명:** `Devian.Samples.Network.asmdef`

```json
{
  "name": "Devian.Samples.Network",
  "rootNamespace": "Devian",
  "references": [
    "Devian.Core",
    "Devian.Unity",
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

**CompoSingleton<GameNetManager>**. Stub, Proxy, Connector를 소유. **Proxy는 인터페이스에만 의존**.

- **중복 인스턴스 금지**: CompoSingleton이 씬 1개 인스턴스를 보장
- **DontDestroyOnLoad**: 기본 활성화

**필드 (non-nullable, 선언 즉시 초기화, readonly):**
- `private readonly Game2CStub _stub = new()` — 서버→클라 수신용
- `private readonly C2Game.Proxy _proxy = new()` — 클라→서버 송신용
- `private readonly INetConnector _connector = new NetWsConnector()` — 세션 팩토리

**Static 접근:**
- `public static C2Game.Proxy Proxy => Instance._proxy` — 메시지 전송용 static 접근

**프로퍼티 (Proxy에 위임):**
- `public bool IsConnected` — _proxy.IsConnected 기반
- `public string Url` — _proxy.Url 기반
- `public string LastError` — _proxy.LastError 기반

**싱글톤 접근:**
- `GameNetManager.Instance` — 인스턴스 조회 (없으면 예외)
- `GameNetManager.TryGet(out var manager)` — 안전한 조회

**이벤트:**
- `public event Action? OnOpen`
- `public event Action<ushort, string>? OnClose`
- `public event Action<Exception>? OnError`

**Unity 라이프사이클:**
- `Awake()` — `base.Awake()` 호출 후 proxy 이벤트 구독 (필드는 선언 시 초기화됨)
- `Update()` — `_proxy.Tick()` 호출
- `OnDestroy()` — 이벤트 언구독 + proxy.Dispose() 후 `base.OnDestroy()` 호출

**Public API:**
- `Connect(string url)` — `_proxy.Connect(_stub, url, _connector)` 호출
- `Disconnect()` — `_proxy?.Disconnect()` 호출

**Connect(url) 내부:**
```csharp
public void Connect(string url)
{
    if (string.IsNullOrEmpty(url))
    {
        Debug.LogError("[GameNetManager] URL cannot be empty");
        return;
    }

    // Proxy takes Game2C.Stub for inbound dispatch
    _proxy.Connect(_stub, url, _connector);
}
```

**Awake():**
```csharp
protected override void Awake()
{
    base.Awake();

    // Subscribe to proxy events (fields already initialized at declaration)
    _proxy.OnOpen += HandleOpen;
    _proxy.OnClose += HandleClose;
    _proxy.OnError += HandleError;
}
```

**OnDestroy():**
```csharp
protected override void OnDestroy()
{
    _proxy.OnOpen -= HandleOpen;
    _proxy.OnClose -= HandleClose;
    _proxy.OnError -= HandleError;

    _proxy.Dispose();
    base.OnDestroy();
}
```

**namespace:** `Devian`

### 6.2 Game2CStub.cs

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

### Step 1: GameNetManager 추가

```csharp
// 방법 1: 에디터 메뉴
// Devian → Samples → Network → Create GameNetManager

// 방법 2: 코드에서
var go = new GameObject("GameNetManager");
go.AddComponent<GameNetManager>();
```

### Step 2: Connect

```csharp
var manager = GetComponent<GameNetManager>();
manager.Connect("ws://localhost:8080");
```

> **내부 동작:**
> - `GameNetManager.Awake()` → `base.Awake()` + stub/proxy/connector 생성 + proxy 이벤트 구독
> - `manager.Connect(url)`:
>   - `_proxy.Connect(_stub, url, _connector)` 호출
>   - Proxy 내부에서: `var runtime = new Game2C.Runtime(stub);` — **수신 방향 런타임**
>   - Connector가 세션 생성: `connector.CreateSession(runtime, url)`
>   - Connector 내부에서: NetClient → NetWsTransport → NetClientBase
> - 연결 성공 시 Proxy.HandleOpen() → OnOpen 이벤트
> - `GameNetManager.Update()` → `_proxy.Tick()`

### Step 3: Tick (GameNetManager가 처리)

```csharp
// GameNetManager.Update()에서 자동 호출
// 사용자 코드에서 직접 Tick 호출 불필요
```

### Step 4: 메시지 전송

```csharp
// Use static Proxy to send messages (recommended)
GameNetManager.Proxy.SendPing(new C2Game.Ping { Timestamp = Time.time });

// Or use instance property
manager.Proxy.SendPing(new C2Game.Ping { Timestamp = Time.time });
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
- ❌ `C2Game.Proxy.Connect`에 `C2Game.Stub`를 넘기는 패턴 금지
- ✅ `C2Game.Proxy.Connect`에는 `Game2C.Stub` (수신 방향)을 전달
- ✅ 프로토콜 Proxy가 받는 stub 타입은 수신 방향 프로토콜의 Stub

---

## 10. 설치 방법

1. Unity 프로젝트 열기
2. Window → Package Manager
3. `Devian Samples` 패키지 선택
4. Samples 섹션에서 "Network" → "Import" 클릭

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

- 정책 문서: `skills/devian-unity/90-samples/01-policy/SKILL.md`
- **Core/Net 정본**: `com.devian.foundation/Runtime/Module/Net/`
- NetClient/NetWsClient: `skills/devian-core/72-network-ws-client/SKILL.md`
- Protocol 코드젠: `skills/devian-builder/41-codegen-protocol-csharp-ts/SKILL.md`

---

## 13. INetSession/INetConnector 원칙 (재발 방지)

**Generated Proxy는 인터페이스에만 의존:**
- `INetSession` — 세션 인터페이스 (Tick/Connect/Close/SendTo + 이벤트)
- `INetConnector` — 세션 팩토리 인터페이스 (CreateSession)
- `C2Game.Proxy.Connect(Game2C.Stub stub, url, connector)` 내부에서:
  - `var runtime = new Game2C.Runtime(stub);` — **수신 방향 프로토콜 런타임 생성**
  - `var session = connector.CreateSession(runtime, url);` — 세션 생성
  - 이벤트 핸들러 연결 + ConnectAsync 시작

**프로토콜 방향 규칙 (Hard Rule):**
- 프로토콜 Proxy가 만드는 runtime(stub)은 **수신 방향 프로토콜**을 사용
- C2Game (클라→서버 송신) Proxy → Game2C.Runtime (서버→클라 수신)
- Game2C (서버→클라 송신) Proxy → C2Game.Runtime (클라→서버 수신)

**Foundation에서 제공하는 구현:**
- `NetWsConnector : INetConnector` — WebSocket 세션 생성
- `NetClientBase : INetSession` — 세션 구현

**GameNetManager가 소유하는 것 (non-nullable, 선언 즉시 초기화, readonly):**
- `_stub: Game2CStub = new()` — 서버→클라 수신용
- `_proxy: C2Game.Proxy = new()` — 클라→서버 송신용
- `_connector: INetConnector = new NetWsConnector()` — 세션 팩토리

**Static 접근:**
- `public static C2Game.Proxy Proxy => Instance._proxy` — 메시지 전송용

**라이프사이클:**
- `Awake()` — `base.Awake()` + proxy 이벤트 구독 (필드는 선언 시 초기화됨)
- `Connect(url)` — `_proxy.Connect(_stub, url, _connector)` 호출
- `Update()` — `_proxy.Tick()` 호출
- `OnDestroy()` — 이벤트 언구독 + proxy.Dispose() + `base.OnDestroy()`

**Sample에서 금지:**
- Generated Proxy가 구체 타입(NetWsTransport, NetClientBase) 참조
- 중간 레이어 클래스 추가 (NetworkClientImpl 등)
- 편의 Send 메서드 구현
- 역방향 stub (C2GameStub) 생성

**Sample에서 허용:**
- GameNetManager가 Stub/Proxy/Connector 소유
- Game2CStub partial 확장
- Proxy 외부 접근 프로퍼티

**사용자 코드에서 수행:**
- `OnPongImpl()`, `OnEchoReplyImpl()` 구현 (partial 확장)
- 메시지 전송 (`GameNetManager.Proxy.SendXxx(...)` — static 접근 권장)
