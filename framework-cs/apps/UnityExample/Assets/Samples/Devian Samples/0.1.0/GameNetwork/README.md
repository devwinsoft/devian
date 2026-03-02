# GameNetwork (Devian Samples)

Unity WebSocket client sample using `Devian.Protocol.Game` with a session-host-owned Tick-based pump.

## Requirements

- `com.devian.foundation` - Core network infrastructure (INetSession, INetConnector, NetWsConnector)
- `com.devian.protocol.game` - Game protocol (C2Game/Game2C)

## Structure

- `Runtime/Game/GameNetManager.cs`
  - Unity facade (`CompoSingleton<GameNetManager>`)
  - owns `Game2CStub`, `C2Game.Proxy`, `ClientSessionHost`
  - forwards `Connect`, `Disconnect`, `Update`, and connection events
- `Runtime/Game/Game2CStub.cs`
  - concrete inbound stub
  - extend handler behavior via partial methods

The generated session host lives in the protocol package:

- `com.devian.protocol.game/Runtime/Generated/ClientSessionHost.g.cs`
  - generated protocol-group session host
  - inherits lifecycle from `NetClientSessionHost`
  - pairs `Game2C.Runtime` with `C2Game.Proxy`

## Quick Start

1. Add `GameNetManager` component to a GameObject
2. Call `Connect(url)` to establish connection
3. Use `GameNetManager.Proxy` to send messages
4. Extend via partial class (`Game2CStub.Partial.cs`) for custom handling
