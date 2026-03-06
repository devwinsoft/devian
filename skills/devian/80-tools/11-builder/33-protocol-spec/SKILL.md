# 33-protocol-spec

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian/10-module/03-ssot/SKILL.md

## Purpose

PROTOCOL(DomainType=PROTOCOL) 입력으로부터 C#/TS 프로토콜 코드를 생성하는 **전체 흐름**을 정의한다.

이 문서는 **입력 포맷 / 레지스트리(결정성) / 경로 규약**만 규정한다.
생성 코드의 구체적 API/산출물은 **런타임/제너레이터 코드**를 정답으로 본다.

---

## Inputs

입력은 `{buildInputJson}`의 `protocols` 섹션(배열)이 정본이다.

> `{buildInputJson}` 위치는 유동적이다. 예: `input/build_input.json`

```json
"protocols": [
  {
    "group": "Game",
    "protocolDir": "./Protocols/Game",
    "protocolFiles": ["C2Game.json", "Game2C.json"]
  }
]
```

- `group`: ProtocolGroup 이름 (C# 프로젝트명, TS 폴더명에 사용)
- `protocolDir`: Protocol JSON 및 Registry 파일이 위치한 디렉토리
- `protocolFiles`: 처리할 Protocol JSON 파일 목록
- 파일명 base가 **ProtocolName**이 된다. (예: `C2Game.json` → `C2Game`)

**금지 필드 (존재 시 빌드 실패):**
- `csTargetDir` — `csConfig.generateDir` 사용
- `tsTargetDir` — `tsConfig.generateDir` 사용
- `upmName` — 자동 계산 (`com.devian.protocol.{normalize(group)}`)

> **normalize 규칙 (요약):** trim → 공백을 `_`로 치환 → 허용 문자 외 제거(영문/숫자/`_`/`-`만 남김) → 소문자화. 정확한 규칙은 빌더의 `normalizeUpmSuffixFromGroup()` 참조.

### Protocol Spec JSON (필수 필드)

최소 구조:

```json
{
  "direction": "client_to_server | server_to_client | bidirectional",
  "messages": [
    {
      "name": "MessageName",
      "opcode": 100,              // optional
      "fields": [
        { "name": "field", "type": "int32", "tag": 1, "optional": true }
      ]
    }
  ]
}
```

추가 키가 존재할 수 있다. "지원 여부/정확한 스키마"는 코드를 정답으로 본다.

---

## Determinism Gate (opcode / tag)

Protocol 호환성을 위해 Registry 파일을 사용한다.

- `{ProtocolName}.opcodes.json`
- `{ProtocolName}.tags.json`

Registry 파일은 `protocolDir/Generated/`에 위치하며, 빌드 시 갱신된다.
Registry는 "생성된 입력" 파일로, 기계가 생성하지만 입력 폴더에 보존된다.

정책:

1) 명시 값 우선
2) 레지스트리 값은 호환성 보존을 위해 유지
3) 미지정 값은 **결정적 규칙으로 자동 할당**
4) Tag의 reserved range(19000..19999) 금지

> 자동 할당의 상세 규칙(최소값/정렬/증가)은 코드를 정답으로 본다.

---

## Outputs & Paths

경로 규약은 SSOT를 따른다.

**C# (ProtocolGroup = {ProtocolGroup}):**
- staging: `{tempDir}/Devian.Protocol.{ProtocolGroup}/cs/Generated/{ProtocolName}.g.cs`
- final: `{csConfig.generateDir}/Devian.Protocol.{ProtocolGroup}/Generated/{ProtocolName}.g.cs`
- 프로젝트 파일: `{csConfig.generateDir}/Devian.Protocol.{ProtocolGroup}/Devian.Protocol.{ProtocolGroup}.csproj` (수기/고정, 빌더가 생성/수정 금지)
- namespace: `Devian.Protocol.{ProtocolGroup}` (변경 금지)

