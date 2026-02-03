# 10-samples-network

> **패키지:** com.devian.samples
> **샘플명:** Network
> **도메인:** devian-upm-samples
> **문서 버전:** v5

---

## 1. 개요

`Devian.Protocol.Game` 프로토콜을 사용하는 Unity 클라이언트의 **실사용 예시** 샘플.

**표준 구조:**
- `NetTickRunner`(Unity)로 Tick 루프 제공
- `GameNetworkClient`(샘플 구현)로 Connect/Tick/Send 제공
- 수신 처리는 `Game2C.Stub` 상속 구현으로 처리

**용도:**
- WebSocket 기반 네트워크 클라이언트 사용 예시
- Protocol Stub 상속 구현 예시
- OnGUI 기반 런타임 UI 예시

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
└── Runtime/
    ├── Devian.Samples.Network.asmdef
    ├── GameNetworkClientSample.cs     (MonoBehaviour + OnGUI)
    ├── GameNetworkClient.cs           (INetTickable, 순수 C# 클라이언트)
    └── GameNetworkClient_Stub.cs      (Game2C.Stub 상속 구현)
```

> **참고:** `_Handlers.g.cs` partial 확장은 어셈블리 경계로 인해 불가능하므로, Stub 상속 방식을 사용한다.

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
| `com.devian.foundation` | ✅ | Core + Unity 네트워크 인프라 (NetTickRunner, NetWsClient 등) |
| `com.devian.protocol.game` | ✅ | Game 프로토콜 (Game2C.Stub, C2Game.Proxy 등) |

---

## 6. 포함 파일

### 6.1 GameNetworkClientSample.cs

Unity MonoBehaviour로 구현된 네트워크 클라이언트 샘플.

**기능:**
- `GameNetworkClient` 인스턴스 관리
- `NetTickRunner`에 등록하여 자동 Tick (없으면 자동 생성)
- OnGUI로 Connect/Disconnect/Ping/Echo 버튼 제공
- 연결 상태 및 수신 메시지 표시

**namespace:** `Devian`

### 6.2 GameNetworkClient.cs

순수 C# 클라이언트 (`INetTickable`, `IDisposable` 구현).

**구성:**
- `NetWsClient` — WebSocket 트랜스포트
- `NetClient` — 프레임 파싱/디스패치
- `Game2C.Runtime` + `SampleGame2CStub` — 수신 처리
- `C2Game.Proxy` — 송신 API

**이벤트:**
- `OnOpen`, `OnClose`, `OnError` — 연결 상태
- `OnPong`, `OnEchoReply` — 프로토콜 수신

**namespace:** `Devian`

### 6.3 GameNetworkClient_Stub.cs

`Game2C.Stub`를 상속한 수신 처리 구현.

**기능:**
- `OnPong` 오버라이드 → `OnPongReceived` 이벤트 발생
- `OnEchoReply` 오버라이드 → `OnEchoReplyReceived` 이벤트 발생

**namespace:** `Devian`

---

## 7. 사용 흐름 (표준)

### Step 1: NetTickRunner 준비

Unity 씬에 GameObject를 추가하고 `NetTickRunner` 컴포넌트를 붙인다.
(샘플은 없으면 자동 생성함)

```csharp
var runner = FindAnyObjectByType<NetTickRunner>();
if (runner == null)
{
    var go = new GameObject("NetTickRunner");
    runner = go.AddComponent<NetTickRunner>();
}
```

### Step 2: GameNetworkClient 생성

```csharp
private GameNetworkClient _client;

void Start()
{
    _client = new GameNetworkClient();

    // 이벤트 구독
    _client.OnOpen += () => Debug.Log("Connected!");
    _client.OnPong += (pong) => Debug.Log($"Pong: {pong.Timestamp}");
    _client.OnEchoReply += (reply) => Debug.Log($"Echo: {reply.Message}");
}
```

### Step 3: Runner에 등록

```csharp
runner.Register(_client);  // 매 프레임 Tick() 자동 호출
```

### Step 4: Connect / Close

```csharp
_client.Connect("ws://localhost:8080");
// ...
_client.Close();
```

### Step 5: 메시지 전송

```csharp
_client.SendPing();                    // Ping 전송
_client.SendEcho("Hello, World!");     // Echo 전송
```

### Step 6: 수신 처리 (Stub 상속 방식)

`GameNetworkClient_Stub.cs`에서 `Game2C.Stub`를 상속:

```csharp
namespace Devian
{
    internal sealed class SampleGame2CStub : Game2C.Stub
    {
        public event Action<Game2C.EnvelopeMeta, Game2C.Pong>? OnPongReceived;
        public event Action<Game2C.EnvelopeMeta, Game2C.EchoReply>? OnEchoReplyReceived;

        protected override void OnPong(Game2C.EnvelopeMeta meta, Game2C.Pong message)
            => OnPongReceived?.Invoke(meta, message);

        protected override void OnEchoReply(Game2C.EnvelopeMeta meta, Game2C.EchoReply message)
            => OnEchoReplyReceived?.Invoke(meta, message);
    }
}
```

> **중요:** `_Handlers.g.cs`의 partial 확장은 어셈블리 경계로 인해 샘플에서 불가능. Stub 상속이 표준 패턴.

---

## 8. 어셈블리 제약 안내

### 8.1 왜 Handlers partial이 아닌가?

`Game2C_Handlers.g.cs`는 `Devian.Protocol.Game` 어셈블리에 생성된다.
C#의 `partial class`는 **동일 어셈블리 내에서만** 동작하므로,
샘플 어셈블리(`Devian.Samples.Network`)에서 partial 확장이 불가능하다.

### 8.2 샘플의 해결책: Stub 상속

`Game2C.Stub`는 abstract class이므로 어떤 어셈블리에서든 상속 가능.
샘플은 `SampleGame2CStub : Game2C.Stub`로 수신 처리를 구현한다.

자세한 정책: `skills/devian-unity-samples/01-policy/SKILL.md` 섹션 3.4.1 참조

---

## 9. 설치 방법

1. Unity 프로젝트 열기
2. Window → Package Manager
3. `Devian Samples` 패키지 선택
4. Samples 섹션에서 "Network" → "Import" 클릭

---

## 10. 테스트 실행

### 10.1 서버 실행 (로컬)

```bash
cd framework-ts
npm install
npm run start:server
```

- **기본 포트:** `ws://localhost:8080`
- **코덱:** Protobuf (기본), `USE_JSON=false`

### 10.2 Unity에서 테스트

**Option A: 메뉴로 원클릭 설정 (권장)**

1. 메뉴: **Devian → Samples → Network → Create Sample Setup**
2. Play 모드 진입
3. 화면 좌상단 OnGUI 버튼으로 Connect → Ping/Echo → Disconnect

**Option B: 수동 설정**

1. `GameNetworkClientSample` 컴포넌트를 빈 GameObject에 추가
2. URL 입력: `ws://localhost:8080`
3. Play 모드 진입
4. 화면 좌상단 OnGUI 버튼으로 Connect → Ping/Echo → Disconnect

### 10.3 예상 로그

**서버 측:**
```
Session connected
Ping handler called
Echo handler called
```

**Unity 측:**
```
[GameNetworkClientSample] Connected!
[GameNetworkClientSample] Pong received - Latency: Xms, ServerTime: Y
[GameNetworkClientSample] EchoReply: "Hello, World!" (echoed at Z)
```

### 10.4 WebGL 참고

WebGL 빌드는 브라우저 보안 제약이 있음:

- **wss:// 필수:** WebGL 브라우저에서는 secure WebSocket (wss://) 필요
- **CORS:** 서버가 Unity WebGL origin에서의 WebSocket 연결 허용 필요
- **.jslib 포함:** `DevianWs.jslib`는 `com.devian.foundation/Runtime/Plugins/WebGL/`에 포함

로컬 테스트는 **에디터/스탠드얼론**에서 먼저 확인하고,
WebGL은 **배포 환경(https + wss)**에서 확인한다.

---

## 11. 커스터마이징

### 11.1 코드 수정

설치된 샘플은 `Assets/Samples/`에 위치하므로 자유롭게 수정 가능:

```csharp
// 1) namespace 변경 (자신의 프로젝트용)
namespace MyGame.Network
{
    // 2) 클래스명 변경
    public class MyNetworkClient : INetTickable, IDisposable
    {
        // 3) 자신의 Protocol Stub 상속
        private MyGame2CStub _stub;
    }
}
```

### 11.2 Protocol 변경

`Devian.Protocol.Game` 대신 자신의 프로토콜 사용:

1. asmdef references 수정
2. `Game2C.Stub` → 자신의 `{Protocol}.Stub` 상속
3. `C2Game.Proxy` → 자신의 `{Protocol}.Proxy` 사용

---

## 12. 참고

- 정책 문서: `skills/devian-unity-samples/01-policy/SKILL.md`
- NetTickRunner: `skills/devian-core/72-network-ws-client/SKILL.md`
- Protocol 코드젠: `skills/devian-protocol/41-codegen-protocol-csharp-ts/SKILL.md`
