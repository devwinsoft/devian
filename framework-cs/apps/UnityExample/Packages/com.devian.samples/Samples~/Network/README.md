# Network Sample

Unity WebSocket client sample using `Devian.Protocol.Game` with Tick-based pump.

## Requirements

- `com.devian.foundation` - Core network infrastructure (INetSession, INetConnector, NetWsConnector)
- `com.devian.protocol.game` - Game protocol (C2Game/Game2C)

## Architecture

**INetSession/INetConnector 기반 (Interface-based DI):**
- `GameNetManager` owns: Stub, Proxy, and Connector
- `Generated Proxy` depends only on interfaces (INetSession, INetConnector)
- `NetWsConnector` implements INetConnector (Foundation에서 제공)

```
GameObject
└── GameNetManager : MonoBehaviour
        ├── _stub (Game2CStub) — created in Awake()
        ├── _proxy (C2Game.Proxy) — created in Awake()
        ├── _connector (NetWsConnector) — created in Awake()
        │
        ├── Connect(url) → proxy.Connect(stub, url, connector)
        │       Proxy내부: runtime = new Runtime(stub)
        │       Connector: session = connector.CreateSession(runtime, url)
        │
        └── Update() → proxy.Tick()
```

## Quick Start

1. Add `GameNetManager` component to a GameObject
2. Call `Connect(url)` to establish connection
3. Use `Proxy` to send messages
4. Extend via partial class for custom handling

## Files

| File | Description |
|------|-------------|
| `GameNetManager.cs` | Unity MonoBehaviour manager (owns Stub/Proxy/Connector) |
| `Game2CStub.cs` | Concrete stub (partial), handles inbound messages |
| `NetworkSampleMenu.cs` | Editor menu for creating GameNetManager |

## API

### Basic Usage

```csharp
var manager = GetComponent<GameNetManager>();
manager.Connect("ws://localhost:8080");

// Send messages via Proxy
manager.Proxy.SendPing(new C2Game.Ping { Timestamp = Time.time });
```

### Connect/Disconnect

```csharp
manager.Connect("ws://localhost:8080");
manager.Disconnect();
```

### Extending via Partial Class

To customize message handling, create a partial class file:

```csharp
// Game2CStub.Partial.cs
namespace Devian
{
    public partial class Game2CStub
    {
        partial void OnPongImpl(Game2C.EnvelopeMeta meta, Game2C.Pong message)
        {
            // Custom Pong handling
            Debug.Log($"Custom Pong: timestamp={message.Timestamp}");
        }

        partial void OnEchoReplyImpl(Game2C.EnvelopeMeta meta, Game2C.EchoReply message)
        {
            // Custom EchoReply handling
            Debug.Log($"Custom EchoReply: text={message.Text}");
        }
    }
}
```

## Local server

```bash
cd framework-ts
npm install
npm -w GameServer run start
```

- **Default port:** `ws://localhost:8080`

## WebGL notes

- **wss:// required**: WebGL browsers require secure WebSocket (wss://)
- **CORS**: Server must allow WebSocket connections from the Unity WebGL origin
- **.jslib included**: `DevianWs.jslib` is in `com.devian.foundation/Runtime/Plugins/WebGL/`