**TypeScript:**
- staging: `{tempDir}/{ProtocolGroup}/ts/Generated/{ProtocolName}.g.ts`
- final: `{tsConfig.generateDir}/devian-protocol-{protocolgroup}/Generated/{ProtocolName}.g.ts`
- `index.ts`는 모듈 루트에 존재하되 수기/고정, 빌더가 생성/수정 금지
- 패키지명: `@devian/protocol-{protocolgroup}`

> **생성물 namespace 고정 (Hard Rule):**
> C# 생성물 namespace는 `Devian.Protocol.{ProtocolGroup}`으로 고정이며, 런타임 모듈 단일화와 무관하게 변경하지 않는다.

---

## Networker / SessionHost 생성 (Hard Rule)

**프로토콜 코드젠은 Stub, Proxy, Runtime 외에 Networker와 SessionHost를 프로토콜별로 생성한다.**

### Stub 변경 (기존 `.g.cs` 내부)

| 항목 | 기존 | 변경 |
|------|------|------|
| 클래스 | `abstract class Stub` / `protected` 생성자 | `sealed class Stub` / `public` 생성자 |
| 메시지 핸들링 | `protected abstract void OnXxx()` | `_handler.OnXxx()` (IHandler 위임) |
| 확장 방식 | 상속 (override) | `IHandler` 구현 + `SetHandler()` 호출 |

### IHandler 인터페이스

각 `{Protocol}.g.cs`의 `static partial class {Protocol}` 내부에 `IHandler` 인터페이스를 생성한다.

```csharp
// Game2C.g.cs 내부
public interface IHandler
{
    void OnPong(EnvelopeMeta meta, Pong message);
    void OnEchoReply(EnvelopeMeta meta, EchoReply message);
}

public sealed class Stub
{
    private IHandler _handler = null!;
    public Stub(ICodec? codec = null) { ... }
    public void SetHandler(IHandler handler) => _handler = handler;
    public void Dispatch(PacketEnvelope envelope) { /* _handler.OnXxx() 호출, unknown opcode → Log.Warn */ }
}
```

### {Protocol}SessionHost

프로토콜별 세션 호스트. 기존 그룹 단위 `ClientSessionHost.g.cs`를 대체한다.
교차 프로토콜 조합: `{Protocol}.Stub`(수신) + `{CounterProtocol}.Proxy`(송신).

```csharp
// Game2CSessionHost.g.cs
public sealed class Game2CSessionHost : NetClientSessionHost
{
    public Game2CSessionHost(
        Game2C.Stub stub,
        C2Game.Proxy proxy,
        INetConnector? connector = null)
        : base(
            () => new Game2C.Runtime(stub),
            new INetSessionBindable[] { proxy },
            connector)
    { }
}
```

### {Protocol}Networker

