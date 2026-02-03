# Network Sample

Unity WebSocket client sample using `Devian.Protocol.Game` (Ping/Echo) with Tick-based pump.

## Requirements

- `com.devian.foundation` - Core network infrastructure
- `com.devian.protocol.game` - Game protocol (C2Game/Game2C)

## Quick Start

### Option A: One-click Setup (Recommended)

1. Menu: **Devian → Samples → Network → Create Sample Setup**
2. This creates:
   - `Devian.NetTickRunner` - Tick loop manager (if not already present)
   - `Devian.GameNetworkClientSample` - Sample client with default URL
3. Enter Play mode
4. Use the on-screen buttons to Connect, Send Ping/Echo, and Disconnect

### Option B: Manual Setup

1. Add `GameNetworkClientSample` component to any GameObject
2. Enter your WebSocket server URL (e.g., `ws://localhost:8080`)
3. Enter Play mode
4. Use the on-screen buttons to Connect, Send Ping/Echo, and Disconnect

## Architecture

```
GameNetworkClientSample (MonoBehaviour)
    └── GameNetworkClient (INetTickable, IDisposable)
            ├── NetWsClient (transport)
            ├── NetClient (core)
            ├── Game2C.Runtime (inbound dispatch)
            │       └── SampleGame2CStub (Pong/EchoReply handling)
            └── C2Game.Proxy (outbound Ping/Echo)
```

## Files

| File | Description |
|------|-------------|
| `GameNetworkClientSample.cs` | MonoBehaviour with OnGUI buttons |
| `GameNetworkClient.cs` | Pure C# client (INetTickable) |
| `GameNetworkClient_Stub.cs` | Game2C.Stub implementation for Pong/EchoReply |

## Tick Management

This sample uses `NetTickRunner` for automatic tick management:
- If no `NetTickRunner` exists in scene, one is auto-created
- `GameNetworkClient` implements `INetTickable` and is registered with the runner
- Every frame, `Tick()` is called automatically to process events

## Customization

After importing, you can freely modify the sample code:
- Change namespace from `Devian` to your own
- Replace `Devian.Protocol.Game` with your own protocol
- Add your own message handlers (by inheriting `{Protocol}.Stub`)

## Verification in Unity (UnityExample)

### Checklist

1. **Open UnityExample project**
   ```
   framework-cs/apps/UnityExample/
   ```

2. **Import the sample**
   - Window → Package Manager
   - Select `Devian Samples` (com.devian.samples)
   - Samples section → "Network" → Import

3. **Test in Editor**
   - Create empty GameObject in any scene
   - Add `GameNetworkClientSample` component
   - Enter URL (default: `ws://localhost:8080`)
   - Play → Use OnGUI buttons to Connect → Ping/Echo → Disconnect
   - Check Console for response logs

4. **Expected results**
   - `[GameNetworkClientSample] Connected!` on successful connection
   - `[GameNetworkClientSample] Pong received - Latency: ...` on Ping response
   - `[GameNetworkClientSample] EchoReply: ...` on Echo response

## Local server (recommended)

로컬에서 바로 통신 확인을 하려면 Devian TS GameServer를 실행한다.

### Server execution

터미널에서 아래 실행:

```bash
cd framework-ts
npm install
npm run start:server
```

- **기본 포트:** `ws://localhost:8080`

### Unity sample URL

```
ws://localhost:8080
```

### Protocol codec

- 기본은 **Protobuf**이며, 서버 코드(`framework-ts/apps/GameServer/src/index.ts`)의 `USE_JSON`은 기본 `false`를 유지한다.

## Expected logs

### Server side

```
Session connected
Ping handler called
Echo handler called
```

### Unity side

```
[GameNetworkClientSample] Connected!
[GameNetworkClientSample] Pong received - Latency: Xms, ServerTime: Y
[GameNetworkClientSample] EchoReply: "Hello, World!" (echoed at Z)
```

## WebGL notes

WebGL 빌드는 브라우저 보안 때문에 대부분 **wss://(TLS) + https 페이지**가 필요하다.

- **wss:// required**: WebGL browsers require secure WebSocket (wss://)
- **CORS**: Server must allow WebSocket connections from the Unity WebGL origin
- **.jslib included**: `DevianWs.jslib` is in `com.devian.foundation/Runtime/Plugins/WebGL/`

로컬 테스트는 **에디터/스탠드얼론**에서 먼저 확인하고,
WebGL은 **배포 환경(https + wss)**에서 확인한다.

## Smoke checklist

- [ ] Unity Package Manager에서 `com.devian.samples` → Samples → Network Import
- [ ] 메뉴 `Devian/Samples/Network/Create Sample Setup` 실행
- [ ] Play → Connect → Pong/EchoReply 로그 확인
- [ ] 서버 로그에 Ping/Echo handler 호출 확인