프로토콜별 Networker (plain C#).

```csharp
// Game2CNetworker.g.cs
public sealed class Game2CNetworker
{
    private readonly Game2C.Stub _stub;
    private readonly C2Game.Proxy _proxy;
    private readonly Game2CSessionHost _session;

    public C2Game.Proxy Proxy => _proxy;

    public Game2CNetworker(INetConnector? connector = null)
    {
        _stub = new Game2C.Stub();
        _proxy = new C2Game.Proxy();
        _session = new Game2CSessionHost(_stub, _proxy, connector);
    }

    public void SetHandler(Game2C.IHandler handler) => _stub.SetHandler(handler);

    public void Connect(string url)
    {
        Log.Info($"[Game2CNetworker] Connect: {url}");
        _session.Connect(url);
    }

    public void Tick() => _session.Tick();

    public void Disconnect()
    {
        Log.Info("[Game2CNetworker] Disconnect");
        _session.Disconnect();
    }
}
```

### 교차 프로토콜 조합 규칙

프로토콜 그룹 내에서 direction이 반대인 프로토콜끼리 페어링된다.

```
Game 그룹:
  Game2C (server_to_client) ↔ C2Game (client_to_server)

  Game2CNetworker  = Game2C.Stub  + C2Game.Proxy  (클라이언트용)
  C2GameNetworker  = C2Game.Stub  + Game2C.Proxy  (서버용)
```

### 표준 사용 흐름

```csharp
// 1. IHandler 구현
public class MyGameHandler : Game2C.IHandler
{
    public void OnPong(Game2C.EnvelopeMeta meta, Game2C.Pong message) { ... }
    public void OnEchoReply(Game2C.EnvelopeMeta meta, Game2C.EchoReply message) { ... }
}

// 2. 생성 + 핸들러 설정 + 연결
var networker = new Game2CNetworker();
networker.SetHandler(new MyGameHandler());
networker.Connect("ws://localhost:8080");

// 3. 매 프레임
networker.Tick();

// 4. 송신
networker.Proxy.SendPing(new C2Game.Ping { Timestamp = now });

// 5. 정리
networker.Disconnect();
```

### SessionHost 연결 규칙 (Hard Rule)

**Generated Proxy는 연결 수명주기를 다시 가지면 안 된다. 연결/정리/이벤트/state는 Networker/SessionHost 한 곳에만 둔다.**

**핵심 규칙:**
1. generated `Proxy` public API는 `AttachSession()`, `DetachSession()`, `SendXxx()`로 설명 가능해야 한다
2. Networker/SessionHost가 paired inbound runtime 생성, `INetSession` 생성, 이벤트 구독, `AttachSession()` 호출을 맡는다
3. 세션 해제 시 Networker/SessionHost가 먼저 이벤트를 끊고 `DetachSession()`으로 stale binding을 제거한다

### OnError 디듀프 가드 (Hard Rule)

**`NetClientSessionHost`는 연결 실패 시 OnError를 Attempt당 최대 1회만 발생시킨다(디듀프 가드).**

> 이 로직은 foundation 수기 코드(`NetClientSessionHost`)에 구현되어 있다. Generated Proxy는 send-only이며 에러 가드를 갖지 않는다.

**핵심 규칙:**
1. 한 연결 시도에서 OnError는 최대 1회만 Invoke
2. _errorNotified가 true이면 추가 에러 무시
3. 새 연결/연결 성공/세션 정리 시 플래그 리셋
4. stale session 이벤트는 `ReferenceEquals` 가드로 무시

---

## UPM 산출물 정책 (Hard Rule)

**Protocol UPM(`com.devian.protocol.*`)은 Runtime-only이며, 빌더가 touch 가능한 범위는 Generated/** 뿐이다.**

| 대상 | 빌더 동작 |
|-----------|----------|
| `Runtime/Devian.Protocol.{Group}.asmdef` | 수기 파일 (빌더 수정 금지) |
| `Runtime/Generated/{ProtocolName}.g.cs` | ✅ 생성/갱신 |
| `Runtime/Generated/{Protocol}SessionHost.g.cs` | ✅ 생성/갱신 |
| `Runtime/Generated/{Protocol}Networker.g.cs` | ✅ 생성/갱신 |
| ~~`Runtime/Generated/ClientSessionHost.g.cs`~~ | ❌ 삭제 (프로토콜별 SessionHost로 대체) |
| `package.json` | 수기 파일 (빌더 수정 금지) |
| `Editor/` 폴더 | ❌ 생성 금지, 존재 시 레거시 청소로 삭제 |

**Runtime asmdef references 정책:**
- `Devian.Core`
- `Devian.Domain.Common`

> SSOT: `skills/devian/80-tools/11-builder/03-ssot/SKILL.md` — Protocol UPM 산출물 정책

---

## Unity Compatibility (Hard Rule)

Unity 환경에서의 호환성을 위해 다음 규칙을 강제한다.

**C# Protocol 코드 생성 시:**

1. **System.Text.Json 사용 금지**
   - Unity는 `System.Text.Json`을 기본 제공하지 않음
   - `using System.Text.Json;` 생성 금지
   - `JsonSerializer`, `JsonSerializerOptions` 등 사용 금지

2. **CodecJson 생성 금지**
   - JSON 코덱은 생성하지 않음
   - `CodecProtobuf`만 생성 (기본 코덱)
   - Stub 생성자 기본값도 `CodecProtobuf` 사용

3. **ICodec 인터페이스**
   - 인터페이스는 유지 (확장성)
   - 기본 구현체는 `CodecProtobuf`만 제공

---

## Protocol 모듈 의존성 (Hard Rule)

**C# PROTOCOL 모듈 의존성:**
- `Devian.Protocol.{ProtocolGroup}.csproj`는 다음을 ProjectReference 한다:
  - `..\Devian\Devian.csproj`
  - `..\Devian.Domain.Common\Devian.Domain.Common.csproj`
- 각 생성물(`{ProtocolName}.g.cs`)은 `using Devian;`을 포함해야 한다. (namespace는 Devian 단일)

**TS PROTOCOL 패키지 의존성:**
- `@devian/protocol-{protocolgroup}`는 `@devian/core` + `@devian/module-common`을 의존한다.

**Unity UPM:**
- Protocol용 `.asmdef` 파일의 `references`에 `Devian.Domain.Common` 포함 필수
- 예: `Devian.Protocol.Game.asmdef" → "references": [..., "Devian.Domain.Common"]`

---

## TypeScript Namespace 규칙

TS 생성물은 **ProtocolName 단위**로 네임스페이스가 생성된다.

**생성 형태:**
```typescript
// {ProtocolName}.g.ts
export namespace {ProtocolName} {
    export interface MessageName { ... }
    export const Opcodes = { ... } as const;
}
```

**핵심 규칙:**
1. `.g.ts` 파일은 `export namespace {ProtocolName}`만 생성
2. `index.ts`에서 Direct export 제공
3. 소비자 코드는 Direct import를 사용

**생성 예시 (index.ts):**
```typescript
import * as C2GameMod from './C2Game.g';
import * as Game2CMod from './Game2C.g';

export const C2Game = C2GameMod.C2Game;
export const Game2C = Game2CMod.Game2C;

export { createServerRuntime } from './Generated/ServerRuntime.g';
export { createClientRuntime } from './Generated/ClientRuntime.g';
```

**사용법 (권장):**
```typescript
import { C2Game, Game2C, createClientRuntime } from '@devian/protocol-game';

// 타입 사용
const req: C2Game.LoginRequest = { ... };
const ack: Game2C.LoginAck = { ... };

// Opcode 사용
const opcode = C2Game.Opcodes.LoginRequest;
```

---

## ServerRuntime / ClientRuntime 생성 (TypeScript)

Protocol 그룹에 inbound와 outbound가 **정확히 1개씩** 존재하면 Runtime을 자동 생성한다.

**ServerRuntime (서버 관점):**
- inbound: client_to_server (예: C2Game)
- outbound: server_to_client (예: Game2C)

**ClientRuntime (클라이언트 관점):**
- inbound: server_to_client (예: Game2C)
- outbound: client_to_server (예: C2Game)

**생성 조건:**
- inbound 1개 + outbound 1개 → 생성
- bidirectional만 존재 → 생성 안함 (정상)
- 그 외 (0개, 2개 이상, 한쪽만 존재) → **빌드 에러**

**생성 파일:**
- `{tsConfig.generateDir}/devian-protocol-{group}/Generated/ServerRuntime.g.ts`
- `{tsConfig.generateDir}/devian-protocol-{group}/Generated/ClientRuntime.g.ts`

---

## TypeScript package.json (생성 산출물)

`devian-protocol-*` 패키지의 `package.json`은 **빌드 시스템이 생성하는 산출물**이다.

**수정 금지 정책:**
- 수동 편집 금지
- 빌드 시 덮어쓰기됨

**생성 내용:**
- `name`: `@devian/protocol-{group}`
- `exports`: `.` + Runtime 존재 시 `./server-runtime`, `./client-runtime`
- `dependencies`: `@devian/core`

> 위 dependencies 목록에는 **항상** `@devian/module-common`이 포함되어야 한다. (참조 판정 없음)

---

## Implementation Reference (정본 위치)

**구현 정본 파일:**

| 파일 | 함수 | 역할 |
|------|------|------|
| `framework-ts/tools/builder/build.js` | `generateCsproj(...)` | C# csproj 생성/보정 (ProtocolGroup 포함) |
| `framework-ts/tools/builder/build.js` | `ensureProtocolPackageJson(...)` | TS package.json 생성/보정 |
| `framework-ts/tools/builder/generators/protocol-cs.js` | `generateCSharpProtocol(...)` | C# `{ProtocolName}.g.cs` 생성 |
| `framework-ts/tools/builder/generators/protocol-cs.js` | `generateCSharpSessionHost(...)` | C# `{Protocol}SessionHost.g.cs` 생성 |
| `framework-ts/tools/builder/generators/protocol-cs.js` | `generateCSharpNetworker(...)` | C# `{Protocol}Networker.g.cs` 생성 |

**의존성 Hard Rule이 실제로 강제되는 지점:**

- **C#:**
  - csproj: `generateCsproj(...)`가 `Devian.csproj` + `Devian.Domain.Common.csproj` ProjectReference 포함
  - g.cs: `generateCSharpProtocol(...)`가 `using Devian;` 포함
- **TypeScript:**
  - package.json: `ensureProtocolPackageJson(...)`가 dependencies에 `@devian/core` + `@devian/module-common` 포함

---

## Verification Checklist (Hard)

빌드 후 반드시 확인해야 하는 사항:

**C#:**

1. 생성된 `framework-cs/module/Devian.Protocol.{ProtocolGroup}/Devian.Protocol.{ProtocolGroup}.csproj`에
   `..\Devian\Devian.csproj` + `..\Devian.Domain.Common\Devian.Domain.Common.csproj` ProjectReference 존재

2. 생성된 `framework-cs/module/Devian.Protocol.{ProtocolGroup}/{ProtocolName}.g.cs` 상단에
   `using Devian;` 존재

3. 생성된 `{ProtocolName}.g.cs`에 `System.Text.Json` 관련 코드 없음

4. 생성된 `{ProtocolName}.g.cs`에 `CodecJson` 클래스 없음

**TypeScript:**

5. 생성된 `framework-ts/module/devian-protocol-{group}/package.json` dependencies에  
   `@devian/module-common` 존재

**Unity UPM:**

6. Protocol용 `.asmdef` 파일의 `references`에 `Devian.Domain.Common` 존재

---

## Protocol Message Pooling

생성된 프로토콜 코드는 메시지 객체 풀링을 지원한다. 풀링은 **새 API**로 제공되며, 기존 `Decode<T>()`/`Decode(opcode)`는 호환성을 위해 그대로 유지된다.

### 생성 API (필수)

각 `{ProtocolName}.g.cs`의 `public static partial class {ProtocolName}` 내부에 아래 API가 생성되어야 한다:

```csharp
// 풀 필드 (메시지 타입별, max=256)
private static readonly PacketPool<Foo> _pool_Foo = new PacketPool<Foo>(256);

// opcode 기반 풀링 디코드
public static object? RentDecodePooled(int opcode, ReadOnlySpan<byte> data);

// 제네릭 풀링 디코드
public static T RentDecodePooled<T>(ReadOnlySpan<byte> data) where T : class, new();

// 반환 (타입 패턴 매칭)
public static void ReturnPooled(object message);

// 제네릭 반환
public static void ReturnPooled<T>(T message) where T : class;
```

### Reset 규약 (필수)

풀에서 꺼낸 메시지는 디코드 전에 반드시 `_Reset()` 호출해야 한다.

**List<T>? 필드의 Reset 처리:**
- `null`로 버리지 말고 `Clear()` 호출
- 생성 형태: `if (Field != null) Field.Clear();`

### 기존 API 호환성

- `CodecProtobuf.Decode<T>()`, `Decode(opcode)`는 **그대로 유지**
- 풀링은 새 API(`RentDecodePooled`/`ReturnPooled`)로만 제공
- 기존 사용자 코드 파괴 금지

### DoD (완료 정의)

- [ ] 생성된 `.g.cs`에서 `PacketPool<T>`가 "정의만" 되지 않고, `RentDecodePooled`/`ReturnPooled`에서 실제로 사용됨
- [ ] `_Reset()` 메서드에서 `List<T>` 필드는 `?.Clear()` 형태로 리셋됨
- [ ] 기존 `Decode<T>()`/`Decode(opcode)`는 그대로 유지됨 (호환성)
- [ ] `ReturnPooled(object)`는 패턴 매칭으로 타입 분기하며, `default`는 조용히 무시

---

## Zero-Alloc Encoding (Hard Rule)

**프로토콜 인코딩은 IBufferWriter<byte> 기반으로 구현되어 GC 할당이 발생하지 않는다.**

### ICodec 인터페이스

```csharp
public interface ICodec
{
    /// <summary>Encode to byte array (legacy, avoid in hot path)</summary>
    byte[] Encode<T>(T message) where T : class;
    /// <summary>Encode directly to IBufferWriter (zero-alloc send path)</summary>
    void EncodeTo<T>(IBufferWriter<byte> writer, T message) where T : class;
    T Decode<T>(ReadOnlySpan<byte> data) where T : class, new();
    object? Decode(int opcode, ReadOnlySpan<byte> data);
}
```

### PooledBufferWriter

`Devian.PooledBufferWriter`는 ArrayPool 기반 IBufferWriter 구현체이다:

```csharp
public sealed class PooledBufferWriter : IBufferWriter<byte>, IDisposable
{
    // ArrayPool<byte>.Shared.Rent/Return 기반
    public int WrittenCount { get; }
    public ReadOnlySpan<byte> WrittenSpan { get; }
    public void Reset();  // 버퍼 반환 없이 재사용
}
```

### ProtoWriter (IBufferWriter 기반)

생성된 ProtoWriter는 IBufferWriter<byte>를 직접 사용한다:

```csharp
private static class ProtoWriter
{
    public static void WriteVarint(IBufferWriter<byte> w, ulong value);
    public static void WriteTag(IBufferWriter<byte> w, int tag, int wireType);
    public static void WriteString(IBufferWriter<byte> w, int tag, string? value);
    public static void WriteInt32(IBufferWriter<byte> w, int tag, int value);
    // ... 등
}
```

---

## Frame Buffer Pooling (Hard Rule)

**Proxy.SendXxx 메서드는 PooledBufferWriter + EncodeTo 패턴으로 Zero-Alloc Send를 구현한다.**

### 생성 패턴 (필수)

```csharp
public void SendFoo(Foo message)
{
    var session = _session ?? throw new InvalidOperationException(...);

    using var bw = new PooledBufferWriter(256);

    // Write opcode (4 bytes LE)
    var span = bw.GetSpan(4);
    BitConverter.TryWriteBytes(span, Opcodes.Foo);
    bw.Advance(4);

    // Encode payload directly to buffer
    _codec.EncodeTo(bw, message);

    // Send frame
    session.SendTo(bw.WrittenSpan);
}
```

### 핵심 규칙

1. **`new byte[]` 금지**: Send 경로에서 `new byte[...]` 또는 `_codec.Encode()` 사용 금지
2. **PooledBufferWriter 사용**: opcode + payload를 연속으로 기록
3. **using 패턴**: `using var bw = new PooledBufferWriter(256);`로 자동 Dispose
4. **EncodeTo 사용**: `_codec.EncodeTo(bw, message)`로 직접 인코딩

### Legacy API 보존 (호환성)

- `ICodec.Encode<T>()`: 내부적으로 `EncodeTo` 호출 후 `ToArray()` (레거시 호환)

### DoD (완료 정의)

- [ ] ICodec에 `EncodeTo<T>(IBufferWriter<byte> writer, T message)` 존재
- [ ] 생성된 `SendXxx`에서 `using var bw = new PooledBufferWriter(...)` 패턴 사용
- [ ] 생성된 `SendXxx`에서 `_codec.EncodeTo(bw, message)` 호출
- [ ] `_codec.Encode(message)` 또는 `new byte[...]`가 SendXxx에 없음
- [ ] `CodecProtobuf.Encode<T>()`는 내부적으로 `EncodeTo` 사용

---

## Zero-Alloc Decoding (Hard Rule)

**프로토콜 디코딩은 임시 byte[] 할당 없이 span 기반으로 구현한다.**

### ProtoReader (Span 기반)

```csharp
internal ref struct ProtoReader
{
    private static readonly Encoding Utf8 = Encoding.UTF8;
    private ReadOnlySpan<byte> _data;
    private int _pos;

    public int Position => _pos;
    public int Remaining => _data.Length - _pos;
}
```

### String Decode: 임시 byte[] 금지 (Hard Rule)

**Reader는 연속 span이 가능하면 `Encoding.UTF8.GetString(ReadOnlySpan<byte>)`로 디코드한다.**

```csharp
public string ReadString()
{
    var len = (int)ReadVarint();
    if (len == 0) return string.Empty;
    // Zero-alloc path: decode directly from contiguous span
    var span = _data.Slice(_pos, len);
    _pos += len;
    return Utf8.GetString(span);  // string 1회만 할당
}
```

**핵심 규칙:**
1. 연속 span이면 `Utf8.GetString(span)` 직접 호출 (string 할당만 발생)
2. 연속 span이 불가능한 경우에만 ArrayPool 임시 버퍼 rent/return
3. `new byte[]` 임시 버퍼 생성 금지

### Collection Decode: 교체 금지 (Hard Rule)

**repeated/dict 필드는 디코드 시 컬렉션을 새로 만들지 않고 Clear() 후 채운다.**

```csharp
internal static void DecodeFoo(ReadOnlySpan<byte> data, Foo m)
{
    // Clear collections before decode (reuse pattern - no new List allocation)
    m.Items?.Clear();

    var reader = new ProtoReader(data);
    while (reader.HasMore)
    {
        var (tag, wireType) = reader.ReadTag();
        switch (tag)
        {
            case 1:
                // Only create if null (first decode), never replace
                (m.Items ??= new List<int>()).Add(reader.ReadInt32());
                break;
        }
    }
}
```

**핵심 규칙:**
1. **리스트 교체 금지**: `m.Items = new List<T>()` 패턴 금지
2. **Clear() 패턴 사용**: 디코드 전 `?.Clear()` 호출
3. **null-coalesce 생성**: `??= new List<T>()` 로 최초 1회만 생성
4. **AddRange 임시 배열 금지**: 루프에서 개별 Add() 사용

### Packed Repeated: Span 기반 파싱 (Hard Rule)

**packed repeated는 length-delimited 블록을 span으로 직접 순회하며 파싱한다(복사 금지).**

```csharp
// ProtoReader에 제공되는 헬퍼
public int ReadLengthDelimitedEnd()
{
    var len = (int)ReadVarint();
    return _pos + len;
}

// 사용 패턴 (블록 전체 복사 없이 직접 순회)
var end = reader.ReadLengthDelimitedEnd();
while (reader.Position < end)
{
    list.Add(reader.ReadInt32());
}
```

### DoD (완료 정의)

- [ ] string decode 경로에 `new byte[]` 없음
- [ ] string decode는 `Encoding.UTF8.GetString(ReadOnlySpan<byte>)` 사용
- [ ] fallback 복사가 필요한 경우 `ArrayPool<byte>.Shared.Rent/Return`만 사용
- [ ] repeated/dict 디코드에서 `new List<>`, `new Dictionary<>` 교체 금지
- [ ] 디코드 전 `?.Clear()` 호출로 기존 컬렉션 재사용
- [ ] packed repeated는 블록 span 직접 순회 (임시 배열 복사 금지)
- [ ] 컴파일 에러 없음

---

## Reference

- Policy SSOT: `skills/devian/10-module/03-ssot/SKILL.md`
- 동작 정본: 런타임/제너레이터 코드
